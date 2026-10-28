"""model.py の DATA から、Javadoc 風の 1 ファイルの HTML（docs/api/index.html）を作る。ローカルで開く用。
GitHub で読むのは、build_md.py が作る docs/api/*.md。
使い方: python3 tools/api_doc/build_html.py [Artifact 用の出力先]
"""
import json, os, sys
from model import data, types, HERE, ROOT

html = open(os.path.join(HERE, "page.html"), encoding="utf-8").read().replace("/*DATA*/null", json.dumps(data, ensure_ascii=False))
out = os.path.join(ROOT, "docs", "api", "index.html")
os.makedirs(os.path.dirname(out), exist_ok=True)
# リポジトリに置く版は、単独で開けるように、html・head・body を付ける。Artifact に出す版は、付けない（公開のときに付く）。
wrapped = ('<!doctype html>\n<html lang="ja"><head><meta charset="utf-8">'
           '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"></head><body>\n' + html + "\n</body></html>\n")
open(out, "w", encoding="utf-8").write(wrapped)
if len(sys.argv) > 1: open(sys.argv[1], "w", encoding="utf-8").write(html)
print("wrote", out, len(wrapped) // 1024, "KB,", len(types), "types")
