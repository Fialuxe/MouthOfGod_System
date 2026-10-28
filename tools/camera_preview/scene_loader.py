"""Unity の .unity / .prefab（YAML）から、Transform の世界座標を読む。入れ子の Prefab も、Prefab Instance の上書き（位置・回転・拡大・名前）を当てながらたどる。
MeshRenderer の見た目の大きさ（bounds）は読まない。名前と位置だけ。
"""
import os, re, glob
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
_guid_path = {}
for m in glob.glob(os.path.join(ROOT, "Assets", "**", "*.meta"), recursive=True):
    if not (m.endswith(".prefab.meta") or m.endswith(".unity.meta")): continue
    with open(m, encoding="utf-8", errors="ignore") as f:
        g = re.search(r"guid: (\w+)", f.read())
    if g: _guid_path[g.group(1)] = m[:-5]

def split_docs(text):
    parts = re.split(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n", text, flags=re.M)
    return [(parts[i], parts[i + 1], bool(parts[i + 2]), parts[i + 3]) for i in range(1, len(parts), 4)]

def vec(body, key):
    m = re.search(key + r": \{x: ([^,]+), y: ([^,]+), z: ([^,}]+)(?:, w: ([^}]+))?\}", body)
    return [float(x) for x in m.groups() if x is not None] if m else None

def quat_mat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                     [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def trs(pos, rot, scl):
    m = np.eye(4); m[:3, :3] = quat_mat(rot) @ np.diag(scl); m[:3, 3] = pos; return m

class Node:
    def __init__(s, name, local, parent): s.name, s.local, s.parent, s.children, s.world = name, local, parent, [], None

def load(path, overrides=None, prefix=""):
    """path のファイルを読み、{fileID(文字列): Node} を返す。overrides は {(fileID, プロパティ): 値}。"""
    overrides = overrides or {}
    with open(path, encoding="utf-8") as f: docs = split_docs(f.read())
    go_name, tf = {}, {}
    for cls, fid, stripped, body in docs:
        if cls == "1" and not stripped: go_name[fid] = re.search(r"m_Name: (.*)", body).group(1).strip("'\"")
    nodes, alias = {}, {}
    for cls, fid, stripped, body in docs:
        if cls in ("4", "224") and stripped:
            c = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+)", body).group(1)
            i = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)", body).group(1)
            alias[fid] = f"{i}/{c}"
    for cls, fid, stripped, body in docs:
        if cls not in ("4", "224") or stripped: continue
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)", body)
        name = go_name.get(go.group(1), "?") if go else "?"
        pos = vec(body, "m_LocalPosition") or [0, 0, 0]; rot = vec(body, "m_LocalRotation") or [0, 0, 0, 1]; scl = vec(body, "m_LocalScale") or [1, 1, 1]
        for k, d in (("m_LocalPosition", pos), ("m_LocalRotation", rot), ("m_LocalScale", scl)):
            for i, ax in enumerate("xyzw"[:len(d)]):
                key = (fid, f"{k}.{ax}")
                if key in overrides: d[i] = overrides[key]
        father = re.search(r"m_Father: \{fileID: (-?\d+)", body).group(1)
        father = alias.get(father, father)
        nodes[fid] = Node(overrides.get((fid, "m_Name"), name), (pos, rot, scl), father)
    # Prefab Instance を展開
    for cls, fid, stripped, body in docs:
        if cls != "1001": continue
        src = re.search(r"m_SourcePrefab: \{fileID: -?\d+, guid: (\w+)", body).group(1)
        parent = re.search(r"m_TransformParent: \{fileID: (-?\d+)", body).group(1)
        parent = alias.get(parent, parent)
        ov = {}
        for m in re.finditer(r"- target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n\s+propertyPath: (\S+)\n\s+value: (.*)\n", body):
            tgt, prop, val = m.groups()
            if prop.startswith(("m_LocalPosition", "m_LocalRotation", "m_LocalScale")):
                try: ov[(tgt, prop)] = float(val)
                except ValueError: pass
            elif prop == "m_Name": ov[(tgt, prop)] = val.strip("'\"")
        if src not in _guid_path: continue
        sub = load(_guid_path[src], ov)
        roots = [n for n in sub.values() if n.parent not in sub]
        for n in roots: n.parent = parent  # 親は、この文書側の Transform
        for k, n in sub.items(): nodes[f"{fid}/{k}"] = n
        for n in sub.values():
            if n.parent in sub: n.parent = f"{fid}/{n.parent}"
    for k, n in list(nodes.items()):
        if n.parent in nodes: pass
    # 子の parent を、この文書のキーに直す（入れ子の分は上で済み）
    return nodes

def world(nodes):
    cache = {}
    def w(k):
        if k in cache: return cache[k]
        n = nodes[k]; local = trs(*n.local)
        cache[k] = (w(n.parent) @ local) if n.parent in nodes else local
        return cache[k]
    for k, n in nodes.items(): n.world = w(k)
    return nodes

def path_of(nodes, k):
    names = []
    while k in nodes: names.append(nodes[k].name); k = nodes[k].parent
    return "/".join(reversed(names))
