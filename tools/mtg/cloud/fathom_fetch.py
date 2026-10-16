"""Fetch MTG transcripts from Fathom (an AI notetaker bot that joins the Zoom meeting). Runs unattended in GitHub Actions: no PC, no admin rights.

Flow: Fathom API (X-Api-Key) -> meetings of the last days that have a transcript -> "[MM:SS] speaker: text" lines
  -> the PRIVATE transcripts repo (<date>/<name>.clean.txt, plus <name>.summary.md when Fathom made a summary)
  -> <name>.minutes.pending marker -> the cloud routine that makes the minutes (mtg-sync) is started (same as transcribe_cloud.py).
  The Whisper flow (transcribe_cloud.py) stays as the fallback for recordings that are put in the Drive inbox.

PRIVACY: this repo and its Actions logs are PUBLIC. This script never prints transcript text, speaker names, titles or ids;
it prints only counts, dates and field NAMES (never values). Never upload outputs as workflow artifacts.

The API shape is taken from Fathom's quickstart (GET /external/v1/meetings?include_transcript=true, header X-Api-Key, result in "items")
and from third-party wrappers for the transcript fields; the parser accepts both a string and an object as "speaker",
and "timestamp" as "HH:MM:SS" or "start_time" as seconds. If a meeting has a transcript that cannot be parsed, the log says which field names it has.

Usage:  python fathom_fetch.py --private-clone PATH [--days 7]
Environment: FATHOM_API_KEY, FATHOM_MIN_MINUTES (optional, default 10), MTG_ROUTINE_URL and MTG_ROUTINE_TOKEN (optional, see transcribe_cloud.py).
"""
import argparse
import datetime as dt
import importlib.util
import json
import os
import re
import sys
import tempfile
import urllib.parse
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent  # tools/mtg
JST = dt.timezone(dt.timedelta(hours=9))
API = "https://api.fathom.ai/external/v1"


def log(msg):
    print(msg, flush=True)


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


tc = _load("transcribe_cloud", HERE / "transcribe_cloud.py")  # archive(), fire_routine()
clean = _load("clean", TOOLS / "clean.py")  # load_corrections()


class Fathom:
    """The one call we need. Tests replace this class with a fake that has the same method."""

    def __init__(self, api_key):
        self.key = api_key

    def meetings(self, since: dt.datetime):
        out, cursor = [], ""
        while True:
            q = {"include_transcript": "true", "include_summary": "true", "created_after": since.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}
            if cursor:
                q["cursor"] = cursor
            req = urllib.request.Request(f"{API}/meetings?{urllib.parse.urlencode(q)}", headers={"X-Api-Key": self.key})
            with urllib.request.urlopen(req, timeout=120) as r:
                data = json.load(r)
            out += data.get("items") or []
            cursor = data.get("next_cursor") or ""
            if not cursor:
                return out


def to_seconds(v) -> float:
    """"HH:MM:SS(.mmm)", "MM:SS" or a number -> seconds."""
    if isinstance(v, (int, float)):
        return float(v)
    secs = 0.0
    for p in str(v).strip().split(":"):
        secs = secs * 60 + float(p)
    return secs


def label(seconds: float) -> str:
    """Same as clean.py: MM:SS under 1 h, H:MM:SS after."""
    s = int(seconds)
    h, m, sec = s // 3600, s % 3600 // 60, s % 60
    return f"{m:02d}:{sec:02d}" if h == 0 else f"{h}:{m:02d}:{sec:02d}"


def speaker_name(sp) -> str:
    if isinstance(sp, dict):
        return str(sp.get("display_name") or sp.get("name") or "").strip()
    return str(sp or "").strip()


def transcript_to_clean(segments, pairs) -> str:
    """Fathom transcript segments -> lines "[MM:SS] speaker: text". Consecutive segments of one speaker are joined. corrections.tsv pairs are applied."""
    lines, last = [], None
    for seg in segments or []:
        text = str(seg.get("text") or "").strip()
        if not text:
            continue
        for wrong, right in pairs:
            text = text.replace(wrong, right)
        name = speaker_name(seg.get("speaker"))
        if name and name == last and lines:
            lines[-1] += " " + text
        else:
            t = seg.get("timestamp", seg.get("start_time", 0))
            try:
                stamp = label(to_seconds(t))
            except ValueError:
                stamp = "00:00"
            lines.append(f"[{stamp}] " + (f"{name}: " if name else "") + text)
        last = name
    return "\n".join(lines)


def summary_text(m: dict) -> str:
    s = m.get("default_summary") or m.get("summary")
    if isinstance(s, dict):
        s = s.get("markdown_formatted") or s.get("text") or s.get("markdown")
    return (s or "").strip() + "\n" if isinstance(s, str) and s.strip() else ""


def meeting_start(m: dict):
    for k in ("recording_start_time", "scheduled_start_time", "created_at"):
        if m.get(k):
            return dt.datetime.fromisoformat(str(m[k]).replace("Z", "+00:00")).astimezone(JST)
    return None


def duration_minutes(m: dict) -> float:
    try:
        a = dt.datetime.fromisoformat(str(m["recording_start_time"]).replace("Z", "+00:00"))
        b = dt.datetime.fromisoformat(str(m["recording_end_time"]).replace("Z", "+00:00"))
        return (b - a).total_seconds() / 60
    except Exception:  # noqa: BLE001  no times: do not filter it out
        return 10 ** 6


def already_handled(private: Path, date: str, stem: str) -> bool:
    """This meeting, or any meeting of that day, already has a transcript or minutes: do not make them twice."""
    d = private / date
    if not d.exists():
        return False
    return (d / f"{stem}.clean.txt").exists() or any(d.glob("*.minutes.done")) or any(d.glob("*.minutes.pending"))


def load_pairs(private: Path):
    pairs = clean.load_corrections(TOOLS / "corrections.tsv")
    for d in (private, private / "config"):
        pairs += clean.load_corrections(d / "corrections.local.tsv")
    return pairs


def process(fathom, private: Path, days: int, min_minutes: int, now: dt.datetime | None = None) -> dict:
    now = now or dt.datetime.now(dt.timezone.utc)
    res = {"seen": 0, "no_transcript": 0, "short": 0, "skipped": 0, "done": 0, "failed": 0}
    pairs = load_pairs(private)
    for m in fathom.meetings(now - dt.timedelta(days=days)):
        res["seen"] += 1
        segs = m.get("transcript")
        if not isinstance(segs, list) or not segs:
            res["no_transcript"] += 1  # Fathom is still making it: the next run tries again
            continue
        if duration_minutes(m) < min_minutes:
            res["short"] += 1
            continue
        start = meeting_start(m)
        if not start:
            res["failed"] += 1
            log("failed: a meeting has no start time (fields: " + ", ".join(sorted(m)) + ")")
            continue
        date = start.date().isoformat()
        stem = f"{date} fathom {start.strftime('%H%M')}"
        if already_handled(private, date, stem):
            res["skipped"] += 1
            continue
        try:
            text = transcript_to_clean(segs, pairs)
            if not text:
                res["failed"] += 1
                log("failed: a transcript could not be read (fields: " + ", ".join(sorted(segs[0])) + ")")
                continue
            with tempfile.TemporaryDirectory() as td:
                tx = Path(td)
                (tx / f"{stem}.clean.txt").write_text(text, encoding="utf-8")
                md = summary_text(m)
                if md:
                    dest = private / date
                    dest.mkdir(parents=True, exist_ok=True)
                    (dest / f"{stem}.summary.md").write_text(md, encoding="utf-8")
                tc.archive(private, tx, stem, date)
            tc.fire_routine(date, stem)
            res["done"] += 1
            log(f"archived: fathom meeting dated {date}" + (" (with summary)" if md else ""))
        except Exception as e:  # never print the message: it can contain names or text
            res["failed"] += 1
            log(f"failed: {type(e).__name__}")
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--private-clone", required=True)
    ap.add_argument("--days", type=int, default=7)
    a = ap.parse_args()
    key = os.environ.get("FATHOM_API_KEY")
    if not key:
        log("FATHOM_API_KEY is not set: skipping")
        return 0
    res = process(Fathom(key), Path(a.private_clone), a.days, int(os.environ.get("FATHOM_MIN_MINUTES") or 10))
    log("fathom: " + ", ".join(f"{k}={v}" for k, v in res.items()))
    return 1 if res["failed"] else 0


def safe_main() -> int:
    """main(), but an unexpected error prints only its class (and the HTTP status): the message can contain a URL, and this log is public."""
    try:
        return main()
    except SystemExit:
        raise
    except Exception as e:  # noqa: BLE001
        status = getattr(e, "code", None)
        log(f"error: {type(e).__name__}" + (f" (HTTP {status})" if status else "") + " - the message is not shown (the log is public)")
        return 1


if __name__ == "__main__":
    sys.exit(safe_main())
