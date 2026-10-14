"""check_conflicts.py のテスト。GitHub にもルーティンにもつながない（git は一時フォルダの中だけ）。"""
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import check_conflicts as cc  # noqa: E402


def git(cwd, *args):
    subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@example.com", *args],
                   cwd=cwd, check=True, capture_output=True)


class ParseMergeTree(unittest.TestCase):
    def test_files_after_tree_id(self):
        out = "abc123\nAssets/A.prefab\ndocs/b.md\n"
        self.assertEqual(cc.parse_merge_tree(out), ["Assets/A.prefab", "docs/b.md"])

    def test_stops_at_blank_line(self):
        out = "abc123\ndocs/b.md\n\nAuto-merging docs/b.md\nCONFLICT (content)\n"
        self.assertEqual(cc.parse_merge_tree(out), ["docs/b.md"])

    def test_empty_and_tree_only(self):
        self.assertEqual(cc.parse_merge_tree(""), [])
        self.assertEqual(cc.parse_merge_tree("abc123\n"), [])

    def test_duplicates_are_merged(self):
        self.assertEqual(cc.parse_merge_tree("t\nb\na\nb\n"), ["a", "b"])


class Key(unittest.TestCase):
    def test_order_does_not_matter(self):
        self.assertEqual(cc.conflict_key("main", ["b", "a"]), cc.conflict_key("main", ["a", "b"]))

    def test_changes_with_files_or_base(self):
        k = cc.conflict_key("main", ["a"])
        self.assertNotEqual(k, cc.conflict_key("main", ["a", "b"]))
        self.assertNotEqual(k, cc.conflict_key("docs/x", ["a"]))
        self.assertEqual(len(k), 12)


class Reported(unittest.TestCase):
    def test_marker_found(self):
        key = cc.conflict_key("main", ["a"])
        self.assertTrue(cc.already_reported(["hi", f"report\n<!-- merge-doctor:{key} -->"], key))

    def test_other_key_or_no_comments(self):
        self.assertFalse(cc.already_reported(["<!-- merge-doctor:000000000000 -->"], "111111111111"))
        self.assertFalse(cc.already_reported([], "111111111111"))


class Pick(unittest.TestCase):
    def test_skips_reported_and_caps(self):
        conflicts = [{"number": n, "key": f"k{n}"} for n in (9, 3, 7, 1, 5, 2, 8)]
        picked = cc.pick_new(conflicts, reported={1, 7})
        self.assertEqual([c["number"] for c in picked], [2, 3, 5, 8, 9])

    def test_nothing_new(self):
        self.assertEqual(cc.pick_new([{"number": 1, "key": "k"}], reported={1}), [])
        self.assertEqual(cc.pick_new([], reported=set()), [])

    def test_routine_text(self):
        self.assertEqual(cc.routine_text([{"number": 3, "key": "aa"}, {"number": 5, "key": "bb"}]), "prs=3:aa,5:bb")


class Fire(unittest.TestCase):
    def test_not_configured_does_not_call(self):
        with mock.patch.dict(os.environ, {}, clear=True), mock.patch("urllib.request.urlopen") as op:
            self.assertFalse(cc.fire_routine("prs=1:a"))
            op.assert_not_called()

    def test_http_error_returns_false(self):
        err = cc.urllib.error.HTTPError("u", 403, "no", {}, None)
        env = {"CONFLICT_ROUTINE_URL": "https://example.invalid", "CONFLICT_ROUTINE_TOKEN": "t"}
        with mock.patch.dict(os.environ, env, clear=True), mock.patch("urllib.request.urlopen", side_effect=err):
            self.assertFalse(cc.fire_routine("prs=1:a"))


class RealGit(unittest.TestCase):
    """本物の git で、ぶつかる・ぶつからないを確かめる。"""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        d = self.d = self.tmp.name
        git(d, "init", "-q", "-b", "main")
        pathlib.Path(d, "a.txt").write_text("one\n")
        pathlib.Path(d, "b.txt").write_text("one\n")
        git(d, "add", ".")
        git(d, "commit", "-q", "-m", "init")
        git(d, "branch", "pr")
        pathlib.Path(d, "a.txt").write_text("main\n")
        git(d, "commit", "-q", "-am", "main side")
        git(d, "checkout", "-q", "pr")

    def tearDown(self):
        self.tmp.cleanup()

    def test_conflict(self):
        pathlib.Path(self.d, "a.txt").write_text("pr\n")
        git(self.d, "commit", "-q", "-am", "pr side")
        self.assertEqual(cc.conflicting_files("main", "pr", cwd=self.d), ["a.txt"])

    def test_no_conflict(self):
        pathlib.Path(self.d, "b.txt").write_text("pr\n")
        git(self.d, "commit", "-q", "-am", "pr side")
        self.assertEqual(cc.conflicting_files("main", "pr", cwd=self.d), [])

    def test_unknown_ref_raises(self):
        with self.assertRaises(RuntimeError):
            cc.conflicting_files("main", "no-such-branch", cwd=self.d)


if __name__ == "__main__":
    unittest.main()
