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

# 8) starting the minutes routine: right headers and body, retry only when the request was not processed, nothing secret in the log
import os
import urllib.error
import urllib.request

calls = []
class FakeResp:
    def __enter__(s): return s
    def __exit__(s, *a): return False
def make_urlopen(outcomes):
    seq = list(outcomes)
    def fake(req, timeout=0):
        calls.append(req)
        o = seq.pop(0)
        if isinstance(o, int):
            raise urllib.error.HTTPError(req.full_url, o, "x", {}, None)
        if isinstance(o, Exception):
            raise o
        return FakeResp()
    return fake
real_urlopen = urllib.request.urlopen
real_sleep = tc.time.sleep
tc.time.sleep = lambda s: None
os.environ.pop("MTG_ROUTINE_URL", None); os.environ.pop("MTG_ROUTINE_TOKEN", None)
urllib.request.urlopen = make_urlopen([])
check("routine not configured: nothing is sent", tc.fire_routine("2026-10-16", "stem") is False and not calls and not tc.routine_configured())
os.environ["MTG_ROUTINE_URL"] = "https://api.example.test/v1/claude_code/routines/trig_SECRETID/fire"
os.environ["MTG_ROUTINE_TOKEN"] = "sk-ant-oat01-SECRETTOKEN"
check("routine configured", tc.routine_configured())

calls.clear(); urllib.request.urlopen = make_urlopen([None])
buf = io.StringIO()
with contextlib.redirect_stdout(buf):
    ok = tc.fire_routine("2026-10-16", "my stem")
req = calls[0]
check("fire: started", ok is True and len(calls) == 1)
check("fire: Bearer token and anthropic-version header", req.get_header("Authorization") == "Bearer sk-ant-oat01-SECRETTOKEN" and req.get_header("Anthropic-version") == "2023-06-01")
check("fire: body is {text: date=... name=...}", __import__("json").loads(req.data)["text"] == "date=2026-10-16 name=my stem")

calls.clear(); urllib.request.urlopen = make_urlopen([429, None])
with contextlib.redirect_stdout(io.StringIO()):
    ok = tc.fire_routine("2026-10-16", "s")
check("fire: 429 is retried once, then started", ok is True and len(calls) == 2)

calls.clear(); urllib.request.urlopen = make_urlopen([500])
buf = io.StringIO()
with contextlib.redirect_stdout(buf):
    ok = tc.fire_routine("2026-10-16", "s")
check("fire: 500 is NOT retried (it may have started a session), returns False", ok is False and len(calls) == 1)
check("fire: the log shows the status but not the URL or the token", "500" in buf.getvalue() and "SECRET" not in buf.getvalue() and "example.test" not in buf.getvalue())

calls.clear(); urllib.request.urlopen = make_urlopen([TimeoutError("timed out")])
buf = io.StringIO()
with contextlib.redirect_stdout(buf):
    ok = tc.fire_routine("2026-10-16", "s")
check("fire: a network error is NOT retried", ok is False and len(calls) == 1 and "SECRET" not in buf.getvalue())

calls.clear(); urllib.request.urlopen = make_urlopen([503, 503, 503])
with contextlib.redirect_stdout(io.StringIO()):
    ok = tc.fire_routine("2026-10-16", "s")
check("fire: 503 three times -> gives up (3 requests)", ok is False and len(calls) == 3)

# the full flow: success fires the routine and the marker says route=cloud; a routine failure does not break the archive
st, drive, folders, priv, tmp = new_world()
tc.transcribe = ok_transcribe
calls.clear(); urllib.request.urlopen = make_urlopen([500])
res, err, out = run_process(drive, folders, priv)
mk = next(priv.rglob("*.minutes.pending"))
check("flow: the routine failed but the recording is in done/ (the transcript is safe)", res == "done" and where(st, folders) == "done")
check("flow: marker says route=cloud", __import__("json").loads(mk.read_text(encoding="utf-8"))["route"] == "cloud")
os.environ.pop("MTG_ROUTINE_URL"); os.environ.pop("MTG_ROUTINE_TOKEN")
st, drive, folders, priv, tmp = new_world()
res, err, out = run_process(drive, folders, priv)
mk = next(priv.rglob("*.minutes.pending"))
check("flow: without the routine secrets the marker says route=pc", __import__("json").loads(mk.read_text(encoding="utf-8"))["route"] == "pc")
urllib.request.urlopen = real_urlopen
tc.time.sleep = real_sleep

# 9) a Zoom meeting folder in the inbox: the audio is taken, the video goes along, the per-participant folder is ignored, the topic stays out of names and log
def zoom_world():
    st, drive, folders, priv, tmp = new_world()
    del st["files"]["a1"]
    fo = "2026-10-16 19.03.11 秘密のトピック 123456789"
    st["files"]["z"] = {"id": "z", "name": fo, "mimeType": tc.FOLDER_MIME, "parents": ["INBOX"], "modifiedTime": "2026-10-16T10:00:00Z"}
    st["files"]["za"] = {"id": "za", "name": "audio123456789.m4a", "mimeType": "audio/mp4", "parents": ["z"], "modifiedTime": "2026-10-16T12:00:00Z", "size": "5000000"}
    st["files"]["zv"] = {"id": "zv", "name": "video123456789.mp4", "mimeType": "video/mp4", "parents": ["z"], "modifiedTime": "2026-10-16T12:00:00Z", "size": "90000000"}
    st["files"]["zp"] = {"id": "zp", "name": "Audio Record", "mimeType": tc.FOLDER_MIME, "parents": ["INBOX"], "modifiedTime": "2026-10-16T12:00:00Z"}
    st["files"]["zpa"] = {"id": "zpa", "name": "audio_only_山田.m4a", "mimeType": "audio/mp4", "parents": ["zp"], "modifiedTime": "2026-10-16T12:00:00Z", "size": "1"}
    return st, drive, folders, priv, tmp

st, drive, folders, priv, tmp = zoom_world()
w = drive.waiting()
check("zoom folder: only the audio file is waiting (not the video, not the per-participant folder)", [f["id"] for f in w] == ["za"])
tc.transcribe = ok_transcribe
res, err, out = run_process(drive, folders, priv, w[0])
check("zoom folder: done", res == "done" and err is None and folders["done"] in st["files"]["za"]["parents"])
check("zoom folder: the video was moved to done/ too (no second transcription)", folders["done"] in st["files"]["zv"]["parents"])
check("zoom folder: nothing is waiting afterwards", not drive.waiting())
files = sorted(p.relative_to(priv).as_posix() for p in priv.rglob("*") if p.is_file() and ".git" not in p.parts)
check("zoom folder: dated 2026-10-16, named 'zoom 1903', no topic in file names", "2026-10-16/2026-10-16 zoom 1903.clean.txt" in files and not any("秘密" in f for f in files))
check("zoom folder: log has no topic", "秘密" not in out and "123456789" not in out)

st, drive, folders, priv, tmp = zoom_world()
tc.transcribe = boom
res, err, out = run_process(drive, folders, priv, drive.waiting()[0])
check("zoom folder: a failure puts the audio back in its meeting folder (not the inbox root)", err is not None and st["files"]["za"]["parents"] == ["z"])
check("zoom folder: after the failure the same audio is waiting again, the video is not", [f["id"] for f in drive.waiting()] == ["za"])

print("\n" + ("ALL PASSED" if not FAILS else f"{len(FAILS)} FAILED: {FAILS}"))
sys.exit(1 if FAILS else 0)
