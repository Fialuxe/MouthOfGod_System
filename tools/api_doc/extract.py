"""Assets/Scripts の C# から、公開 API・Inspector の項目・メニューを取り出す。
docs/api-reference.md の表は、この出力から作る（手で写さないので、コードとずれない）。
使い方: python3 tools/api_doc/extract.py > api.json
読むのは宣言と /// の説明、[SerializeField]・[Tooltip]・[Header]・[MenuItem]・[ContextMenu] だけ。C# を解析してはいない（正規表現）。
"""
import glob, json, os, re, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Scripts")

def nicify(name):
    name = re.sub(r"^(m_|_)", "", name)
    name = re.sub(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", name)
    return name[:1].upper() + name[1:]

def xml_text(lines):
    s = " ".join(lines)
    s = re.sub(r'<see cref="(?:[\w.]*\.)?([\w]+)(?:\([^"]*\))?"\s*/>', r"\1", s)
    s = re.sub(r'<paramref name="(\w+)"\s*/>', r"\1", s)
    s = re.sub(r"<c>(.*?)</c>", r"\1", s)
    s = re.sub(r"<[^>]+>", "", s)
    s = s.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
    s = re.sub(r"\s+", " ", s).strip()
    return re.sub(r"([。！？）」]) (?=\S)", r"\1", s)

def summary(doc_lines):
    text = "\n".join(doc_lines)
    m = re.search(r"<summary>(.*?)</summary>", text, re.S)
    return xml_text([m.group(1)]) if m else ""

def add_field(cur, decl, hdr, tip):
    dm = re.match(r"(?:public |private |internal )?(.+?)\s+(\w+)\s*(?:=\s*(.+))?$", decl.strip())
    if dm:
        cur["fields"].append({"header": hdr, "type": dm.group(1).strip(), "name": dm.group(2), "label": nicify(dm.group(2)),
                              "default": (dm.group(3) or "").strip(), "tooltip": tip or ""})


def parse(path):
    src = open(path, encoding="utf-8-sig").read().split("\n")
    ns, types, stack = None, [], []   # stack: (indent, type)
    doc, attrs, tip, hdr = [], [], None, None
    i = 0
    while i < len(src):
        raw = src[i]; t = raw.strip(); indent = len(raw) - len(raw.lstrip())
        i += 1
        m = re.match(r"namespace ([\w.]+)", t)
        if m: ns = m.group(1); continue
        if t.startswith("///"): doc.append(re.sub(r"^///\s?", "", t)); continue
        if not t: continue
        if t.startswith("//") or t.startswith("#"): continue
        m = re.match(r'\[Tooltip\("(.*)"\)\]$', t)
        if m: tip = m.group(1); continue
        m = re.match(r'\[Header\("(.*)"\)\]$', t)
        if m: hdr = m.group(1); continue
        while stack and indent <= stack[-1][0] and not t.startswith("[") and t not in ("{", "}"):
            stack.pop()
        cur = stack[-1][1] if stack else None
        lead = re.match(r"^((?:\[[^\]]*\]\s*)+)(\S.*)$", t)
        if lead and not t.startswith("[SerializeField"):
            attrs.extend(re.findall(r"\[[^\]]*\]", lead.group(1)))
            t = lead.group(2)
        if t.startswith("[SerializeField") and cur is not None:
            text = t
            while not re.search(r";\s*(//.*)?$", text) and i < len(src):
                text += " " + src[i].strip(); i += 1
            mm = re.match(r"\[SerializeField[^\]]*\]\s*(.+?);\s*(?://\s*(.*))?$", text)
            if mm: add_field(cur, mm.group(1), hdr, tip or mm.group(2))
            doc, attrs, tip = [], [], None
            continue
        if t.startswith("["):
            attrs.append(t); continue
        if t in ("{", "}"): continue
        m = re.match(r"((?:public|internal|private|protected)\s+)?((?:static|sealed|abstract|partial|readonly)\s+)*(class|struct|interface|enum)\s+(\w+)\s*(?::\s*([^{]+?))?\s*(\{.*)?$", t)
        if m and not t.endswith(";"):
            cur = {"name": m.group(4), "kind": m.group(3), "access": (m.group(1) or "internal").strip(),
                   "mods": re.findall(r"static|sealed|abstract|readonly", t.split(m.group(3))[0]),
                   "bases": [b.strip() for b in (m.group(5) or "").split(",") if b.strip()],
                   "attrs": [a for a in attrs if not a.startswith("[SerializeField")], "summary": summary(doc),
                   "ns": ns, "file": os.path.relpath(path, ROOT).replace("\\", "/"), "members": [], "fields": [], "menus": [], "values": [],
                   "nested_in": stack[-1][1]["name"] if stack else None}
            types.append(cur); stack.append((indent, cur))
            doc, attrs, tip, hdr = [], [], None, None
            if m.group(3) == "enum":
                # 値を拾う
                body = t
                j = i
                depth = body.count("{") - body.count("}")
                while ("{" not in body or depth > 0) and j < len(src):
                    body += "\n" + src[j]; depth = body.count("{") - body.count("}"); j += 1
                inner = body[body.index("{") + 1: body.rindex("}")]
                vals, pend = [], []
                lines = inner.split("\n") if inner.count("\n") > 1 else [x + "," for x in re.sub(r"//[^\n]*", "", inner).split(",") if x.strip()]
                expanded = []
                for line in lines:
                    code = line.split("//")[0]
                    if code.count(",") > 1 and not line.strip().startswith("///"):
                        expanded.extend(x.strip() + "," for x in code.split(",") if x.strip())
                    else:
                        expanded.append(line)
                for line in expanded:
                    lt = line.strip()
                    if lt.startswith("///"): pend.append(re.sub(r"^///\s?", "", lt)); continue
                    mv = re.match(r"(\w+)\s*(?:=\s*[^,/]+)?\s*,?\s*(?://\s*(.*))?$", lt)
                    if mv and lt and not lt.startswith("//") and not lt.startswith("["):
                        vals.append({"name": mv.group(1), "doc": summary(pend) or (mv.group(2) or "").strip()}); pend = []
                    elif not lt: pend = []
                cur["values"] = vals
            continue
        cur = stack[-1][1] if stack else None
        if cur is None: doc, attrs = [], []; continue
        # 項目（Inspector）。[SerializeField] だけの行のあとの宣言
        if any(x.startswith("[SerializeField") for x in attrs):
            text = t
            while not re.search(r";\s*(//.*)?$", text) and i < len(src):
                text += " " + src[i].strip(); i += 1
            mm = re.match(r"(.+?);\s*(?://\s*(.*))?$", text)
            if mm: add_field(cur, mm.group(1), hdr, tip or mm.group(2))
            doc, attrs, tip = [], [], None
            continue
        # [Serializable] の公開フィールド（FoodRow など）
        if "[Serializable]" in cur["attrs"] and re.match(r"public [\w<>\[\],. ?]+\s+\w+\s*(=.*)?;", t) and not t.startswith("public static"):
            dm = re.match(r"public\s+(.+?)\s+(\w+)\s*(?:=\s*(.+?))?;\s*(?://\s*(.*))?$", t)
            if dm:
                cur["fields"].append({"header": hdr, "type": dm.group(1), "name": dm.group(2), "label": nicify(dm.group(2)),
                                      "default": (dm.group(3) or "").strip(), "tooltip": (tip or dm.group(4) or "").strip()})
            doc, attrs, tip = [], [], None
            continue
        # メニュー
        for a in attrs:
            mm = re.match(r'\[(MenuItem|ContextMenu)\("([^"]*)"(?:,\s*(true|false))?', a)
            if mm and mm.group(3) != "true":
                cur["menus"].append({"kind": mm.group(1), "path": mm.group(2)})
        # メンバー
        if cur["kind"] == "interface":
            ok = re.match(r"[\w<>\[\],.? ]+\s+\w+\s*(\(.*\))?\s*(\{.*\})?;?$", t) or t.startswith("event ")
        else:
            ok = re.match(r"(public|protected)\b", t)
        if ok and not t.startswith("[") and "class " not in t:
            while t.count("(") > t.count(")") and i < len(src):
                t += " " + src[i].strip(); i += 1
            sig = re.sub(r"\s*(=>|\{).*$", "", t).rstrip(";").strip()
            if re.match(r"(public|protected)\s+(const|static readonly)", t): sig = re.sub(r"\s*=.*$", "", t).rstrip(";")
            if "=" in sig and "(" not in sig and "event" not in sig: sig = sig.split("=")[0].strip()
            if cur["kind"] == "interface" and "{ get; }" in t: sig = re.sub(r"\s*\{.*\}", "", t).rstrip(";") + " { get; }"
            if "{ get; set; }" in t: sig = re.sub(r"\s*\{.*\}", "", t).rstrip(";") + " { get; set; }"
            if "{ get; }" in t and "{ get; }" not in sig: sig += " { get; }"
            kind = "event" if " event " in " " + sig + " " or sig.startswith("event ") else ("method" if "(" in sig else "property")
            cur["members"].append({"sig": re.sub(r"\s+", " ", sig), "kind": kind, "doc": summary(doc), "menus": [a for a in attrs if a.startswith(("[MenuItem", "[ContextMenu"))]})
        doc, attrs, tip, hdr = [], [], None, None
    return types

def main():
    out = []
    for path in sorted(glob.glob(os.path.join(ROOT, "**", "*.cs"), recursive=True)):
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        if "/Tests/" in rel or "/PlayTests/" in rel or rel.endswith("AssemblyInfo.cs"): continue
        out.extend(parse(path))
    json.dump(out, sys.stdout, ensure_ascii=False, indent=1)

if __name__ == "__main__":
    main()
