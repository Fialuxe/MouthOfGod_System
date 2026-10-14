"""チームの Google カレンダー（IVRC2026）の、これから 3 週間の予定を、非公開リポジトリに書く。水曜の議題（mtg-agenda）が読む。

- カレンダーは、iCal 形式の非公開 URL（Secrets の IVRC_CALENDAR_ICS）から読む。URL は秘密なので、ログにも出さない
- 予定はチームだけのものなので、公開リポジトリには書かない。書くのは非公開リポジトリの calendar/upcoming.md だけ
- 公開リポジトリのログには、件数だけを出す（予定の名前・日付は出さない）
- Secrets が無ければ、何もせず終わる
"""
import argparse
import datetime as dt
import os
import pathlib
import subprocess
import sys
import urllib.request

import icalendar
import recurring_ical_events

JST = dt.timezone(dt.timedelta(hours=9))
DAYS_AHEAD = 21
OUT = pathlib.Path("calendar") / "upcoming.md"
WEEKDAYS = "月火水木金土日"


def log(msg: str) -> None:
    print(msg, flush=True)


def events_between(ics: bytes, start: dt.datetime, end: dt.datetime) -> list[dict]:
    """start から end までの予定（繰り返しは展開する）。時刻は日本時間。終日の予定は time が None。"""
    cal = icalendar.Calendar.from_ical(ics)
    out = []
    for ev in recurring_ical_events.of(cal).between(start, end):
        s = ev.get("DTSTART").dt
        e = ev.get("DTEND").dt if ev.get("DTEND") else None
        if isinstance(s, dt.datetime):
            s = (s if s.tzinfo else s.replace(tzinfo=JST)).astimezone(JST)
            e = (e if e.tzinfo else e.replace(tzinfo=JST)).astimezone(JST) if isinstance(e, dt.datetime) else None
            out.append({"date": s.date(), "time": s.strftime("%H:%M"), "end": e.strftime("%H:%M") if e else None,
                        "end_date": None, "summary": str(ev.get("SUMMARY", "")).strip() or "（題なし）"})
        else:
            # 終日の予定。DTEND は翌日（含まない）なので、最終日は 1 日前
            last = (e - dt.timedelta(days=1)) if isinstance(e, dt.date) else s
            out.append({"date": s, "time": None, "end": None, "end_date": last if last > s else None,
                        "summary": str(ev.get("SUMMARY", "")).strip() or "（題なし）"})
    return sorted(out, key=lambda x: (x["date"], x["time"] or ""))


def day(d: dt.date) -> str:
    return f"{d.month}/{d.day}（{WEEKDAYS[d.weekday()]}）"


def to_markdown(events: list[dict], now: dt.datetime) -> str:
    lines = ["# カレンダー（IVRC2026）のこれからの予定", "",
             f"更新: {now.astimezone(JST):%Y-%m-%d %H:%M}（日本時間）。今日から {DAYS_AHEAD} 日分。"
             "GitHub Actions（Calendar sync）が毎日書き直す。手で編集しない。", ""]
    if not events:
        return "\n".join(lines + ["予定なし", ""])
    lines += ["| 日付 | 時刻 | 予定 |", "|---|---|---|"]
    for e in events:
        date = day(e["date"]) + (f"〜{day(e['end_date'])}" if e["end_date"] else "")
        time = "終日" if e["time"] is None else e["time"] + (f"〜{e['end']}" if e["end"] else "")
        lines.append(f"| {date} | {time} | {e['summary'].replace('|', '／')} |")
    return "\n".join(lines + [""])


def write_and_push(private: pathlib.Path, text: str) -> bool:
    """変わったときだけコミットして push する。push したら True。"""
    path = private / OUT
    if path.exists() and path.read_text(encoding="utf-8") == text:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")
    git = ["git", "-C", str(private)]
    subprocess.run(git + ["add", "--", str(OUT)], check=True)
    subprocess.run(git + ["-c", "user.name=calendar-sync", "-c", "user.email=calendar-sync@users.noreply.github.com",
                          "commit", "-q", "-m", "calendar: update upcoming"], check=True)
    for _ in range(3):
        if subprocess.run(git + ["push", "-q"]).returncode == 0:
            return True
        subprocess.run(git + ["pull", "-q", "--rebase"], check=True)
    raise RuntimeError("git push failed")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--private-clone", required=True, help="非公開リポジトリのクローン")
    args = ap.parse_args()
    url = os.environ.get("IVRC_CALENDAR_ICS")
    if not url:
        log("IVRC_CALENDAR_ICS is not set; nothing to do")
        return 0
    with urllib.request.urlopen(url, timeout=60) as r:
        ics = r.read()
    now = dt.datetime.now(JST)
    start = now.replace(hour=0, minute=0, second=0, microsecond=0)
    events = events_between(ics, start, start + dt.timedelta(days=DAYS_AHEAD))
    pushed = write_and_push(pathlib.Path(args.private_clone), to_markdown(events, now))
    log(f"calendar: {len(events)} events in the next {DAYS_AHEAD} days ({'updated' if pushed else 'no change'})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
