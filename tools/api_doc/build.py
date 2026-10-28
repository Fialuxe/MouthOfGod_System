"""tools/api_doc/template.md を読み、{{TYPES: ファイル…}} などの指示を、extract.py の出力から作った表に置き換えて、
docs/api-reference.md を書く。使い方: python3 tools/api_doc/build.py
手で書くのは template.md だけ。表は、コードの宣言・/// の説明・[Tooltip] から作るので、コードとずれない。
"""
import json, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..")
TYPES = json.loads(subprocess.check_output([sys.executable, os.path.join(HERE, "extract.py")]).decode("utf-8"))
SKIP_MEMBER = re.compile(r"\b(Equals|GetHashCode|ToString)\b|operator ")

def esc(s): return s.replace("|", "\\|").replace("\n", " ")

def type_heading(t):
    kind = {"class": "class", "struct": "struct", "interface": "interface", "enum": "enum"}[t["kind"]]
    mods = " ".join(m for m in t["mods"] if m in ("static", "abstract", "sealed", "readonly"))
    return f"{t['name']}（{(mods + ' ' if mods else '')}{kind}）"

def render_type(t, level):
    out = [f"{'#' * level} `{t['name']}`", ""]
    meta = [f"名前空間: `{t['ns']}`", f"ファイル: `Assets/Scripts/{t['file']}`"]
    out.append("- " + "\n- ".join(meta))
    decl = type_heading(t).split("（")[1].rstrip("）")
    bases = ", ".join(t["bases"])
    out.append(f"- 種類: {decl}" + (f"、継承・実装: `{bases}`" if bases else ""))
    attrs = [a for a in t["attrs"] if not a.startswith("[Serializable")]
    if attrs: out.append("- 属性: " + " ".join(f"`{a}`" for a in attrs))
    if "[Serializable]" in t["attrs"]: out.append("- Inspector では、配列や List の 1 要素として見えます（`[Serializable]`）。")
    out.append("")
    if t["summary"]: out += [t["summary"], ""]
    if t["values"]:
        out += ["| 値 | 意味 |", "|---|---|"] + [f"| `{v['name']}` | {esc(v['doc'])} |" for v in t["values"]] + [""]
    members = [m for m in t["members"] if not SKIP_MEMBER.search(m["sig"]) and not re.match(r"(public )?(const|static readonly)", m["sig"].replace("public ", "", 1) if False else "")]
    members = [m for m in members if t["kind"] == "interface" or m["sig"].startswith(("public", "protected"))]
    if members:
        out += ["**API**", "", "| メンバー | 説明 |", "|---|---|"]
        for m in members:
            sig = re.sub(r"^public ", "", m["sig"])
            out.append(f"| `{esc(sig)}` | {esc(m['doc'])} |")
        out.append("")
    if t["fields"]:
        out += ["**Inspector**", "", "| 項目 | 型 | 既定値 | 説明 |", "|---|---|---|---|"]
        group = object()
        for f in t["fields"]:
            if f["header"] != group and f["header"]:
                out.append(f"| **{esc(f['header'])}** | | | |")
            group = f["header"]
            default = f["default"] or ""
            out.append(f"| {f['label']} | `{esc(f['type'])}` | {('`' + esc(default) + '`') if default else ''} | {esc(f['tooltip'])} |")
        out.append("")
    menus = list(t["menus"])
    if menus:
        out += ["**メニュー**", ""] + [f"- {'右クリック（ContextMenu）' if m['kind']=='ContextMenu' else 'エディタ（MenuItem）'}: `{esc(m['path'])}`" for m in menus] + [""]
    return out

def public_types_in(path):
    return [t for t in TYPES if t["file"] == path and t["access"] == "public"]

def expand_types(arg, level):
    out = []
    for path in arg.split():
        ts = public_types_in(path)
        if not ts:
            ts = [t for t in TYPES if t["file"] == path]
        if not ts: raise SystemExit(f"型が見つかりません: {path}")
        # 入れ子の型は、親のあとに並ぶ
        for t in ts: out += render_type(t, level)
    return out

def expand_module(name, level):
    out = []
    for t in TYPES:
        f = t["file"]
        if not f.startswith(name + "/") or "/Editor/" in f or "/Sandbox/" in f: continue
        if t["access"] != "public" or t["nested_in"] and not any(x["name"] == t["nested_in"] and x["access"] == "public" for x in TYPES): continue
        out += render_type(t, level)
    return out

def expand_compact(prefix, level):
    """Editor・Sandbox の型を、名前・説明・メニューだけで並べる。"""
    out = []
    for t in TYPES:
        f = t["file"]
        if not f.startswith(prefix) or t["access"] != "public" or t["nested_in"]: continue
        out += [f"{'#' * level} `{t['name']}`", "", f"- 名前空間: `{t['ns']}`、ファイル: `Assets/Scripts/{f}`"]
        out += [""] + ([t["summary"], ""] if t["summary"] else [])
        paths = []
        for m in t["menus"] + [{"kind": "MenuItem", "path": re.match(r'\[MenuItem\("([^"]*)"', a).group(1)} for mem in t["members"] for a in mem["menus"] if a.startswith("[MenuItem")]:
            if m["path"] not in paths: paths.append(m["path"])
        out += [f"- メニュー: `{esc(p)}`" for p in paths]
        out.append("")
    return out

def expand_menus():
    rows = []
    for t in TYPES:
        for m in t["menus"]: rows.append((m["kind"], m["path"], t["name"], t["file"]))
        for mem in t["members"]:
            for a in mem["menus"]:
                mm = re.match(r'\[(MenuItem|ContextMenu)\("([^"]*)"', a)
                if mm: rows.append((mm.group(1), mm.group(2), t["name"], t["file"]))
    seen, out = set(), ["| 種類 | メニュー | 型 |", "|---|---|---|"]
    for kind, path, name, f in sorted(set(rows), key=lambda r: (r[0] != "MenuItem", r[1])):
        if (kind, path, name) in seen: continue
        seen.add((kind, path, name))
        out.append(f"| {'エディタ' if kind=='MenuItem' else '右クリック'} | `{esc(path)}` | `{name}` |")
    return out

def main():
    tpl = open(os.path.join(HERE, "template.md"), encoding="utf-8").read()
    def sub(m):
        kind, arg = m.group(1), m.group(3).strip()
        level = int(m.group(2)) if m.group(2) else 4
        if kind == "TYPES": return "\n".join(expand_types(arg, level))
        if kind == "MODULE": return "\n".join(expand_module(arg, level))
        if kind == "COMPACT": return "\n".join(expand_compact(arg, level))
        if kind == "MENUS": return "\n".join(expand_menus())
        raise SystemExit("未知の指示: " + kind)
    design = open(os.path.join(HERE, "design.md"), encoding="utf-8").read().split("\n", 1)[1]
    design = re.sub(r"^## (\w+) \| ", "### ", design, flags=re.M)
    design = re.sub(r"^### (決めたこと|なぜ|あきらめたこと|読む人へ)$", r"**\1**", design, flags=re.M)
    design = re.sub(r"^types: (.*)$", lambda m: "**この決まりから生まれた型**: " + "、".join("`" + x.strip() + "`" for x in m.group(1).split(",")), design, flags=re.M)
    design = re.sub(r"^source: (.*)$", r"出典: \1", design, flags=re.M)
    tpl = tpl.replace("## 1. 全体像", "## 0. 設計の考え方（なぜこの形か）\n" + design.strip() + "\n\n## 1. 全体像", 1)
    text = re.sub(r"\{\{(TYPES|MODULE|COMPACT|MENUS)(\d)?\s*:?\s*([^}]*)\}\}", sub, tpl)
    out = os.path.join(ROOT, "docs", "api-reference.md")
    open(out, "w", encoding="utf-8").write(text)
    print("wrote", out, len(text.split("\n")), "lines")

if __name__ == "__main__":
    main()
