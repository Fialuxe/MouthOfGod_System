"""extract.py の出力と template.md から、Javadoc 風の 1 ファイルの HTML（docs/api/index.html）を作る。
左に名前空間と型の一覧、右に型のページ（概要の表 → 詳細）。検索あり。
使い方: python3 tools/api_doc/build_html.py
"""
import json, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..")
TYPES = json.loads(subprocess.check_output([sys.executable, os.path.join(HERE, "extract.py")]).decode("utf-8"))
TEMPLATE = open(os.path.join(HERE, "template.md"), encoding="utf-8").read()
GITHUB = "https://github.com/Fialuxe/MouthOfGod_System/blob/main/"
MODS = ["Core", "Device", "Fork", "Food", "Village", "UI", "Miko", "Tutorial", "Game", "Tracking", "CameraWork", "DevTools"]
SKIP = re.compile(r"\b(Equals|GetHashCode|ToString)\b|operator ")
MODIFIERS = {"public", "protected", "internal", "static", "override", "virtual", "abstract", "sealed", "new", "readonly", "const", "event", "extern"}

def first_sentence(s):
    m = re.search(r"^(.*?。)", s)
    return (m.group(1) if m else s)[:160]

def split_sig(sig, type_name):
    """シグネチャを、修飾・戻り値の型・名前・引数に分ける。"""
    s = re.sub(r"\s*\{\s*get;.*\}$", "", sig)
    accessors = re.search(r"\{\s*(get;(?:\s*set;)?)\s*\}$", sig)
    toks_before = s.split("(")[0]
    mods, rest = [], toks_before.split()
    while rest and rest[0] in MODIFIERS:
        mods.append(rest.pop(0))
    params = ""
    if "(" in s: params = s[s.index("(") + 1:].rsplit(")", 1)[0]
    name = rest[-1] if rest else s
    rtype = " ".join(rest[:-1])
    # ジェネリクスの空白（Action<A, B>）が分かれたとき
    if rtype.count("<") != rtype.count(">"):
        m = re.match(r"(.*)\s+(\w+)$", toks_before.replace(" ".join(mods), "", 1).strip())
        if m: rtype, name = m.group(1), m.group(2)
    kind = "event" if "event" in mods else ("method" if "(" in s else "property")
    if kind == "method" and not rtype and name == type_name: kind = "ctor"
    if "const" in mods or ("static" in mods and "readonly" in mods): kind = "field"
    return {"mods": [m for m in mods if m not in ("public", "event")], "rtype": rtype, "name": name, "params": params,
            "kind": kind, "acc": accessors.group(1) if accessors else ""}

def module_of(t):
    return t["file"].split("/")[0]

def section_of(t):
    f = t["file"]
    if "/Editor/" in f: return "Editor"
    if "/Sandbox/" in f: return "Sandbox"
    return "Runtime"

types, seen = [], {}
for t in TYPES:
    if t["access"] != "public": continue
    if t["nested_in"] and not any(x["name"] == t["nested_in"] and x["access"] == "public" for x in TYPES): continue
    key = "t-" + t["name"]
    seen[key] = seen.get(key, 0) + 1
    if seen[key] > 1: key += "-%d" % seen[key]
    members = []
    for m in t["members"]:
        if SKIP.search(m["sig"]): continue
        if t["kind"] != "interface" and not m["sig"].startswith(("public", "protected")): continue
        p = split_sig(m["sig"], t["name"])
        p["doc"] = m["doc"]; p["brief"] = first_sentence(m["doc"])
        members.append(p)
    types.append({"id": key, "name": t["name"], "kind": t["kind"], "mods": t["mods"], "ns": t["ns"], "file": t["file"], "mod": module_of(t),
                  "sec": section_of(t), "bases": t["bases"], "attrs": [a for a in t["attrs"] if not a.startswith("[Serializable")],
                  "serializable": "[Serializable]" in t["attrs"], "doc": t["summary"], "brief": first_sentence(t["summary"]),
                  "values": t["values"], "fields": t["fields"], "menus": t["menus"], "members": members, "nested_in": t["nested_in"]})

# ---- 章の文章（template.md）----
def menus_table():
    rows = []
    for t in types:
        for m in t["menus"]: rows.append((m["kind"], m["path"], t["name"]))
    out = ["| 種類 | メニュー | 型 |", "|---|---|---|"]
    for kind, path, name in sorted(set(rows), key=lambda r: (r[0] != "MenuItem", r[1])):
        out.append(f"| {'エディタ' if kind == 'MenuItem' else '右クリック'} | `{path}` | `{name}` |")
    return "\n".join(out)

body = TEMPLATE.split("\n", 1)[1]
body = body.replace("{{MENUS}}", menus_table())
sections = re.split(r"^## ", body, flags=re.M)
guide, module_intro = [], {}
for sec in sections[1:]:
    title, _, text = sec.partition("\n")
    if title.startswith("4. "):
        for sub in re.split(r"^### ", text, flags=re.M)[1:]:
            st, _, st_text = sub.partition("\n")
            name = re.sub(r"^4\.\d+\s+", "", st).strip()
            intro = []
            for line in st_text.split("\n"):
                if line.startswith("{{") or line.startswith("エディタ専用") or line.startswith("試験用"): break
                intro.append(line)
            module_intro[name] = "\n".join(intro).strip()
        continue
    text = re.sub(r"\{\{[^}]*\}\}", "", text).replace("](architecture.md", "](" + GITHUB + "docs/architecture.md")
    text = re.sub(r"\]\(((?:requests|progress-keys|fields)\.md)", lambda m: "](" + GITHUB + "docs/" + m.group(1), text)
    gid = {"この文書の読み方": "reading", "1. 全体像": "overview-arch", "2. 機能どうしのつなぎ方": "wiring", "3. 共有シーン Main の姿": "main-scene",
           "付録 A. メニュー一覧": "menus", "付録 B. Resources の置き場所": "resources"}.get(title.strip())
    if gid: guide.append({"id": "g-" + gid.replace("_", "-"), "title": title.strip(), "md": text.strip()})

data = {"types": types, "guide": guide, "modules": MODS, "intro": module_intro, "preface": sections[0].strip()}
html = open(os.path.join(HERE, "page.html"), encoding="utf-8").read().replace("/*DATA*/null", json.dumps(data, ensure_ascii=False))
out = os.path.join(ROOT, "docs", "api", "index.html")
os.makedirs(os.path.dirname(out), exist_ok=True)
# リポジトリに置く版は、単独で開けるように、html・head・body を付ける。Artifact に出す版は、付けない（公開のときに付く）。
wrapped = ('<!doctype html>\n<html lang="ja"><head><meta charset="utf-8">'
           '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"></head><body>\n' + html + "\n</body></html>\n")
open(out, "w", encoding="utf-8").write(wrapped)
if len(sys.argv) > 1: open(sys.argv[1], "w", encoding="utf-8").write(html)
print("wrote", out, len(wrapped) // 1024, "KB,", len(types), "types")
