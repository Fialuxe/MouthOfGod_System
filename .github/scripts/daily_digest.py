"""1 日のふりかえりを Slack に送る。

役割を分ける。数えれば分かること・決まりで決まることは、すべてこのスクリプトが構造的に出す。
LLM（Gemini）には、文章を読んで意味を取る必要があるところだけを任せる。

  スクリプト: 今日変わったもの（PR・Issue・状態）の一覧、区切りの進み具合の変化、外れたブロック、
              機械的に分かる見落とし（signals）、次の一手の候補と、決まりによる並び
  LLM:        各 PR で何ができるようになったか（本文を読む）、状況の変化の解釈、
              機械では分からない見落とし、次の一手を候補から選んで理由を付ける

LLM の出力は、番号や候補の ID がスクリプトの事実にあるかを検査し、通らなければ捨てる。
Gemini は、モデル × API キーの順に試す（時間切れ・モデルがなければ次のモデル、上限に当たったら次のキー）。
どれも使えない日・動きのない日も、スクリプトの部分だけで送る（何も送らない日を作らない）。

GitHub Actions（.github/workflows/daily-digest.yml）から実行する。手元で試す場合（Slack に送らず、表示するだけ）:
    GITHUB_TOKEN=$(gh auth token) GEMINI_API_KEY_1=... python .github/scripts/daily_digest.py --dry-run
"""

import json
import os
import re
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

import update_roadmap as roadmap  # 今の区切りの計算は、ロードマップと同じものを使う

REPO = roadmap.REPO
JST = roadmap.JST
API = f"https://api.github.com/repos/{REPO}"
WEB = f"https://github.com/{REPO}"
SKIP_LABELS = {"roadmap", "activity"}
PROMPT_FILE = os.path.join(os.path.dirname(__file__), "daily_digest_prompt.md")

# 上から順に試す。リポジトリの変数 GEMINI_MODELS（カンマ区切り）で上書きできる。コードを直さずに差し替えるため。
# 最後の gemini-flash-latest は Google 側で最新版を指す別名なので、上の版が消えても動く。
# 今のモデルは https://ai.google.dev/gemini-api/docs/models 、終了予定は https://ai.google.dev/gemini-api/docs/deprecations
DEFAULT_MODELS = "gemini-3.8-flash,gemini-3.5-flash,gemini-3.5-flash-lite,gemini-flash-latest"
# 上のモデルがすべて使えないとき、API のモデル一覧から見つけて試す数
DISCOVER_LIMIT = 3
GEMINI = "https://generativelanguage.googleapis.com/v1beta"
# Slack の 1 つの欄に並べる数（超えた分は「ほか n 件」）。LLM に本文を読ませる PR の数も同じ
SHOW = 8
NEXT = 3  # 次の一手の数
STALE_DAYS = 2  # この日数以上レビューを待っている PR を、見落としとして出す

SEARCH = """
query($q: String!, $after: String) {
  search(query: $q, type: ISSUE, first: 50, after: $after) {
    pageInfo { hasNextPage endCursor }
    nodes {
      ... on PullRequest {
        number title body isDraft createdAt reviewDecision author { login }
        files(first: 30) { nodes { path } }
        closingIssuesReferences(first: 10) { nodes { number } }
      }
      ... on Issue {
        number title stateReason labels(first: 10) { nodes { name } }
      }
    }
  }
}
"""


# ---- GitHub から事実を集める ------------------------------------------------
def search(q):
    out, after = [], None
    while True:
        res = roadmap.request("POST", "https://api.github.com/graphql", {"query": SEARCH, "variables": {"q": f"repo:{REPO} {q}", "after": after}})
        if "errors" in res:
            sys.exit(f"GraphQL error: {res['errors']}")
        conn = res["data"]["search"]
        out += [n for n in conn["nodes"] if n]
        if not conn["pageInfo"]["hasNextPage"]:
            return out
        after = conn["pageInfo"]["endCursor"]


def excerpt(text, limit=600):
    """PR テンプレートのコメントや空行を落として短くする。"""
    text = re.sub(r"<!--.*?-->", "", text or "", flags=re.S)
    text = re.sub(r"\n\s*\n+", "\n", text).strip()
    return text if len(text) <= limit else text[: limit - 1] + "…"


def refs(nums):
    return " ".join(f"#{n}" for n in nums)


def labels(i):
    return {l["name"] for l in i.get("labels", {}).get("nodes", [])}


def login(x):
    return (x.get("author") or {}).get("login") or "?"


def failing_ci():
    """main での最新の実行が失敗しているワークフロー。"""
    runs = roadmap.request("GET", f"{API}/actions/runs?branch=main&per_page=100")["workflow_runs"]
    latest = {}
    for r in runs:  # 新しい順に並んでいる
        if r["status"] == "completed" and r["name"] not in latest:
            latest[r["name"]] = r
    out = []
    for name, r in latest.items():
        if r["conclusion"] in ("failure", "timed_out"):
            jobs = roadmap.request("GET", f"{API}/actions/runs/{r['id']}/jobs")["jobs"]
            steps = [f"{j['name']} / {s['name']}" for j in jobs for s in j.get("steps", []) if s["conclusion"] == "failure"]
            out.append({"workflow": name, "url": r["html_url"], "failed_steps": steps[:3]})
    return out


def collect(hours):
    now = datetime.now(timezone.utc)
    since = now - timedelta(hours=hours)
    since_iso = since.strftime("%Y-%m-%dT%H:%M:%SZ")

    merged = search(f"is:pr is:merged merged:>={since_iso}")
    open_prs = [p for p in search("is:pr is:open") if not p["isDraft"]]
    closed = [i for i in search(f"is:issue closed:>={since_iso}") if not SKIP_LABELS & labels(i)]
    opened = [i for i in search(f"is:issue created:>={since_iso}") if not SKIP_LABELS & labels(i)]
    commits = roadmap.request("GET", f"{API}/commits?sha=main&since={since_iso}&per_page=50")

    issues, milestones = roadmap.fetch()
    _, _, focus = roadmap.build(issues, milestones, None)
    by_number = {i["number"]: i for i in issues if not SKIP_LABELS & labels(i)}

    def is_state(n):
        return roadmap.STATE in labels(by_number[n])

    def is_work(n):  # 区切りの進み具合に数えるもの（ロードマップと同じ決まり）
        return not is_state(n) and roadmap.OPTIONAL not in labels(by_number[n])

    done = [i["number"] for i in closed if i["stateReason"] == "COMPLETED" and i["number"] in by_number]
    via_pr = {n["number"] for p in merged for n in p["closingIssuesReferences"]["nodes"]}

    # 今日の完了で、ブロックが外れた Issue と、まだ待っている Issue
    unblocked, still_waiting = [], []
    for n, i in by_number.items():
        if i["state"] != "OPEN" or is_state(n):  # 状態は作業ではない（子が閉じれば自動で閉じる）
            continue
        blockers = [b["number"] for b in i["blockedBy"]["nodes"] if b["number"] in by_number]
        freed = [b for b in blockers if b in done]
        if not freed:
            continue
        rest = [b for b in blockers if by_number[b]["state"] == "OPEN"]
        entry = {"number": n, "title": i["title"], "freed_by": freed, "assignees": [a["login"] for a in i["assignees"]["nodes"]]}
        (still_waiting if rest else unblocked).append({**entry, "still_blocked_by": rest})

    # 今の区切りの進み具合: 今日の完了の分だけ戻すと、今朝の値になる
    if focus:
        focus["done_today"] = sum(1 for n in done if (by_number[n]["milestone"] or {}).get("number") == focus["number"] and is_work(n))

    ages = {p["number"]: (now - datetime.fromisoformat(p["createdAt"].replace("Z", "+00:00"))).days for p in open_prs}
    ci = failing_ci()

    # 機械的に分かる見落とし
    signals = []
    no_ms = [n for n, i in by_number.items() if i["state"] == "OPEN" and not i["milestone"]]
    if no_ms:
        signals.append(f"マイルストーンのない Issue {len(no_ms)} 件: {refs(no_ms[:SHOW])}")
    loose = [p["number"] for p in merged if not p["closingIssuesReferences"]["nodes"]]
    if loose:
        signals.append(f"`Closes #番号` のない PR: {refs(loose)}")
    unlinked = [n for n in done if n not in via_pr and not is_state(n)]
    if unlinked:
        signals.append(f"PR を通さずに閉じた Issue: {refs(unlinked)}")
    stale = [n for n, d in ages.items() if d >= STALE_DAYS]
    if stale:
        signals.append(f"{STALE_DAYS} 日以上レビューを待っている PR: {refs(stale)}")
    for c in ci:
        signals.append(f"CI が失敗したまま: {c['workflow']}（{' / '.join(c['failed_steps']) or '失敗したステップ不明'}）")

    # 次の一手の候補。上から決まりの優先順（CI → レビュー → 今日外れたブロック → 今の区切りですぐ着手 → 着手中）
    candidates, seen = [], set()

    def add(cid, text, rule):
        if cid not in seen:
            seen.add(cid)
            candidates.append({"id": cid, "text": text, "rule": rule})

    for c in ci:
        add(f"ci:{c['workflow']}", f"CI「{c['workflow']}」を直す", "main の CI が失敗したまま")
    for p in sorted(open_prs, key=lambda p: -ages[p["number"]]):
        if p["reviewDecision"] != "APPROVED":
            add(f"#{p['number']}", f"#{p['number']} {p['title']} をレビューする（@{login(p)}）", f"レビュー待ち {ages[p['number']]} 日")
    current = (focus or {}).get("number")
    for u in sorted(unblocked, key=lambda u: (by_number[u["number"]]["milestone"] or {}).get("number") != current):
        if not u["assignees"]:
            add(f"#{u['number']}", f"#{u['number']} {u['title']} に着手する", f"今日 {refs(u['freed_by'])} が閉じて、着手できるようになった")
    for n in (focus or {}).get("ready_numbers", []):
        add(f"#{n}", f"#{n} {by_number[n]['title']} に着手する", f"今の区切り（{focus['title']}）で、すぐ着手できる")
    for n in (focus or {}).get("doing_numbers", []):
        who = " ".join(f"@{a['login']}" for a in by_number[n]["assignees"]["nodes"])
        add(f"#{n}", f"#{n} {by_number[n]['title']} を進める（{who}）", "着手中")

    def jst(dt):
        return dt.astimezone(JST).strftime("%m/%d %H:%M")

    return {
        "period": f"{jst(since)} 〜 {jst(now)} (JST)",
        "merged": [{
            "number": p["number"], "title": p["title"], "author": login(p),
            "closes": [n["number"] for n in p["closingIssuesReferences"]["nodes"]],
            "files": [f["path"] for f in p["files"]["nodes"]][:15], "body": excerpt(p["body"]),
        } for p in merged],
        "direct_commits": [c["commit"]["message"].splitlines()[0] for c in commits if not re.search(r"\(#\d+\)$", c["commit"]["message"].splitlines()[0])],
        "done": [{"number": n, "title": by_number[n]["title"], "is_state": is_state(n), "via_pr": n in via_pr} for n in done],
        "opened": [{"number": i["number"], "title": i["title"]} for i in opened],
        "unblocked": unblocked,
        "still_waiting": still_waiting,
        "open_prs": len(open_prs),
        "focus": focus,
        "signals": signals,
        "candidates": candidates[:10],
        # LLM が書いた #番号が、本当にあるものかを確かめるための一覧
        "known": sorted(set(by_number) | {p["number"] for p in merged + open_prs}),
    }


def has_activity(f):
    return any(f[k] for k in ("merged", "direct_commits", "done", "opened"))


# ---- Gemini: 意味を読むところだけ ------------------------------------------
SCHEMA = {
    "type": "OBJECT",
    "properties": {
        "headline": {"type": "STRING", "description": "今日を一言で。40 字以内"},
        "effects": {
            "type": "ARRAY", "description": "PR ごとに、それで何ができるようになったか",
            "items": {"type": "OBJECT", "properties": {"number": {"type": "INTEGER"}, "text": {"type": "STRING"}}, "required": ["number", "text"]},
        },
        "situation": {"type": "STRING", "description": "状況がどう変わったかの解釈。1〜2 文"},
        "insights": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "signals にない見落とし。0〜2 個"},
        "next": {
            "type": "ARRAY", "description": "候補から選んだ次の一手。優先順に 1〜3 個",
            "items": {"type": "OBJECT", "properties": {"id": {"type": "STRING"}, "reason": {"type": "STRING"}}, "required": ["id", "reason"]},
        },
    },
    "required": ["headline", "effects", "situation", "insights", "next"],
    "propertyOrdering": ["headline", "effects", "situation", "insights", "next"],
}


def llm_input(f):
    """LLM に渡すのは、判断に要るものだけ。一覧や件数はスクリプトが出すので渡さない。"""
    focus = f["focus"]
    return {
        "period": f["period"],
        "merged_prs": [{k: p[k] for k in ("number", "title", "closes", "files", "body")} for p in f["merged"][:SHOW]],
        "done_issues": [{k: d[k] for k in ("number", "title", "is_state")} for d in f["done"][: SHOW * 2]],
        "new_issue_titles": [f"#{o['number']} {o['title']}" for o in f["opened"][: SHOW * 2]],
        "unblocked": [f"#{u['number']} {u['title']}" for u in f["unblocked"]],
        "current_milestone": focus and {k: focus[k] for k in ("title", "due_text", "risk", "done", "total", "done_today", "chain")},
        "signals": f["signals"],
        "candidates": [{"id": c["id"], "text": c["text"], "rule": c["rule"]} for c in f["candidates"]],
    }


class SkipModel(Exception):
    """このモデルは、どのキーでも使えない（存在しない・時間切れ・出力が検査に通らない）。"""


def gemini(path, key, body=None, timeout=120):
    req = urllib.request.Request(
        f"{GEMINI}/{path}",
        method="GET" if body is None else "POST",
        data=None if body is None else json.dumps(body).encode(),
        # キーは URL ではなくヘッダーで渡す（エラーやログに URL が出ても漏れないように）
        headers={"x-goog-api-key": key, "Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=timeout) as res:
        return json.load(res)


def with_thinking(body, model):
    """Gemini 3 以降は、考える深さを浅くして速くする（判断の材料はそろえてあるので深く考えなくてよい）。2 系はこの設定を受け付けない。"""
    if model.startswith("gemini-2"):
        return body
    config = {**body["generationConfig"], "thinkingConfig": {"thinkingLevel": "low"}}
    return {**body, "generationConfig": config}


def discover(key, tried):
    """API のモデル一覧から、テキスト生成に使える flash 系を新しい順に選ぶ。"""
    try:
        models = gemini("models?pageSize=200", key)["models"]
    except (urllib.error.URLError, TimeoutError, KeyError, ValueError) as e:
        print(f"::warning::モデル一覧を取れなかった: {e}")
        return []
    names = [
        m["name"].removeprefix("models/") for m in models
        if "generateContent" in m.get("supportedGenerationMethods", [])
        and "flash" in m["name"] and not re.search(r"image|tts|audio|live|embed|vision", m["name"])
    ]
    return [n for n in sorted(names, reverse=True) if n not in tried][:DISCOVER_LIMIT]


def validate(out, f):
    """検査に通らなければ理由を返す。通れば None。"""
    if not isinstance(out, dict) or not str(out.get("headline", "")).strip():
        return "headline がない"
    merged = {p["number"] for p in f["merged"][:SHOW]}
    if {e.get("number") for e in out.get("effects", [])} - merged:
        return "effects に、今日マージしていない PR がある"
    ids = [n.get("id") for n in out.get("next", [])]
    if set(ids) - {c["id"] for c in f["candidates"]} or len(ids) != len(set(ids)):
        return f"next に、候補にない・重複した ID がある: {ids}"
    if f["candidates"] and not ids:
        return "次の一手がない"
    text = json.dumps(out, ensure_ascii=False)
    unknown = {int(n) for n in re.findall(r"#(\d+)", text)} - set(f["known"])
    if unknown:
        return f"存在しない番号を書いた: {sorted(unknown)}"
    return None


def summarize(f):
    """(LLM の出力, 使ったモデル) を返す。どれも使えなければ (None, None)。"""
    keys = [(n, os.environ[f"GEMINI_API_KEY_{n}"]) for n in (1, 2, 3) if os.environ.get(f"GEMINI_API_KEY_{n}")]
    if not keys:
        print("::warning::GEMINI_API_KEY_1〜3 がない。LLM なしで送る")
        return None, None
    models = [m.strip() for m in (os.environ.get("GEMINI_MODELS") or DEFAULT_MODELS).split(",") if m.strip()]
    with open(PROMPT_FILE, encoding="utf-8") as fp:
        prompt = fp.read()
    body = {
        "systemInstruction": {"parts": [{"text": prompt}]},
        "contents": [{"role": "user", "parts": [{"text": json.dumps(llm_input(f), ensure_ascii=False, indent=1)}]}],
        "generationConfig": {"temperature": 0.3, "maxOutputTokens": 4096, "responseMimeType": "application/json", "responseSchema": SCHEMA},
    }
    dead = set()  # 無効なキー（以降は使わない）
    tried, discovered = [], False
    while True:
        if not models and not discovered:
            discovered = True
            alive = [k for n, k in keys if n not in dead]
            models = discover(alive[0], tried) if alive else []
            if models:
                print(f"::warning::設定したモデルがすべて使えなかったので、一覧から見つけたものを試す: {models}")
        if not models:
            print("::warning::Gemini がどれも使えなかった。LLM なしで送る")
            return None, None
        model = models.pop(0)
        tried.append(model)
        try:
            for n, key in keys:
                if n in dead:
                    continue
                try:
                    res = gemini(f"models/{model}:generateContent", key, with_thinking(body, model))
                except urllib.error.HTTPError as e:
                    detail = " ".join(e.read().decode(errors="replace").split())[:300]
                    if e.code in (401, 403) or "API_KEY" in detail:  # キーが無効（無効なキーは 400 で返る）
                        dead.add(n)
                    elif e.code in (400, 404):  # モデルがない・この設定に対応していない
                        raise SkipModel(f"{e.code} {detail}")
                    print(f"::warning::{model} / キー {n}: {e.code} {detail}")
                    continue  # 429（上限）・5xx は次のキーへ
                except (urllib.error.URLError, TimeoutError) as e:
                    if isinstance(e, TimeoutError) or isinstance(getattr(e, "reason", None), TimeoutError):
                        # 時間切れはモデル側の問題なので、キーを替えても同じ。次のモデルへ
                        raise SkipModel(f"時間切れ（キー {n}）")
                    print(f"::warning::{model} / キー {n}: {e}")
                    continue
                try:
                    text = "".join(p.get("text", "") for p in res["candidates"][0]["content"]["parts"] if not p.get("thought"))
                    out = json.loads(text)
                except (KeyError, IndexError, ValueError):
                    raise SkipModel(f"出力を読めない: {json.dumps(res, ensure_ascii=False)[:300]}")
                why = validate(out, f)
                if why:
                    raise SkipModel(f"検査に通らない: {why}")
                print(f"要約: {model}（キー {n}）")
                return out, model
        except SkipModel as e:
            print(f"::warning::{model} を飛ばす: {e}")


# ---- Slack: 組み立てはスクリプトが行う ----------------------------------------
def slack_text(text):
    """Slack の mrkdwn にする。全員への通知を止め、#番号を GitHub へのリンクにする。"""
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    text = re.sub(r"@(channel|here|everyone)\b", r"＠\1", text)
    return re.sub(r"#(\d+)", lambda m: f"<{WEB}/issues/{m[1]}|#{m[1]}>", text)


def section(title, lines):
    shown = lines[:SHOW] + ([f"ほか {len(lines) - SHOW} 件"] if len(lines) > SHOW else [])
    text = f"*{title}*\n" + "\n".join(f"• {slack_text(l)}" for l in shown)
    return {"type": "section", "text": {"type": "mrkdwn", "text": text[:2900]}}


def render(f, llm, model):
    llm = llm or {}
    effects = {e["number"]: e["text"] for e in llm.get("effects", [])}
    focus = f["focus"]

    changes = [f"#{p['number']} {p['title']}（@{p['author']}）" + (f"\n　→ {effects[p['number']]}" if p["number"] in effects else "") for p in f["merged"]]
    changes += [f"main に直接: {m}" for m in f["direct_commits"]]
    changes += [f"状態が成り立った: #{d['number']} {d['title']}" for d in f["done"] if d["is_state"]]
    changes += [f"完了（PR なし）: #{d['number']} {d['title']}" for d in f["done"] if not d["is_state"] and not d["via_pr"]]

    situation = []
    if focus:
        before = focus["done"] - focus["done_today"]
        risk = f"　⚠ {focus['risk']}" if focus["risk"] else ""
        situation.append(f"今の区切り「{focus['title']}」: {before} → {focus['done']}/{focus['total']} 完了（今日 +{focus['done_today']}）・{focus['due_text']}{risk}")
    if f["opened"]:
        situation.append(f"新しい Issue {len(f['opened'])} 件: {refs(o['number'] for o in f['opened'][:SHOW])}")
    if llm.get("situation"):
        situation.append(llm["situation"])

    unlocked = [f"#{u['number']} {u['title']}（{refs(u['freed_by'])} が閉じた）" for u in f["unblocked"]]
    unlocked += [f"#{u['number']} はあと {refs(u['still_blocked_by'])} 待ち" for u in f["still_waiting"]]

    gaps = f["signals"] + [f"見立て: {t}" for t in llm.get("insights", [])[:2]]

    by_id = {c["id"]: c for c in f["candidates"]}
    picks = [(by_id[n["id"]], n["reason"]) for n in llm.get("next", [])[:NEXT]] or [(c, c["rule"]) for c in f["candidates"][:NEXT]]
    nexts = [f"{c['text']} — {reason}" for c, reason in picks] or ["ロードマップ Issue を見て、次にやるものを決める"]

    if llm.get("headline"):
        headline = llm["headline"]
    elif has_activity(f):
        headline = f"PR {len(f['merged'])} 件をマージ・Issue {len(f['done'])} 件を完了"
    else:
        headline = "今日は記録された動きなし。次の一手から"

    today = datetime.now(JST)
    blocks = [
        {"type": "header", "text": {"type": "plain_text", "text": f"{today.month}/{today.day} のふりかえり"}},
        {"type": "section", "text": {"type": "mrkdwn", "text": f"*{slack_text(headline)}*"}},
    ]
    for title, lines in (("🛠 今日変わったこと", changes), ("📈 状況の変化", situation), ("🔓 外れたブロック", unlocked),
                         ("🔍 見落とし・気になる点", gaps), ("👉 次の一手", nexts)):
        if lines:
            blocks.append(section(title, lines))
    stats = f"マージした PR {len(f['merged'])}・完了した Issue {len(f['done'])}・新しい Issue {len(f['opened'])}・レビュー待ち PR {f['open_prs']}"
    by = f"意味の読み取り: {model}" if model else "意味の読み取り: なし（LLM を使わなかった）"
    links = f"<{WEB}/issues?q=is%3Aissue+is%3Aopen+label%3Aroadmap|ロードマップ>・<{WEB}/actions/workflows/daily-digest.yml|この通知の実行>"
    blocks.append({"type": "context", "elements": [{"type": "mrkdwn", "text": f"{stats}\n{by}・{links}"}]})
    return {"text": f"ふりかえり: {headline}", "blocks": blocks}


def post(payload):
    url = os.environ.get("SLACK_WEBHOOK_URL")
    if not url:
        sys.exit("SLACK_WEBHOOK_URL がない")
    req = urllib.request.Request(url, method="POST", data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as res:
        print(f"Slack: {res.status}")


def main():
    hours = int(sys.argv[sys.argv.index("--hours") + 1]) if "--hours" in sys.argv else 24
    f = collect(hours)
    # 動きのない日は LLM を呼ばない（読む文章がない。無料枠も使わない）
    llm, model = summarize(f) if has_activity(f) else (None, None)
    payload = render(f, llm, model)
    if "--dry-run" in sys.argv:
        print(json.dumps(llm_input(f), ensure_ascii=False, indent=1))
        print(json.dumps(llm, ensure_ascii=False, indent=1))
        print(json.dumps(payload, ensure_ascii=False, indent=1))
        return
    post(payload)


if __name__ == "__main__":
    main()
