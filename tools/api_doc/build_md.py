"""model.py の DATA から、GitHub のページでそのまま読める Markdown を docs/api/ に書く。
入口は docs/api/README.md。機能ごとに 1 ページ、ほかに設計の考え方・つながりの図・手順・ガイドのページを作る。
使い方: python3 tools/api_doc/build_md.py
"""
import os, re
from model import data, types, ROOT

OUT = os.path.join(ROOT, "docs", "api")
by_name = {}
for t in types: by_name.setdefault(t["name"], t)
GEN = "<!-- このページは tools/api_doc/build_md.py が作ります。手で直さないでください。 -->\n"
NAV = "[入口](README.md) ・ [設計の考え方](design.md) ・ [つながりの図](diagrams.md) ・ [やりたいこと別の手順](recipes.md) ・ [ガイド](guide.md)\n"

def esc(s): return (s or "").replace("|", "\\|").replace("\n", " ")
def tlink(name, here=None):
    t = by_name.get(name)
    if not t: return f"`{name}`"
    page = t["mod"] + ".md"
    return f"[`{name}`]({'' if page == here else page}#{t['id'].lower()})"
def link_code(text, here=None, self_name=None):
    """文の中の `型名` を、その型へのリンクにする。"""
    return re.sub(r"`([A-Za-z_]\w*)`", lambda m: tlink(m.group(1), here) if m.group(1) in by_name and m.group(1) != self_name else m.group(0), text or "")
def link_words(text, here=None, self_name=None):
    """コードの説明文（バッククォート無し）の型名を、リンクにする。"""
    return re.sub(r"\b([A-Z][A-Za-z0-9_]+)\b", lambda m: tlink(m.group(1), here) if m.group(1) in by_name and m.group(1) != self_name else m.group(1), text or "")
def anchor(i): return f'<a id="{i.lower()}"></a>\n'
def why_for(names): return [p for p in data["design"] if any(n in p["types"] for n in names)]
def recipes_for(name): return [r for r in data["recipes"] if name in r["types"]]
def first(s): m = re.search(r"^(.*?。)", s or ""); return m.group(1) if m else (s or "")
def mid(s): return re.sub(r"\W", "_", s)

def ego(t):
    """型を真ん中にした図。左が使う所、右が使う型。"""
    if not t["uses"] and not t["usedBy"]: return ""
    out = ["```mermaid", "flowchart LR"]
    c = "C_" + mid(t["name"])
    out.append(f'  {c}["{t["name"]}<br/>{t["mod"]}"]:::me')
    for n in t["usedBy"]:
        o = by_name[n]; out.append(f'  U_{mid(n)}["{n}<br/>{o["mod"]}"] --> {c}')
    for n in t["uses"]:
        o = by_name[n]; out.append(f'  {c} --> D_{mid(n)}["{n}<br/>{o["mod"]}"]')
    out += ["  classDef me stroke-width:3px", "```"]
    return "\n".join(out)

def member_kind(m):
    return {"ctor": "コンストラクタ", "event": "イベント（知らせ）", "property": "プロパティ（読む値）", "field": "定数", "method": "メソッド（呼ぶもの）"}[m["kind"]]

def type_section(t, here):
    L = [anchor(t["id"]), f"## {t['name']}", ""]
    note = dict(data["roles"]).get(t["role"], "")
    mods = " ".join(x for x in t["mods"] if x != "readonly")
    L.append(f"**{t['role']}**（{note}）・ `{(mods + ' ') if mods else ''}{t['kind']}` ・ 名前空間 `{t['ns']}` ・ [`{t['file']}`](../../Assets/Scripts/{t['file']})")
    if t["bases"]: L.append(f"<br>継承・実装: " + "、".join(tlink(b, here) if b in by_name else f"`{b}`" for b in t["bases"]))
    if t["nested_in"]: L.append(f"<br>{tlink(t['nested_in'], here)} の中の型")
    if t["attrs"]: L.append("<br>属性: " + " ".join(f"`{a}`" for a in t["attrs"]))
    L.append("")
    if t["doc"]: L += [link_words(t["doc"], here, t["name"]), ""]
    if t["serializable"]: L += ["Inspector では、配列や List の 1 要素として見えます（`[Serializable]`）。", ""]
    why = why_for([t["name"]])
    if why:
        L += ["### なぜこの形か", ""]
        for p in why: L.append(f"- **[{p['title']}](design.md#{p['id']})**: {first(p['secs'].get('なぜ', ''))}")
        L.append("")
    if t["remarks"]: L += ["### しくみ・注意", "", link_code(t["remarks"], here, t["name"]), ""]
    if t["uses"] or t["usedBy"]:
        L += ["### つながり", "", ego(t), ""]
        if t["usedBy"]: L.append("- この型を使う所: " + "、".join(tlink(n, here) for n in t["usedBy"]))
        if t["uses"]: L.append("- この型が使う型: " + "、".join(tlink(n, here) for n in t["uses"]))
        L.append("")
    rs = recipes_for(t["name"])
    if rs: L += ["### この型を使う手順", ""] + [f"- [{r['title']}](recipes.md#{r['id']})" for r in rs] + [""]
    if t["values"]:
        L += ["### 値", "", "| 値 | 意味 |", "|---|---|"] + [f"| `{v['name']}` | {esc(v['doc'])} |" for v in t["values"]] + [""]
    if t["fields"]:
        L += [f"### Inspector（{len(t['fields'])} 項目）", "", "| 項目 | 型 | 既定値 | 説明 |", "|---|---|---|---|"]
        last = None
        for f in t["fields"]:
            if f["header"] and f["header"] != last: L.append(f"| **{esc(f['header'])}** | | | |")
            last = f["header"]
            L.append(f"| {f['label']} | `{esc(f['type'])}` | {esc(pretty(f['default'], f['type']))} | {esc(f['tooltip'])} |")
        L.append("")
    if t["members"]:
        L += [f"### メンバー（{len(t['members'])}）", "", "| 種類 | メンバー | 説明 |", "|---|---|---|"]
        order = ["ctor", "event", "property", "field", "method"]
        for m in sorted(t["members"], key=lambda m: order.index(m["kind"])):
            args = f"({m['params']})" if m["kind"] in ("method", "ctor") else ""
            sig = " ".join(x for x in [" ".join(m["mods"]), m["rtype"], m["name"] + args] if x)
            if m["acc"]: sig += " { " + m["acc"] + " }"
            L.append(f"| {member_kind(m).split('（')[0]} | `{esc(sig)}` | {esc(link_words(m['doc'], here, t['name'])) or '（説明はコードにありません）'} |")
        L.append("")
    if t["menus"]:
        L += ["### メニュー", ""] + [f"- {'右クリック' if m['kind'] == 'ContextMenu' else 'エディタ'}: `{m['path']}`" for m in t["menus"]] + [""]
    return "\n".join(L)

def pretty(v, typ):
    if not v:
        return {"bool": "オフ", "string": "空"}.get(typ, "0" if typ in ("int", "float", "double") else "なし")
    if v in ("true", "false"): return "オン" if v == "true" else "オフ"
    if v == '""': return "空"
    if re.match(r"^new [\w<>.]+\[0\]$|^new (List<.*>|UnityEvent)\(\)$|^System\.Array\.Empty<.*>\(\)$", v): return "空"
    m = re.match(r"^new (Vector[234]|Color)\((.*)\)$", v)
    if m: return "(" + re.sub(r"(\d)f\b", r"\1", m.group(2)) + ")"
    if re.match(r"^-?\d+(\.\d+)?f$", v): return v[:-1]
    m = re.match(r"^[A-Z]\w*\.(\w+)$", v)
    if m and not v.startswith(("Vector", "Color", "Quaternion")): return m.group(1)
    return f"`{v}`"

def write(name, text):
    with open(os.path.join(OUT, name), "w", encoding="utf-8") as f: f.write(GEN + NAV + "\n" + text.strip() + "\n")

def module_page(mod):
    here = mod + ".md"
    ts = sorted([t for t in types if t["mod"] == mod], key=lambda t: t["name"])
    L = [f"# MouthOfGod.{mod}", "", link_code(data["intro"].get(mod, ""), here), ""]
    why = why_for([t["name"] for t in ts])
    if why:
        L += ["## この機能に関わる決まり（なぜこの形か）", ""]
        for p in why: L.append(f"- **[{p['title']}](design.md#{p['id']})**: {first(p['secs'].get('なぜ', ''))}")
        L.append("")
    L += ["## 型の一覧", "", "| 役割 | 型 | 説明 |", "|---|---|---|"]
    for role, _ in data["roles"]:
        for t in [x for x in ts if x["role"] == role]:
            L.append(f"| {role} | {tlink(t['name'], here)} | {esc(t['brief'])} |")
    L.append("")
    for t in ts:
        if t["sec"] == "Runtime": L += ["---", "", type_section(t, here), ""]
    others = [t for t in ts if t["sec"] != "Runtime"]
    if others:
        L += ["---", "", "## エディタ専用・試験用", ""]
        for t in others:
            L += [anchor(t["id"]), f"### {t['name']}", "", f"`{t['file']}`（{t['role']}）", "", t["doc"] or "", ""]
            if t["menus"]: L += [f"- メニュー: `{m['path']}`" for m in t["menus"]] + [""]
    write(here, "\n".join(L))

def design_page():
    L = [data["designIntro"].replace("# 設計の考え方", "# 設計の考え方（なぜこの形か）", 1), "", "## 決まりの一覧", ""]
    L += [f"{i + 1}. [{p['title']}](#{p['id']})" for i, p in enumerate(data["design"])] + [""]
    for p in data["design"]:
        L += ["---", "", anchor(p["id"]), f"## {p['title']}", ""]
        for k, v in p["secs"].items(): L += [f"**{k}**", "", link_code(v, "design.md"), ""]
        L += ["**この決まりから生まれた型**: " + "、".join(tlink(n) for n in p["types"]), "", f"出典: {p['source']}", ""]
    write("design.md", "\n".join(L))

def recipes_page():
    L = [data["recipesIntro"], "", "## 手順の一覧", ""]
    L += [f"{i + 1}. [{r['title']}](#{r['id']})" for i, r in enumerate(data["recipes"])] + [""]
    for r in data["recipes"]:
        L += ["---", "", anchor(r["id"]), f"## {r['title']}", "", link_code(r["md"], "recipes.md"), "", "使う型: " + "、".join(tlink(n) for n in r["types"]), ""]
    write("recipes.md", "\n".join(L))

def diagrams_page():
    count = {}
    for t in types:
        if t["sec"] != "Runtime": continue
        for u in t["uses"]:
            o = by_name[u]
            if o["mod"] == t["mod"] or o["sec"] != "Runtime": continue
            count[(t["mod"], o["mod"])] = count.get((t["mod"], o["mod"]), 0) + 1
    g = ["```mermaid", "flowchart TB"]
    for (a, b), c in sorted(count.items()): g.append(f"  {a} -->|{c}| {b}")
    g += ["  Tracking", "  CameraWork", "  classDef core stroke-width:3px", "  class Core core", "```"]
    flow1 = """```mermaid
flowchart LR
  MockDevice -->|Confirmed| FoodSpawner
  FoodSpawner -->|Resolve| FoodTables
  StandArea -->|Set| Standing
  Standing -->|Field| FoodSpawner
  FoodSpawner -->|Spawned| FoodShadow
  FoodSpawner -->|物理で飛ぶ| DeliveryZone
  DeliveryZone -->|Receive| FieldStages
  FieldStages -->|Issued・Closed| RequestBubbles
  FieldStages -->|StepDone| DeliveryFx
  FieldStages -->|SetWorried| VillagerMood
```"""
    flow2 = """```mermaid
flowchart LR
  TutorialRunner -->|子を順に動かす| TutorialScript
  TutorialRunner -->|Set| Phases
  Phases -->|Changed| GameRunner
  TutorialScript -->|Cue・Say| Cues
  TutorialScript -->|Set・Has| TutorialMemory
  Cues -->|miko.*| MikoCueReceiver
  Cues -->|cloud.*| CloudCues
  Cues -->|shadow.*| FoodShadow
  Cues -->|footprint| StandArea
  Cues -->|そのほか| CueReaction
```"""
    L = ["# つながりの図", "", "矢印は「呼ぶ・知らせる」向きです。図の中の型は押せません。型のページへは、図の下の一覧から移ります。", "",
         "## 機能どうしのつながり", "",
         "実行時の型が、ほかの機能の型を名前で使っている回数です（エディタ専用・試験用の型は数えません）。ほとんどの矢印が Core に向かいます。機能どうしは Core だけを通してつながる決まりだからです（[なぜ](design.md#d-core)）。Tracking と CameraWork は、どの機能ともつながりません。", "",
         "\n".join(g), "", "## 流れ 1: 食べ物を生んで、届けるまで", "",
         "デバイスで決定すると食べ物が生まれ、体験者が狙って飛ばします。落ちた所の `DeliveryZone` が、そのフィールドに届けます。", "", flow1, "",
         "図の型: " + "、".join(tlink(n) for n in ["MockDevice", "FoodSpawner", "FoodTables", "StandArea", "Standing", "FoodShadow", "DeliveryZone", "FieldStages", "RequestBubbles", "DeliveryFx", "VillagerMood"]), "",
         "## 流れ 2: チュートリアルから本編まで", "",
         "チュートリアルは、合図（`Cues`）を出すだけです。巫女・雲・影・足あとは、合図を受けた側が見せ方を決めます。全部終わると、場面（`Phases`）を見ていた `GameRunner` が本編を始めます。", "", flow2, "",
         "図の型: " + "、".join(tlink(n) for n in ["TutorialRunner", "TutorialScript", "Phases", "GameRunner", "Cues", "TutorialMemory", "MikoCueReceiver", "CloudCues", "FoodShadow", "StandArea", "CueReaction"]), "",
         "型ごとの細かい図は、各型の「つながり」にあります。"]
    write("diagrams.md", "\n".join(L))

def guide_page():
    L = ["# ガイド", ""]
    for g in data["guide"]:
        if g["id"] == "g-reading": continue
        L += [f"## {g['title']}", "", link_code(g["md"].replace("](architecture.md", "](../architecture.md"), "guide.md"), ""]
    write("guide.md", "\n".join(L))

def readme():
    pref = re.sub(r"^# .*\n", "", data["preface"]).strip()
    reading = next((g["md"] for g in data["guide"] if g["id"] == "g-reading"), "")
    L = ["# MouthOfGod スクリプト リファレンス", "", pref, "",
         "## 初めて読む人へ", "",
         "コードの形には、理由があります。数人が並行して作り、シーンと Prefab は Git でマージできない、という前提から来ています。型を読む前に、[設計の考え方（なぜこの形か）](design.md) を読んでください。各型の「なぜこの形か」にも、その型に関わる理由が出ます。", "",
         "| ページ | 中身 |", "|---|---|",
         "| [設計の考え方（なぜこの形か）](design.md) | 決まりと、その理由・あきらめたこと・生まれた型 |",
         "| [つながりの図](diagrams.md) | 機能どうしのつながりと、実行時の流れの図 |",
         "| [やりたいこと別の手順](recipes.md) | 生き物を元気にする、チュートリアルでイベントを待つ、食べ物を足す、など |",
         "| [ガイド](guide.md) | 全体像、機能どうしのつなぎ方（置き場・合図・場面）、Main シーンの姿、メニュー、Resources |", "",
         "## 機能（名前空間）", "", "| 機能 | 役割 | 型の数 |", "|---|---|---|"]
    for m in data["modules"]:
        n = len([t for t in types if t["mod"] == m])
        L.append(f"| [MouthOfGod.{m}]({m}.md) | {esc(data['intro'].get(m, '').split(chr(10))[0])} | {n} |")
    L += ["", "## すべての型", "", "<details><summary>名前順の一覧を開く（" + str(len(types)) + " 個）</summary>", "", "| 型 | 機能 | 役割 | 説明 |", "|---|---|---|---|"]
    for t in sorted(types, key=lambda t: t["name"].lower()):
        L.append(f"| {tlink(t['name'])} | {t['mod']} | {t['role']} | {esc(t['brief'])} |")
    L += ["", "</details>", "", "## この文書について", "", reading, "",
          "コードや説明文を直したら、`python3 tools/api_doc/build_md.py` を実行して、このページを作り直します。作り直し忘れは、PR の検査（API docs check）が警告します。検索つきの 1 ファイルの HTML が欲しいときは、`python3 tools/api_doc/build_html.py` で `tools/api_doc/out/index.html` に作れます（リポジトリには入れません）。"]
    write("README.md", "\n".join(L))

os.makedirs(OUT, exist_ok=True)
for m in data["modules"]: module_page(m)
design_page(); recipes_page(); diagrams_page(); guide_page(); readme()
print("wrote", sorted(os.listdir(OUT)))
