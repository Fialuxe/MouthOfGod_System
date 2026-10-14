"""sync_calendar.py のテスト。カレンダーにも GitHub にもつながない。"""
import datetime as dt
import pathlib
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import sync_calendar as sc  # noqa: E402

ICS = """BEGIN:VCALENDAR
VERSION:2.0
PRODID:test
BEGIN:VEVENT
UID:a
DTSTART:20261016T020000Z
DTEND:20261016T040000Z
SUMMARY:MTG
END:VEVENT
BEGIN:VEVENT
UID:b
DTSTART;VALUE=DATE:20261022
DTEND;VALUE=DATE:20261024
SUMMARY:Diorama | build
END:VEVENT
BEGIN:VEVENT
UID:c
DTSTART;TZID=Asia/Tokyo:20261014T190000
DTEND;TZID=Asia/Tokyo:20261014T210000
RRULE:FREQ=WEEKLY;COUNT=3
SUMMARY:作業会
END:VEVENT
BEGIN:VEVENT
UID:d
DTSTART;VALUE=DATE:20261201
DTEND;VALUE=DATE:20261202
SUMMARY:too far
END:VEVENT
BEGIN:VEVENT
UID:e
DTSTART;VALUE=DATE:20261019
DTEND;VALUE=DATE:20261020
END:VEVENT
END:VCALENDAR
""".replace("\n", "\r\n").encode("utf-8")

START = dt.datetime(2026, 10, 14, tzinfo=sc.JST)
END = START + dt.timedelta(days=21)


class Events(unittest.TestCase):
    def setUp(self):
        self.ev = sc.events_between(ICS, START, END)

    def test_utc_converted_to_jst(self):
        mtg = next(e for e in self.ev if e["summary"] == "MTG")
        self.assertEqual((mtg["date"], mtg["time"], mtg["end"]), (dt.date(2026, 10, 16), "11:00", "13:00"))

    def test_all_day_range_last_day_is_inclusive(self):
        d = next(e for e in self.ev if e["summary"].startswith("Diorama"))
        self.assertEqual((d["date"], d["end_date"], d["time"]), (dt.date(2026, 10, 22), dt.date(2026, 10, 23), None))

    def test_single_all_day_has_no_range(self):
        e = next(e for e in self.ev if e["date"] == dt.date(2026, 10, 19))
        self.assertIsNone(e["end_date"])
        self.assertEqual(e["summary"], "（題なし）")

    def test_weekly_is_expanded_and_far_is_dropped(self):
        self.assertEqual([e["date"].day for e in self.ev if e["summary"] == "作業会"], [14, 21, 28])
        self.assertFalse(any(e["summary"] == "too far" for e in self.ev))

    def test_sorted(self):
        keys = [(e["date"], e["time"] or "") for e in self.ev]
        self.assertEqual(keys, sorted(keys))

    def test_broken_ics_raises(self):
        with self.assertRaises(Exception):
            sc.events_between(b"not a calendar", START, END)


class Markdown(unittest.TestCase):
    def test_table(self):
        md = sc.to_markdown(sc.events_between(ICS, START, END), START)
        self.assertIn("| 10/16（金） | 11:00〜13:00 | MTG |", md)
        self.assertIn("| 10/22（木）〜10/23（金） | 終日 | Diorama ／ build |", md)

    def test_empty(self):
        self.assertIn("予定なし", sc.to_markdown([], START))


class Write(unittest.TestCase):
    def test_writes_once_then_no_change(self):
        with tempfile.TemporaryDirectory() as d:
            remote, clone = pathlib.Path(d, "remote.git"), pathlib.Path(d, "clone")
            subprocess.run(["git", "init", "-q", "--bare", "-b", "main", str(remote)], check=True)
            subprocess.run(["git", "clone", "-q", str(remote), str(clone)], check=True, capture_output=True)
            self.assertTrue(sc.write_and_push(clone, "a\n"))
            self.assertFalse(sc.write_and_push(clone, "a\n"))
            self.assertTrue(sc.write_and_push(clone, "b\n"))
            log = subprocess.run(["git", "-C", str(remote), "log", "--oneline", "main"], capture_output=True, text=True).stdout
            self.assertEqual(len(log.strip().splitlines()), 2)


class Main(unittest.TestCase):
    def test_no_secret_does_nothing(self):
        with unittest.mock.patch.dict("os.environ", {}, clear=True), \
             unittest.mock.patch.object(sys, "argv", ["x", "--private-clone", "nowhere"]):
            self.assertEqual(sc.main(), 0)


import unittest.mock  # noqa: E402

if __name__ == "__main__":
    unittest.main()
