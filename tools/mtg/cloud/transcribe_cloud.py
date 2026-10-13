"""Transcribe MTG recordings in the cloud (GitHub Actions, CPU, large-v3). Slow is fine; it runs unattended.

Flow: a Google Drive folder (shared with a service account) holds the recordings.
  inbox/  -> a recording is moved to processing/ (the claim; Drive moves are atomic) -> downloaded -> loudness-normalised
  -> transcribe.py (faster-whisper large-v3, CPU) -> clean.py -> the PRIVATE transcripts repo (<date>/<name>.clean.txt/.srt)
  -> moved to done/.   On failure the recording goes back to inbox/ and is retried by the next run, at most MAX_ATTEMPTS times;
  after that, or if it can never work (too long, unreadable), it is put in failed/ and the job fails so that a person is told.
  A transcript is saved with a <name>.minutes.pending marker. If MTG_ROUTINE_URL and MTG_ROUTINE_TOKEN are set, the cloud routine
  that makes the minutes (mtg-sync) is started right after (marker route=cloud); a PC (watch.ps1) is the fallback: it waits 6 hours
  for the cloud, and makes the minutes itself if they do not exist by then. Without the secrets, the marker says route=pc.
  While a PC holds a fresh claim file in the folder, the cloud does nothing.

PRIVACY: this repo and its Actions logs are PUBLIC. This script never prints transcript text, file names or Drive ids;
it prints only counts, dates and timings. Never upload outputs as workflow artifacts.

Usage:
  python transcribe_cloud.py --count                    # print count=N (recordings waiting) to stdout / $GITHUB_OUTPUT
  python transcribe_cloud.py --private-clone PATH       # process everything waiting
  python transcribe_cloud.py --benchmark 300            # transcribe the first 300 s of one recording; print only the speed
  python transcribe_cloud.py --release                  # put every recording in processing/ back into inbox/ (after a crash or a timeout)
Environment: GDRIVE_SA_JSON (service account key), MTG_INBOX_FOLDER_ID (the Drive folder id),
  MTG_ROUTINE_URL and MTG_ROUTINE_TOKEN (optional: the API trigger of the routine that makes the minutes).
"""
import argparse
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
import tempfile
import time
from pathlib import Path

AUDIO_EXTS = {".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg"}
TOOLS = Path(__file__).resolve().parent.parent  # tools/mtg
NORMALISE = "highpass=f=80,dynaudnorm=f=200:g=21:p=0.9:m=30,loudnorm=I=-16:TP=-1.5:LRA=11"  # same as transcribe.ps1
JST = dt.timezone(dt.timedelta(hours=9))
FOLDER_MIME = "application/vnd.google-apps.folder"
MAX_ATTEMPTS = 3  # a recording that fails this many times is put in failed/ and not retried (no endless retry every 30 minutes)
MAX_AUDIO_HOURS = 4.0  # longer than this will not fit in the 6-hour job (about 0.9x real time): split it into parts
PC_CLAIM_NAME = ".mtg-processing.claim"  # written by watch.ps1 on a PC; the cloud stays away while a fresh one exists
PC_CLAIM_HOURS = 3
FIRE_RETRY_STATUSES = (429, 503)  # the request was not processed: safe to retry. (Every accepted request starts a new session: never retry anything else.)


class Quarantine(Exception):
    """This recording cannot be processed as it is; put it in failed/ at once (retrying will not help)."""


def log(msg):
    print(msg, flush=True)


def mtg_date(name: str, modified_time: str) -> str:
    """A date in the file name (2026-10-16 / 20261016) wins over the Drive modified time (JST)."""
    m = re.search(r"(20\d{2})[-_.]?(\d{2})[-_.]?(\d{2})", name)
    if m:
        try:
            return dt.date(int(m.group(1)), int(m.group(2)), int(m.group(3))).isoformat()
        except ValueError:
            pass
    t = dt.datetime.fromisoformat(modified_time.replace("Z", "+00:00"))
    return t.astimezone(JST).date().isoformat()


def is_audio(name: str) -> bool:
    return Path(name).suffix.lower() in AUDIO_EXTS


class Drive:
    """The few Drive calls we need. `svc` is a googleapiclient Drive v3 service (or a test double)."""

    def __init__(self, svc, inbox_id):
        self.svc, self.inbox = svc, inbox_id

    def _list(self, q, fields="files(id,name,modifiedTime,size,mimeType,appProperties)"):
        out, token = [], None
        while True:
            r = self.svc.files().list(q=q, fields="nextPageToken," + fields, pageToken=token, pageSize=100,
                                      supportsAllDrives=True, includeItemsFromAllDrives=True).execute()
            out += r.get("files", [])
            token = r.get("nextPageToken")
            if not token:
                return out

    def subfolder(self, name):
        found = self._list(f"'{self.inbox}' in parents and name = '{name}' and mimeType = '{FOLDER_MIME}' and trashed = false")
        if found:
            return found[0]["id"]
        body = {"name": name, "mimeType": FOLDER_MIME, "parents": [self.inbox]}
        return self.svc.files().create(body=body, fields="id", supportsAllDrives=True).execute()["id"]

    def waiting(self):
        files = self._list(f"'{self.inbox}' in parents and trashed = false and mimeType != '{FOLDER_MIME}'")
        return sorted((f for f in files if is_audio(f["name"])), key=lambda f: f["modifiedTime"])

    def attempts(self, f) -> int:
        try:
            return int((f.get("appProperties") or {}).get("mtg_attempts", "0"))
        except ValueError:
            return 0

    def set_attempts(self, file_id, n: int):
        self.svc.files().update(fileId=file_id, body={"appProperties": {"mtg_attempts": str(n)}}, fields="id", supportsAllDrives=True).execute()

    def pc_busy(self) -> bool:
        """A PC is processing this folder right now (a claim file written less than PC_CLAIM_HOURS ago)."""
        files = self._list(f"'{self.inbox}' in parents and name = '{PC_CLAIM_NAME}' and trashed = false")
        now = dt.datetime.now(dt.timezone.utc)
        for f in files:
            t = dt.datetime.fromisoformat(f["modifiedTime"].replace("Z", "+00:00"))
            if (now - t).total_seconds() < PC_CLAIM_HOURS * 3600:
                return True
        return False

    def move(self, file_id, src, dst):
        self.svc.files().update(fileId=file_id, addParents=dst, removeParents=src, fields="id", supportsAllDrives=True).execute()

    def download(self, file_id, dest: Path):
        from googleapiclient.http import MediaIoBaseDownload
        with open(dest, "wb") as fh:
            dl = MediaIoBaseDownload(fh, self.svc.files().get_media(fileId=file_id, supportsAllDrives=True), chunksize=32 * 1024 * 1024)
            done = False
            while not done:
                _, done = dl.next_chunk()


def make_drive():
    from google.oauth2 import service_account
    from googleapiclient.discovery import build
    creds = service_account.Credentials.from_service_account_info(
        json.loads(os.environ["GDRIVE_SA_JSON"]), scopes=["https://www.googleapis.com/auth/drive"])
    return Drive(build("drive", "v3", credentials=creds, cache_discovery=False), os.environ["MTG_INBOX_FOLDER_ID"])


def run(cmd, **kw):
    return subprocess.run(cmd, check=True, **kw)


def duration_seconds(path: Path) -> float:
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)],
                       capture_output=True, text=True)
    try:
        return float(r.stdout.strip())
    except ValueError:
        return 0.0


def normalise(src: Path, dst: Path, seconds: int = 0):
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", str(src)]
    if seconds:
        cmd += ["-t", str(seconds)]
    run(cmd + ["-af", NORMALISE, "-ar", "16000", "-ac", "1", str(dst)])


def transcribe(wav: Path, out_dir: Path, stem: str, model: str, extra_glossary: Path | None):
    cmd = [sys.executable, "-I", str(TOOLS / "transcribe.py"), str(wav), str(out_dir), "--stem", stem, "--model", model, "--device", "cpu"]
    if extra_glossary and extra_glossary.exists():
        cmd += ["--extra-glossary", str(extra_glossary)]
    # transcribe.py prints only the device line and the segment count; show nothing else (the log is public)
    r = subprocess.run(cmd, capture_output=True, text=True)
    for line in r.stdout.splitlines():
        if line.startswith("device=") or re.match(r"^\S+: \d+ segments$", line):
            log(re.sub(r"^\S+: ", "recording: ", line))
    if r.returncode != 0:
        # Show the call trail (file path + function names) and the last line when it is an exception line.
        # Neither contains transcript text. Everything else is dropped (the log is public).
        lines = [l.rstrip() for l in r.stderr.splitlines() if l.strip()]
        trail = [re.sub(r'^\s*File "[^"]*/(site-packages|python3[^/]*)/', '  File "…/', l)[:160] for l in lines if re.match(r'^\s*File "', l)][-8:]
        last = lines[-1] if lines else ""
        shown = last[:200] if re.match(r"^([\w.]+(Error|Exception|Exit)\b|transcribe\.py: error:)", last) else "(not an exception line)"
        log(f"transcribe.py exit={r.returncode}: {shown}")
        for l in trail:
            log(l)
        raise RuntimeError("transcribe.py failed")


def routine_configured() -> bool:
    return bool(os.environ.get("MTG_ROUTINE_URL") and os.environ.get("MTG_ROUTINE_TOKEN"))


def fire_routine(date: str, stem: str, attempts: int = 3) -> bool:
    """Start the cloud routine that makes the minutes. Best effort: a failure only logs the HTTP status (never the URL or the token),
    because the transcript is already saved and a PC will make the minutes later. Returns True when the routine was started."""
    if not routine_configured():
        return False
    body = json.dumps({"text": f"date={date} name={stem}"}).encode("utf-8")
    req = urllib.request.Request(os.environ["MTG_ROUTINE_URL"], data=body, method="POST", headers={
        "Authorization": "Bearer " + os.environ["MTG_ROUTINE_TOKEN"],
        "anthropic-version": "2023-06-01",
        "Content-Type": "application/json"})
    for i in range(attempts):
        try:
            with urllib.request.urlopen(req, timeout=60):
                log("minutes routine: started")
                return True
        except urllib.error.HTTPError as e:
            if e.code in FIRE_RETRY_STATUSES and i < attempts - 1:
                time.sleep(10 * (i + 1))
                continue
            log(f"minutes routine: not started (HTTP {e.code}). A PC will make the minutes later.")
            return False
        except Exception as e:  # noqa: BLE001  network error etc.: do not retry (the request may have been accepted)
            log(f"minutes routine: not started ({type(e).__name__}). A PC will make the minutes later.")
            return False
    return False


def archive(private: Path, tx_dir: Path, stem: str, date: str):
    dest = private / date
    dest.mkdir(parents=True, exist_ok=True)
    for ext in (".clean.txt", ".clean.srt"):
        f = tx_dir / f"{stem}{ext}"
        if f.exists():
            shutil.copy(f, dest / f.name)
    # tell a PC that the minutes (mtg-sync) are still to be made from this transcript; the PC replaces this with .minutes.done
    marker = dest / f"{stem}.minutes.pending"
    if not (dest / f"{stem}.minutes.done").exists():
        marker.write_text(json.dumps({"created": dt.datetime.now(dt.timezone.utc).isoformat(), "attempts": 0,
                                      "route": "cloud" if routine_configured() else "pc"}), encoding="utf-8")
    git = ["git", "-C", str(private)]
    run(git + ["add", "--", date])
    if subprocess.run(git + ["diff", "--cached", "--quiet"]).returncode != 0:
        run(git + ["-c", "user.name=mtg-cloud", "-c", "user.email=mtg-cloud@users.noreply.github.com", "commit", "-q", "-m", f"{date} (cloud)"])
        for attempt in range(3):
            if subprocess.run(git + ["push", "-q"]).returncode == 0:
                return
            run(git + ["pull", "-q", "--rebase"])
        raise RuntimeError("git push failed")


def process_one(drive: Drive, f: dict, folders: dict, private: Path, model: str) -> str:
    """Returns "done" or "quarantined". Raises on a failure that will be retried (or quarantined after MAX_ATTEMPTS)."""
    date = mtg_date(f["name"], f["modifiedTime"])
    stem = Path(f["name"]).stem
    tries = drive.attempts(f)
    if tries >= MAX_ATTEMPTS:  # a person moved it back from failed/ after fixing the cause: start again from zero
        tries = 0
        drive.set_attempts(f["id"], 0)
    log(f"start: recording dated {date}, {int(f.get('size', 0)) // 1_000_000} MB, attempt {tries + 1}/{MAX_ATTEMPTS}")
    drive.move(f["id"], folders["inbox"], folders["processing"])  # claim
    dest = folders["inbox"]  # where the recording goes when this function ends
    try:
        with tempfile.TemporaryDirectory() as td:
            td = Path(td)
            src = td / ("audio" + Path(f["name"]).suffix.lower())
            drive.download(f["id"], src)
            wav = td / "audio.norm.wav"
            normalise(src, wav)
            audio_min = duration_seconds(wav) / 60
            if audio_min == 0:
                raise Quarantine("the audio could not be read (0 minutes)")
            if audio_min > MAX_AUDIO_HOURS * 60:
                raise Quarantine(f"too long ({audio_min:.0f} min): split it into parts of about 2 hours")
            tx_dir = private / "transcripts"  # clean.py looks for config/corrections.local.tsv next to this folder
            tx_dir.mkdir(parents=True, exist_ok=True)
            t0 = time.time()
            transcribe(wav, tx_dir, stem, model, private / "config" / "glossary.local.txt")
            log(f"transcribed: {audio_min:.0f} min of audio in {(time.time() - t0) / 60:.0f} min")
            run([sys.executable, "-I", str(TOOLS / "clean.py"), str(tx_dir / f"{stem}.srt")], capture_output=True)
            archive(private, tx_dir, stem, date)
            for p in tx_dir.glob(f"{stem}.*"):
                p.unlink()
        dest = folders["done"]
        log(f"archived: {date}")
        fire_routine(date, stem)
    except Quarantine as e:
        dest = folders["failed"]
        log(f"quarantined: {e}. The recording was put in failed/ (not retried).")
    except Exception:
        n = tries + 1
        try:
            drive.set_attempts(f["id"], n)
        except Exception:
            pass
        if n >= MAX_ATTEMPTS:
            dest = folders["failed"]
            log(f"quarantined: failed {n} times. The recording was put in failed/ (not retried). Fix the cause, then move it back to the inbox.")
        raise
    finally:
        drive.move(f["id"], folders["processing"], dest)
    return "quarantined" if dest == folders["failed"] else "done"


def benchmark(drive: Drive, seconds: int, model: str):
    files = drive.waiting()
    done_id = drive.subfolder("done")
    if not files:
        files = drive._list(f"'{done_id}' in parents and trashed = false")
        files = [f for f in files if is_audio(f["name"])]
    if not files:
        log("benchmark: no recording found (put one in the Drive folder)")
        return 1
    f = files[-1]
    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        src = td / ("audio" + Path(f["name"]).suffix.lower())
        drive.download(f["id"], src)
        wav = td / "clip.wav"
        normalise(src, wav, seconds)
        clip = duration_seconds(wav)
        t0 = time.time()
        transcribe(wav, td / "out", "clip", model, None)
        took = time.time() - t0
    log(f"benchmark: {clip:.0f} s of audio, model {model}, CPU: {took:.0f} s -> {took / max(clip, 1):.2f}x the audio length (includes the model load)")
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", action="store_true")
    ap.add_argument("--private-clone")
    ap.add_argument("--benchmark", type=int, default=0)
    ap.add_argument("--release", action="store_true")
    ap.add_argument("--model", default="large-v3")
    a = ap.parse_args()

    if not os.environ.get("GDRIVE_SA_JSON") or not os.environ.get("MTG_INBOX_FOLDER_ID"):
        log("secrets are not set: skipping")
        if a.count:
            log("count=0")
            if os.environ.get("GITHUB_OUTPUT"):
                Path(os.environ["GITHUB_OUTPUT"]).open("a").write("count=0\n")
        return 0
    drive = make_drive()
    if a.count:
        busy = drive.pc_busy()
        n = 0 if busy else len(drive.waiting())
        if busy:
            log("a PC is processing the folder now: leaving it alone")
        log(f"count={n}")
        if os.environ.get("GITHUB_OUTPUT"):
            Path(os.environ["GITHUB_OUTPUT"]).open("a").write(f"count={n}\n")
        return 0
    if a.release:
        proc = drive.subfolder("processing")
        stuck = [f for f in drive._list(f"'{proc}' in parents and trashed = false") if is_audio(f["name"])]
        for f in stuck:
            drive.move(f["id"], proc, drive.inbox)
        log(f"released: {len(stuck)}")
        return 0
    if a.benchmark:
        return benchmark(drive, a.benchmark, a.model)
    if not a.private_clone:
        log("--private-clone is required")
        return 2
    folders = {"inbox": drive.inbox, "processing": drive.subfolder("processing"), "done": drive.subfolder("done"), "failed": drive.subfolder("failed")}
    failed = 0
    if drive.pc_busy():
        log("a PC is processing the folder now: leaving it alone")
        return 0
    for f in drive.waiting():
        try:
            if process_one(drive, f, folders, Path(a.private_clone), a.model) == "quarantined":
                failed += 1  # make the job fail, so that a person is told by e-mail
        except Exception as e:  # never print the message: it can contain names or text
            failed += 1
            log(f"failed: {type(e).__name__}")
    return 1 if failed else 0


def safe_main() -> int:
    """main(), but an unexpected error prints only its class (and the HTTP status), never its message:
    a Google API error contains the request URL, which has the Drive folder id, and this log is public."""
    try:
        return main()
    except SystemExit:
        raise
    except Exception as e:  # noqa: BLE001
        status = getattr(getattr(e, "resp", None), "status", None) or getattr(e, "status_code", None)
        log(f"error: {type(e).__name__}" + (f" (HTTP {status})" if status else "") + " - the message is not shown (the log is public)")
        return 1


if __name__ == "__main__":
    sys.exit(safe_main())
