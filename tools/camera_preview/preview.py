"""CameraWork の軌道計算を Python に移して、ワイヤーフレームで動きを確かめる仮のパイプライン。
使い方: python3 preview.py [出力先.mp4]
Unity とは左手系・Y 上で合わせてある。計算式は CameraPathMath.cs と同じ。
"""
import math, subprocess, sys, os
import numpy as np
from PIL import Image, ImageDraw

W, H, FPS, FOV = 960, 540, 30, 60.0
NEAR, FAR = 0.05, 14.0  # 手前の切り取りと、奥で色を落とす距離（小さい模型の場面では preview の外から変える）

# ---------- CameraPathMath ----------
def ease(kind, t, k=2.0):
    t = min(max(t, 0.0), 1.0); k = max(k, 1.0)
    if kind == "in": return t ** k
    if kind == "out": return 1 - (1 - t) ** k
    if kind == "inout": return 0.5 * (2 * t) ** k if t < 0.5 else 1 - 0.5 * (2 * (1 - t)) ** k
    return t

def bezier(pts, t):
    w = [np.array(p, float) for p in pts]
    for n in range(len(w) - 1, 0, -1):
        for i in range(n): w[i] = w[i] + (w[i + 1] - w[i]) * t
    return w[0]

def orbit(start, center, axis, angle, radius_scale, height, t):
    ax = np.array(axis, float); ax /= np.linalg.norm(ax)
    v = np.array(start, float) - center
    a = math.radians(angle * t)
    # ロドリゲスの回転（Unity の AngleAxis は左手系なので符号を反転）
    a = -a
    v = v * math.cos(a) + np.cross(ax, v) * math.sin(a) + ax * np.dot(ax, v) * (1 - math.cos(a))
    return center + v * (1 + (radius_scale - 1) * t) + ax * height * t

# ---------- ShotPath ----------
class Shot:
    def __init__(s, kind, seconds=2.0, speed=None, ease_kind="inout", strength=2.0, aim="path", target=None, **kw):
        s.kind, s.seconds, s.speed, s.ease, s.strength, s.aim, s.target, s.kw = kind, seconds, speed, ease_kind, strength, aim, target, kw

class Path:
    def __init__(s, shot, start):
        s.shot, s.start, kw = shot, np.array(start, float), shot.kw
        if shot.kind == "line": s.end = np.array(kw["end"], float)
        elif shot.kind == "curve":
            s.pts = [s.start] + [np.array(p, float) for p in kw["control"]] + [np.array(kw["end"], float)]
        elif shot.kind == "orbit": s.center = np.array(kw["center"], float)
        s.length = 0.0 if shot.kind == "hold" else sum(np.linalg.norm(s.pos((i + 1) / 48) - s.pos(i / 48)) for i in range(48))
        s.duration = max(s.length / shot.speed if shot.speed else shot.seconds, 0.001)
    def pos(s, u):
        k, kw = s.shot.kind, s.shot.kw
        if k == "line": return s.start + (s.end - s.start) * u
        if k == "curve": return bezier(s.pts, u)
        if k == "orbit": return orbit(s.start, s.center, kw.get("axis", (0, 1, 0)), kw["angle"], kw.get("radius_scale", 1), kw.get("height", 0), u)
        return s.start
    def tangent(s, u, fb):
        d = s.pos(min(u + .01, 1)) - s.pos(max(u - .01, 0))
        n = np.linalg.norm(d); return d / n if n > 1e-5 else fb

def look(fwd):
    f = fwd / np.linalg.norm(fwd)
    r = np.cross((0, 1, 0), f); r /= np.linalg.norm(r)
    return np.stack([r, np.cross(f, r), f])  # 行 = カメラの右・上・前

def slerp_dir(a, b, k):
    d = a * (1 - k) + b * k
    return d / np.linalg.norm(d)

def simulate(shots, start, fwd0):
    """全カットを FPS で刻んで、(位置, 前向き, カット番号) の列を返す。"""
    pos, fwd, frames = np.array(start, float), np.array(fwd0, float), []
    for idx, shot in enumerate(shots):
        path = Path(shot, pos); start_fwd = fwd.copy(); n = max(int(round(path.duration * FPS)), 1)
        for i in range(1, n + 1):
            t = i / n; u = ease(shot.ease, t, shot.strength)
            p = path.pos(u)
            if shot.aim == "path": want = path.tangent(u, fwd)
            elif shot.aim == "target":
                aim_at = np.array(shot.target, float)
                if "aim_to" in shot.kw: aim_at = aim_at + (np.array(shot.kw["aim_to"], float) - aim_at) * u  # 見る点も動かす
                want = aim_at - p
            else: want = start_fwd
            fwd = slerp_dir(start_fwd, want / np.linalg.norm(want), min(1, i / (0.3 * FPS)))
            frames.append((p, fwd.copy(), idx))
        pos = path.pos(1.0)
    return frames

# ---------- 仮のシーン（ワイヤー） ----------
def box(c, s):
    x, y, z = c; a, b, d = s
    v = [(x + i * a, y + j * b, z + k * d) for i in (-1, 1) for j in (0, 1) for k in (-1, 1)]
    e = [(0,1),(2,3),(4,5),(6,7),(0,2),(1,3),(4,6),(5,7),(0,4),(1,5),(2,6),(3,7)]
    return [(np.array(v[i]), np.array(v[j])) for i, j in e]

def build_scene():
    seg, col = [], []
    def add(ss, c): seg.extend(ss); col.extend([c] * len(ss))
    g = [(np.array((x, 0, -12.)), np.array((x, 0, 12.))) for x in range(-12, 13, 2)]
    g += [(np.array((-12., 0, z)), np.array((12., 0, z))) for z in range(-12, 13, 2)]
    add(g, (60, 80, 70))
    rng = np.random.default_rng(3)
    for i in range(7):  # 村の家
        a = i / 7 * 2 * math.pi
        add(box((3.5 * math.cos(a), 0, 3.5 * math.sin(a)), (.5, .7 + .2 * (i % 3), .5)), (230, 200, 120))
    for _ in range(26):  # 木
        a, r = rng.uniform(0, 2 * math.pi), rng.uniform(6, 11)
        add(box((r * math.cos(a), 0, r * math.sin(a)), (.12, 1.1, .12)), (90, 170, 90))
        add(box((r * math.cos(a), 1.1, r * math.sin(a)), (.5, .6, .5)), (60, 140, 70))
    add(box((0, 0, 0), (.3, 1.6, .3)), (240, 120, 120))  # 中央の目印（巫女）
    return seg, col

def project(frame, seg, col):
    pos, fwd, _ = frame; R = look(fwd); f = 1 / math.tan(math.radians(FOV / 2)); asp = W / H
    out, near = [], NEAR
    for (a, b), c in zip(seg, col):
        ca, cb = R @ (a - pos), R @ (b - pos)
        if ca[2] < near and cb[2] < near: continue
        if ca[2] < near: ca = ca + (cb - ca) * ((near - ca[2]) / (cb[2] - ca[2]))
        if cb[2] < near: cb = cb + (ca - cb) * ((near - cb[2]) / (ca[2] - cb[2]))
        pts = [((p[0] * f / asp / p[2] + 1) * W / 2, (1 - p[1] * f / p[2]) * H / 2) for p in (ca, cb)]
        d = min(ca[2], cb[2]); k = max(.3, min(1, 1 - d / FAR))
        out.append((pts, tuple(int(x * k) for x in c)))
    return out

def render(frame, seg, col, label):
    img = Image.new("RGB", (W, H), (14, 18, 24)); d = ImageDraw.Draw(img)
    for pts, c in project(frame, seg, col): d.line(pts[0] + pts[1], fill=c, width=1)
    d.text((12, 10), label, fill=(220, 220, 220))
    return img

def topdown(frames, shots, seg):
    s = 18; img = Image.new("RGB", (480, 480), (14, 18, 24)); d = ImageDraw.Draw(img)
    T = lambda p: (240 + p[0] * s, 240 - p[2] * s)
    for a, b in seg: d.line(T(a) + T(b), fill=(50, 60, 60))
    palette = [(255,120,120),(120,220,255),(255,220,100),(160,255,150),(220,150,255)]
    for p, _, i in frames: d.ellipse([T(p)[0]-1.5, T(p)[1]-1.5, T(p)[0]+1.5, T(p)[1]+1.5], fill=palette[i % 5])
    return img

# ---------- 撮るカット ----------
SHOTS = [
    Shot("line", speed=2.2, ease_kind="out", strength=2.5, aim="target", target=(0, 1.0, 0), end=(-9, 2.2, -9)),          # 村へ寄る押し
    Shot("orbit", seconds=4.0, ease_kind="inout", aim="target", target=(0, 0.9, 0), center=(0, 2.2, 0), angle=200, radius_scale=0.55, height=-0.8),  # 巫女のまわりを回り込み
    Shot("curve", speed=2.5, ease_kind="inout", aim="path", control=[(4, 6, 6)], end=(7, 1.4, 5)),                          # 弧を描いて引く
    Shot("hold", seconds=0.8, aim="target", target=(0, 1.0, 0)),
    Shot("line", seconds=2.0, ease_kind="in", strength=3, aim="target", target=(0, 1.2, 0), end=(1.8, 1.4, 1.2)),            # 最後にぐっと寄る
]

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "camera_preview.mp4"
    seg, col = build_scene()
    frames = simulate(SHOTS, (-14, 4.0, -14), (1, -.2, 1))
    names = [f"{i+1}: {s.kind}" for i, s in enumerate(SHOTS)]
    proc = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                             "-r", str(FPS), "-i", "-", "-pix_fmt", "yuv420p", out], stdin=subprocess.PIPE)
    for n, fr in enumerate(frames):
        proc.stdin.write(render(fr, seg, col, f"shot {names[fr[2]]}   {n/FPS:4.1f}s").tobytes())
    proc.stdin.close(); proc.wait()
    topdown(frames, SHOTS, seg).save(os.path.splitext(out)[0] + "_topdown.png")
    # 確認用の静止画（各カットの中ほど）
    for i in range(len(SHOTS)):
        idx = [n for n, f in enumerate(frames) if f[2] == i]
        render(frames[idx[len(idx)//2]], seg, col, names[i]).save(os.path.splitext(out)[0] + f"_shot{i+1}.png")
    print(len(frames), "frames,", len(frames) / FPS, "s ->", out)
