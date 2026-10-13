"""Tests for transcribe_cloud.py. No network, no ffmpeg, no Google libraries: Drive, the audio tools and Whisper are replaced by fakes.
Run: python tools/mtg/cloud/test_transcribe_cloud.py
"""
import contextlib
import datetime as dt
import importlib.util
import io
import subprocess
import sys
import tempfile
from pathlib import Path

spec = importlib.util.spec_from_file_location("tc", Path(__file__).with_name("transcribe_cloud.py"))
tc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tc)

FAILS = []


def check(name, cond):
    print(("ok   " if cond else "FAIL ") + name)
    if not cond:
        FAILS.append(name)


class Req:
    def __init__(s, v): s.v = v
    def execute(s): return s.v


class Files:
    def __init__(s, st): s.st = st

    def list(s, q=None, **kw):
        parent = q.split("'")[1]
        fs = [f for f in s.st["files"].values() if parent in f["parents"] and not f.get("trashed")]
        if "name = '" in q:
            fs = [f for f in fs if f["name"] == q.split("name = '")[1].split("'")[0]]
        if "mimeType = '" + tc.FOLDER_MIME + "'" in q:
            fs = [f for f in fs if f["mimeType"] == tc.FOLDER_MIME]
        elif "mimeType != '" in q:
            fs = [f for f in fs if f["mimeType"] != tc.FOLDER_MIME]
        return Req({"files": [dict(f) for f in fs]})

    def create(s, body=None, **kw):
        i = f"id{len(s.st['files'])}"
        s.st["files"][i] = {"id": i, "name": body["name"], "mimeType": body["mimeType"], "parents": list(body["parents"]),
                            "modifiedTime": "2026-10-16T00:00:00Z"}
        return Req({"id": i})

    def update(s, fileId, addParents=None, removeParents=None, body=None, **kw):
        f = s.st["files"][fileId]
        if addParents:
            f["parents"] = [p for p in f["parents"] if p != removeParents] + [addParents]
        if body and "appProperties" in body:
            f["appProperties"] = {**f.get("appProperties", {}), **body["appProperties"]}
        return Req({"id": fileId})


class Svc:
    def __init__(s, st): s.st = st
    def files(s): return Files(s.st)


def new_world(audio_name="2026-10-16 MTG.m4a"):
    st = {"files": {"a1": {"id": "a1", "name": audio_name, "mimeType": "audio/mp4", "parents": ["INBOX"],
                           "modifiedTime": "2026-10-17T01:00:00Z", "size": "1000000"}}}
    drive = tc.Drive(Svc(st), "INBOX")
    drive.download = lambda file_id, dest: Path(dest).write_bytes(b"x")
    folders = {"inbox": "INBOX", "processing": drive.subfolder("processing"), "done": drive.subfolder("done"), "failed": drive.subfolder("failed")}
    tmp = Path(tempfile.mkdtemp())
    subprocess.run(["git", "init", "-q", "--bare", str(tmp / "remote.git")], check=True)
    priv = tmp / "priv"
    subprocess.run(["git", "clone", "-q", str(tmp / "remote.git"), str(priv)], check=True, capture_output=True)
    g = ["git", "-C", str(priv), "-c", "user.name=t", "-c", "user.email=t@t"]
    subprocess.run(g + ["commit", "-q", "--allow-empty", "-m", "init"], check=True)
    subprocess.run(["git", "-C", str(priv), "push", "-q", "origin", "HEAD"], check=True, capture_output=True)
    return st, drive, folders, priv, tmp


def where(st, folders):
    parents = st["files"]["a1"]["parents"]
    return next((k for k, v in folders.items() if v in parents), "?")


def fake_normalise(src, dst, seconds=0): Path(dst).write_bytes(b"w")
def ok_transcribe(wav, out_dir, stem, model, extra):
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / f"{stem}.srt").write_text("1\n00:00:00,000 --> 00:00:02,000\nテストの発言です\n", encoding="utf-8")
def boom(*a, **k): raise RuntimeError("テスト text must not be printed")


tc.normalise = fake_normalise
tc.duration_seconds = lambda path: 600.0


def run_process(drive, folders, priv, f=None):
    buf = io.StringIO()
    err = None
    with contextlib.redirect_stdout(buf):
        try:
            res = tc.process_one(drive, f or drive.waiting()[0], folders, priv, "large-v3")
        except Exception as e:  # noqa: BLE001
            res, err = None, e
    return res, err, buf.getvalue()


# 1) dates
for name, mod, want in [("2026-10-16 MTG.m4a", "2026-10-17T20:00:00Z", "2026-10-16"), ("20261016.m4a", "2026-10-17T20:00:00Z", "2026-10-16"),
                        ("新規録音 12.m4a", "2026-10-16T15:30:00Z", "2026-10-17"), ("x_2026-13-45.m4a", "2026-10-16T03:00:00Z", "2026-10-16")]:
    check(f"mtg_date {name}", tc.mtg_date(name, mod) == want)

# 2) success: moved to done, transcript and the minutes.pending marker saved, nothing leaks to the log
st, drive, folders, priv, tmp = new_world()
tc.transcribe = ok_transcribe
res, err, out = run_process(drive, folders, priv)
check("success: result is done", res == "done" and err is None)
check("success: recording in done/", where(st, folders) == "done")
files = sorted(p.relative_to(priv).as_posix() for p in priv.rglob("*") if p.is_file() and ".git" not in p.parts)
check("success: cleaned transcript saved (.clean.txt and .clean.srt)", any(f.endswith(".clean.txt") for f in files) and any(f.endswith(".clean.srt") for f in files))
check("success: minutes.pending marker saved", any(f.endswith(".minutes.pending") for f in files))
check("success: pushed to the remote", "(cloud)" in subprocess.run(["git", "-C", str(tmp / "remote.git"), "log", "--oneline", "-1"], capture_output=True, text=True).stdout)
check("success: log has no transcript text or file name", "テスト" not in out and "MTG.m4a" not in out)

# 3) failure: back to inbox, attempt counter; third failure -> failed/
st, drive, folders, priv, tmp = new_world()
tc.transcribe = boom
for n in (1, 2):
    res, err, out = run_process(drive, folders, priv)
    check(f"failure {n}: raised and back in inbox", err is not None and where(st, folders) == "inbox")
    check(f"failure {n}: attempts = {n}", drive.attempts(st["files"]["a1"]) == n)
    check(f"failure {n}: log has no message text", "テスト" not in out)
res, err, out = run_process(drive, folders, priv)
check("failure 3: quarantined in failed/", err is not None and where(st, folders) == "failed" and "quarantined" in out)
check("failed recording is not listed again", not drive.waiting())

# 3b) a person moves a quarantined recording back to the inbox: the counter starts again from zero
st["files"]["a1"]["parents"] = ["INBOX"]
check("quarantined recording is listed again once moved back", [f["name"] for f in drive.waiting()] == ["2026-10-16 MTG.m4a"])
res, err, out = run_process(drive, folders, priv)
check("moved back: counted from 1 again, not quarantined at once", err is not None and where(st, folders) == "inbox" and drive.attempts(st["files"]["a1"]) == 1)

# 4) too long -> failed/ at once, no retry, job fails (returns quarantined)
st, drive, folders, priv, tmp = new_world()
tc.transcribe = ok_transcribe
tc.duration_seconds = lambda path: 5 * 3600.0
res, err, out = run_process(drive, folders, priv)
check("too long: quarantined at once", res == "quarantined" and where(st, folders) == "failed")
tc.duration_seconds = lambda path: 0.0
st, drive, folders, priv, tmp = new_world()
res, err, out = run_process(drive, folders, priv)
check("unreadable audio (0 min): quarantined", res == "quarantined" and where(st, folders) == "failed")
tc.duration_seconds = lambda path: 600.0

# 5) a PC claim file
st, drive, folders, priv, tmp = new_world()
now = dt.datetime.now(dt.timezone.utc)
st["files"]["c1"] = {"id": "c1", "name": tc.PC_CLAIM_NAME, "mimeType": "application/json", "parents": ["INBOX"],
                     "modifiedTime": now.isoformat().replace("+00:00", "Z")}
check("fresh PC claim: cloud stays away", drive.pc_busy())
st["files"]["c1"]["modifiedTime"] = (now - dt.timedelta(hours=4)).isoformat().replace("+00:00", "Z")
check("stale PC claim (4 h): ignored", not drive.pc_busy())
check("the claim file is not treated as a recording", [f["name"] for f in drive.waiting()] == ["2026-10-16 MTG.m4a"])

# 6) a non-audio file is ignored; the stuck file in processing/ is released
st, drive, folders, priv, tmp = new_world()
st["files"]["t1"] = {"id": "t1", "name": "memo.txt", "mimeType": "text/plain", "parents": ["INBOX"], "modifiedTime": "2026-10-17T01:00:00Z"}
check("non-audio ignored", [f["name"] for f in drive.waiting()] == ["2026-10-16 MTG.m4a"])

# 7) an unexpected error prints only its class: the Drive folder id (inside the Google error message) must not reach the public log
class FakeHttpError(Exception):
    pass
tc.main = lambda: (_ for _ in ()).throw(FakeHttpError("<HttpError 404 when requesting https://www.googleapis.com/drive/v3/files?q='1PBN9SECRETFOLDERID' in parents>"))
buf = io.StringIO()
with contextlib.redirect_stdout(buf):
    rc = tc.safe_main()
check("unexpected error: exit code 1", rc == 1)
check("unexpected error: class name shown", "FakeHttpError" in buf.getvalue())
check("unexpected error: the folder id is NOT in the log", "SECRETFOLDERID" not in buf.getvalue() and "googleapis" not in buf.getvalue())

print("\n" + ("ALL PASSED" if not FAILS else f"{len(FAILS)} FAILED: {FAILS}"))
sys.exit(1 if FAILS else 0)
