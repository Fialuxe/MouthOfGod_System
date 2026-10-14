"""open の PR が、ベースとコンフリクトしていないかを調べる。新しくぶつかった PR があるときだけ、Claude のルーティンを呼ぶ。

ループの「発見」を、LLM を使わずに行う（無料で速い）。調べるのは `git merge-tree` だけで、作業ツリーは変えない。
- ぶつかったファイルの組から目印（key）を作る。PR に同じ目印のコメントがすでにあれば、報告済みとして呼ばない
- 1 回に渡す PR は MAX_PRS 件まで（使用量の上限）
- ルーティンの Secrets（CONFLICT_ROUTINE_URL・CONFLICT_ROUTINE_TOKEN）が無ければ、警告を出すだけ
終了コードは常に 0（PR を止めない。コンフリクト自体は GitHub が PR に出す）。
"""
import hashlib
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

MAX_PRS = 5
MARKER = "<!-- merge-doctor:{key} -->"
FIRE_RETRY_STATUSES = {429, 500, 502, 503, 504}


def log(msg: str) -> None:
    print(msg, flush=True)


def parse_merge_tree(stdout: str) -> list[str]:
    """`git merge-tree --write-tree --name-only --no-messages` の出力から、ぶつかったファイルを取り出す。
    1 行目はツリーの ID、2 行目から空行までがファイル。"""
    files = []
    for line in stdout.splitlines()[1:]:
        if not line.strip():
            break
        files.append(line.strip())
    return sorted(set(files))


def conflicting_files(base: str, head: str, cwd: str | None = None) -> list[str]:
    """base と head をマージしたときに、ぶつかるファイル。ぶつからなければ空。git が失敗したら例外。"""
    r = subprocess.run(["git", "merge-tree", "--write-tree", "--name-only", "--no-messages", base, head],
                       cwd=cwd, capture_output=True, text=True, encoding="utf-8")
    if r.returncode == 0:
        return []
    # 1 は「ぶつかった」だが、存在しない ref でも 1 になる（そのときは出力が空）
    if r.returncode == 1 and r.stdout.strip():
        return parse_merge_tree(r.stdout)
    raise RuntimeError(f"git merge-tree failed ({r.returncode}): {r.stderr.strip()}")


def conflict_key(base_branch: str, files: list[str]) -> str:
    """同じベースで、同じファイルの組がぶつかっている間は、同じ目印になる（同じコンフリクトを二度報告しない）。"""
    text = base_branch + "\n" + "\n".join(sorted(files))
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:12]


def already_reported(comment_bodies: list[str], key: str) -> bool:
    return any(MARKER.format(key=key) in body for body in comment_bodies)


def pick_new(conflicts: list[dict], reported: set[int], limit: int = MAX_PRS) -> list[dict]:
    """報告済みでないものを、番号の小さい順（古い PR から）に limit 件まで。"""
    fresh = [c for c in conflicts if c["number"] not in reported]
    return sorted(fresh, key=lambda c: c["number"])[:limit]


def routine_text(picked: list[dict]) -> str:
    return "prs=" + ",".join(f"{c['number']}:{c['key']}" for c in picked)


def gh_json(args: list[str]):
    r = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8", check=True)
    return json.loads(r.stdout or "null")


def comment_bodies(repo: str, number: int) -> list[str]:
    pages = gh_json(["api", f"repos/{repo}/issues/{number}/comments", "--paginate", "--slurp"])
    return [c.get("body") or "" for page in (pages or []) for c in page]


def fire_routine(text: str, attempts: int = 3) -> bool:
    """ルーティンを呼ぶ。失敗しても止めない（次の push でもう一度調べる）。URL とトークンはログに出さない。"""
    url, token = os.environ.get("CONFLICT_ROUTINE_URL"), os.environ.get("CONFLICT_ROUTINE_TOKEN")
    if not (url and token):
        return False
    req = urllib.request.Request(url, data=json.dumps({"text": text}).encode("utf-8"), method="POST", headers={
        "Authorization": "Bearer " + token,
        "anthropic-version": "2023-06-01",
        "Content-Type": "application/json"})
    for i in range(attempts):
        try:
            with urllib.request.urlopen(req, timeout=60):
                log("conflict routine: started")
                return True
        except urllib.error.HTTPError as e:
            if e.code in FIRE_RETRY_STATUSES and i < attempts - 1:
                time.sleep(10 * (i + 1))
                continue
            log(f"conflict routine: not started (HTTP {e.code})")
            return False
        except Exception as e:  # noqa: BLE001  network error etc.: do not retry (the request may have been accepted)
            log(f"conflict routine: not started ({type(e).__name__})")
            return False
    return False


def main() -> int:
    repo = os.environ["GITHUB_REPOSITORY"]
    subprocess.run(["git", "fetch", "-q", "origin", "+refs/heads/*:refs/remotes/origin/*",
                    "+refs/pull/*/head:refs/remotes/pr/*"], check=True)
    prs = gh_json(["pr", "list", "--repo", repo, "--state", "open", "--limit", "100",
                   "--json", "number,baseRefName,headRefOid"])
    conflicts, reported = [], set()
    for pr in prs:
        try:
            files = conflicting_files(f"origin/{pr['baseRefName']}", pr["headRefOid"])
        except RuntimeError as e:
            log(f"#{pr['number']}: skipped ({e})")
            continue
        if not files:
            continue
        key = conflict_key(pr["baseRefName"], files)
        conflicts.append({"number": pr["number"], "key": key, "files": files})
        if already_reported(comment_bodies(repo, pr["number"]), key):
            reported.add(pr["number"])
        log(f"#{pr['number']}: conflict ({len(files)} files){' — already reported' if pr['number'] in reported else ''}")

    picked = pick_new(conflicts, reported)
    if not picked:
        log("no new conflicts")
        return 0
    text = routine_text(picked)
    if not fire_routine(text):
        for c in picked:
            log(f"::warning::PR #{c['number']} conflicts with its base: {', '.join(c['files'])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
