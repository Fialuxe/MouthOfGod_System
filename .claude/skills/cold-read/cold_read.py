#!/usr/bin/env python3
"""下書きを、前提を知らない目で読ませる（cold-reader の軽い起動版）。

使い方: python3 cold_read.py <下書きのファイル>

サブエージェントで呼ぶと、親の長い指示文とツールの定義が付いてくる。
ここでは、`claude -p` を指示文を短くし、ツールを外して起動し、使うトークンを減らす
（日本語チェック .claude/hooks/ja-check/ja_check.py と同じ軽い起動）。
指示の本文は .claude/agents/cold-reader.md を使う。軽い起動のフラグを知らない版では、いつもの起動でやり直す。
"""
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

AGENT = Path(__file__).resolve().parents[2] / "agents" / "cold-reader.md"
MODEL = os.environ.get("COLD_READ_MODEL", "sonnet")
TIMEOUT = 120
SLIM_ARGS = ["--system-prompt", "You are a careful reader. Follow the user's instructions exactly.", "--tools", ""]


def main():
    if len(sys.argv) != 2:
        print("使い方: cold_read.py <下書きのファイル>", file=sys.stderr)
        return 2
    draft = Path(sys.argv[1]).read_text(encoding="utf-8")
    body = re.sub(r"\A---\n.*?\n---\n", "", AGENT.read_text(encoding="utf-8"), flags=re.S)
    prompt = body.strip() + "\n\n---下書き---\n" + draft.strip() + "\n---ここまで---\n"
    exe = shutil.which("claude")
    if not exe:
        print("claude が見つかりません", file=sys.stderr)
        return 1
    env = dict(os.environ, JA_CHECK_ACTIVE="1")  # 日本語チェックの hook を、この呼び出しでは動かさない
    for extra in (SLIM_ARGS, []):
        r = subprocess.run([exe, "-p", "--model", MODEL] + extra, input=prompt, capture_output=True,
                           text=True, encoding="utf-8", timeout=TIMEOUT, env=env)
        if r.returncode == 0 and r.stdout.strip():
            print(r.stdout.strip())
            return 0
    print(r.stderr.strip()[:500], file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
