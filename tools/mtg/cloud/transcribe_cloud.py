"""Transcribe MTG recordings in the cloud (GitHub Actions, CPU, large-v3). Slow is fine; it runs unattended.

Flow: a Google Drive folder (shared with a service account) holds the recordings.
  inbox/  -> a recording is moved to processing/ (the claim; Drive moves are atomic) -> downloaded -> loudness-normalised
  -> transcribe.py (faster-whisper large-v3, CPU) -> clean.py -> the PRIVATE transcripts repo (<date>/<name>.clean.txt/.srt)
  -> moved to done/.   On failure the recording goes back to inbox/ so the next run retries it.

PRIVACY: this repo and its Actions logs are PUBLIC. This script never prints transcript text, file names or Drive ids;
it prints only counts, dates and timings. Never upload outputs as workflow artifacts.

Usage:
  python transcribe_cloud.py --count                    # print count=N (recordings waiting) to stdout / $GITHUB_OUTPUT
  python transcribe_cloud.py --private-clone PATH       # process everything waiting
  python transcribe_cloud.py --benchmark 300            # transcribe the first 300 s of one recording; print only the speed
  python transcribe_cloud.py --release                  # put every recording in processing/ back into inbox/ (after a crash or a timeout)
Environment: GDRIVE_SA_JSON (service account key), MTG_INBOX_FOLDER_ID (the Drive folder id).
"""
import argparse
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

AUDIO_EXTS = {".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg"}
TOOLS = Path(__file__).resolve().parent.parent  # tools/mtg
NORMALISE = "highpass=f=80,dynaudnorm=f=200:g=21:p=0.9:m=30,loudnorm=I=-16:TP=-1.5:LRA=11"  # same as transcribe.ps1
JST = dt.timezone(dt.timedelta(hours=9))
FOLDER_MIME = "application/vnd.google-apps.folder"


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

    def _list(self, q, fields="files(id,name,modifiedTime,size,mimeType)"):
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


def archive(private: Path, tx_dir: Path, stem: str, date: str):
    dest = private / date
    dest.mkdir(parents=True, exist_ok=True)
    for ext in (".clean.txt", ".clean.srt"):
        f = tx_dir / f"{stem}{ext}"
        if f.exists():
            shutil.copy(f, dest / f.name)
    git = ["git", "-C", str(private)]
    run(git + ["add", "--", date])
    if subprocess.run(git + ["diff", "--cached", "--quiet"]).returncode != 0:
        run(git + ["-c", "user.name=mtg-cloud", "-c", "user.email=mtg-cloud@users.noreply.github.com", "commit", "-q", "-m", f"{date} (cloud)"])
        for attempt in range(3):
            if subprocess.run(git + ["push", "-q"]).returncode == 0:
                return
            run(git + ["pull", "-q", "--rebase"])
        raise RuntimeError("git push failed")


def process_one(drive: Drive, f: dict, folders: dict, private: Path, model: str):
    date = mtg_date(f["name"], f["modifiedTime"])
    stem = Path(f["name"]).stem
    log(f"start: recording dated {date}, {int(f.get('size', 0)) // 1_000_000} MB")
    drive.move(f["id"], folders["inbox"], folders["processing"])  # claim
    ok = False
    try:
        with tempfile.TemporaryDirectory() as td:
            td = Path(td)
            src = td / ("audio" + Path(f["name"]).suffix.lower())
            drive.download(f["id"], src)
            wav = td / "audio.norm.wav"
            normalise(src, wav)
            audio_min = duration_seconds(wav) / 60
            tx_dir = private / "transcripts"  # clean.py looks for config/corrections.local.tsv next to this folder
            tx_dir.mkdir(parents=True, exist_ok=True)
            t0 = time.time()
            transcribe(wav, tx_dir, stem, model, private / "config" / "glossary.local.txt")
            log(f"transcribed: {audio_min:.0f} min of audio in {(time.time() - t0) / 60:.0f} min")
            run([sys.executable, "-I", str(TOOLS / "clean.py"), str(tx_dir / f"{stem}.srt")], capture_output=True)
            archive(private, tx_dir, stem, date)
            for p in tx_dir.glob(f"{stem}.*"):
                p.unlink()
        ok = True
    finally:
        drive.move(f["id"], folders["processing"], folders["done"] if ok else folders["inbox"])
    log(f"archived: {date}")


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
        n = len(drive.waiting())
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
    folders = {"inbox": drive.inbox, "processing": drive.subfolder("processing"), "done": drive.subfolder("done")}
    failed = 0
    for f in drive.waiting():
        try:
            process_one(drive, f, folders, Path(a.private_clone), a.model)
        except Exception as e:  # never print the message: it can contain names or text
            failed += 1
            log(f"failed: {type(e).__name__} (the recording was put back in the inbox)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
