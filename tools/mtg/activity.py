"""Dump recent GitHub activity (issues, PRs, comments, label/close events, commits)
since a timestamp, so mtg-sync knows what was already written or changed.

Usage: python activity.py <since, UTC ISO8601 e.g. 2026-10-12T05:00:00Z> <out.md> [--repo OWNER/REPO]
Needs the gh CLI (logged in). Times in the output are local time.
"""
import argparse
import json
import re
import subprocess
from datetime import datetime
from pathlib import Path

EVENTS = {"labeled", "unlabeled", "closed", "reopened", "assigned", "unassigned",
          "milestoned", "demilestoned", "merged", "renamed", "referenced"}


def gh(*args):
    r = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")
    if r.returncode:
        raise RuntimeError(r.stderr.strip())
    return r.stdout


def api(path, **params):
    q = "&".join(f"{k}={v}" for k, v in params.items())
    out = gh("api", "--paginate", f"{path}?{q}&per_page=100", "--jq", ".[]")
    return [json.loads(line) for line in out.splitlines() if line.strip()]


def local(ts):
    return datetime.fromisoformat(ts.replace("Z", "+00:00")).astimezone().strftime("%m/%d %H:%M")


def one_line(text, n=240):
    s = re.sub(r"\s+", " ", text or "").strip()
    return s if len(s) <= n else s[:n] + "…"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("since")
    ap.add_argument("out")
    ap.add_argument("--repo")
    a = ap.parse_args()
    repo = a.repo or gh("repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner").strip()

    issues = api(f"repos/{repo}/issues", since=a.since, state="all", sort="updated", direction="desc")
    comments = api(f"repos/{repo}/issues/comments", since=a.since, sort="created", direction="asc")
    events = [e for e in api(f"repos/{repo}/issues/events") if e["created_at"] >= a.since and e["event"] in EVENTS]
    commits = api(f"repos/{repo}/commits", since=a.since, sha="main")

    L = [f"# 最近の GitHub の動き（{local(a.since)} 以降、{repo}）", "",
         "mtg-sync は、ここに書かれたことを**重複して書かない**。すでにクローズ・ラベル変更・コメントされたものは、もう一度しない。", ""]

    L.append("## 更新された Issue / PR")
    for i in issues:
        kind = "PR" if "pull_request" in i else "Issue"
        state = "merged" if (i.get("pull_request") or {}).get("merged_at") else i["state"]
        labels = ",".join(l["name"] for l in i["labels"])
        L.append(f"- {kind} #{i['number']} [{state}] {i['title']}（{i['user']['login']}、更新 {local(i['updated_at'])}、作成 {local(i['created_at'])}）"
                 + (f" ラベル: {labels}" if labels else ""))

    L += ["", "## コメント（古い順）"]
    for c in comments[-150:]:
        num = c["issue_url"].rsplit("/", 1)[1]
        L.append(f"- #{num} {local(c['created_at'])} {c['user']['login']}: {one_line(c['body'])}")

    L += ["", "## ラベル・クローズ・担当などの変更（新しい順）"]
    for e in events[:200]:
        num = (e.get("issue") or {}).get("number", "?")
        extra = (e.get("label") or {}).get("name") or (e.get("assignee") or {}).get("login") or (e.get("milestone") or {}).get("title") or ""
        L.append(f"- #{num} {local(e['created_at'])} {e['event']} {extra} （{(e.get('actor') or {}).get('login', '?')}）")

    L += ["", "## main へのコミット"]
    for c in commits[:60]:
        L.append(f"- {local(c['commit']['author']['date'])} {c['sha'][:7]} {one_line(c['commit']['message'].splitlines()[0], 100)} （{(c.get('author') or {}).get('login', '?')}）")

    Path(a.out).write_text("\n".join(L) + "\n", encoding="utf-8")
    print(f"activity: {len(issues)} issues/PRs, {len(comments)} comments, {len(events)} events, {len(commits)} commits")


if __name__ == "__main__":
    main()
