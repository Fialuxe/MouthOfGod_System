"""ロードマップ Issue（ラベル `roadmap`）の本文を、マイルストーンと Issue の依存関係から作り直す。

マイルストーンは、ゴールに向かう日付つきの区切り。期限のいちばん近い未完了の区切りを「今の区切り」として前に出す。

GitHub Actions（.github/workflows/roadmap.yml）から実行する。手元で試す場合:
    GITHUB_TOKEN=$(gh auth token) GITHUB_REPOSITORY=Fialuxe/MouthOfGod_System python .github/scripts/update_roadmap.py --dry-run
--mermaid-dir <dir> を付けると、README に載せる図の元（milestones.mmd）と、今の区切りのカード（focus.svg）も書き出す。
"""

import json
import os
import re
import sys
import urllib.request
from datetime import date, datetime, timedelta, timezone

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
        assignees(first: 5) { nodes { login } }
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


def esc(text):
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def due_date(m):
    if not m.get("dueOn"):
        return None
    return datetime.fromisoformat(m["dueOn"].replace("Z", "+00:00")).astimezone(JST).date()


def clip(text, limit=56):
    return text if len(text) <= limit else text[: limit - 1] + "…"


def focus_svg(f):
    """README に載せる「今の区切り」のカード。ライト／ダークは閲覧者の設定に合わせる。"""
    width, row = 860, 22
    doing, ready = f["doing"][:6], f["ready"][:8]
    rest_doing, rest_ready = len(f["doing"]) - len(doing), len(f["ready"]) - len(ready)
    rows = max(len(doing), 1) + (rest_doing > 0) + max(len(ready), 1) + (rest_ready > 0)
    height = 140 + row * rows + 2 * (row + 6) + 30
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-label="今の区切り">',
        "<style>",
        "svg{font-family:system-ui,-apple-system,'Segoe UI','Hiragino Sans','Noto Sans JP',sans-serif}",
        ".bg{fill:#fcfcfb;stroke:#d0cfca}.t1{fill:#0b0b0b}.t2{fill:#52514e}.track{fill:#ecebe8}.bar{fill:#2a78d6}.warn{fill:#8a4b00}",
        "@media (prefers-color-scheme: dark){.bg{fill:#1a1a19;stroke:#383835}.t1{fill:#ffffff}.t2{fill:#c3c2b7}.track{fill:#2c2c2a}.bar{fill:#3987e5}.warn{fill:#f0a64a}}",
        "</style>",
        f'<rect class="bg" x="0.5" y="0.5" width="{width - 1}" height="{height - 1}" rx="8"/>',
        '<text class="t2" x="20" y="30" font-size="12">今の区切り</text>',
        f'<text class="t1" x="20" y="54" font-size="20" font-weight="600">{esc(f["title"])}</text>',
        f'<text class="t1" x="{width - 20}" y="54" font-size="16" text-anchor="end">{esc(f["due_text"])}</text>',
    ]
    bar_w = width - 40
    filled = 0 if f["total"] == 0 else round(bar_w * f["done"] / f["total"])
    out.append(f'<rect class="track" x="20" y="68" width="{bar_w}" height="10" rx="5"/>')
    if filled:
        out.append(f'<rect class="bar" x="20" y="68" width="{filled}" height="10" rx="5"/>')
    out.append(f'<text class="t2" x="20" y="98" font-size="13">{f["done"]}/{f["total"]} 完了・残り {f["open"]} 件・待ちの最長 {f["chain"]} 段</text>')
    if f["risk"]:
        out.append(f'<text class="warn" x="{width - 20}" y="98" font-size="13" text-anchor="end">⚠ {esc(f["risk"])}</text>')
    y = 130
    for heading, items, rest in (("着手中", doing, rest_doing), ("すぐ着手できる（担当なし）", ready, rest_ready)):
        out.append(f'<text class="t1" x="20" y="{y}" font-size="13" font-weight="600">{heading}</text>')
        y += row
        for text in items or ["なし"]:
            out.append(f'<text class="{"t1" if items else "t2"}" x="32" y="{y}" font-size="13">{esc(clip(text, 70))}</text>')
            y += row
        if rest > 0:
            out.append(f'<text class="t2" x="32" y="{y}" font-size="13">ほか {rest} 件</text>')
            y += row
        y += 6
    out.append(f'<text class="t2" x="20" y="{height - 14}" font-size="11">詳しくはピン留めのロードマップ Issue。着手したら自分を Assignee にする。</text>')
    out.append("</svg>")
    return "\n".join(out)


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
        i["who"] = [a["login"] for a in i["assignees"]["nodes"]]

    def is_open(n):
        return by_number[n]["state"] == "OPEN"

    def status(i):
        if i["state"] == "CLOSED":
            return "done"
        if i["who"]:
            return "doing"
        return "waiting" if any(is_open(b) for b in i["blockers"]) else "ready"

    chain_memo = {}

    def chain(n):
        """n を終えるまでに、順番に片付ける必要がある未完了 Issue の段数（n を含む）。"""
        if n not in chain_memo:
            chain_memo[n] = 0 if not is_open(n) else 1 + max((chain(b) for b in by_number[n]["blockers"]), default=0)
        return chain_memo[n]

    def item(i, with_who=True):
        who = " " + " ".join(f"@{w}" for w in i["who"]) if with_who and i["who"] else ""
        return f"#{i['number']} {i['title']}{who}"

    # 閉じたマイルストーンは出さない。期限の近い順（期限なしは最後）
    ms_list = sorted((m for m in milestones if m["state"] == "OPEN"),
                     key=lambda m: (due_date(m) is None, due_date(m) or date.max, natural_key(m["title"])))
    ms_by_number = {m["number"]: m for m in milestones}
    groups = [(m, [i for i in issues if i["ms"] == m["number"]]) for m in ms_list]
    no_ms = [i for i in issues if i["ms"] is None and i["state"] == "OPEN"]
    if no_ms:
        groups.append(({"number": None, "title": "マイルストーンなし", "url": None, "dueOn": None, "state": "OPEN"}, no_ms))
    shown = {m["number"] for m, _ in groups}

    def short(ms_number):
        if ms_number is None:
            return "なし"
        return ms_by_number[ms_number]["title"].split(" ")[0]

    today = datetime.now(JST).date()

    def summary(m, members):
        open_items = [i for i in members if i["state"] == "OPEN"]
        due = due_date(m)
        longest = max((chain(i["number"]) for i in open_items), default=0)
        risk = ""
        if due is None:
            due_text = "期限なし"
        else:
            days = (due - today).days
            if days < 0:
                due_text = f"期限 {due.month}/{due.day}（{-days} 日過ぎ）"
                risk = "期限を過ぎている" if open_items else ""
            else:
                due_text = f"期限 {due.month}/{due.day}（あと {days} 日）"
                # 1 日に 1 段しか進まないとしても、順番待ちの段数が残りの日数を超えたら危ない
                if open_items and longest > max(days, 1):
                    risk = "順番待ちの段数が、残りの日数より多い"
        return {
            "title": m["title"], "due_text": due_text, "risk": risk, "chain": longest,
            "done": len(members) - len(open_items), "total": len(members), "open": len(open_items),
            "doing": [item(i) for i in open_items if status(i) == "doing"],
            "ready": [item(i, False) for i in open_items if status(i) == "ready"],
        }

    current = next(((m, mem) for m, mem in groups if m["number"] is not None and any(i["state"] == "OPEN" for i in mem)), None)
    focus = summary(*current) if current else None

    now = datetime.now(JST).strftime("%Y-%m-%d %H:%M")
    out = [
        "<!-- この本文は .github/workflows/roadmap.yml が自動で書き換えます。手で編集しても上書きされます。 -->",
        f"最終更新: {now} (JST)",
        "",
        "マイルストーンは、ゴールに向かう**日付つきの区切り**です。期限の近い区切りから順に終わらせます。",
        "Issue の開閉・マイルストーン・担当の変更のたびに更新されます。依存関係（各 Issue の **Relationships → Blocked by**）だけを変えた場合は、1 時間以内に反映されます。",
        "",
        "**使い方**: 「すぐ着手できる」から選び、**自分を Assignee にする**（着手中になる）。週に 1 回、この Issue を見て、今週やるものと、区切りに間に合わないものの扱い（削る・後ろへ回す）を決める。",
        "",
    ]
    if focus:
        out += [
            f"## 今の区切り: [{focus['title']}]({current[0]['url']})",
            "",
            f"{focus['due_text']}・`{progress_bar(focus['done'], focus['total'])}`・残り {focus['open']} 件・待ちの最長 {focus['chain']} 段"
            + (f"　⚠ **{focus['risk']}**" if focus["risk"] else ""),
            "",
            "**着手中**",
            "",
            *([f"- {t}" for t in focus["doing"]] or ["- なし"]),
            "",
            "**すぐ着手できる（担当なし）**",
            "",
            *([f"- {t}" for t in focus["ready"]] or ["- なし"]),
            "",
        ]
    out += [
        "## 区切りの一覧",
        "",
        "「待ちの最長」は、その区切りを終えるまでに順番に片付ける必要がある Issue の段数（前の区切りの残りも含む）。残りの日数より多いと ⚠ が付きます。",
        "",
        "| 区切り | 期限 | 進み具合 | 待ちの最長 | 着手中 | すぐ着手できる |",
        "|---|---|---|---|---|---|",
    ]
    for m, members in groups:
        s = summary(m, members)
        name = f"[{m['title']}]({m['url']})" if m["url"] else m["title"]
        doing = " ".join(f"#{i['number']}" for i in members if status(i) == "doing") or "—"
        ready = " ".join(f"#{i['number']}" for i in members if status(i) == "ready") or "—"
        out.append(f"| {name} | {s['due_text']}{' ⚠' if s['risk'] else ''} | `{progress_bar(s['done'], s['total'])}` | {s['chain']} 段 | {doing} | {ready} |")

    # 区切り同士: A の Issue が B の Issue をブロックしていれば A → B
    edges = set()
    for i in issues:
        for b in i["blockers"]:
            src, dst = by_number[b]["ms"], i["ms"]
            if src != dst and src in shown and dst in shown and src is not None and dst is not None:
                edges.add((src, dst))
    graphs = {}  # ファイル名 → Mermaid のコード（README に載せる画像の元）
    lines = ["flowchart LR"]
    for m, members in groups:
        if m["number"] is None:
            continue
        s = summary(m, members)
        style = ":::done" if members and s["open"] == 0 else ":::risk" if s["risk"] else ""
        lines.append(f'  m{m["number"]}["<b>{label_text(m["title"], 30)}</b><br/>{label_text(s["due_text"], 30)}<br/>{s["done"]}/{s["total"]} 完了"]{style}')
    for src, dst in sorted(edges):
        lines.append(f"  m{src} --> m{dst}")
    lines += [
        "  classDef done fill:#d4edda,stroke:#28a745,color:#155724",
        "  classDef risk fill:#fff3cd,stroke:#a15c00,color:#533f03",
    ]
    graphs["milestones"] = "\n".join(lines)
    out += ["", "## 区切り同士のつながり", "", "矢印の元の区切りに、先に終わらせる必要がある Issue があります。黄色は ⚠ の区切りです。", "", "```mermaid", graphs["milestones"], "```", ""]

    out += [
        "## 区切りごとの Issue",
        "",
        "🟩 完了　🟦 着手中　🟨 すぐ着手できる　⬜ 待ち（先に終わらせる Issue がある）　点線 = 別の区切りの Issue",
        "",
    ]
    for m, members in groups:
        if not members:
            continue
        open_count = sum(i["state"] == "OPEN" for i in members)
        is_current = current is not None and m is current[0]
        out += [f"<details{' open' if is_current else ''}>", f"<summary><b>{m['title']}</b>（残り {open_count} / {len(members)}）</summary>", ""]
        lines, external = ["flowchart TD"], set()
        member_numbers = {i["number"] for i in members}
        for i in members:
            who = f"<br/>@{' @'.join(i['who'])}" if i["who"] and i["state"] == "OPEN" else ""
            lines.append(f'  i{i["number"]}["#35;{i["number"]} {label_text(i["title"])}{who}"]:::{status(i)}')
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
            "  classDef doing fill:#cde2fb,stroke:#2a78d6,color:#0d366b",
            "  classDef ready fill:#fff3cd,stroke:#d39e00,color:#533f03",
            "  classDef waiting fill:#f6f8fa,stroke:#8c959f,color:#24292f",
            "  classDef ext fill:#ffffff,stroke:#8c959f,stroke-dasharray:4 3,color:#57606a",
            "  classDef extdone fill:#ffffff,stroke:#28a745,stroke-dasharray:4 3,color:#57606a",
        ]
        out += ["```mermaid", *lines, "```", "", "</details>", ""]
    return "\n".join(out), graphs, focus


def main():
    api = f"https://api.github.com/repos/{REPO}"
    found = request("GET", f"{api}/issues?labels={LABEL}&state=open&per_page=10")
    roadmap = found[0] if found else None
    issues, milestones = fetch()
    body, graphs, focus = build(issues, milestones, roadmap["number"] if roadmap else None)
    if "--mermaid-dir" in sys.argv:
        out_dir = sys.argv[sys.argv.index("--mermaid-dir") + 1]
        os.makedirs(out_dir, exist_ok=True)
        for name, code in graphs.items():
            with open(os.path.join(out_dir, f"{name}.mmd"), "w", encoding="utf-8") as f:
                f.write(code + "\n")
        if focus:
            with open(os.path.join(out_dir, "focus.svg"), "w", encoding="utf-8") as f:
                f.write(focus_svg(focus))
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
