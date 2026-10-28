"""Main シーンの実際の位置から、草原 → 村へ引いていく「村の一連」のカットを確かめる。
使い方: python3 village_tour.py [出力先.mp4]
位置は Assets/Scenes/Main.unity と、入れ子の Prefab から読む（scene_loader.py）。山の円錐など、Mesh の大きさが要る所は目安。
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
import preview as P
import scene_loader as S

preview_dir = os.path.dirname(os.path.abspath(__file__))
nodes = S.world(S.load(os.path.join(preview_dir, "..", "..", "Assets", "Scenes", "Main.unity")))

def find(path_end, first=True):
    hits = [n for k, n in nodes.items() if S.path_of(nodes, k).endswith(path_end)]
    return [h.world[:3, 3] for h in hits]

village = np.mean(find("Content/Village/Village/Plaza"), axis=0)
mount = find("MountVer2/mount")[0]
grass = find("Content/Grass/Tree1")[0]  # 草原の見どころ（木）
camera0 = find("Main Camera")[0]
print("village", village.round(2), "grass", grass.round(2), "player camera", camera0.round(2))

# ---- ワイヤーのシーン（模型）----
seg, col = [], []
def add(ss, c): seg.extend(ss); col.extend([c] * len(ss))
add(P.box((0, 0.055, 0), (0.6, 0.055, 0.6)), (110, 110, 130))            # 土台（Border の内側）
grid = [(np.array((x, 0.11, -.6)), np.array((x, 0.11, .6))) for x in np.linspace(-.6, .6, 13)]
grid += [(np.array((-.6, 0.11, z)), np.array((.6, 0.11, z))) for z in np.linspace(-.6, .6, 13)]
add(grid, (45, 60, 55))
def pillar(c, r, h, col_, n=10):  # 山の目安（円錐）
    ring = lambda y, rr: [np.array((c[0] + rr * np.cos(a), y, c[2] + rr * np.sin(a))) for a in np.linspace(0, 2 * np.pi, n, endpoint=False)]
    base, rim = ring(c[1], r), ring(c[1] + h * .55, r * .45)
    tip = np.array((c[0], c[1] + h, c[2]))
    ss = [(base[i], base[(i + 1) % n]) for i in range(n)] + [(base[i], rim[i]) for i in range(n)] + [(rim[i], tip) for i in range(n)]
    add(ss, col_)
pillar(mount - np.array((0, 0.02, 0)), 0.28, 0.32, (170, 150, 140))
for name, size, c in (("House_Ruined", (.035, .05, .035), (230, 200, 120)), ("Tree_Bare", (.01, .06, .01), (120, 170, 110)),
                      ("Crop_Withered", (.005, .015, .005), (150, 200, 90)), ("Plaza", (.06, .004, .06), (200, 120, 120)),
                      ("Farmland", (.07, .004, .05), (160, 120, 70))):
    for p in find("Content/Village/Village/" + name): add(P.box(tuple(p), size), c)
for p in find("Content/Grass/Tree1") + find("Content/Grass/Bush1"): add(P.box(tuple(p), (.02, .04, .02)), (80, 170, 90))
sea = find("Content/Sea_work/Terrain_sea")[0]; add(P.box((sea[0] + .27, .12, sea[2] + .27), (.28, .004, .28)), (80, 130, 220))
add(P.box((-1.17, 1.21, .26), (.05, .1, .05)), (240, 120, 120))  # 巫女の待機位置

# ---- 撮るカット: 草原 → 引きながら回り込む → 村を見渡す ----
summit = grass + np.array((0, 0.05, 0))  # 草原の見る点
vil = village + np.array((0, 0.03, 0))
P.NEAR, P.FAR = 0.01, 2.8
SHOTS = [
    # 草原に寄ったところから始める。視線は木
    P.Shot("hold", seconds=0.8, aim="target", target=summit),
    # 木を見ながら、上へ・外へ引き、見る点を草原から村へ移す
    P.Shot("curve", seconds=5.0, ease_kind="inout", strength=2.0, aim="target", target=summit, aim_to=vil,
           control=[grass + np.array((0.35, 0.42, -0.55))], end=village + np.array((0.05, 0.30, -0.62))),
    # 村へ向かってゆっくり押しながら、村の一連（家・畑・広場）を見渡す
    P.Shot("orbit", seconds=4.5, ease_kind="inout", aim="target", target=vil, center=vil, angle=-110, radius_scale=0.8, height=-0.08),
    P.Shot("hold", seconds=0.8, aim="target", target=vil),
]
start = grass + np.array((0.20, 0.12, 0.17))
frames = P.simulate(SHOTS, start, summit - start)

if __name__ == "__main__":
    import subprocess
    out = sys.argv[1] if len(sys.argv) > 1 else "village_tour.mp4"
    labels = ["1 hold(grassland)", "2 curve(pull back, grassland->village)", "3 orbit(village)", "4 hold(village)"]
    proc = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{P.W}x{P.H}",
                             "-r", str(P.FPS), "-i", "-", "-pix_fmt", "yuv420p", out], stdin=subprocess.PIPE)
    for n, fr in enumerate(frames):
        proc.stdin.write(P.render(fr, seg, col, f"shot {labels[fr[2]]}  {n/P.FPS:4.1f}s").tobytes())
    proc.stdin.close(); proc.wait()
    # 上から見た軌道
    s = 380; img = Image.new("RGB", (560, 560), (14, 18, 24)); d = ImageDraw.Draw(img)
    T = lambda p: (280 + p[0] * s * 0 + (p[0] + 0.3) * s, 280 - p[2] * s)
    for a, b in seg: d.line(T(a) + T(b), fill=(60, 75, 70))
    pal = [(255, 120, 120), (120, 220, 255), (255, 220, 100), (160, 255, 150)]
    for p, _, i in frames: d.ellipse([T(p)[0] - 2, T(p)[1] - 2, T(p)[0] + 2, T(p)[1] + 2], fill=pal[i])
    d.text((10, 10), "top view (red=1, blue=2, yellow=3, green=4)", fill=(220, 220, 220))
    img.save(os.path.splitext(out)[0] + "_topdown.png")
    for i in range(len(SHOTS)):
        idx = [n for n, f in enumerate(frames) if f[2] == i]
        P.render(frames[idx[len(idx) // 2]], seg, col, labels[i]).save(os.path.splitext(out)[0] + f"_shot{i+1}.png")
    print(len(frames), "frames", len(frames) / P.FPS, "s")
