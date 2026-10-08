"""ロードマップ Issue（ラベル `roadmap`）の本文を、マイルストーンと Issue の依存関係から作り直す。

GitHub Actions（.github/workflows/roadmap.yml）から実行する。手元で試す場合:
    GITHUB_TOKEN=$(gh auth token) GITHUB_REPOSITORY=Fialuxe/MouthOfGod_System python .github/scripts/update_roadmap.py --dry-run
--mermaid-dir <dir> を付けると、README に載せる図の Mermaid ファイル（milestones.mmd, m<番号>.mmd）も書き出す。
"""

import json
import os
import re
import sys
import urllib.request
from datetime import datetime, timedelta, timezone

REPO = os.environ.get("GITHUB_REPOSITORY", "Fialuxe/MouthOfGod_System")
OWNER, NAME = REPO.split("/")
TOKEN = os.environ["GITHUB_TOKEN"]
LABEL = "roadmap"
TITLE = "ロードマップ（自動更新）"
JST = timezone(timedelta(hours=9))
# 「対応しない」「重複」で閉じた Issue は、進み具合にも図にも含めない
SKIPPED_REASONS = {"NOT_PLANNED", "DUPLICATE"}

QUERY = """
query($owner: String!, $name: String!, $after: String) {
  repository(owner: $owner, name: $name) {
    issues(first: 100, after: $after, orderBy: {field: CREATED_AT, direction: ASC}) {
      pageInfo { hasNextPage endCursor }
      nodes {
        number title state stateReason url
        labels(first: 20) { nodes { name } }
        milestone { number }
        blockedBy(first: 50) { nodes { number } }
      }
    }
    milestones(first: 100, states: [OPEN, CLOSED]) {
      nodes { number title url state dueOn }
    }
  }
}
"""


def request(method, url, body=None):
    req = urllib.request.Request(
        url,
        method=method,
        data=None if body is None else json.dumps(body).encode(),
        headers={
            "Authorization": f"Bearer {TOKEN}",
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
        },
    )
    with urllib.request.urlopen(req) as res:
        return json.load(res)


def fetch():
    issues, milestones, after = [], None, None
    while True:
        res = request("POST", "https://api.github.com/graphql", {
            "query": QUERY,
            "variables": {"owner": OWNER, "name": NAME, "after": after},
        })
        if "errors" in res:
            sys.exit(f"GraphQL error: {res['errors']}")
        repo = res["data"]["repository"]
        issues += repo["issues"]["nodes"]
        milestones = repo["milestones"]["nodes"]
        page = repo["issues"]["pageInfo"]
        if not page["hasNextPage"]:
            return issues, milestones
        after = page["endCursor"]


def natural_key(text):
    return [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", text)]


def label_text(text, limit=22):
    text = text if len(text) <= limit else text[: limit - 1] + "…"
    # Mermaid のラベル内で意味を持つ文字を実体参照にする
    for src, dst in (("#", "#35;"), ('"', "#quot;"), ("<", "#lt;"), (">", "#gt;")):
        text = text.replace(src, dst)
    return text


def progress_bar(done, total, width=10):
    if total == 0:
        return "—"
    filled = round(width * done / total)
    return f"{'█' * filled}{'░' * (width - filled)} {done}/{total}"


def build(issues, milestones, roadmap_number):
    issues = [
        i for i in issues
        if i["number"] != roadmap_number and i["stateReason"] not in SKIPPED_REASONS
        and not {LABEL, "activity"} & {l["name"] for l in i["labels"]["nodes"]}
    ]
    by_number = {i["number"]: i for i in issues}
    for i in issues:
        i["blockers"] = [b["number"] for b in i["blockedBy"]["nodes"] if b["number"] in by_number]
        i["ms"] = i["milestone"]["number"] if i["milestone"] else None

    def is_open(n):
        return by_number[n]["state"] == "OPEN"

    def is_ready(i):
        return i["state"] == "OPEN" and not any(is_open(b) for b in i["blockers"])

    ms_list = sorted(milestones, key=lambda m: natural_key(m["title"]))
    ms_by_number = {m["number"]: m for m in ms_list}
    groups = [(m, [i for i in issues if i["ms"] == m["number"]]) for m in ms_list]
    no_ms = [i for i in issues if i["ms"] is None and i["state"] == "OPEN"]
    if no_ms:
        groups.append(({"number": None, "title": "マイルストーンなし", "url": None, "dueOn": None, "state": "OPEN"}, no_ms))

    def short(ms_number):
        if ms_number is None:
            return "なし"
        return ms_by_number[ms_number]["title"].split(" ")[0]

    now = datetime.now(JST).strftime("%Y-%m-%d %H:%M")
    out = [
        "<!-- この本文は .github/workflows/roadmap.yml が自動で書き換えます。手で編集しても上書きされます。 -->",
        f"最終更新: {now} (JST)",
        "",
        "マイルストーンの進み具合と、Issue 同士の依存関係（各 Issue 右側の **Relationships → Blocked by**）をまとめたページです。",
        "Issue の開閉・マイルストーン変更のたびに更新されます。依存関係だけを変えた場合は、1 時間以内に反映されます。",
        "",
        "## マイルストーン",
        "",
        "「すぐ着手できる」は、未完了で、先に終わっている必要がある Issue がすべて閉じているものです。",
        "",
        "| マイルストーン | 進み具合 | 期限 | すぐ着手できる |",
        "|---|---|---|---|",
    ]
    for m, members in groups:
        if m["state"] == "CLOSED" and all(i["state"] == "CLOSED" for i in members):
            continue
        done = sum(i["state"] == "CLOSED" for i in members)
        due = m["dueOn"][:10] if m["dueOn"] else ""
        ready = " ".join(f"#{i['number']}" for i in members if is_ready(i)) or "—"
        name = f"[{m['title']}]({m['url']})" if m["url"] else m["title"]
        out.append(f"| {name} | `{progress_bar(done, len(members))}` | {due} | {ready} |")

    # マイルストーン同士: A の Issue が B の Issue をブロックしていれば A → B
    edges = set()
    for i in issues:
        for b in i["blockers"]:
            src, dst = by_number[b]["ms"], i["ms"]
            if src != dst and src is not None and dst is not None:
                edges.add((src, dst))
    graphs = {}  # ファイル名 → Mermaid のコード（README に載せる画像の元）
    lines = ["flowchart LR"]
    for m, members in groups:
        if m["number"] is None:
            continue
        done = sum(i["state"] == "CLOSED" for i in members)
        ready = " ".join(f"#35;{i['number']}" for i in members if is_ready(i))
        style = ":::done" if members and done == len(members) else ""
        extra = f"<br/>すぐ着手: {ready}" if ready else ""
        lines.append(f'  m{m["number"]}["<b>{label_text(m["title"], 30)}</b><br/>{done}/{len(members)} 完了{extra}"]{style}')
    for src, dst in sorted(edges):
        lines.append(f"  m{src} --> m{dst}")
    lines.append("  classDef done fill:#d4edda,stroke:#28a745,color:#155724")
    graphs["milestones"] = "\n".join(lines)
    out += ["", "## マイルストーン同士のつながり", "", "矢印の元のマイルストーンに、先に終わらせる必要がある Issue があります。", "", "```mermaid", graphs["milestones"], "```", ""]

    out += [
        "## マイルストーンごとの Issue",
        "",
        "🟩 完了　🟨 すぐ着手できる　⬜ 待ち（先に終わらせる Issue がある）　点線 = 別マイルストーンの Issue",
        "",
    ]
    for m, members in groups:
        if not members:
            continue
        open_count = sum(i["state"] == "OPEN" for i in members)
        is_done = open_count == 0
        out += [f"<details{'' if is_done else ' open'}>", f"<summary><b>{m['title']}</b>（残り {open_count} / {len(members)}）</summary>", ""]
        lines, external = ["flowchart TD"], set()
        member_numbers = {i["number"] for i in members}
        for i in members:
            cls = "done" if i["state"] == "CLOSED" else "ready" if is_ready(i) else "waiting"
            lines.append(f'  i{i["number"]}["#35;{i["number"]} {label_text(i["title"])}"]:::{cls}')
            for b in i["blockers"]:
                if b not in member_numbers:
                    external.add(b)
                lines.append(f"  i{b} --> i{i['number']}")
        for b in sorted(external):
            x = by_number[b]
            cls = "extdone" if x["state"] == "CLOSED" else "ext"
            lines.append(f'  i{b}["#35;{b} {label_text(x["title"], 16)}<br/>（{short(x["ms"])}）"]:::{cls}')
        lines += [
            "  classDef done fill:#d4edda,stroke:#28a745,color:#155724",
            "  classDef ready fill:#fff3cd,stroke:#d39e00,color:#533f03",
            "  classDef waiting fill:#f6f8fa,stroke:#8c959f,color:#24292f",
            "  classDef ext fill:#ffffff,stroke:#8c959f,stroke-dasharray:4 3,color:#57606a",
            "  classDef extdone fill:#ffffff,stroke:#28a745,stroke-dasharray:4 3,color:#57606a",
        ]
        if m["number"] is not None:
            graphs[f"m{m['number']}"] = "\n".join(lines)
        out += ["```mermaid", *lines, "```", "", "</details>", ""]
    return "\n".join(out), graphs


def main():
    api = f"https://api.github.com/repos/{REPO}"
    found = request("GET", f"{api}/issues?labels={LABEL}&state=open&per_page=10")
    roadmap = found[0] if found else None
    issues, milestones = fetch()
    body, graphs = build(issues, milestones, roadmap["number"] if roadmap else None)
    if "--mermaid-dir" in sys.argv:
        out_dir = sys.argv[sys.argv.index("--mermaid-dir") + 1]
        os.makedirs(out_dir, exist_ok=True)
        for name, code in graphs.items():
            with open(os.path.join(out_dir, f"{name}.mmd"), "w", encoding="utf-8") as f:
                f.write(code + "\n")
    if "--dry-run" in sys.argv:
        print(body)
        return
    if roadmap:
        request("PATCH", f"{api}/issues/{roadmap['number']}", {"body": body})
        print(f"Updated #{roadmap['number']}")
    else:
        created = request("POST", f"{api}/issues", {"title": TITLE, "body": body, "labels": [LABEL]})
        print(f"Created #{created['number']} — ピン留めしておくこと")


if __name__ == "__main__":
    main()
