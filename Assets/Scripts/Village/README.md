# Village

ミニチュアの村を、Play 時にコードで組み立てる。いまは「村」のフィールドだけ（草原・海・山は別に作る。定義は [docs/fields.md](../../../docs/fields.md)）。

> 状態: Unity では未確認。配置の数字・倍率は仮。

## 使い方
- Play 時に `VillageBuilder.GetOrCreate()` を呼ぶと、村ができる（Bootstrap #34 から呼ぶ想定。自分のサンドボックスシーンで試すときは、空の GameObject に `VillageBuilder` を付けるか、コードから呼ぶ）。共有シーン `Main` には直接置かない。
- `SetDelivered("village", true)` で「届いた後の状態」、`false` で「足りない状態」にする。始まりは足りない状態。
  - 足りない状態: 地面が茶色く、畑の作物がない。
  - 届いた後: 地面が緑になり、作物（`Carrot` `Turnip` `Pumpkin`）が現れる。

## Prefab を作る（最初の 1 回）
素材（FBX）をそのまま `Resources` に置けないので、Unity のメニュー **MouthOfGod > Village > Generate Prefabs** で、
`Assets/Resources/Prefabs/Village/` に Prefab を作る。できた Prefab をコミットすれば、以後は Play するだけで動く。
Prefab が無い物は、仮の箱で置く（警告が出る）。この手順は開発中の補助で、Prefab をコミットしたら不要。

## 置き物（素材の探し方は [docs/quaternius-assets.md](../../../docs/quaternius-assets.md)）
| 置き物 | 素材 | 出る状態 |
|---|---|---|
| `House_Sample` | 既存の Prefab（Medieval Village MegaKit の組み立て例） | 常に |
| `Well` `Windmill` `Barn` `Fence` | Farm Buildings | 常に |
| `Casual_Male`（困っている人: 村人） | Ultimate Animated Character Pack | 常に |
| `Carrot` `Turnip` `Pumpkin`（畑の作物） | Ultimate Food Pack | 届いた後 |

## 配置を変えるには
`VillageLayout.cs` の数字を書き換える（コードに置き物の名前と位置が並んでいる）。配置図（#79）が決まったらここを直す。
食べ物の Prefab は小さいので、`Scale` で大きくしている。

## 仮に決めたこと
- ミニチュアへの縮め方 0.05（`VillageBuilder` の Inspector）、フィールドの地面の大きさ 10 m、配置の座標。
- 村人は 1 人（`Casual_Male`）。アニメーションはまだ付けていない。
- `House_Sample` の Prefab は、位置が大きくずれたまま保存されている（Transform が (-7, -5.8, 7.8)）。置き方がおかしければ Prefab 側を直す。
