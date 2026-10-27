#!/usr/bin/env python3
"""Unity を動かさずに、Main シーンの「立つ場所 → 出る食べ物」を確かめる。

シーンと Prefab の YAML を読んで、実行時と同じ階層を組み立てる（Prefab の入れ子・Variant・上書きに対応）。
そのうえで、次のコードの判定を Python に写して動かす。

- Village/StandArea.cs   : 立つ場所の判定（Field の決め方、Tick、Enter/Exit、重なったときの戻り）
- Core/Standing.cs       : いま立っているフィールド
- Food/FoodTable(s).cs   : 食感 → 食べ物の表（専用の表 → 共通の表）

各フィールドの立つ場所の中心に頭を動かし、食感ごとに生まれる食べ物を出す。
その食べ物が、そのフィールドの欲しい物（FieldStep）に入っているかを確かめる。

使い方:
    python3 tools/field-food-check/field_food_check.py [--project .] [--scene Assets/Scenes/Main.unity]

終了コード: 全部通れば 0、失敗があれば 1。
注意: C# を実行しているわけではない。コードを変えたら、この写しも直す。
"""
import argparse
import math
import os
import re
import sys

import yaml

try:
    YamlLoader = yaml.CSafeLoader
except AttributeError:  # libyaml なし
    YamlLoader = yaml.SafeLoader

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)
PLACEHOLDER_TEXTURES = ("placeholder-a", "placeholder-b", "placeholder-c", "placeholder-d")


# ---------------------------------------------------------------- Unity のデータを読む

class Project:
    def __init__(self, root):
        self.root = root
        self.guid_path = {}
        self.script_name = {}
        self._docs = {}
        for base, _, files in os.walk(os.path.join(root, "Assets")):
            for f in files:
                if not f.endswith(".meta"):
                    continue
                with open(os.path.join(base, f), encoding="utf-8", errors="replace") as fh:
                    m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
                if not m:
                    continue
                path = os.path.join(base, f[:-5])
                self.guid_path[m.group(1)] = path
                if path.endswith(".cs"):
                    self.script_name[m.group(1)] = os.path.basename(path)[:-3]

    def docs(self, path):
        """path の YAML を、[(classId, fileID, stripped, data)] にして返す。"""
        if path in self._docs:
            return self._docs[path]
        with open(path, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        marks = list(HEADER.finditer(text))
        out = []
        for i, m in enumerate(marks):
            end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
            data = yaml.load(text[m.end():end], Loader=YamlLoader)
            out.append((int(m.group(1)), int(m.group(2)), bool(m.group(3)), data))
        self._docs[path] = out
        return out


class Node:
    def __init__(self, name):
        self.name = name
        self.parent = None
        self.children = []
        self.pos = [0.0, 0.0, 0.0]
        self.rot = [0.0, 0.0, 0.0, 1.0]
        self.scale = [1.0, 1.0, 1.0]
        self.active = True
        self.tag = "Untagged"
        self.comps = []

    def path(self):
        return (self.parent.path() + "/" if self.parent else "/") + self.name

    def active_in_hierarchy(self):
        return self.active and (self.parent is None or self.parent.active_in_hierarchy())

    def ancestors(self):
        n = self
        while n is not None:
            yield n
            n = n.parent

    def walk(self):
        yield self
        for c in self.children:
            yield from c.walk()


class Comp:
    def __init__(self, cls, props, node, ctx):
        self.cls = cls
        self.props = props
        self.node = node
        self.ctx = ctx  # このコンポーネントを定義したファイルの fileID → オブジェクト

    @property
    def enabled(self):
        return bool(self.props.get("m_Enabled", 1))


# ---- 数学（Unity の TRS をそのまま）

def q_rotate(q, v):
    x, y, z, w = q
    # v' = v + 2w(q×v) + 2 q×(q×v)
    cx, cy, cz = (y * v[2] - z * v[1], z * v[0] - x * v[2], x * v[1] - y * v[0])
    dx, dy, dz = (y * cz - z * cy, z * cx - x * cz, x * cy - y * cx)
    return [v[0] + 2 * (w * cx + dx), v[1] + 2 * (w * cy + dy), v[2] + 2 * (w * cz + dz)]


def q_conj(q):
    return [-q[0], -q[1], -q[2], q[3]]


def local_to_world(node, p):
    while node is not None:
        s = [p[i] * node.scale[i] for i in range(3)]
        r = q_rotate(node.rot, s)
        p = [r[i] + node.pos[i] for i in range(3)]
        node = node.parent
    return p


def world_to_local(node, p):
    if node.parent is not None:
        p = world_to_local(node.parent, p)
    d = [p[i] - node.pos[i] for i in range(3)]
    r = q_rotate(q_conj(node.rot), d)
    return [r[i] / node.scale[i] if node.scale[i] != 0 else 0.0 for i in range(3)]


# ---- 階層を組み立てる

class Builder:
    def __init__(self, project):
        self.p = project
        self.unresolved = []

    def instantiate(self, path):
        """path のファイルを、新しい Node の木にして返す: (roots, idmap, nodes)。"""
        idmap = {}
        roots = []
        nodes = []  # 新しく作った Node（ファイルの並び順）
        docs = self.p.docs(path)
        pending_parents = []  # (root_nodes_of_instance, m_TransformParent の fileID)

        # 1) 入れ子の PrefabInstance
        instance_maps = {}
        for cls, fid, stripped, data in docs:
            if cls != 1001:
                continue
            body = data["PrefabInstance"]
            src_guid = body["m_SourcePrefab"]["guid"]
            src_path = self.p.guid_path.get(src_guid)
            if src_path is None or not src_path.endswith(".prefab"):
                continue  # 飾りの FBX などは読まない
            sroots, smap, snodes = self.instantiate(src_path)
            instance_maps[fid] = smap
            nodes.extend(snodes)
            for mod in body["m_Modification"]["m_Modifications"] or []:
                self.apply_mod(smap, mod, idmap)
            parent_id = body["m_Modification"].get("m_TransformParent", {}).get("fileID", 0)
            for r in sroots:
                if parent_id:
                    pending_parents.append((r, parent_id))
                else:
                    roots.append(r)

        # 2) stripped の目印（入れ子の中身を、このファイルの fileID で指すためのもの）
        for cls, fid, stripped, data in docs:
            if not stripped:
                continue
            body = list(data.values())[0]
            src = body.get("m_CorrespondingSourceObject", {}).get("fileID", 0)
            inst = body.get("m_PrefabInstance", {}).get("fileID", 0)
            target = instance_maps.get(inst, {}).get(src)
            if target is not None:
                idmap[fid] = target

        # 3) 普通の GameObject / Transform / コンポーネント
        gos = {}
        for cls, fid, stripped, data in docs:
            if stripped or cls != 1:
                continue
            body = data["GameObject"]
            n = Node(str(body.get("m_Name", "")))
            n.active = bool(body.get("m_IsActive", 1))
            n.tag = str(body.get("m_TagString", "Untagged"))
            gos[fid] = n
            idmap[fid] = n
            nodes.append(n)
        for cls, fid, stripped, data in docs:
            if stripped or cls not in (4, 224):
                continue
            body = list(data.values())[0]
            n = idmap.get(body["m_GameObject"]["fileID"])
            if n is None:
                continue
            idmap[fid] = n
            lp, lr, ls = body["m_LocalPosition"], body["m_LocalRotation"], body["m_LocalScale"]
            n.pos = [float(lp["x"]), float(lp["y"]), float(lp["z"])]
            n.rot = [float(lr["x"]), float(lr["y"]), float(lr["z"]), float(lr["w"])]
            n.scale = [float(ls["x"]), float(ls["y"]), float(ls["z"])]
        for cls, fid, stripped, data in docs:
            if stripped or cls in (1, 4, 224, 1001):
                continue
            top = list(data.keys())[0]
            body = data[top]
            if not isinstance(body, dict) or "m_GameObject" not in body:
                continue
            n = idmap.get(body["m_GameObject"]["fileID"])
            if n is None:
                continue
            name = top
            if cls == 114:
                name = self.p.script_name.get(body.get("m_Script", {}).get("guid"), "MonoBehaviour")
            c = Comp(name, body, n, idmap)
            n.comps.append(c)
            idmap[fid] = c
        # 4) 親子
        for cls, fid, stripped, data in docs:
            if stripped or cls not in (4, 224):
                continue
            body = list(data.values())[0]
            n = idmap.get(body["m_GameObject"]["fileID"])
            father = body.get("m_Father", {}).get("fileID", 0)
            if n is None:
                continue
            if father and idmap.get(father) is not None:
                self.attach(idmap[father], n)
            elif not father:
                roots.append(n)
        for r, parent_id in pending_parents:
            parent = idmap.get(parent_id)
            if parent is None:
                roots.append(r)  # 飾りの入れ子。親が読めなくても、判定には関係しない
            else:
                self.attach(parent, r)
        return roots, idmap, nodes

    @staticmethod
    def attach(parent, child):
        if child.parent is not None and child in child.parent.children:
            child.parent.children.remove(child)
        child.parent = parent
        parent.children.append(child)

    def apply_mod(self, smap, mod, ctx):
        target = smap.get(mod["target"]["fileID"])
        path = mod["propertyPath"]
        if target is None:
            if path in ("head", "fieldId", "margin", "rows") or path.startswith(("size.", "center.")):
                self.unresolved.append("上書きの対象が見つかりません: %s の %s" % (mod["target"]["fileID"], path))
            return
        value, ref = mod.get("value"), mod.get("objectReference") or {}
        if isinstance(target, Node):
            self.apply_node_mod(target, path, value)
            return
        has_ref = bool(ref.get("fileID")) or bool(ref.get("guid"))
        set_path(target.props, path, dict(ref, _ctx=ctx) if has_ref else value, is_ref=isinstance(get_path(target.props, path), dict))

    @staticmethod
    def apply_node_mod(n, path, value):
        if path == "m_Name":
            n.name = str(value)
        elif path == "m_IsActive":
            n.active = bool(int(value))
        elif path == "m_TagString":
            n.tag = str(value)
        else:
            m = re.match(r"m_Local(Position|Rotation|Scale)\.([xyzw])$", path)
            if m:
                arr = {"Position": n.pos, "Rotation": n.rot, "Scale": n.scale}[m.group(1)]
                arr["xyzw".index(m.group(2))] = float(value)


def _tokens(path):
    out = []
    for part in path.split("."):
        m = re.match(r"data\[(\d+)\]$", part)
        if m:
            out.append(int(m.group(1)))
        elif part != "Array":
            out.append(part)
    return out


def get_path(props, path):
    cur = props
    for t in _tokens(path):
        try:
            cur = cur[t]
        except (KeyError, IndexError, TypeError):
            return None
    return cur


def set_path(props, path, value, is_ref=False):
    toks = _tokens(path)
    if path.endswith(".Array.size"):
        return
    cur = props
    for t in toks[:-1]:
        try:
            cur = cur[t]
        except (KeyError, IndexError, TypeError):
            return
    try:
        if isinstance(value, str) and value != "" and re.fullmatch(r"-?\d+(\.\d+)?([eE]-?\d+)?", value):
            value = float(value) if ("." in value or "e" in value.lower()) else int(value)
        if is_ref and value in ("", None):
            value = {"fileID": 0}
        cur[toks[-1]] = value
    except (KeyError, IndexError, TypeError):
        pass


def resolve_ref(project, ref, comp):
    """objectReference を Node / ファイルのパスにする。空なら None。"""
    if not isinstance(ref, dict) or not ref.get("fileID"):
        return None
    if ref.get("guid"):
        return project.guid_path.get(ref["guid"])
    ctx = ref.get("_ctx") or comp.ctx
    return ctx.get(ref["fileID"])


# ---------------------------------------------------------------- C# の判定を写す

class World:
    """シーン 1 つ分。Standing / FoodTables と、StandArea の状態を持つ。"""

    def __init__(self, project, roots):
        self.p = project
        self.roots = roots
        self.nodes = [n for r in roots for n in r.walk()]
        self.standing = ""  # FieldId。空は None
        self.occupied = []  # StandArea.Occupied
        self.warnings = []
        self.default_table = None
        self.field_tables = {}
        self.field_requests = set()  # IFieldRequests を実装するクラス名
        for guid, name in project.script_name.items():
            with open(project.guid_path[guid], encoding="utf-8", errors="replace") as fh:
                if re.search(r"class\s+%s\b[^{]*\bIFieldRequests\b" % re.escape(name), fh.read()):
                    self.field_requests.add(name)
        self.areas = []
        self.inside = {}
        for n in self.nodes:
            if not n.active_in_hierarchy():
                continue
            for c in n.comps:
                if not c.enabled:
                    continue
                if c.cls == "FoodTable":
                    self.register_table(c)
                elif c.cls == "StandArea":
                    self.areas.append(c)
                    self.inside[c] = False

    # --- FoodTables.Register
    def register_table(self, c):
        fid = str(c.props.get("fieldId") or "")
        if fid == "":
            if self.default_table is not None and self.default_table is not c:
                self.warnings.append("共通の表（Field Id が空）が重なっています: %s は登録されません" % c.node.path())
                return
            self.default_table = c
            return
        if fid in self.field_tables and self.field_tables[fid] is not c:
            self.warnings.append("フィールド「%s」の表が重なっています: %s は登録されません" % (fid, c.node.path()))
            return
        self.field_tables[fid] = c

    def rows(self, table):
        out = []
        for row in table.props.get("rows") or []:
            tex = str(row.get("texture") or "")
            path = resolve_ref(self.p, row.get("food"), table)
            out.append((tex, os.path.basename(path)[:-7] if path else None))
        return out

    @staticmethod
    def find(rows, texture):
        for tex, food in rows:
            if tex and tex == texture:
                return food  # 先の行を使う（Prefab が空なら None）
        return None

    # --- FoodTables.Resolve
    def resolve(self, field, texture):
        if not texture:
            return None
        if field and field in self.field_tables:
            food = self.find(self.rows(self.field_tables[field]), texture)
            if food is not None:
                return food
        if self.default_table is not None:
            return self.find(self.rows(self.default_table), texture)
        return None

    # --- StandArea.Field
    def area_field(self, area):
        fid = str(area.props.get("fieldId") or "")
        if fid:
            return fid
        for n in area.node.ancestors():  # GetComponentInParent<IFieldRequests>()
            if not n.active_in_hierarchy():
                continue
            for c in n.comps:
                if c.cls in self.field_requests and c.enabled:
                    return str(c.props.get("fieldId") or "")
        return ""

    def area_head(self, area):
        ref = area.props.get("head")
        if isinstance(ref, dict) and ref.get("fileID"):
            return resolve_ref(self.p, ref, area), True
        return self.main_camera(), False

    def main_camera(self):
        for n in self.nodes:  # Camera.main: 有効で、MainCamera のタグが付いた Camera
            if n.tag == "MainCamera" and n.active_in_hierarchy() and any(c.cls == "Camera" and c.enabled for c in n.comps):
                return n
        return None

    # --- StandArea.Contains / NextInside / Tick / Enter / Exit
    @staticmethod
    def contains(local, center, size, margin, ignore_height):
        half = [abs(size[0]) / 2 + margin, abs(size[1]) / 2 + margin, abs(size[2]) / 2 + margin]
        d = [local[i] - center[i] for i in range(3)]
        if abs(d[0]) > half[0] or abs(d[2]) > half[2]:
            return False
        return True if ignore_height else abs(d[1]) <= half[1]

    def vec(self, area, key):
        v = area.props[key]
        return [float(v["x"]), float(v["y"]), float(v["z"])]

    def tick(self, area, head_world):
        local = world_to_local(area.node, head_world)
        was = self.inside[area]
        inside = self.contains(local, self.vec(area, "center"), self.vec(area, "size"),
                               float(area.props["margin"]) if was else 0.0, bool(area.props["ignoreHeight"]))
        if inside and not was:
            self.enter(area)
        elif not inside and was:
            self.exit(area)

    def enter(self, area):
        field = self.area_field(area)
        if not field:
            return
        self.inside[area] = True
        if area in self.occupied:
            self.occupied.remove(area)
        self.occupied.append(area)
        self.standing = field

    def exit(self, area):
        if not self.inside[area]:
            return
        self.inside[area] = False
        if area in self.occupied:
            self.occupied.remove(area)
        if self.area_field(area) == self.standing:
            self.standing = ""
        if not self.standing and self.occupied:
            self.standing = self.area_field(self.occupied[-1])

    def move_head(self, world_pos):
        """頭を world_pos に動かし、全 StandArea の Update を 1 回ずつ呼ぶ（head の指定を含めて）。"""
        for area in self.areas:
            head, explicit = self.area_head(area)
            if head is None:
                continue
            pos = world_pos if (not explicit or head is self.main_camera()) else local_to_world(head, [0.0, 0.0, 0.0])
            self.tick(area, pos)


# ---------------------------------------------------------------- 確かめる

def field_of_root(world, node):
    """node の祖先から、FieldStages（IFieldRequests）のある Node を探す。"""
    for n in node.ancestors():
        for c in n.comps:
            if c.cls in world.field_requests:
                return n, str(c.props.get("fieldId") or "")
    return None, ""


def expected_foods(world, field):
    """そのフィールドの FieldStep の食べ物（FieldStages の木の中）。"""
    foods = []
    for n in world.nodes:
        for c in n.comps:
            if c.cls in world.field_requests and str(c.props.get("fieldId") or "") == field:
                for m in n.walk():
                    for s in m.comps:
                        if s.cls == "FieldStep" and s.props.get("food"):
                            foods.append(str(s.props["food"]))
    return foods


def device_textures(project, builder):
    """MockDevice の Prefab に書かれた、噛んでいる間に順に出る食感。"""
    path = os.path.join(project.root, "Assets/Resources/Prefabs/Device/MockDevice.prefab")
    if not os.path.exists(path):
        return []
    roots, _, _ = builder.instantiate(path)
    for r in roots:
        for n in r.walk():
            for c in n.comps:
                if c.cls == "MockDevice" and c.props.get("textureIds"):
                    return [str(t) for t in c.props["textureIds"]]
    return []


FIELD_COLORS = {"village": "#e0a030", "mountain": "#3a9a5a", "grassland": "#9acd32", "sea": "#3a7bd5"}


def render_svg(world, textures, out_path, title):
    """真上から見た図を SVG に書く。床を一定の間隔で歩かせ、どのフィールドと判定されるかを色にする。"""
    boxes = []
    for area in world.areas:
        c, sz = world.vec(area, "center"), world.vec(area, "size")
        corners = [local_to_world(area.node, [c[0] + dx * sz[0] / 2, c[1], c[2] + dz * sz[2] / 2])
                   for dx, dz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        boxes.append((area, corners, local_to_world(area.node, c)))
    xs = [p[0] for _, cs, _ in boxes for p in cs] + [0.0]
    zs = [p[2] for _, cs, _ in boxes for p in cs] + [0.0]
    x0, x1, z0, z1 = min(xs) - 0.6, max(xs) + 0.6, min(zs) - 0.6, max(zs) + 0.6
    scale = 150.0  # 1 m = 150 px
    W, H = (x1 - x0) * scale, (z1 - z0) * scale
    top = 80.0

    def px(x, z):
        return (x - x0) * scale, top + (z1 - z) * scale

    out = []
    step = 0.04
    saved = (world.standing, list(world.occupied), dict(world.inside))
    z = z0
    while z < z1:
        x = x0
        while x < x1:
            world.standing = ""
            world.occupied.clear()
            for a in world.areas:
                world.inside[a] = False
            world.move_head([x, 1.6, z])
            if world.standing:
                u, v = px(x, z + step)
                out.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" fill-opacity="0.28"/>'
                           % (u, v, step * scale + 0.5, step * scale + 0.5, FIELD_COLORS.get(world.standing, "#888")))
            x += step
        z += step
    world.standing, world.occupied[:], world.inside = saved[0], saved[1], saved[2]
    world.occupied[:] = saved[1]

    for area, corners, center in boxes:
        field = world.area_field(area)
        col = FIELD_COLORS.get(field, "#888")
        pts = " ".join("%.1f,%.1f" % px(p[0], p[2]) for p in corners)
        out.append('<polygon points="%s" fill="none" stroke="%s" stroke-width="3"/>' % (pts, col))
        world.standing = ""
        world.occupied.clear()
        for a in world.areas:
            world.inside[a] = False
        world.move_head(center)
        got = world.standing
        cu, cv = px(center[0], center[2])
        out.append('<circle cx="%.1f" cy="%.1f" r="7" fill="%s" stroke="#fff" stroke-width="2"/>' % (cu, cv, col if got == field else "#d33"))
        rows = world.rows(world.field_tables[field]) if field in world.field_tables else []
        label = "  ".join("%s=%s" % (t.replace("placeholder-", ""), f) for t, f in rows[:4])
        out.append('<text x="%.1f" y="%.1f" font-size="15" font-weight="bold" fill="%s" text-anchor="middle">%s</text>' % (cu, cv - 14, col, field))
        out.append('<text x="%.1f" y="%.1f" font-size="12" fill="#222" text-anchor="middle">%s</text>' % (cu, cv + 22, label))
        out.append('<text x="%.1f" y="%.1f" font-size="12" fill="#222" text-anchor="middle">立つと Standing=%s</text>' % (cu, cv + 38, got or "なし"))

    ou, ov = px(0.0, 0.0)
    out.append('<path d="M%.1f %.1f l-8 0 l16 0 M%.1f %.1f l0 -8 l0 16" stroke="#555" stroke-width="2"/>' % (ou, ov, ou, ov))
    out.append('<text x="%.1f" y="%.1f" font-size="11" fill="#555">ワールド原点（リグの根元）</text>' % (ou + 10, ov - 8))
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="%.0f" height="%.0f" viewBox="0 0 %.0f %.0f" '
           'font-family="IPAGothic, IPAPGothic, sans-serif"><rect width="100%%" height="100%%" fill="#fafafa"/>'
           '<text x="14" y="26" font-size="18" font-weight="bold" fill="#111">%s</text>'
           '<text x="14" y="48" font-size="12" fill="#444">真上から見た図（1 マス 4 cm）。色の付いた床に頭を置くと、その色のフィールドに「立った」と判定されます。</text><text x="14" y="64" font-size="12" fill="#444">枠は立つ場所の箱、点は箱の中心（赤い点は、中心に立っても期待したフィールドにならなかった所）。</text>%s</svg>'
           % (W, H + top, W, H + top, title, "".join(out)))
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(svg)
    print("  図を書きました: %s" % out_path)


def run(project_root, scene_rel, svg_path=None):
    project = Project(project_root)
    scene = os.path.join(project_root, scene_rel)
    builder = Builder(project)
    roots, _, _ = builder.instantiate(scene)
    world = World(project, roots)

    fails = []
    warns = []

    def check(ok, label, detail=""):
        print("  %s %s%s" % ("OK  " if ok else "NG  ", label, (" … " + detail) if (detail and not ok) else ""))
        if not ok:
            fails.append(label + (" … " + detail if detail else ""))

    def warn(label):
        print("  !!   %s" % label)
        warns.append(label)

    print("== シーン: %s" % scene_rel)
    for msg in builder.unresolved:
        print("  ?? 読めなかった所: %s" % msg)
    print("  フィールドの表: %s" % (", ".join(sorted(world.field_tables)) or "(なし)"))
    print("  共通の表: %s" % ("あり (%s)" % world.default_table.node.path() if world.default_table else "なし"))
    for w in world.warnings:
        print("  !! %s" % w)
    cam = world.main_camera()
    check(cam is not None, "Camera.main がある", cam.path() if cam else "MainCamera のタグが付いた有効な Camera がありません")

    textures = device_textures(project, builder)
    print("  デバイス（MockDevice）が出す食感: %s" % (", ".join(textures) or "(読めません)"))
    if not textures:
        textures = list(PLACEHOLDER_TEXTURES)

    # --- 立つ場所ごと
    print("\n== 立つ場所（StandArea）")
    centers = {}
    for area in world.areas:
        field = world.area_field(area)
        print("  - %s  → Field=%r" % (area.node.path(), field))
        check(bool(field), "%s: フィールドが決まる" % area.node.path(),
              "Field Id が空で、親にも FieldStages が無いので、立っても Standing が変わりません" if not field else "")
        head, explicit = world.area_head(area)
        if explicit:
            check(head is cam, "%s: Head が Main Camera を指す" % area.node.path(),
                  "Head=%s" % (head.path() if hasattr(head, "path") else head))
        if field:
            centers.setdefault(field, []).append(area)

    # --- フィールドごとに、中心に立って、食感ごとに引く
    print("\n== フィールドごとの食べ物")
    for field in sorted(set(list(world.field_tables) + list(centers))):
        print("\n[%s]" % field)
        areas = centers.get(field, [])
        check(bool(areas), "立つ場所がある")
        want = expected_foods(world, field)
        print("  欲しい物（FieldStep）: %s" % (", ".join(want) or "(なし)"))
        check(field in world.field_tables, "専用の食べ物の表がある")
        for area in areas:
            world.standing = ""
            world.occupied.clear()
            for a in world.areas:
                world.inside[a] = False
            center = local_to_world(area.node, world.vec(area, "center"))
            world.move_head(center)
            check(world.standing == field, "中心に立つと Standing が %r になる" % field, "実際: %r" % world.standing)
            own_rows = world.rows(world.field_tables[field]) if field in world.field_tables else []
            for tex in textures:
                food = world.resolve(world.standing, tex)
                own = world.find(own_rows, tex)
                note = ""
                if own is None and food is not None:
                    note = "  (共通の表の食べ物)"
                elif food is None:
                    note = "  (何も生まれません)"
                print("    %-14s → %-10s%s" % (tex, food, note))
                if own is not None:
                    check(food == own, "%s は専用の表の食べ物 %r になる" % (tex, own), "実際: %r" % food)
                    if want:
                        check(own in want, "%s の食べ物 %r が、そのフィールドの欲しい物に入る" % (tex, own), "欲しい物: %s" % want)
                elif food is not None:
                    warn("%s は専用の表に行が無く、どのフィールドでも共通の食べ物 %r になります" % (tex, food))
                else:
                    warn("%s は %s で何も生まれません" % (tex, field))
            # 出る側から外へ歩く
            world.move_head([center[0] + 50.0, center[1], center[2] + 50.0])
            check(world.standing == "", "遠くへ離れると Standing が空になる", "実際: %r" % world.standing)

    # --- 4 つのフィールドで、食べ物が違うこと
    print("\n== フィールドどうしの違い")
    sets = {}
    for field in sorted(world.field_tables):
        sets[field] = tuple(world.find(world.rows(world.field_tables[field]), t) for t in textures)
        print("  %-10s %s" % (field, sets[field]))
    check(len(set(sets.values())) == len(sets), "フィールドごとに食べ物の並びが違う")

    # --- 立つ場所の重なり（箱の床を 5 cm 間隔で調べる）
    print("\n== 立つ場所の重なり")
    for i, a in enumerate(world.areas):
        for b in world.areas[i + 1:]:
            ca, sa = world.vec(a, "center"), world.vec(a, "size")
            hits, step = 0, 0.05
            gx = -abs(sa[0]) / 2
            while gx <= abs(sa[0]) / 2:
                gz = -abs(sa[2]) / 2
                while gz <= abs(sa[2]) / 2:
                    w = local_to_world(a.node, [ca[0] + gx, ca[1], ca[2] + gz])
                    local = world_to_local(b.node, w)
                    if world.contains(local, world.vec(b, "center"), world.vec(b, "size"), 0.0, bool(b.props["ignoreHeight"])):
                        hits += 1
                    gz += step
                gx += step
            if hits:
                warn("%s と %s の箱が重なっています（約 %d 点）。重なった所では、あとから入った方が選ばれます" % (a.node.path(), b.node.path(), hits))
    print("  （重なりは、立つ場所の箱の大きさを決めた人が意図したものか、確かめてください）")

    if svg_path:
        render_svg(world, textures, svg_path, "%s の立つ場所と判定（%s）" % (os.path.basename(scene_rel), os.path.basename(project_root)))

    print("\n== 結果: %s（注意 %d 件）" % ("すべて通りました" if not fails else "%d 件が通りませんでした" % len(fails), len(warns)))
    for f in fails:
        print("  - %s" % f)
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--project", default=".", help="Unity プロジェクトの場所（Assets の親）")
    ap.add_argument("--scene", default="Assets/Scenes/Main.unity")
    ap.add_argument("--svg", help="真上から見た図を、この SVG に書く")
    args = ap.parse_args()
    sys.setrecursionlimit(10000)
    sys.exit(run(os.path.abspath(args.project), args.scene, args.svg))


if __name__ == "__main__":
    main()
