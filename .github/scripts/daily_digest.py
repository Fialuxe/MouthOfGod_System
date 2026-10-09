"""1 日のふりかえりを Slack に送る。

GitHub から「今日起きたこと」を事実として集め（マージした PR・閉じた/作った Issue・外れたブロック・CI・今の区切り）、
Gemini に次の 4 つを評価させて、Slack に送る。
  1. 今日、何が変わったか・何が実装されたか
  2. それで状況がどう変わったか
  3. これから何ができるようになるか・どのブロックが外れたか・何の見落としが分かったか
  4. これからどうすればよいか（区切り・状態を踏まえて）

Gemini は、モデル × API キーの順に試す（モデルが消えたら次のモデル、上限に当たったら次のキー）。
どれも使えないときや、出力が検査に通らないときは、LLM なしで作った要約を送る（何も送らない日を作らない）。

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
# LLM に渡す件数の上限。作ったばかりの時期などで件数が多い日に、入力が大きくなって遅くなるのを防ぐ
MAX_ITEMS = 20
# 上のモデルがすべて使えないとき、API のモデル一覧から見つけて試す数
DISCOVER_LIMIT = 3
GEMINI = "https://generativelanguage.googleapis.com/v1beta"

SEARCH = """
query($q: String!, $after: String) {
  search(query: $q, type: ISSUE, first: 50, after: $after) {
    pageInfo { hasNextPage endCursor }
    nodes {
      ... on PullRequest {
        number title url body isDraft createdAt mergedAt reviewDecision author { login }
        additions deletions changedFiles
        files(first: 30) { nodes { path } }
        closingIssuesReferences(first: 10) { nodes { number } }
      }
      ... on Issue {
        number title url body stateReason createdAt closedAt author { login }
        labels(first: 10) { nodes { name } }
        milestone { title }
      }
    }
  }
}
"""


# ---- GitHub ---------------------------------------------------------------
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


def ci_status(since_iso):
    """今も失敗しているワークフロー（main での最新の実行が失敗）と、期間中に失敗した実行の数。"""
    runs = roadmap.request("GET", f"{API}/actions/runs?branch=main&per_page=100")["workflow_runs"]
    latest = {}
    for r in runs:  # 新しい順に並んでいる
        if r["status"] == "completed" and r["name"] not in latest:
            latest[r["name"]] = r
    failing = []
    for name, r in latest.items():
        if r["conclusion"] not in ("failure", "timed_out"):
            continue
        jobs = roadmap.request("GET", f"{API}/actions/runs/{r['id']}/jobs")["jobs"]
        steps = [f"{j['name']} / {s['name']}" for j in jobs for s in j.get("steps", []) if s["conclusion"] == "failure"]
        failing.append({"workflow": name, "url": r["html_url"], "failed_steps": steps[:5], "since": r["created_at"]})
    failed_in_period = sum(1 for r in runs if r["created_at"] >= since_iso and r["conclusion"] in ("failure", "timed_out"))
    return {"failing_now": failing, "failed_runs_in_period": failed_in_period}


def collect(hours):
    now = datetime.now(timezone.utc)
    since = now - timedelta(hours=hours)
    since_iso = since.strftime("%Y-%m-%dT%H:%M:%SZ")

    merged = search(f"is:pr is:merged merged:>={since_iso}")
    open_prs = search("is:pr is:open")
    closed = [i for i in search(f"is:issue closed:>={since_iso}") if not SKIP_LABELS & labels(i)]
    opened = [i for i in search(f"is:issue created:>={since_iso}") if not SKIP_LABELS & labels(i)]
    commits = roadmap.request("GET", f"{API}/commits?sha=main&since={since_iso}&per_page=50")

    issues, milestones = roadmap.fetch()
    _, _, focus = roadmap.build(issues, milestones, None)
    by_number = {i["number"]: i for i in issues}
    ms_title = {m["number"]: m["title"] for m in milestones}

    # 今日閉じた Issue で、ブロックが外れた Issue と、まだ待っている Issue
    done_today = {i["number"] for i in closed if i["stateReason"] == "COMPLETED"}
    unblocked, still_waiting = [], []
    for i in issues:
        if i["state"] != "OPEN" or SKIP_LABELS & labels(i):
            continue
        blockers = [b["number"] for b in i["blockedBy"]["nodes"] if b["number"] in by_number]
        freed = [b for b in blockers if b in done_today]
        if not freed:
            continue
        rest = [b for b in blockers if by_number[b]["state"] == "OPEN"]
        entry = {
            "number": i["number"], "title": i["title"], "milestone": ms_title.get((i["milestone"] or {}).get("number")),
            "is_state": roadmap.STATE in labels(i), "assignees": [a["login"] for a in i["assignees"]["nodes"]], "closed_today": freed,
        }
        if rest:
            still_waiting.append({**entry, "still_blocked_by": rest})
        else:
            unblocked.append(entry)

    merged_closes = {n["number"] for p in merged for n in p["closingIssuesReferences"]["nodes"]}
    no_ms = [i["number"] for i in issues if i["state"] == "OPEN" and not i["milestone"] and not SKIP_LABELS & labels(i)]
    signals = []
    if no_ms:
        signals.append(f"マイルストーンのない open Issue: {refs(no_ms[:10])}")
    loose = [p["number"] for p in merged if not p["closingIssuesReferences"]["nodes"]]
    if loose:
        signals.append(f"Issue を閉じずにマージした PR: {refs(loose)}")
    unlinked = [i["number"] for i in closed if i["stateReason"] == "COMPLETED" and i["number"] not in merged_closes]
    if unlinked:
        signals.append(f"PR を通さずに閉じた Issue（作業の記録が PR に残っていない可能性）: {refs(unlinked)}")
    stale = [p for p in open_prs if not p["isDraft"] and (now - datetime.fromisoformat(p["createdAt"].replace("Z", "+00:00"))).days >= 2]
    if stale:
        signals.append(f"2 日以上レビューを待っている PR: {refs(p['number'] for p in stale)}")

    def jst(dt):
        return dt.astimezone(JST).strftime("%m/%d %H:%M")

    facts = {
        "period": f"{jst(since)} 〜 {jst(now)} (JST)",
        "merged_prs": [{
            "number": p["number"], "title": p["title"], "author": (p["author"] or {}).get("login"),
            "closes": [n["number"] for n in p["closingIssuesReferences"]["nodes"]],
            "size": f"+{p['additions']} -{p['deletions']}（{p['changedFiles']} ファイル）",
            "files": [f["path"] for f in p["files"]["nodes"]][:15], "body": excerpt(p["body"]),
        } for p in merged[:MAX_ITEMS]],
        "direct_commits": [c["commit"]["message"].splitlines()[0] for c in commits if not re.search(r"\(#\d+\)$", c["commit"]["message"].splitlines()[0])],
        "issues_closed": [{
            "number": i["number"], "title": i["title"], "reason": i["stateReason"],
            "is_state": roadmap.STATE in labels(i), "milestone": (i["milestone"] or {}).get("title"),
        } for i in closed[:MAX_ITEMS * 2]],
        "issues_opened": [{
            "number": i["number"], "title": i["title"], "author": (i["author"] or {}).get("login"),
            "labels": sorted(labels(i)), "milestone": (i["milestone"] or {}).get("title"), "body": excerpt(i["body"], 300),
        } for i in opened[:MAX_ITEMS]],
        # 上限を超えて省いた件数（数は Slack の末尾にも出る）
        "omitted": {"merged_prs": max(len(merged) - MAX_ITEMS, 0), "issues_opened": max(len(opened) - MAX_ITEMS, 0)},
        "unblocked": unblocked,
        "still_waiting": still_waiting,
        "open_prs": [{
            "number": p["number"], "title": p["title"], "author": (p["author"] or {}).get("login"), "draft": p["isDraft"],
            "review": p["reviewDecision"], "age_days": (now - datetime.fromisoformat(p["createdAt"].replace("Z", "+00:00"))).days,
        } for p in open_prs],
        "ci": ci_status(since_iso),
        "current_milestone": focus,
        "signals": signals,
    }
    # LLM が書いた #番号が、本当にあるものかを確かめるための一覧
    known = set(by_number) | {p["number"] for p in merged + open_prs}
    counts = {"merged_prs": len(merged), "issues_closed": len(closed), "issues_opened": len(opened)}
    return facts, known, counts


def has_activity(f):
    return any(f[k] for k in ("merged_prs", "direct_commits", "issues_closed", "issues_opened"))


# ---- Gemini ---------------------------------------------------------------
SCHEMA = {
    "type": "OBJECT",
    "properties": {
        "headline": {"type": "STRING", "description": "今日を一言で。40 字以内"},
        "changes": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "今日、何が変わったか・何が実装されたか"},
        "impact": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "それで状況がどう変わったか"},
        "unlocked": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "これから何ができるようになるか・外れたブロック"},
        "gaps": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "分かった見落とし・気になる点"},
        "next": {"type": "ARRAY", "items": {"type": "STRING"}, "description": "これからどうすればよいか"},
    },
    "required": ["headline", "changes", "impact", "unlocked", "gaps", "next"],
    "propertyOrdering": ["headline", "changes", "impact", "unlocked", "gaps", "next"],
}
LIST_KEYS = ("changes", "impact", "unlocked", "gaps", "next")


class SkipModel(Exception):
    """このモデルは、どのキーでも使えない（存在しない・出力が検査に通らない）。"""


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
    """Gemini 3 以降は、考える深さを浅くして速くする（事実の要約なので深く考えなくてよい）。2 系はこの設定を受け付けない。"""
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


def validate(out, known):
    """検査に通らなければ理由を返す。通れば None。"""
    if not isinstance(out, dict) or not str(out.get("headline", "")).strip():
        return "headline がない"
    for k in LIST_KEYS:
        if not isinstance(out.get(k), list) or not all(isinstance(x, str) for x in out[k]):
            return f"{k} が文字列の配列でない"
    if not out["next"]:
        return "次の一手がない"
    text = json.dumps(out, ensure_ascii=False)
    unknown = {int(n) for n in re.findall(r"#(\d+)", text)} - known
    if unknown:
        return f"存在しない番号を書いた: {sorted(unknown)}"
    return None


def summarize(facts, known):
    """(結果, 使ったモデル) を返す。どれも使えなければ (None, None)。"""
    keys = [(n, os.environ[f"GEMINI_API_KEY_{n}"]) for n in (1, 2, 3) if os.environ.get(f"GEMINI_API_KEY_{n}")]
    if not keys:
        print("::warning::GEMINI_API_KEY_1〜3 がない。LLM なしで送る")
        return None, None
    models = [m.strip() for m in (os.environ.get("GEMINI_MODELS") or DEFAULT_MODELS).split(",") if m.strip()]
    with open(PROMPT_FILE, encoding="utf-8") as f:
        prompt = f.read()
    body = {
        "systemInstruction": {"parts": [{"text": prompt}]},
        "contents": [{"role": "user", "parts": [{"text": "今日の事実（JSON）:\n" + json.dumps(facts, ensure_ascii=False, indent=1)}]}],
        "generationConfig": {"temperature": 0.3, "maxOutputTokens": 8192, "responseMimeType": "application/json", "responseSchema": SCHEMA},
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
                why = validate(out, known)
                if why:
                    raise SkipModel(f"検査に通らない: {why}")
                print(f"要約: {model}（キー {n}）")
                return out, model
        except SkipModel as e:
            print(f"::warning::{model} を飛ばす: {e}")


# ---- Slack ----------------------------------------------------------------
def slack_text(text):
    """Slack の mrkdwn にする。全員への通知を止め、#番号を GitHub へのリンクにする。"""
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    text = re.sub(r"@(channel|here|everyone)\b", r"＠\1", text)
    return re.sub(r"#(\d+)", lambda m: f"<{WEB}/issues/{m[1]}|#{m[1]}>", text)


def section(title, lines):
    text = f"*{title}*\n" + "\n".join(f"• {slack_text(l)}" for l in lines)
    return {"type": "section", "text": {"type": "mrkdwn", "text": text[:2900]}}


def fallback(f):
    """LLM なしの要約。事実を並べるだけ。"""
    s = {}
    s["changes"] = [f"#{p['number']} {p['title']}（@{p['author']}）" for p in f["merged_prs"]]
    s["changes"] += [f"main に直接: {m}" for m in f["direct_commits"]]
    s["changes"] += [f"#{i['number']} {i['title']} を閉じた" for i in f["issues_closed"] if i["reason"] == "COMPLETED"]
    s["impact"] = [f"新しい Issue: #{i['number']} {i['title']}" for i in f["issues_opened"]]
    s["unlocked"] = [f"#{i['number']} {i['title']}（{refs(i['closed_today'])} が閉じた）" for i in f["unblocked"]]
    s["unlocked"] += [f"#{i['number']} はあと {refs(i['still_blocked_by'])} 待ち" for i in f["still_waiting"]]
    s["gaps"] = f["signals"] + [f"CI 失敗中: {c['workflow']}" for c in f["ci"]["failing_now"]]
    focus = f["current_milestone"]
    s["next"] = [f"すぐ着手できる: {t}" for t in (focus or {}).get("ready", [])[:3]] or ["ロードマップ Issue を見て、次にやるものを決める"]
    s["headline"] = "今日の動き" if has_activity(f) else "今日は記録された動きなし"
    return s


def blocks(f, s, model, counts):
    today = datetime.now(JST)
    out = [
        {"type": "header", "text": {"type": "plain_text", "text": f"{today.month}/{today.day} のふりかえり"}},
        {"type": "section", "text": {"type": "mrkdwn", "text": f"*{slack_text(s['headline'])}*"}},
    ]
    for key, title in (("changes", "🛠 今日変わったこと"), ("impact", "📈 状況の変化"), ("unlocked", "🔓 できるようになったこと・外れたブロック"),
                       ("gaps", "🔍 見落とし・気になる点"), ("next", "👉 次の一手")):
        if s[key]:
            out.append(section(title, s[key][:6]))
    focus = f["current_milestone"]
    if focus:
        bar = roadmap.progress_bar(focus["done"], focus["total"])
        risk = f"　⚠ {focus['risk']}" if focus["risk"] else ""
        out.append({"type": "context", "elements": [{"type": "mrkdwn", "text": slack_text(f"今の区切り: *{focus['title']}*　{focus['due_text']}　`{bar}`{risk}")}]})
    waiting = sum(1 for p in f["open_prs"] if not p["draft"])
    stats = (f"マージした PR {counts['merged_prs']}・閉じた Issue {counts['issues_closed']}・新しい Issue {counts['issues_opened']}"
             f"・レビュー待ち PR {waiting}・失敗中の CI {len(f['ci']['failing_now'])}")
    by = f"要約: {model}" if model else "要約: LLM なし（Gemini が使えなかった）"
    links = f"<{WEB}/issues?q=is%3Aissue+is%3Aopen+label%3Aroadmap|ロードマップ>・<{WEB}/actions/workflows/daily-digest.yml|この通知の実行>"
    out.append({"type": "context", "elements": [{"type": "mrkdwn", "text": f"{stats}\n{by}・{links}"}]})
    return out


def post(payload):
    url = os.environ.get("SLACK_WEBHOOK_URL")
    if not url:
        sys.exit("SLACK_WEBHOOK_URL がない")
    req = urllib.request.Request(url, method="POST", data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as res:
        print(f"Slack: {res.status}")


def main():
    hours = int(sys.argv[sys.argv.index("--hours") + 1]) if "--hours" in sys.argv else 24
    facts, known, counts = collect(hours)
    # 動きのない日は LLM を呼ばない（無料枠を使わない）。次の一手だけは出す
    s, model = summarize(facts, known) if has_activity(facts) else (None, None)
    s = s or fallback(facts)
    payload = {"text": f"ふりかえり: {s['headline']}", "blocks": blocks(facts, s, model, counts)}
    if "--dry-run" in sys.argv:
        print(json.dumps(facts, ensure_ascii=False, indent=1))
        print(json.dumps(payload, ensure_ascii=False, indent=1))
        return
    post(payload)


if __name__ == "__main__":
    main()
