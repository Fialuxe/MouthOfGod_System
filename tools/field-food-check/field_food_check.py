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


class AnyId(dict):
    """どの fileID で引いても、同じ Node を返す（FBX の中身は読まないので、入れ物 1 つに寄せる）。"""

    def __init__(self, node):
        super().__init__()
        self.node = node

    def get(self, key, default=None):
        return self.node

    def __getitem__(self, key):
        return self.node


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
                if src_path is None:
                    continue
                model = Node(os.path.splitext(os.path.basename(src_path))[0])  # FBX など。1 つの Node として扱う
                sroots, smap, snodes = [model], AnyId(model), [model]
            else:
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
        for cls, fid, stripped, data in docs:  # 兄弟の並びは、Transform の m_Children の順（ステップの順番になる）
            if stripped or cls not in (4, 224):
                continue
            body = list(data.values())[0]
            n = idmap.get(body["m_GameObject"]["fileID"])
            if n is None or not body.get("m_Children"):
                continue
            ordered = [idmap[c["fileID"]] for c in body["m_Children"] if isinstance(idmap.get(c["fileID"]), Node)]
            n.children = [c for c in ordered if c in n.children] + [c for c in n.children if c not in ordered]
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
            if path in ("head", "fieldId", "margin", "rows") or path.startswith(("size.", "center.", "hideWhenDone", "helped")):
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


RESULTS = []  # (フィールド, OK/NG/WARN, ラベル, 詳細)
CTX = {"f": ""}  # いま確かめているフィールド（図にまとめるため）
COLLIDERS = ("BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider")
REQUEST_OPENERS = ("OpenAllRequestsAfterTutorial", "GameRunner")


def ref_node(world, ref, comp):
    t = resolve_ref(world.p, ref, comp)
    if isinstance(t, Node):
        return t
    if isinstance(t, Comp):
        return t.node
    return None


def field_root(world, field):
    for n in world.nodes:
        for c in n.comps:
            if c.cls in world.field_requests and str(c.props.get("fieldId") or "") == field:
                return n, c
    return None, None


def steps_in_order(root):
    return [c for m in root.walk() for c in m.comps if c.cls == "FieldStep"]


def apply_steps(steps, done, hides):
    """FieldStages.Apply と同じ。済んだステップの子を出し、消す物を消す（「消す」が「出す」に勝つ）。"""
    for i, st in enumerate(steps):
        for go in hides[i]:
            go.active = True
    for i, st in enumerate(steps):
        for child in st.node.children:
            child.active = done[i]
    for i, st in enumerate(steps):
        if done[i]:
            for go in hides[i]:
                go.active = False


def step_anchor(world, st):
    """FieldStep.Anchor と同じ。(Node, 種類)。種類は mark（村人の頭の印）/ villager（村人）/ step（ステップ自身）。"""
    helped = [ref_node(world, r, st) for r in st.props.get("helped") or []]
    first = helped[0] if helped else None
    if first is None:
        return st.node, "step"
    mood = next((c for c in first.comps if c.cls == "VillagerMood"), None)
    mark = ref_node(world, mood.props.get("worryMark"), mood) if mood else None
    return (mark, "mark") if mark is not None else (first, "villager")


def bubble_settings(project):
    path = os.path.join(project.root, "Assets/Resources/RequestBubbleSettings.asset")
    try:
        body = project.docs(path)[0][3]["MonoBehaviour"]
        return float(body.get("_height", 0.15)), float(body.get("_size", 0.07))
    except Exception:
        return 0.15, 0.07


KIND_NAMES = {"mark": "村人の頭の印の上", "villager": "村人の足元の上", "step": "ステップを置いた場所の上"}


def bubble_points(world, project):
    """吹き出しが出る場所（ワールド座標）。RequestBubbles と同じく、Anchor の真上に Height だけ上げる。"""
    height, size = bubble_settings(project)
    points = []
    for field in sorted(world.field_tables):
        root, stages = field_root(world, field)
        if root is None:
            continue
        for st in steps_in_order(root):
            food = str(st.props.get("food") or "")
            if not food:
                continue
            anchor, kind = step_anchor(world, st)
            a = local_to_world(anchor, [0.0, 0.0, 0.0])
            points.append({"field": field, "step": st.node.name, "food": food, "kind": kind, "anchor": anchor,
                           "anchor_pos": a, "pos": [a[0], a[1] + height, a[2]], "active": anchor.active_in_hierarchy()})
    return points, height, size


def render_bubble_svg(world, project, out_path, title):
    """吹き出しが出る場所を、真上から見た図と一覧にして SVG に書く。"""
    points, height, size = bubble_points(world, project)
    stand = {}
    for area in world.areas:
        f = world.area_field(area)
        stand[f] = (local_to_world(area.node, world.vec(area, "center")), area)
    xs = [pt["pos"][0] for pt in points] + [c[0][0] for c in stand.values()]
    zs = [pt["pos"][2] for pt in points] + [c[0][2] for c in stand.values()]
    for f, (c, area) in stand.items():  # 立つ場所の箱の四隅も入れる（図からはみ出さないように）
        sz, ce = world.vec(area, "size"), world.vec(area, "center")
        for dx, dz in ((-1, -1), (1, 1)):
            q = local_to_world(area.node, [ce[0] + dx * sz[0] / 2, 0, ce[2] + dz * sz[2] / 2])
            xs.append(q[0])
            zs.append(q[2])
    x0, x1, z0, z1 = min(xs) - 0.3, max(xs) + 0.3, min(zs) - 0.3, max(zs) + 0.3
    scale, top = 150.0, 110.0
    W, H = max((x1 - x0) * scale, 820.0), (z1 - z0) * scale

    def px(x, z):
        return (x - x0) * scale, top + (z1 - z) * scale

    out = ['<rect width="100%" height="100%" fill="#fafafa"/>',
           '<text x="14" y="26" font-size="18" font-weight="bold" fill="#111">%s</text>' % title,
           '<text x="14" y="48" font-size="12" fill="#444">真上から見た図（図の 150 px ＝ 1 m）。吹き出しは、印の真上 %.2f m に出て、カメラの方を向きます。</text>' % height,
           '<text x="14" y="66" font-size="12" fill="#444">● 吹き出しが出る場所　▲ 立つ場所の中心　破線: フィールド（食べ物が届く範囲）　同じ場所の吹き出しは、上に積まれます。</text>']
    for f, (c, area) in stand.items():
        col = FIELD_COLORS.get(f, "#888")
        sz = world.vec(area, "size")
        cs = [local_to_world(area.node, [world.vec(area, "center")[0] + dx * sz[0] / 2, 0, world.vec(area, "center")[2] + dz * sz[2] / 2])
              for dx, dz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        out.append('<polygon points="%s" fill="%s" fill-opacity="0.07" stroke="%s" stroke-width="1.5"/>' % (" ".join("%.1f,%.1f" % px(q[0], q[2]) for q in cs), col, col))
        u, v = px(c[0], c[2])
        out.append('<path d="M%.1f %.1f l-7 12 l14 0 z" fill="%s"/>' % (u, v - 6, col))
        zone = field_zone(world, f)
        if zone:
            out.append('<polygon points="%s" fill="none" stroke="%s" stroke-width="2" stroke-dasharray="6 4"/>' % (" ".join("%.1f,%.1f" % px(q[0], q[2]) for q in zone[0]), col))
    seen = {}
    for pt in points:
        col = FIELD_COLORS.get(pt["field"], "#888")
        key = (round(pt["pos"][0], 2), round(pt["pos"][2], 2))
        seen[key] = seen.get(key, 0) + 1
        u, v = px(pt["pos"][0], pt["pos"][2])
        v -= (seen[key] - 1) * 16
        out.append('<circle cx="%.1f" cy="%.1f" r="6" fill="%s" stroke="#fff" stroke-width="1.5"/>' % (u, v, col))
        out.append('<text x="%.1f" y="%.1f" font-size="11" fill="#222">%s</text>' % (u + 9, v + 4, pt["food"]))
    for f, (c, area) in stand.items():
        u, v = px(c[0], c[2])
        out.append('<text x="%.1f" y="%.1f" font-size="14" font-weight="bold" fill="%s" text-anchor="middle">%s</text>' % (u, v + 28, FIELD_COLORS.get(f, "#333"), f))

    y = top + H + 30
    out.append('<text x="14" y="%.1f" font-size="15" font-weight="bold" fill="#111">吹き出しが出る場所の一覧</text>' % y)
    heads = ["フィールド", "欲しい物", "出る場所", "世界座標 x / y（高さ）/ z", "立つ場所の中心からの距離"]
    xsx = [14, 110, 200, 400, 640]
    y += 24
    for hx, h in zip(xsx, heads):
        out.append('<text x="%d" y="%.1f" font-size="12" fill="#444">%s</text>' % (hx, y, h))
    for pt in points:
        y += 21
        c = stand.get(pt["field"], (None,))[0]
        dist = math.hypot(pt["pos"][0] - c[0], pt["pos"][2] - c[2]) if c else float("nan")
        col = FIELD_COLORS.get(pt["field"], "#333")
        cells = [pt["field"], pt["food"], KIND_NAMES[pt["kind"]] + ("" if pt["active"] else "（いまは非表示）"),
                 "%.2f / %.2f / %.2f" % tuple(pt["pos"]), "%.2f m" % dist]
        out.append('<line x1="14" x2="%.1f" y1="%.1f" y2="%.1f" stroke="#e5e5e5"/>' % (W - 14, y - 15, y - 15))
        for i, (hx, cell) in enumerate(zip(xsx, cells)):
            out.append('<text x="%d" y="%.1f" font-size="12" fill="%s">%s</text>' % (hx, y, col if i == 0 else "#222", cell))
    y += 34
    same = {}
    for pt in points:
        same.setdefault((pt["field"], round(pt["pos"][0], 2), round(pt["pos"][2], 2)), []).append(pt["food"])
    notes = ["気づいたこと:"]
    for (f, _, _), foods in same.items():
        if len(foods) > 1:
            notes.append("・%s: %s の吹き出しは、同じ場所に出ます（同時に出ると上に積まれます）。" % (f, "、".join(foods)))
    notes += ["確認できていないこと:",
             "・吹き出しが、地形や建物に隠れず、立つ場所から見えるか（物の高さと向きは、Play で見ないと分かりません）。",
             "・吹き出しの大きさが、読める大きさか（設定の大きさは %.2f）。" % size,
             "・「村人の足元の上」「ステップを置いた場所の上」に出るものは、頭の高さに出ず、低く出る可能性があります。"]
    for line in notes:
        out.append('<text x="14" y="%.1f" font-size="12" fill="%s">%s</text>' % (y, "#a33" if line.startswith(("確認", "・吹", "・「")) else "#222", line))
        y += 18
    Ht = y + 10
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="%.0f" height="%.0f" viewBox="0 0 %.0f %.0f" font-family="IPAGothic, IPAPGothic, sans-serif">%s</svg>'
           % (W, Ht, W, Ht, "".join(out)))
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(svg)
    print("  吹き出しの位置の図を書きました: %s" % out_path)
    return points


def check_events(world, project, builder, textures, check, warn):
    """食べ物が出る・吹き出しが出る・物の表示と非表示が切り替わる、の流れを、フィールドごとに確かめる。"""
    print("\n== 食べ物が出る流れ（デバイス → 食べ物を生む所 → 表 → Prefab）")
    spawner = next((c for n in world.nodes for c in n.comps if c.cls == "FoodSpawner"), None)
    check(spawner is not None and spawner.enabled and spawner.node.active_in_hierarchy(), "FoodSpawner が、シーンで有効になっている")
    if spawner is not None:
        point = ref_node(world, spawner.props.get("spawnPoint"), spawner)
        print("  生む位置: %s / 初速 %s m/s" % (point.path() if point else "FoodSpawner 自身の位置", spawner.props.get("launchSpeed")))
    device_prefab = os.path.join(project.root, "Assets/Resources/Prefabs/Device/MockDevice.prefab")
    check(os.path.exists(device_prefab), "MockDevice の Prefab がある（実機がなければ、これが自動で置かれる）")

    seen = {}
    for field in sorted(world.field_tables):
        for tex, food in world.rows(world.field_tables[field]):
            if not food or food in seen:
                continue
            ref = next((r.get("food") for r in world.field_tables[field].props.get("rows") or [] if r.get("texture") == tex), None)
            path = resolve_ref(project, ref, world.field_tables[field])
            roots, _, _ = builder.instantiate(path) if path else ([], None, None)
            comps = [c.cls for r in roots for m in r.walk() for c in m.comps]
            scale = roots[0].scale if roots else None
            seen[food] = (path, comps, scale)
    for food, (path, comps, scale) in sorted(seen.items()):
        icon = os.path.exists(os.path.join(project.root, "Assets/Resources/Icons/Food", food + ".png"))
        print("  - %-9s Prefab=%s  Rigidbody=%s  Collider=%s  アイコン=%s" % (
            food, "あり" if path else "なし", "あり" if "Rigidbody" in comps else "なし",
            "あり" if any(c in COLLIDERS for c in comps) else "なし", "あり" if icon else "なし（名前の文字で出る）"))
        check(path is not None, "%s: 食べ物の Prefab が見つかる" % food)
        check("Rigidbody" in comps, "%s: Rigidbody がある（飛んで、DeliveryZone に当たる）" % food)
        check(any(c in COLLIDERS for c in comps), "%s: Collider がある（DeliveryZone に当たる）" % food)
        if not icon:
            warn("%s: 吹き出しのアイコンがありません。名前の文字で出ます" % food)

    CTX["f"] = ""
    openers = [c.cls for n in world.nodes if n.active_in_hierarchy() for c in n.comps if c.cls in REQUEST_OPENERS and c.enabled]
    print("\n== 欲しい物を出す役: %s" % (", ".join(openers) or "なし"))
    check(bool(openers), "欲しい物を出す役が、シーンにある（なければ、吹き出しは出ない）")
    if "OpenAllRequestsAfterTutorial" in openers:
        warn("欲しい物を出す役は、サンドボックス用の仮のスクリプト（OpenAllRequestsAfterTutorial）です。チュートリアルが終わると、全フィールドにいっぺんに出します")

    regs = [(n, str(c.props.get("fieldId") or "")) for n in world.nodes if n.active_in_hierarchy() for c in n.comps if c.cls in world.field_requests and c.enabled]
    dup = sorted({i for _, i in regs if [j for _, j in regs].count(i) > 1})
    CTX["f"] = dup[0] if dup else ""
    check(not dup and all(i for _, i in regs), "フィールドの ID が、空でも重なってもいない",
          "; ".join("%s: %s" % (d, " と ".join(n.path() for n, i in regs if i == d)) for d in dup) + "。先に登録された方だけが使われ、あとの方は警告が出て外れます")

    for field in sorted(world.field_tables):
        print("\n[%s]" % field)
        CTX["f"] = field
        root, stages = field_root(world, field)
        if root is None:
            check(False, "フィールド（FieldStages）がある")
            continue
        steps = steps_in_order(root)
        hides = []
        for st in steps:
            nodes = []
            for r in st.props.get("hideWhenDone") or []:
                n = ref_node(world, r, st)
                if n is not None:
                    nodes.append(n)
            hides.append(nodes)
            check(len(nodes) == len(st.props.get("hideWhenDone") or []), "%s: 消す物の指定が、全部見つかる" % st.node.name)
        print("  ステップ: %s" % ", ".join("%s(%s)" % (st.node.name, st.props.get("food")) for st in steps))

        # --- 届く範囲
        zone = next((c for m in root.walk() for c in m.comps if c.cls == "DeliveryZone"), None)
        box = next((c for c in (zone.node.comps if zone else []) if c.cls in COLLIDERS and c.enabled and c.props.get("m_IsTrigger")), None)
        check(zone is not None and box is not None and zone.node.active_in_hierarchy(),
              "食べ物が届く範囲（DeliveryZone と Trigger の BoxCollider）が、有効である")

        zones = [c for m in root.walk() for c in m.comps if c.cls == "DeliveryZone" and c.enabled and m.active_in_hierarchy()]
        targets = {}
        for z in zones:
            fid = str(z.props.get("fieldId") or "")
            if fid:
                targets[z.node.path()] = "ID「%s」の登録先" % fid
                continue
            owner = next((n for n in z.node.ancestors() if any(c.cls in world.field_requests for c in n.comps)), None)
            targets[z.node.path()] = owner.path() if owner else "(なし)"
        if len(zones) > 1:
            print("  DeliveryZone が %d つ: %s" % (len(zones), "; ".join("%s → %s" % (k.split("/")[-1], v.split("/")[-1]) for k, v in targets.items())))
        check(len(set(targets.values())) <= 1, "届く範囲（DeliveryZone）が、すべて同じ FieldStages に届ける",
              "別々の FieldStages に届きます。片方は Fields に登録されず、欲しい物も出ません。食べ物が両方の範囲に入ると、先に当たった範囲が「頼まれていない物」として印を付けることがあり、そのとき頼まれた物は届きません（当たる順番は未確認）")

        # --- 欲しい物を出す → 吹き出し
        maxopen = int(stages.props.get("maxOpenRequests") or 2)
        opened = [i for i, st in enumerate(steps) if st.props.get("food")][:maxopen]
        check(bool(opened), "欲しい物を出せるステップがある")
        for i in opened:
            st = steps[i]
            anchor, kind = step_anchor(world, st)
            first = anchor if kind != "step" else None
            food = str(st.props.get("food"))
            icon = os.path.exists(os.path.join(project.root, "Assets/Resources/Icons/Food", food + ".png"))
            print("  吹き出し: %-8s を欲しがる（%s）→ 出る場所: %s%s / %s" % (
                food, st.node.name, anchor.path().split("/")[-1], "" if anchor.active_in_hierarchy() else "（いまは非表示）",
                "アイコン" if icon else "名前の文字"))
            if first is None:
                warn("%s: 吹き出しを出す村人（Helped）が空です。吹き出しは、ステップの場所に出ます" % st.node.name)

        # --- 表示と非表示
        everything = [n for n in root.walk()] + [g for h in hides for g in h]
        authored = {id(n): n.active for n in everything}
        done = [False] * len(steps)
        apply_steps(steps, done, hides)  # Start() で呼ばれる
        start = {id(n): n.active for n in everything}
        changed = [n.name for n in everything if authored[id(n)] != start[id(n)]]
        print("  始めの見た目に直される物: %s" % (", ".join(changed) or "なし"))
        def names(nodes):
            nodes = list(nodes)
            return ", ".join(n.name for n in nodes[:4]) + (" …ほか %d 個" % (len(nodes) - 4) if len(nodes) > 4 else "") if nodes else "なし"

        for i, st in enumerate(steps):
            kids = list(st.node.children)
            hide_now = [g for g in hides[i]]
            before_kids = [c.active for c in kids]
            before_hide = [g.active for g in hide_now]
            done[i] = True
            apply_steps(steps, done, hides)
            shown = [c for c, b in zip(kids, before_kids) if c.active and not b]
            hidden = [g for g, b in zip(hide_now, before_hide) if b and not g.active]
            print("  %-12s が済む → 出る(%d): %s / 消える(%d): %s" % (st.node.name, len(shown), names(shown), len(hidden), names(hidden)))
            check(bool(shown or hidden), "%s: 済むと、見た目が変わる" % st.node.name)
            invisible = [c for c in kids if not c.active_in_hierarchy()]
            check(not invisible, "%s: 出るはずの子 %d 個が、全部見える" % (st.node.name, len(kids)), "見えない: %s（親が非表示）" % names(invisible))
            still = [g for g in hide_now if g.active]
            check(not still, "%s: 消す物 %d 個が、全部消える" % (st.node.name, len(hide_now)), "残る: %s" % names(still))
        done = [False] * len(steps)
        apply_steps(steps, done, hides)  # Restart
        check(all(n.active == start[id(n)] for n in everything), "はじめに戻すと、始めの見た目になる")



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


def field_zone(world, field):
    """フィールドの「食べ物が届く範囲」（DeliveryZone と同じ GameObject にある、Trigger の BoxCollider）。(4 隅, 中心) か None。"""
    for n in world.nodes:
        if not any(c.cls in world.field_requests and str(c.props.get("fieldId") or "") == field for c in n.comps):
            continue
        for c in n.comps:
            if c.cls == "BoxCollider" and c.props.get("m_IsTrigger"):
                ce, sz = c.props["m_Center"], c.props["m_Size"]
                ce = [float(ce["x"]), float(ce["y"]), float(ce["z"])]
                sz = [float(sz["x"]), float(sz["y"]), float(sz["z"])]
                corners = [local_to_world(n, [ce[0] + dx * sz[0] / 2, ce[1], ce[2] + dz * sz[2] / 2])
                           for dx, dz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
                return corners, local_to_world(n, ce)
    return None


def result_panel(world, textures, y0, width):
    """決定ボタンを押したとき、食感ごとに何が出るかの表（SVG の部品, 高さ）。"""
    cam = world.main_camera()
    pre_like = any(world.area_head(a)[1] and world.area_head(a)[0] is not cam for a in world.areas)
    cols = []  # (見出し, 色, Standing)
    if pre_like:
        # Head がカメラ以外を指している（修正前）。頭は動かないので、その位置の判定を 1 つ出す。
        world.standing = ""
        world.occupied.clear()
        for a in world.areas:
            world.inside[a] = False
        world.move_head([0.0, 1.6, 0.0])
        repro = world.standing
        cols.append(("実機の状態: どこにも立っていない判定", "#a33", ""))
        cols.append(("私の再現: %s に立っている判定" % (repro or "どこにも立っていない"), "#555", repro))
        note = "Head がリグの根元を指すため、体験者が動いても立つ場所は変わらない。台の位置合わせ（AprilTag）で箱が動くと、実機では左の状態になる。"
    else:
        for f in sorted(world.field_tables):
            cols.append((f + " に立つ", FIELD_COLORS.get(f, "#555"), f))
        note = "Head は Main Camera を指す。各フィールドの立つ場所に立つと、そのフィールド専用の食べ物が出る。"
    rows = []
    for tex in textures:
        cells = []
        for _, _, st in cols:
            food = world.resolve(st, tex)
            own = world.find(world.rows(world.field_tables[st]), tex) if st in world.field_tables else None
            if food is None:
                cells.append("何も生まれない（警告）")
            elif st and own is None:
                cells.append("%s（共通の表）" % food)
            elif not st:
                cells.append("%s（共通の表）" % food)
            else:
                cells.append(food)
        rows.append((tex.replace("placeholder-", ""), cells))
    x0, cw = 14.0, (width - 28.0 - 60.0) / max(len(cols), 1)
    out = ['<text x="14" y="%.1f" font-size="15" font-weight="bold" fill="#111">決定ボタンを押したとき、食感ごとに出る食べ物</text>' % (y0 + 6)]
    y = y0 + 34
    out.append('<text x="%.1f" y="%.1f" font-size="12" fill="#444">食感</text>' % (x0, y))
    for i, (head, col, _) in enumerate(cols):
        out.append('<text x="%.1f" y="%.1f" font-size="12" font-weight="bold" fill="%s">%s</text>' % (x0 + 60 + i * cw, y, col, head))
    for tex, cells in rows:
        y += 22
        out.append('<line x1="14" x2="%.1f" y1="%.1f" y2="%.1f" stroke="#ddd"/>' % (width - 14, y - 15, y - 15))
        out.append('<text x="%.1f" y="%.1f" font-size="13" fill="#111">%s</text>' % (x0, y, tex))
        for i, c in enumerate(cells):
            bad = c.startswith("何も")
            out.append('<text x="%.1f" y="%.1f" font-size="13" fill="%s">%s</text>' % (x0 + 60 + i * cw, y, "#a33" if bad else "#111", c))
    y += 26
    for line in note.split("。"):
        if line:
            out.append('<text x="14" y="%.1f" font-size="12" fill="#444">%s。</text>' % (y, line))
            y += 18
    return "".join(out), (y + 6) - (y0 - 16.0) + 16.0


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
    top = 100.0
    H += 20.0

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
        shared = "  ".join("%s=%s(共通)" % (t.replace("placeholder-", ""), world.resolve(field, t)) for t in textures
                           if world.find(rows, t) is None and world.resolve(field, t) is not None)
        zone = field_zone(world, field)
        if zone:
            zpts = " ".join("%.1f,%.1f" % px(q[0], q[2]) for q in zone[0])
            out.append('<polygon points="%s" fill="%s" fill-opacity="0.10" stroke="%s" stroke-width="2" stroke-dasharray="6 4"/>' % (zpts, col, col))
            zu, zv = px(zone[1][0], zone[1][2])
            out.append('<rect x="%.1f" y="%.1f" width="10" height="10" fill="%s" stroke="#fff" stroke-width="1.5"/>' % (zu - 5, zv - 5, col))
            dist = math.hypot(zone[1][0] - center[0], zone[1][2] - center[2])
            out.append('<text x="%.1f" y="%.1f" font-size="11" fill="#222" text-anchor="middle">立つ場所の中心 ⇔ フィールドの中心: %.2f m</text>' % (cu, cv + 70, dist))
        out.append('<text x="%.1f" y="%.1f" font-size="15" font-weight="bold" fill="%s" text-anchor="middle">%s</text>' % (cu, cv - 14, col, field))
        out.append('<text x="%.1f" y="%.1f" font-size="12" fill="#222" text-anchor="middle">%s</text>' % (cu, cv + 22, label))
        out.append('<text x="%.1f" y="%.1f" font-size="12" fill="#222" text-anchor="middle">立つと Standing=%s</text>' % (cu, cv + 38, got or "なし"))
        if shared:
            out.append('<text x="%.1f" y="%.1f" font-size="11" fill="#a33" text-anchor="middle">%s</text>' % (cu, cv + 54, shared))

    out.append('<text x="14" y="%.1f" font-size="12" fill="#a33">どこにも立っていないとき（Standing が空）は、共通の表だけが使われます: %s</text>'
               % (H + top - 10, "  ".join("%s=%s" % (t.replace("placeholder-", ""), world.resolve("", t) or "なし") for t in textures)))
    ou, ov = px(0.0, 0.0)
    out.append('<path d="M%.1f %.1f l-8 0 l16 0 M%.1f %.1f l0 -8 l0 16" stroke="#555" stroke-width="2"/>' % (ou, ov, ou, ov))
    out.append('<text x="%.1f" y="%.1f" font-size="11" fill="#555">ワールド原点（リグの根元）</text>' % (ou + 10, ov - 8))
    panel, extra = result_panel(world, textures, H + top + 16.0, max(W, 760.0))
    out.append(panel)
    W = max(W, 760.0)
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="%.0f" height="%.0f" viewBox="0 0 %.0f %.0f" '
           'font-family="IPAGothic, IPAPGothic, sans-serif"><rect width="100%%" height="100%%" fill="#fafafa"/>'
           '<text x="14" y="26" font-size="18" font-weight="bold" fill="#111">%s</text>'
           '<text x="14" y="48" font-size="12" fill="#444">真上から見た図（1 マス 4 cm）。色の付いた床に頭を置くと、その色のフィールドに「立った」と判定されます。</text><text x="14" y="64" font-size="12" fill="#444">枠は立つ場所の箱、丸い点は箱の中心（赤い点は、中心に立っても期待したフィールドにならなかった所）。</text><text x="14" y="82" font-size="12" fill="#444">破線の四角と■は、フィールド（食べ物が届く範囲）とその中心です。立つ場所は、フィールドの外側にあります。</text>%s</svg>'
           % (W, H + top + extra, W, H + top + extra, title, "".join(out)))
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(svg)
    print("  図を書きました: %s" % out_path)


SUMMARY_ROWS = [
    ("立つと、そのフィールドになる", ("Standing が", "遠くへ離れると", "フィールドが決まる", "Head が")),
    ("専用の食べ物が出る（a〜c）", ("は専用の表の食べ物", "欲しい物に入る", "専用の食べ物の表がある")),
    ("食べ物が飛んで届く準備", ("食べ物が届く範囲", "DeliveryZone）が、すべて", "Prefab が見つかる", "Rigidbody", "Collider がある", "FoodSpawner")),
    ("吹き出しが出る", ("欲しい物を出せる", "欲しい物を出す役", "Helped", "吹き出し")),
    ("物の表示と非表示", ("済むと、見た目が変わる", "全部見える", "全部消える", "はじめに戻す", "消す物の指定")),
    ("フィールドの登録", ("フィールドの ID",)),
]


def commit_label(project_root):
    import subprocess
    try:
        sha = subprocess.run(["git", "-C", project_root, "rev-parse", "--short", "HEAD"], capture_output=True, text=True, timeout=10).stdout.strip()
        return "コミット " + sha if sha else os.path.basename(project_root)
    except Exception:
        return os.path.basename(project_root)


def summary_notes():
    """表の下に書く、注意と通らなかったことの説明。"""
    lines = []
    for f, st, label, detail in RESULTS:
        if st == "NG" and "FieldStages" in label:
            lines.append("× %s: 村のフィールドが 2 つ重なっています（Village と Village (1)）。食べ物が届かないことがあります（当たる順番は未確認）" % f)
        elif st == "NG" and "フィールドの ID" in label:
            lines.append("× %s: フィールドの ID が重なり、先に登録された 1 つだけが使われます" % f)
        elif st == "NG":
            lines.append("× %s: %s" % (f, label))
    helped = sorted({f for f, st, label, _ in RESULTS if st == "WARN" and "Helped" in label})
    if helped:
        lines.append("△ %s: 助かる村人（Helped）が空で、吹き出しは村人の上ではなく、ステップの場所に出ます" % "・".join(helped))
    if any(st == "WARN" and "OpenAllRequestsAfterTutorial" in label for _, st, label, _ in RESULTS):
        lines.append("△ 欲しい物を出す役は、サンドボックス用の仮のスクリプトだけです（チュートリアル後に全フィールドへ出します）")
    overlaps = []
    for _, st, label, _ in RESULTS:
        if st == "WARN" and "箱が重なっています" in label:
            a, b = label.split(" と ")[0].split("/")[-2], label.split(" と ")[1].split(" の箱")[0].split("/")[-2]
            overlaps.append("%s と %s（%s）" % (a, b, label.split("（")[-1].rstrip("）。").split("）")[0]))
    if overlaps:
        lines.append("△ 立つ場所の箱が重なっています: " + "、".join(overlaps))
    return lines


def render_summary(out_path, fields, title, footer):
    """確認の結果を、フィールド × 項目の表にして SVG に書く。"""
    colw, rowh, left, top = 190.0, 76.0, 250.0, 118.0
    W = left + colw * len(fields) + 20
    H = top + rowh * len(SUMMARY_ROWS) + 60 + 20 * len(footer)
    out = ['<rect width="100%" height="100%" fill="#fafafa"/>',
           '<text x="16" y="30" font-size="20" font-weight="bold" fill="#111">%s</text>' % title,
           '<text x="16" y="54" font-size="12" fill="#444">○ 通った　△ 通ったが注意あり　× 通らなかった（シーンと Prefab のファイルから再現した結果です）</text>']
    for j, f in enumerate(fields):
        out.append('<text x="%.1f" y="%.1f" font-size="15" font-weight="bold" fill="%s" text-anchor="middle">%s</text>'
                   % (left + colw * j + colw / 2, top - 14, FIELD_COLORS.get(f, "#333"), f))
    for i, (name, keys) in enumerate(SUMMARY_ROWS):
        y = top + rowh * i
        out.append('<line x1="16" x2="%.1f" y1="%.1f" y2="%.1f" stroke="#ddd"/>' % (W - 16, y, y))
        out.append('<text x="16" y="%.1f" font-size="13" font-weight="bold" fill="#222">%s</text>' % (y + rowh / 2 + 4, name))
        for j, f in enumerate(fields):
            rel = [r for r in RESULTS if r[0] == f and any(k in r[2] for k in keys)]
            if name == "フィールドの登録":
                everyone = [r for r in RESULTS if any(k in r[2] for k in keys)]
                rel = [r for r in everyone if r[0] == f] or [(f, "OK", "登録", "")] * (1 if everyone else 0)
            ng = [r for r in rel if r[1] == "NG"]
            wr = [r for r in rel if r[1] == "WARN"]
            ok = [r for r in rel if r[1] == "OK"]
            if ng:
                mark, col, note = "×", "#c33", "通らない（下の注記）"
            elif wr:
                mark, col, note = "△", "#d90", "注意あり（下の注記）"
            elif ok:
                mark, col, note = "○", "#2a8", "%d 件 通過" % len(ok) if name != "フィールドの登録" else "ID が重ならない"
            else:
                mark, col, note = "－", "#999", "対象なし"
            cx = left + colw * j + colw / 2
            out.append('<text x="%.1f" y="%.1f" font-size="30" font-weight="bold" fill="%s" text-anchor="middle">%s</text>' % (cx, y + 40, col, mark))
            out.append('<text x="%.1f" y="%.1f" font-size="10" fill="#555" text-anchor="middle">%s</text>' % (cx, y + 60, note))
    y = top + rowh * len(SUMMARY_ROWS) + 24
    out.append('<line x1="16" x2="%.1f" y1="%.1f" y2="%.1f" stroke="#ddd"/>' % (W - 16, y - 10, y - 10))
    for k, line in enumerate(footer):
        out.append('<text x="16" y="%.1f" font-size="12" fill="#444">%s</text>' % (y + 12 + 18 * k, line))
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="%.0f" height="%.0f" viewBox="0 0 %.0f %.0f" '
           'font-family="IPAGothic, IPAPGothic, sans-serif">%s</svg>' % (W, H, W, H, "".join(out)))
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(svg)
    print("  まとめの図を書きました: %s" % out_path)



def run(project_root, scene_rel, svg_path=None, summary_path=None, bubble_path=None):
    project = Project(project_root)
    scene = os.path.join(project_root, scene_rel)
    builder = Builder(project)
    roots, _, _ = builder.instantiate(scene)
    world = World(project, roots)

    fails = []
    warns = []

    def check(ok, label, detail=""):
        RESULTS.append((CTX["f"], "NG" if not ok else "OK", label, detail))
        print("  %s %s%s" % ("OK  " if ok else "NG  ", label, (" … " + detail) if (detail and not ok) else ""))
        if not ok:
            fails.append(label + (" … " + detail if detail else ""))

    def warn(label):
        RESULTS.append((CTX["f"], "WARN", label, ""))
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
        CTX["f"] = field
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
        CTX["f"] = field
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

    check_events(world, project, builder, textures, check, warn)

    # --- どこにも立っていないとき
    print("\n== どこにも立っていないとき（Standing が空）")
    world.standing = ""
    only = []
    for tex in textures:
        food = world.resolve("", tex)
        print("    %-14s → %s" % (tex, food))
        if food is not None:
            only.append(tex)
    print("  反応するのは: %s だけ（共通の表にある食感）" % (", ".join(only) or "なし"))

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

    if bubble_path:
        render_bubble_svg(world, project, bubble_path, "吹き出しが出る場所（%s）" % commit_label(project_root))

    if summary_path:
        footer = summary_notes()
        footer.append("この確認で見ていないこと: 食べ物が飛んで当たる物理 / 吹き出しの描画 / 実機（HMD・デバイス・AprilTag）での動き。")
        footer.append("これは「データと判定コードの上で、そうなる」ことの確認です。動作の保証ではありません。Unity の Play で別に確かめてください。")
        render_summary(summary_path, sorted(world.field_tables), "Main.unity の確認結果（%s）" % commit_label(project_root), footer)

    print("\n== 結果: %s（注意 %d 件）" % ("すべて通りました" if not fails else "%d 件が通りませんでした" % len(fails), len(warns)))
    for f in fails:
        print("  - %s" % f)
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--project", default=".", help="Unity プロジェクトの場所（Assets の親）")
    ap.add_argument("--scene", default="Assets/Scenes/Main.unity")
    ap.add_argument("--svg", help="真上から見た図を、この SVG に書く")
    ap.add_argument("--bubble-svg", help="吹き出しが出る場所を、真上から見た図と一覧にして、この SVG に書く")
    ap.add_argument("--summary-svg", help="確認の結果を、フィールド × 項目の表にして、この SVG に書く")
    args = ap.parse_args()
    sys.setrecursionlimit(10000)
    sys.exit(run(os.path.abspath(args.project), args.scene, args.svg, args.summary_svg, args.bubble_svg))


if __name__ == "__main__":
    main()
