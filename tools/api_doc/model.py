"""リファレンスの中身（型・設計の考え方・手順・つながり）を、コードと tools/api_doc/*.md から集める。
build_html.py（HTML）と build_md.py（GitHub で読む Markdown）が、ここの DATA を使う。
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

# ---- 役割（一覧の分け方）----
def role_of(t, by_name):
    if t["sec"] == "Editor": return "エディタ"
    if t["sec"] == "Sandbox": return "試験用"
    if t["kind"] == "enum": return "選択肢（enum）"
    if t["kind"] == "interface": return "interface"
    seen, base = set(), list(t["bases"])
    while base:
        b = base.pop(0)
        if b in seen: continue
        seen.add(b)
        if b == "MonoBehaviour": return "コンポーネント"
        if b == "ScriptableObject": return "設定アセット"
        if b == "CustomYieldInstruction": return "待ち（yield return）"
        if b in by_name: base += by_name[b]["bases"]
    if "static" in t["mods"]: return "static（置き場・計算）"
    if t["kind"] == "struct" or t["serializable"]: return "データ"
    return "クラス"

ROLE_NOTE = {"コンポーネント": "GameObject に付ける", "設定アセット": "Project に置くアセット", "待ち（yield return）": "人形劇スクリプトで yield return する",
             "static（置き場・計算）": "コードから直接呼ぶ", "データ": "値を運ぶ型", "interface": "機能どうしの約束", "選択肢（enum）": "決まった値の中から選ぶ",
             "クラス": "コードから new して使う", "エディタ": "Unity エディタのメニュー", "試験用": "動作確認用。展示では使わない"}
by_name = {t["name"]: t for t in types}
# interface を実装したメンバーに説明が無ければ、interface の説明を使う
for t in types:
    for m in t["members"]:
        if m["doc"]: continue
        for b in t["bases"]:
            src = by_name.get(b)
            if src and src["kind"] == "interface":
                hit = next((x for x in src["members"] if x["name"] == m["name"] and x["doc"]), None)
                if hit: m["doc"], m["brief"] = hit["doc"] + "（" + b + " の説明）", hit["brief"]; break
for t in types:
    t["role"] = role_of(t, by_name)
    t["remarks"] = next((x["remarks"] for x in TYPES if x["name"] == t["name"] and x["file"] == t["file"]), "")

# ---- つながり（どのファイルが、どの型の名前を使っているか。コメント行は数えない）----
import glob as _glob
code = {}
for path in _glob.glob(os.path.join(ROOT, "Assets", "Scripts", "**", "*.cs"), recursive=True):
    rel = os.path.relpath(path, os.path.join(ROOT, "Assets", "Scripts")).replace("\\", "/")
    if "/Tests/" in rel or "/PlayTests/" in rel or rel.endswith("AssemblyInfo.cs"): continue
    lines = [l for l in open(path, encoding="utf-8-sig").read().split("\n") if not l.strip().startswith("//")]
    code[rel] = "\n".join(re.sub(r"//.*$", "", re.sub(r'"[^"\n]*"', '""', l)) for l in lines)
# asmdef: ファイルがどのアセンブリに入り、どのアセンブリを参照できるか（名前が同じ別の型を、つながりと数えないため）
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts")
asm_of_dir = {}
for a in _glob.glob(os.path.join(SCRIPTS, "**", "*.asmdef"), recursive=True):
    j = json.load(open(a, encoding="utf-8-sig"))
    asm_of_dir[os.path.dirname(os.path.relpath(a, SCRIPTS)).replace("\\", "/")] = (j["name"], set(j.get("references", [])))
def asm_of(rel):
    d = os.path.dirname(rel)
    while d and d not in asm_of_dir: d = os.path.dirname(d)
    return asm_of_dir.get(d, ("?", set()))
def can_see(user_file, type_file):
    ua, refs = asm_of(user_file)
    ta, _ = asm_of(type_file)
    return ua == ta or ta in refs
owner = {}
for t in types:  # ファイルの持ち主は、ファイル名と同じ名前の型。無ければ最初の型
    base = os.path.splitext(os.path.basename(t["file"]))[0]
    if t["name"] == base or (t["file"] not in owner and not t["nested_in"]): owner[t["file"]] = t["name"]
for t in types:
    pat = re.compile(r"\b" + re.escape(t["name"]) + r"\b")
    users = sorted({owner[f] for f, c in code.items() if f != t["file"] and f in owner and can_see(f, t["file"]) and pat.search(c)})
    own = code.get(t["file"], "")
    defined_here = {x["name"] for x in types if x["file"] == t["file"]}
    uses = sorted({x["name"] for x in types if x["name"] not in defined_here and can_see(t["file"], x["file"]) and re.search(r"\b" + re.escape(x["name"]) + r"\b", own)})
    t["usedBy"], t["uses"] = users, uses
data_roles = list(ROLE_NOTE.items())

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

# ---- 設計の考え方（design.md）----
design_src = open(os.path.join(HERE, "design.md"), encoding="utf-8").read()
parts = re.split(r"^## ", design_src.split("\n", 1)[1], flags=re.M)
principles = []
for part in parts[1:]:
    head, _, rest = part.partition("\n")
    pid, _, title = head.partition("|")
    meta = dict(re.findall(r"^(types|source): (.*)$", rest, flags=re.M))
    body = re.sub(r"^(types|source): .*\n", "", rest, flags=re.M).strip()
    secs = {}
    for sec in re.split(r"^### ", body, flags=re.M)[1:]:
        st, _, sx = sec.partition("\n"); secs[st.strip()] = sx.strip()
    names = [x.strip() for x in meta.get("types", "").split(",") if x.strip()]
    missing = [n for n in names if n not in by_name]
    if missing: raise SystemExit(f"design.md の {pid.strip()} に、無い型があります: {missing}")
    principles.append({"id": "d-" + pid.strip(), "title": title.strip(), "types": names, "source": meta.get("source", ""), "secs": secs})
design_intro = parts[0].strip()

# ---- やりたいこと別の手順（recipes.md）----
rsrc = open(os.path.join(HERE, "recipes.md"), encoding="utf-8").read()
rparts = re.split(r"^## ", rsrc.split("\n", 1)[1], flags=re.M)
recipes = []
for part in rparts[1:]:
    head, _, rest = part.partition("\n")
    rid, _, title = head.partition("|")
    m = re.search(r"^types: (.*)$", rest, flags=re.M)
    names = [x.strip() for x in (m.group(1) if m else "").split(",") if x.strip()]
    missing = [n for n in names if n not in by_name]
    if missing: raise SystemExit(f"recipes.md の {rid.strip()} に、無い型があります: {missing}")
    recipes.append({"id": "r-" + rid.strip(), "title": title.strip(), "types": names, "md": re.sub(r"^types: .*\n", "", rest, flags=re.M).strip()})

data = {"recipes": recipes, "recipesIntro": rparts[0].strip(), "design": principles, "designIntro": design_intro, "roles": data_roles, "types": types, "guide": guide, "modules": MODS, "intro": module_intro, "preface": sections[0].strip()}
