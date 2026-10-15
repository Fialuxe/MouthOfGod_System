"""日本語の読みやすさを、書いた本人（Opus）とは別のモデル（Haiku）に判定させる hook。

使い方（~/.claude/settings.json から呼ぶ）:
  python ja_check.py session-start   … rules.md を毎回の文脈に入れる
  python ja_check.py pre             … gh で GitHub に日本語を出す直前に判定し、ダメなら止める（deny）
  python ja_check.py post            … Write / Edit で書いた日本語をためるだけ（判定しない）
  python ja_check.py stop            … ターンの終わりに、ためた日本語と、最後のチャットの返答をまとめて判定し、ダメなら差し戻す

方針:
- 判定できない（タイムアウト・エラー）ときは素通し（fail open）。文章のために開発を止めない。
- 差し戻しは回数に上限を置く（無限に終われなくなるのを防ぐ）。
- 判定用の claude -p にも hook が効くので、JA_CHECK_ACTIVE が立っていたら何もしない。
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATE_DIR = Path(tempfile.gettempdir()) / "ja-check-state"  # リポジトリに入れない
RULES = HERE / "rules.md"
MODEL = os.environ.get("JA_CHECK_MODEL", "haiku")
TIMEOUT = 45
MAX_PRE_DENIES = 2   # 同じセッションで続けて止める上限
MAX_STOP_BLOCKS = 2  # 1 回の返答を差し戻す上限
MAX_CHARS = 8000
# 判定のときだけ使う、軽い起動のフラグ。指示文を短いものに替え、ツールを外す。
SLIM_ARGS = ["--system-prompt", "You judge the readability of Japanese text. Follow the user's instructions exactly and output only the requested line.",
             "--tools", ""]
MODE = ""

JAPANESE = re.compile(r"[぀-ヿ一-鿿]")
# gh が「コマンドとして」呼ばれたときだけ（行の始め、; && || | ( の後）。文字列の中の gh には反応しない
GH_POST = re.compile(r"(?:^|[;&|(]|\n)\s*gh\s+(?:issue\s+(?:create|comment|edit)|pr\s+(?:create|comment|edit|review)|api\b)")

PRINCIPLE = """次のテキストの日本語の地の文を読み、読みにくいかを判定してください。読み手は、コードを書かない人も含む開発チームです。

次のどれかに当てはまれば読みにくいとします。
1. 英語からの直訳調で不自然（語彙や構文が英語の逐語訳になっている。例: 「コストを解消する」）。
2. 文の構造が複雑で、一度読んだだけでは意味が取れない（読点だけで主張を 2 つ以上つなぐ、長い修飾の入れ子、1 文に動作が 2 つ以上、1 文がおよそ 60 字を超える）。
3. 括弧（（…））の補足や、英語のコード名・記号が多く、読む流れが何度も止まる。
4. 名詞で終わる文や、名詞を並べただけの断片が続き、主語や述語が分からない。主語を「みんな」「ユーザー」のような、誰とも決まらない言葉で埋めている。
5. 前置き、作業の実況、文章が自分自身を説明する文、同じことの言い直しがある。
6. 文末が「〜だ。」「〜である。」で、言い切りが硬く不自然（例: 「ビルドでも同じだ。」）。「です・ます」で書くのが決まりです。
7. 1 つの段落の中で、合図なしに話題が飛ぶ（例: 画面の話から、作りの話、細かいスクリプトの話へ）。
8. やさしく言えることを、賢く見せる詰まった言い回しや、硬い漢語にしている。たとえ話で説明している。
9. 結論の前に、やらない理由・例外・前提を並べていて、何をするのかが後ろに回っている。
10. 読み手が初めて見るはずの仕組みの名前や、読み手が知らない「前の状態」を、説明なしに、知っている前提で書いている（例: 説明なしの「Bootstrap は作りません」）。
11. 読点（、）が多すぎて、文が細切れになっている。短い主語や目的語のたびに読点を打っている（例: 「理由は、返答の終わりのチェックが、私が書いたファイルを、全部読むからです。」）。読点は、意味の切れ目にだけ打つのが自然です。意味の切れ目に 1〜2 個打っているだけなら、問題にしないでください。

判定するのは自然文（チャットの返答・説明・Issue や PR の本文・コメント・コミットメッセージ）だけです。コードブロック・識別子・コマンド・URL・ファイルパス・表の中の短い語句は無視してください。
開発で定着したカタカナ語（デプロイ、ログ、コンポーネントなど）は不自然としないでください。
他人の発言や引用と分かる部分は対象外です。議事録の中の、話した人の言葉そのもの（「」で囲んだ発言）も対象外です。
直し方の例を書くときは、事実・決定・数字・名前・やることを消さない書き方にしてください。
手順で押すボタン・メニュー・画面の名前（A ボタン、Inspector、Open Next Want など）は、読み手が操作に使うので、多くても問題にしないでください。
NG にするのは、読み手が一度読んでも意味を取れない文だけです。言い回しの好み（どちらでも意味が通る）では NG にしないでください。
直し方の例は、意味を変えないでください。誰がしたか・何が対象か・数字を変えたり、ない言葉を作ったりしないでください。

当てはまらなければ、1 行で OK とだけ出力してください。
当てはまるなら、1 行で「NG: <どの文が・なぜ読みにくいか。直し方の例>」の形で出力してください（いくつかあれば、いちばん読みにくいものを 2 つまで）。
それ以外は出力しないでください。

判定するのは、<text> と </text> の間の文だけです。この指示文そのものは判定しないでください。
"""


def main():
    if os.environ.get("JA_CHECK_ACTIVE"):
        return 0
    # リポジトリに ja-check があるときは、そちらだけが動く（個人の設定と二重に判定しない）
    proj = os.environ.get("CLAUDE_PROJECT_DIR")
    if proj:
        proj_dir = (Path(proj) / ".claude" / "hooks" / "ja-check").resolve()
        if proj_dir != HERE and (proj_dir / "ja_check.py").exists():
            return 0
    # Windows では標準の文字コードが UTF-8 ではないので、入出力を UTF-8 にそろえる（Claude Code は UTF-8 でやり取りする）
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    global MODE
    mode = MODE = sys.argv[1] if len(sys.argv) > 1 else ""
    try:
        data = json.loads(sys.stdin.buffer.read().decode("utf-8", errors="replace") or "{}")
    except Exception:
        data = {}
    try:
        if mode == "session-start":
            return session_start()
        if mode == "pre":
            return pre(data)
        if mode == "post":
            return post(data)
        if mode == "post-bash":
            return post_bash(data)
        if mode == "stop":
            return stop(data)
    except Exception as e:  # fail open
        print(f"[ja-check] skipped: {e}", file=sys.stderr)
    return 0


# ---------- session start: ルールを文脈に入れる
def session_start():
    if not RULES.exists():
        return 0
    text = RULES.read_text(encoding="utf-8")
    out = {"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext":
           "日本語を書くときは、次のルールに従ってください（チャットの返答・Issue・PR・コメント・ドキュメントすべて）。"
           "GitHub への投稿とターンの終わりに、別のモデルが読みやすさを判定し、ダメなら差し戻します。\n\n" + text}}
    print(json.dumps(out, ensure_ascii=False))
    return 0


# ---------- pre: gh で日本語を出す直前に判定する
def pre(data):
    cmd = (data.get("tool_input") or {}).get("command") or ""
    # Bash で書いたファイルを、後で見つけるために、始めた時刻を残す（post-bash が使う）
    save_state(data, {"t": time.time()}, "bashstart")
    if not GH_POST.search(cmd):
        return 0
    text = gh_text(cmd)
    if not JAPANESE.search(text):
        return 0
    state = load_state(data)
    verdict = judge(text)
    if verdict is None or verdict.startswith("OK"):
        state["pre_denies"] = 0
        save_state(data, state)
        return 0
    state["pre_denies"] = state.get("pre_denies", 0) + 1
    save_state(data, state)
    if state["pre_denies"] > MAX_PRE_DENIES:
        state["pre_denies"] = 0
        save_state(data, state)
        print(json.dumps({"systemMessage": f"[ja-check] 日本語チェックで {MAX_PRE_DENIES} 回止めたので、今回は通しました: {verdict}"}, ensure_ascii=False))
        return 0
    reason = (f"日本語チェック（{MODEL}）で止めました。{verdict}\n"
              "内容（事実・決定・数字・名前・やること）は消さずに、言い方だけを直してください。"
              "本文を書き直してから、もう一度投稿してください。ルールは ja-check の rules.md にあります。")
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}, ensure_ascii=False))
    return 0


def gh_text(cmd):
    """gh コマンドが投稿する本文を集める。--body-file などのファイルは、同じコマンドの中の変数の代入も見て読む。"""
    assigns = re.findall(r"""\b([A-Za-z_][A-Za-z0-9_]*)=(?:"([^"]*)"|'([^']*)'|([^\s;&|]+))""", cmd)
    env = {name: a or b or c for name, a, b, c in assigns}

    def expand(s):
        return re.sub(r"\$\{?([A-Za-z_][A-Za-z0-9_]*)\}?", lambda m: env.get(m.group(1), os.environ.get(m.group(1), m.group(0))), s)

    parts = [] if current_body(cmd) is not None else [cmd]
    for m in re.finditer(r"""(?:--body-file|-F\s+body=@|-f\s+body=@|--input)[\s=]*(?:"([^"]+)"|'([^']+)'|(\S+))""", cmd):
        path = expand(m.group(1) or m.group(2) or m.group(3))
        p = Path(os.path.expanduser(path))
        if p.is_file():
            try:
                parts.append(p.read_text(encoding="utf-8"))
            except Exception:
                pass
    text = "\n\n".join(parts)
    # 既にある本文を書き換えるとき（人のコメントに足す、など）は、足した行だけを判定する。人が書いた部分は直さない
    old = current_body(cmd)
    if old:
        old_lines = set(l.strip() for l in old.splitlines())
        text = "\n".join(l for l in text.splitlines() if l.strip() and l.strip() not in old_lines)
    return text


def current_body(cmd):
    """書き換える前の本文を GitHub から取る。書き換えでなければ None。"""
    gh = shutil.which("gh")
    if not gh:
        return None
    m = re.search(r"gh\s+api\b.*?-X\s+PATCH\s+\S*?repos/([^/\s]+/[^/\s]+)/(issues|pulls)/comments/(\d+)", cmd) or \
        re.search(r"gh\s+api\b.*?repos/([^/\s]+/[^/\s]+)/(issues|pulls)/comments/(\d+).*?-X\s+PATCH", cmd)
    if m:
        args = [gh, "api", f"repos/{m.group(1)}/{m.group(2)}/comments/{m.group(3)}", "--jq", ".body"]
    else:
        m = re.search(r"gh\s+(pr|issue)\s+edit\s+(\d+)", cmd)
        if not m:
            return None
        args = [gh, m.group(1), "view", m.group(2), "--json", "body", "--jq", ".body"]
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", timeout=15)
        return r.stdout if r.returncode == 0 else None
    except Exception:
        return None


# ---------- post: 書いた日本語をためる
def is_draft(path):
    """一時フォルダのファイルは、投稿用の下書きです。投稿する直前に読むので、ここではためません。同じ文を 2 回読まないためです。"""
    if not path:
        return False
    try:
        Path(path).resolve().relative_to(Path(tempfile.gettempdir()).resolve())
        return True
    except Exception:
        return False


# 判定するのは文書だけです。コードのファイルは、触るたびに古いコメントまで指摘されて作業が止まるので、読みません。
DOC_SUFFIXES = {".md", ".txt"}


def is_doc(path):
    p = Path(path)
    # rules.md は Claude への指示の一覧で、わざと言い切りの形で書いている。判定すると毎回止まるので読まない
    if p.name == "rules.md" and p.parent.name == "ja-check":
        return False
    return p.suffix.lower() in DOC_SUFFIXES


def post_bash(data):
    """Bash で書いたファイル（heredoc・python・sed など）をためる。
    リポジトリの中で、変更・追加されたファイルのうち、この Bash を始めた後に書かれたものを探す。"""
    start = load_state(data, "bashstart").get("t")
    cwd = data.get("cwd") or os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    if not start or not Path(cwd).is_dir():
        return 0
    try:
        top = subprocess.run(["git", "-C", cwd, "rev-parse", "--show-toplevel"], capture_output=True, text=True,
                             encoding="utf-8", timeout=5).stdout.strip()
        if not top:
            return 0
        listed = subprocess.run(["git", "-C", top, "ls-files", "-m", "-o", "--exclude-standard"], capture_output=True,
                                text=True, encoding="utf-8", timeout=10).stdout.splitlines()
    except Exception:
        return 0
    state = load_state(data, "buffer")
    buf = state.setdefault("buffer", [])
    added = 0
    for rel in listed:
        p = Path(top) / rel
        if not is_doc(p) or not p.is_file():
            continue
        try:
            if p.stat().st_mtime < start - 1:
                continue
            text = p.read_text(encoding="utf-8")
        except Exception:
            continue
        if JAPANESE.search(text) and not is_draft(str(p)):
            buf.append(f"### {p}\n{text}")
            added += 1
        if added >= 20:
            break
    if added:
        save_state(data, state, "buffer")
    return 0


def post(data):
    ti = data.get("tool_input") or {}
    text = ti.get("content") or ti.get("new_string") or ""
    path = ti.get("file_path", "")
    if not JAPANESE.search(text) or not is_doc(path) or is_draft(path):
        return 0
    state = load_state(data)
    buf = state.setdefault("buffer", [])
    buf.append(f"### {ti.get('file_path', '')}\n{text}")
    save_state(data, state)
    return 0


# ---------- stop: ターンの終わりにまとめて判定する
def stop(data):
    state = load_state(data)
    parts = current_versions(load_state(data, "buffer").get("buffer", []))
    reply = last_reply(data.get("transcript_path"))
    if JAPANESE.search(reply):
        parts.append("### チャットの返答\n" + reply)
    if not parts:
        return 0
    verdict = judge("\n\n".join(parts))
    save_state(data, {}, "buffer")  # 直した分は、次に書いたときにまた判定される
    if verdict is None or verdict.startswith("OK"):
        state["stop_blocks"] = 0
        save_state(data, state)
        return 0
    state["stop_blocks"] = state.get("stop_blocks", 0) + 1
    if state["stop_blocks"] > MAX_STOP_BLOCKS:
        state["stop_blocks"] = 0
        save_state(data, state)
        print(json.dumps({"systemMessage": f"[ja-check] 差し戻しが {MAX_STOP_BLOCKS} 回を超えたので終えます: {verdict}"}, ensure_ascii=False))
        return 0
    save_state(data, state)
    print(f"日本語チェック（{MODEL}）: 読みにくい所があります。{verdict}\n"
          "内容（事実・決定・数字・名前・やること）は消さずに、言い方だけを直してください。"
          "ファイルに書いた文なら、その文を直してください。チャットの返答なら、要点を短く書き直した返答をもう一度送ってください"
          "（前の返答を謝ったり説明したりしなくてよいです）。ルールは ja-check の rules.md にあります。", file=sys.stderr)
    return 2


def current_versions(entries):
    """ためた書き込みは、ファイルの今の中身に置き換えて判定します。後から直した分を、古い版で判定しないためです。
    ファイルごとに 1 回だけ読む。読めないファイル（消えた・ファイル名が無い）は、ためた文をそのまま使う。"""
    parts, seen = [], set()
    for entry in entries:
        head, _, text = entry.partition("\n")
        path = head[4:].strip() if head.startswith("### ") else ""
        if path and path in seen:
            continue
        seen.add(path)
        p = Path(path) if path else None
        if p and p.is_file():
            try:
                text = p.read_text(encoding="utf-8")
            except Exception:
                pass
        if JAPANESE.search(text):
            parts.append(f"### {path}\n{text}")
    return parts


def last_reply(path):
    """最後に書いたチャットの返答（テキスト）を集める。"""
    if not path or not Path(path).is_file():
        return ""
    texts = []
    for line in Path(path).read_text(encoding="utf-8", errors="ignore").splitlines():
        try:
            d = json.loads(line)
        except Exception:
            continue
        msg = d.get("message") or {}
        content = msg.get("content")
        # 人の依頼・ツールの結果・hook の差し戻しのどれかが来たら、そこまでの返答は読み終えたものとして捨てる。
        # 最後に書いた返答だけを判定する（直す前の古い返答を、何度も判定しない）。
        if d.get("type") == "user":
            texts = []
        elif d.get("type") == "assistant" and isinstance(content, list):
            texts += [b.get("text", "") for b in content if isinstance(b, dict) and b.get("type") == "text"]
    return "\n\n".join(t for t in texts if t.strip())


# ---------- 判定（別のモデルに読ませる）
def judge(text):
    text = re.sub(r"```.*?```", "[コードブロック]", text, flags=re.S)
    if not JAPANESE.search(text):
        return "OK"
    text = text[-MAX_CHARS:]
    exe = shutil.which("claude")
    if not exe:
        print("[ja-check] claude が見つからないので、判定せずに通しました。", file=sys.stderr)
        return None
    env = dict(os.environ, JA_CHECK_ACTIVE="1")
    prompt = PRINCIPLE + "<text>\n" + text + "\n</text>\n"
    # まず軽く起動する。Claude Code の長い指示文とツールを外すと、入力が約 5 分の 1 になる（約 31,000 → 約 6,700 トークン）。
    # 軽い起動が使えない版の Claude Code（フラグを知らない）では、いつもの起動でやり直す。判定が黙って止まらないように。
    for extra in (SLIM_ARGS, []):
        try:
            r = subprocess.run([exe, "-p", "--model", MODEL] + extra, input=prompt, capture_output=True,
                               text=True, encoding="utf-8", timeout=TIMEOUT, env=env)
        except Exception as e:
            print(f"[ja-check] 判定できなかったので通しました: {e}", file=sys.stderr)
            return None
        for line in (r.stdout or "").strip().splitlines():
            line = line.strip()
            if line.startswith("OK") or line.startswith("NG"):
                return line
        if r.returncode == 0:
            return None  # 起動はできたが、形式が違う返事だった。判定できなかったとして通す
    print("[ja-check] 判定できなかったので通しました（軽い起動も、いつもの起動も失敗）。", file=sys.stderr)
    return None


# ---------- セッションごとの状態
# モード（pre / post / stop）ごとに別のファイルにする。hook が同時に動いても、互いの記録を上書きしないように。
# 書いた日本語を、判定まで置いておくファイルは "buffer" です。post と post-bash が書き、stop が読みます。
def state_path(data, kind=None):
    sid = re.sub(r"[^A-Za-z0-9_-]", "", str(data.get("session_id") or "default"))[:64]
    kind = kind or ("buffer" if MODE == "post" else MODE)
    return STATE_DIR / f"{sid}.{kind}.json"


def load_state(data, kind=None):
    p = state_path(data, kind)
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except Exception:
        return {}


def save_state(data, state, kind=None):
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    state_path(data, kind).write_text(json.dumps(state, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
