"""Tests for fathom_fetch.py. No network: Fathom is replaced by a fake; the private repo is a temp git repo with a local remote.
Run: python tools/mtg/cloud/test_fathom_fetch.py
"""
import datetime as dt
import importlib.util
import subprocess
import tempfile
from pathlib import Path

spec = importlib.util.spec_from_file_location("ff", Path(__file__).with_name("fathom_fetch.py"))
ff = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ff)

FAILS = []


def check(name, cond):
    print(("ok   " if cond else "FAIL ") + name)
    if not cond:
        FAILS.append(name)


SEGS = [
    {"speaker": {"display_name": "山田"}, "text": "こんにちは", "timestamp": "00:00:05"},
    {"speaker": {"display_name": "山田"}, "text": "始めます", "timestamp": "00:00:08"},
    {"speaker": "鈴木", "text": "はい", "timestamp": "01:01:02"},
    {"speaker": {"display_name": "鈴木"}, "text": "", "timestamp": "01:01:05"},
]


def meeting(start="2026-10-16T10:00:00Z", end="2026-10-16T11:00:00Z", segs=SEGS, summary=True):
    m = {"title": "秘密のタイトル", "recording_id": 1, "recording_start_time": start, "recording_end_time": end, "transcript": segs}
    if summary:
        m["default_summary"] = {"markdown_formatted": "## 概要\n全体"}
    return m


class FakeFathom:
    def __init__(s, items): s.items = items
    def meetings(s, since): return s.items


out = ff.transcript_to_clean(SEGS, [("こんにちは", "こんばんは")])
check("same speaker joined, correction applied", out.splitlines()[0] == "[00:05] 山田: こんばんは 始めます")
check("hour label, speaker as a plain string, empty text dropped", out.splitlines() == ["[00:05] 山田: こんばんは 始めます", "[1:01:02] 鈴木: はい"])
check("start_time in seconds works", ff.transcript_to_clean([{"speaker": "A", "text": "x", "start_time": 75}], []) == "[01:15] A: x")
check("summary text", ff.summary_text(meeting()) == "## 概要\n全体\n" and ff.summary_text(meeting(summary=False)) == "")


def sh(*a): subprocess.run(a, check=True, capture_output=True)


with tempfile.TemporaryDirectory() as td:
    td = Path(td)
    sh("git", "init", "--bare", "-b", "main", str(td / "remote.git"))
    sh("git", "clone", str(td / "remote.git"), str(td / "private"))
    private = td / "private"
    sh("git", "-C", str(private), "config", "user.name", "t")
    sh("git", "-C", str(private), "config", "user.email", "t@example.invalid")
    sh("git", "-C", str(private), "commit", "--allow-empty", "-m", "init")
    sh("git", "-C", str(private), "push", "-u", "origin", "HEAD:main")
    now = dt.datetime(2026, 10, 17, tzinfo=dt.timezone.utc)

    items = [meeting(), meeting(segs=None, start="2026-10-15T10:00:00Z"), meeting(end="2026-10-14T10:03:00Z", start="2026-10-14T10:00:00Z")]
    r = ff.process(FakeFathom(items), private, 7, 10, now)
    d = private / "2026-10-16"
    check("1 done, 1 without transcript, 1 short", (r["done"], r["no_transcript"], r["short"], r["failed"]) == (1, 1, 1, 0))
    check("clean.txt archived, named 'fathom 1900' (JST), no title in names", (d / "2026-10-16 fathom 1900.clean.txt").read_text(encoding="utf-8").startswith("[00:05] 山田:")
          and not any("秘密" in p.name for p in d.iterdir()))
    check("summary and pending marker saved", (d / "2026-10-16 fathom 1900.summary.md").exists() and (d / "2026-10-16 fathom 1900.minutes.pending").exists())
    check("pushed to the remote", subprocess.run(["git", "-C", str(td / "remote.git"), "log", "--oneline"], capture_output=True, text=True).stdout.count("cloud") == 1)
    r = ff.process(FakeFathom([meeting()]), private, 7, 10, now)
    check("second run does nothing", r["done"] == 0 and r["skipped"] == 1)
    r = ff.process(FakeFathom([meeting(segs=[{"foo": "bar"}], start="2026-10-17T10:00:00Z", end="2026-10-17T11:00:00Z")]), private, 7, 10, now)
    check("unreadable transcript: counted as failed, no crash", r["failed"] == 1 and r["done"] == 0)

print("FAILED: " + ", ".join(FAILS) if FAILS else "all ok")
raise SystemExit(1 if FAILS else 0)
