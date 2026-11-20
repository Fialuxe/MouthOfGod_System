"""Issue の題と本文が、write-issue スキルの決まりに合うかを、投稿の前に確かめる hook。

使い方（.claude/settings.json の PreToolUse / Bash から呼ぶ）:
  python issue_check.py     … 標準入力に hook の JSON を受け取る

確かめるのは、決まりのうち機械で見分けられるものだけです。
- gh issue create: 題・本文・ラベル
- gh issue edit --title: 題だけ

方針:
- 題や本文を読み取れないとき（シェルの変数やループで組み立てているなど）は、その部分を確かめずに通します（fail open）。
- 定期実行が書く報告用の Issue（ラベル hygiene・roadmap・activity）は確かめません。
- 決まりの中身は .claude/skills/write-issue/ にあります。ここを変えたら、そちらも合わせます。
"""
import json
import os
import re
import shlex
import sys
from pathlib import Path

GH_ISSUE = re.compile(r"(?:^|[;&|(]|\n)\s*(gh\s+issue\s+(?:create|edit)\b[^\n;&|]*)")
SKIP_LABELS = {"hygiene", "roadmap", "activity"}
REQUIRED_LABEL = "claude-proposal"

TITLE_RULES = [
    (re.compile(r"(が|は|に)?(ない|無い|しかない|できない|いない)$"),
     "題が「〜がない」のような状態になっています。やる作業を動詞で終える形にしてください（例: 「エンディングを作る」）。"),
    (re.compile(r"^【"), "題に【】の飾りがあります。外してください。"),
    (re.compile(r"^(feat|fix|chore|docs|refactor|test)(\(.+?\))?:", re.I), "題にコミットの接頭辞があります。外してください。"),
    (re.compile(r"^[^\s:：]{1,20}[:：]\s*\S"), "題が「場所: 〜」の形です。やる作業を動詞で終える 1 文にしてください。"),
    (re.compile(r"#\d+"), "題に Issue の番号があります。番号は本文に書いてください。"),
]
BODY_RULES = [
    (re.compile(r"🤖"), "本文にロボットの絵文字があります。外してください。"),
    (re.compile(r"下書きしました"), "本文に「Claude が下書きしました」の断り書きがあります。外して、ラベル claude-proposal で示してください。"),
    (re.compile(r"^\s*[-*]\s+\[[ xX]\]", re.M), "本文にチェックボックスがあります。手順なら番号つきの箇条書きにし、作業の一覧は書かないでください。"),
    (re.compile(r"^#{1,6}\s*(やること|完了条件|できたと言える状態|決めること|なぜやるか|何をするか|今の状態|目的|範囲外)", re.M),
     "本文に「やること」「完了条件」などの見出しがあります。見出しなしの短い文章にしてください。"),
    (re.compile(r"^\s*(?:[-*]\s+)?\*\*[^*\n]{1,20}[:：]\*\*|\*\*[^*\n]{1,20}\*\*[:：]", re.M),
     "本文に「**ラベル:** 〜」の形の行があります。文章で書いてください。マイルストーンは Issue の設定で付けます。"),
    (re.compile(r"決まっていません|未定です"),
     "本文に「決まっていません」の言い切りがあります。「メモ:」の段落で、軽い問いかけにしてください（例: 「〜したほうが面白いでしょうか？」）。"),
]
MENTION = re.compile(r"(?<![\w`/.@])@[A-Za-z0-9][A-Za-z0-9-]{0,38}\b")
CODE = re.compile(r"```.*?```|`[^`\n]*`", re.S)


def main():
    sys.stdout.reconfigure(encoding="utf-8")  # Windows でも、Claude Code が UTF-8 で読めるように
    try:
        data = json.load(sys.stdin)
    except Exception:
        return 0
    cmd = (data.get("tool_input") or {}).get("command") or ""
    problems = []
    for m in GH_ISSUE.finditer(cmd):
        try:
            problems += check(m.group(1), cmd)
        except Exception:
            continue
    if not problems:
        return 0
    reason = ("Issue の書き方の決まり（.claude/skills/write-issue/）に合わない所があります。\n- "
              + "\n- ".join(dict.fromkeys(problems))
              + "\n直してから、もう一度投稿してください。")
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}, ensure_ascii=False))
    return 0


def check(segment, whole):
    try:
        args = shlex.split(segment, posix=True)
    except ValueError:
        return []
    create = len(args) > 2 and args[2] == "create"
    title = body = None
    labels = []
    i = 3
    while i < len(args):
        a = args[i]
        val = args[i + 1] if i + 1 < len(args) else None
        if a in ("--title", "-t"):
            title = resolve(val, whole); i += 2; continue
        if a.startswith("--title="):
            title = resolve(a.split("=", 1)[1], whole); i += 1; continue
        if a in ("--body", "-b"):
            body = resolve(val, whole); i += 2; continue
        if a in ("--body-file", "-F"):
            body = read_file(resolve(val, whole)); i += 2; continue
        if a in ("--label", "-l", "--add-label"):
            labels += [x.strip() for x in (val or "").split(",")]; i += 2; continue
        i += 1

    if SKIP_LABELS & set(labels):
        return []
    problems = []
    if title:
        for pat, msg in TITLE_RULES:
            if pat.search(title.strip()):
                problems.append(msg)
    if create and body:
        for pat, msg in BODY_RULES:
            if pat.search(body):
                problems.append(msg)
        if MENTION.search(CODE.sub("", body)):
            problems.append("本文に @ つきの名前があります。頼まれたとき以外は、@ を外してください（GitHub が通知を送ります）。")
    if create and REQUIRED_LABEL not in labels:
        problems.append(f"ラベル {REQUIRED_LABEL} がありません。--label {REQUIRED_LABEL} を付けてください。")
    return problems


def resolve(value, whole):
    """$(cat パス) と、同じコマンドの中の VAR=値 を展開する。展開できない $ が残れば None。"""
    if value is None:
        return None
    env = {k: a or b or c for k, a, b, c in re.findall(r"""\b([A-Za-z_]\w*)=(?:"([^"]*)"|'([^']*)'|([^\s;&|]+))""", whole)}
    value = re.sub(r"\$\{?([A-Za-z_]\w*)\}?", lambda m: env.get(m.group(1), os.environ.get(m.group(1), m.group(0))), value)
    m = re.fullmatch(r"\$\(\s*cat\s+(.+?)\s*\)", value.strip())
    if m:
        return read_file(m.group(1).strip("\"'"))
    return None if "$" in value else value


def read_file(path):
    if not path:
        return None
    p = Path(os.path.expanduser(path))
    try:
        return p.read_text(encoding="utf-8") if p.is_file() else None
    except Exception:
        return None


if __name__ == "__main__":
    sys.exit(main())
