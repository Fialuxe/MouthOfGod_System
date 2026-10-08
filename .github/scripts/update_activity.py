"""活動記録 Issue（ラベル `activity`）の本文と、README に載せる活動の図（SVG）を作り直す。

比べるためではなく、自分がやったことを振り返るための記録。数えるのは次の 3 つ:
- コミット: PR に含まれるコミット（squash される前の 1 つずつ）と、PR を通さず main に入ったコミット。日時は書いた時刻
- マージした PR: PR の作者に数える
- クローズした Issue: 「完了」で閉じた Issue を、閉じた人に数える

GitHub Actions（.github/workflows/roadmap.yml）から実行する。手元で試す場合:
    GITHUB_TOKEN=$(gh auth token) GITHUB_REPOSITORY=Fialuxe/MouthOfGod_System python .github/scripts/update_activity.py --dry-run --svg-dir out
"""

import json
import os
import sys
import urllib.request
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta, timezone

REPO = os.environ.get("GITHUB_REPOSITORY", "Fialuxe/MouthOfGod_System")
OWNER, NAME = REPO.split("/")
TOKEN = os.environ["GITHUB_TOKEN"]
LABEL = "activity"
TITLE = "活動記録（自動更新）"
JST = timezone(timedelta(hours=9))
WEEKS = 12  # 図に出す週の数
SKIP_LABELS = {"roadmap", "activity"}

PR_QUERY = """
query($owner: String!, $name: String!, $after: String) {
  repository(owner: $owner, name: $name) {
    pullRequests(first: 50, after: $after, orderBy: {field: CREATED_AT, direction: DESC}) {
      pageInfo { hasNextPage endCursor }
      nodes {
        number title url mergedAt author { login }
        commits(first: 100) { nodes { commit { oid authoredDate author { user { login } } } } }
      }
    }
  }
}
"""
MAIN_QUERY = """
query($owner: String!, $name: String!, $after: String) {
  repository(owner: $owner, name: $name) {
    defaultBranchRef { target { ... on Commit {
      history(first: 100, after: $after) {
        pageInfo { hasNextPage endCursor }
        nodes { oid authoredDate author { user { login } } associatedPullRequests(first: 1) { totalCount } }
      }
    } } }
  }
}
"""
ISSUE_QUERY = """
query($owner: String!, $name: String!, $after: String) {
  repository(owner: $owner, name: $name) {
    issues(first: 100, after: $after, states: [CLOSED], orderBy: {field: UPDATED_AT, direction: DESC}) {
      pageInfo { hasNextPage endCursor }
      nodes {
        number title url stateReason
        labels(first: 20) { nodes { name } }
        timelineItems(itemTypes: [CLOSED_EVENT], last: 1) { nodes { ... on ClosedEvent { createdAt actor { login } } } }
      }
    }
  }
}
"""


def request(method, url, body=None):
    req = urllib.request.Request(
        url,
        method=method,
        data=None if body is None else json.dumps(body).encode(),
        headers={"Authorization": f"Bearer {TOKEN}", "Accept": "application/vnd.github+json", "Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req) as res:
        return json.load(res)


def paginate(query, path):
    after = None
    while True:
        res = request("POST", "https://api.github.com/graphql", {"query": query, "variables": {"owner": OWNER, "name": NAME, "after": after}})
        if "errors" in res:
            sys.exit(f"GraphQL error: {res['errors']}")
        conn = res["data"]["repository"]
        for key in path:
            conn = conn[key]
        yield from conn["nodes"]
        if not conn["pageInfo"]["hasNextPage"]:
            return
        after = conn["pageInfo"]["endCursor"]


def to_day(iso):
    return datetime.fromisoformat(iso.replace("Z", "+00:00")).astimezone(JST).date()


def is_person(login):
    return login and not login.endswith("[bot]")


def collect(since):
    """(種類, 人, 日, 説明) の一覧を返す。"""
    events, seen = [], set()
    for pr in paginate(PR_QUERY, ["pullRequests"]):
        for c in pr["commits"]["nodes"]:
            c = c["commit"]
            who = (c["author"]["user"] or {}).get("login")
            if c["oid"] not in seen and is_person(who):
                seen.add(c["oid"])
                events.append(("commit", who, to_day(c["authoredDate"]), None))
        who = (pr["author"] or {}).get("login")
        if pr["mergedAt"] and is_person(who):
            events.append(("pr", who, to_day(pr["mergedAt"]), f"[#{pr['number']}]({pr['url']}) {pr['title']}"))
    for c in paginate(MAIN_QUERY, ["defaultBranchRef", "target", "history"]):
        who = (c["author"]["user"] or {}).get("login")
        if c["oid"] not in seen and c["associatedPullRequests"]["totalCount"] == 0 and is_person(who):
            seen.add(c["oid"])
            events.append(("commit", who, to_day(c["authoredDate"]), None))
        if to_day(c["authoredDate"]) < since - timedelta(days=30):
            break
    for i in paginate(ISSUE_QUERY, ["issues"]):
        closed = i["timelineItems"]["nodes"]
        if not closed or i["stateReason"] != "COMPLETED" or SKIP_LABELS & {l["name"] for l in i["labels"]["nodes"]}:
            continue
        who = (closed[0]["actor"] or {}).get("login")
        if is_person(who):
            events.append(("issue", who, to_day(closed[0]["createdAt"]), f"[#{i['number']}]({i['url']}) {i['title']}"))
    return [e for e in events if e[2] >= since]


# ---- 図 -------------------------------------------------------------------
# 1 日のマス目の色: 0 件は地の色、それ以外は青の濃淡（多いほど濃い）。ライト／ダーク別に段を選ぶ
LEVELS = [(0, "#ecebe8", "#2c2c2a"), (1, "#b7d3f6", "#184f95"), (3, "#6da7ec", "#256abf"), (6, "#2a78d6", "#3987e5"), (10, "#184f95", "#86b6ef")]
CELL, GAP, LEFT, TOP = 11, 2, 96, 34


def level(n):
    return max(i for i, (lo, _, _) in enumerate(LEVELS) if n >= lo)


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def heatmap_svg(people, per_day, start, days):
    width = LEFT + days * (CELL + GAP) + 8
    rows = len(people)
    height = TOP + rows * (CELL + GAP + 10) + 36
    styles = "".join(f".l{i}{{fill:{light}}}" for i, (_, light, _) in enumerate(LEVELS))
    dark = "".join(f".l{i}{{fill:{d}}}" for i, (_, _, d) in enumerate(LEVELS))
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-label="日ごとの活動">',
        "<style>",
        "svg{font-family:system-ui,-apple-system,'Segoe UI','Hiragino Sans','Noto Sans JP',sans-serif}",
        ".bg{fill:#fcfcfb}.t1{fill:#0b0b0b;font-size:12px}.t2{fill:#52514e;font-size:10px}" + styles,
        "@media (prefers-color-scheme: dark){.bg{fill:#1a1a19}.t1{fill:#ffffff}.t2{fill:#c3c2b7}" + dark + "}",
        "</style>",
        f'<rect class="bg" width="{width}" height="{height}" rx="6"/>',
    ]
    for d in range(days):
        day = start + timedelta(days=d)
        if day.weekday() == 0:  # 月曜に日付を書く
            out.append(f'<text class="t2" x="{LEFT + d * (CELL + GAP)}" y="{TOP - 10}">{day.month}/{day.day}</text>')
    for r, who in enumerate(people):
        y = TOP + r * (CELL + GAP + 10)
        out.append(f'<text class="t1" x="10" y="{y + CELL - 1}">{esc(who)}</text>')
        for d in range(days):
            day = start + timedelta(days=d)
            c = per_day[who].get(day, Counter())
            n = sum(c.values())
            tip = f"{who} {day.isoformat()}: コミット {c['commit']}・PR {c['pr']}・Issue {c['issue']}"
            out.append(f'<rect class="l{level(n)}" x="{LEFT + d * (CELL + GAP)}" y="{y}" width="{CELL}" height="{CELL}" rx="2"><title>{tip}</title></rect>')
    y = height - 18
    out.append(f'<text class="t2" x="10" y="{y + 9}">1 マス = 1 日（コミット＋マージした PR＋クローズした Issue の数）</text>')
    lx = width - 8 - len(LEVELS) * (CELL + GAP) - 64
    out.append(f'<text class="t2" x="{lx}" y="{y + 9}">少</text>')
    for i, (lo, _, _) in enumerate(LEVELS):
        label = "0 件" if lo == 0 else f"{lo} 件以上"
        out.append(f'<rect class="l{i}" x="{lx + 16 + i * (CELL + GAP)}" y="{y}" width="{CELL}" height="{CELL}" rx="2"><title>{label}</title></rect>')
    out.append(f'<text class="t2" x="{lx + 20 + len(LEVELS) * (CELL + GAP)}" y="{y + 9}">多</text>')
    out.append("</svg>")
    return "\n".join(out)


# ---- 本文 -----------------------------------------------------------------
def build(events, start, today):
    people = sorted({e[1] for e in events}, key=str.lower)  # 順位ではなく名前順
    per_day = defaultdict(dict)
    for kind, who, day, _ in events:
        per_day[who].setdefault(day, Counter())[kind] += 1
    days = (today - start).days + 1
    svg = heatmap_svg(people or ["（まだ記録なし）"], per_day, start, days)

    now = datetime.now(JST).strftime("%Y-%m-%d %H:%M")
    out = [
        "<!-- この本文は .github/workflows/roadmap.yml が自動で書き換えます。手で編集しても上書きされます。 -->",
        f"最終更新: {now} (JST)",
        "",
        "**比べるためではなく、自分がやったことを振り返るための記録です。** 話し合い・調べもの・ものづくり・試遊など、数字に出ない作業もたくさんあります。",
        "",
        "- コミット: PR の中のコミット 1 つずつ（書いた日）と、PR を通さず main に入ったコミット",
        "- PR: マージされた PR（作者に数える）",
        "- Issue: 「完了」で閉じた Issue（閉じた人に数える）",
        "",
        "## 週ごとの記録",
        "",
        "| 週（月曜始まり） | " + " | ".join(people) + " |",
        "|---|" + "---|" * len(people),
    ]
    monday = today - timedelta(days=today.weekday())
    for w in range(WEEKS):
        ws = monday - timedelta(weeks=w)
        cells = []
        for who in people:
            c = Counter()
            active = 0
            for d in range(7):
                dc = per_day[who].get(ws + timedelta(days=d))
                if dc:
                    c += dc
                    active += 1
            cells.append(f"コミット {c['commit']}・PR {c['pr']}・Issue {c['issue']}（{active} 日）" if active else "")
        if any(cells):
            out.append(f"| {ws.month}/{ws.day} 〜 | " + " | ".join(cells) + " |")
    out += ["", "（ ）内は、記録のあった日数です。", "", "## 最近 2 週間でやったこと", ""]
    recent = sorted((e for e in events if e[3] and e[2] >= today - timedelta(days=13)), key=lambda e: e[2], reverse=True)
    for who in people:
        mine = [e for e in recent if e[1] == who]
        if not mine:
            continue
        out += [f"<details open>", f"<summary><b>{who}</b>（{len(mine)} 件）</summary>", ""]
        for kind, _, day, text in mine:
            out.append(f"- {day.month}/{day.day} {'PR' if kind == 'pr' else 'Issue'} {text}")
        out += ["", "</details>", ""]
    if not recent:
        out.append("まだありません。")
    return "\n".join(out), svg


def main():
    today = datetime.now(JST).date()
    start = today - timedelta(days=today.weekday()) - timedelta(weeks=WEEKS - 1)
    events = collect(start)
    body, svg = build(events, start, today)
    if "--svg-dir" in sys.argv:
        out_dir = sys.argv[sys.argv.index("--svg-dir") + 1]
        os.makedirs(out_dir, exist_ok=True)
        with open(os.path.join(out_dir, "activity.svg"), "w", encoding="utf-8") as f:
            f.write(svg)
    if "--dry-run" in sys.argv:
        print(body)
        return
    api = f"https://api.github.com/repos/{REPO}"
    found = request("GET", f"{api}/issues?labels={LABEL}&state=open&per_page=10")
    if found:
        request("PATCH", f"{api}/issues/{found[0]['number']}", {"body": body})
        print(f"Updated #{found[0]['number']}")
    else:
        created = request("POST", f"{api}/issues", {"title": TITLE, "body": body, "labels": [LABEL]})
        print(f"Created #{created['number']} — ピン留めしておくこと")


if __name__ == "__main__":
    main()
