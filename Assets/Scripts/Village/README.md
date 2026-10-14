# Village

ミニチュアの村を、Play 時にコードで組み立てる。いまは「村」のフィールドだけ（草原・海・山は別に作る。定義は [docs/fields.md](../../../docs/fields.md)）。

> 状態: Unity では未確認。配置の数字・倍率は仮。

## シーンとのつながり
`Assets/Scenes/Main.unity` の `MinitureBase > Content > Village` は、立方体の地面（スケール 0.54, 0.2, 0.54。親の倍率をかけた見かけの大きさも縦に潰れている）。
`VillageBuilder` はこの `Village` に付け、置き物をその**子**として、立方体の上面（ローカルの y = 0.5）に並べる。
- 位置: 地面の一辺を `VillageField.Size`（10 m）として、`X / Size` `Z / Size` に置く。
- 大きさ: 親の潰れた倍率を打ち消して、ゆがまないようにする。
- 地面の見た目はシーンのまま（コードでは変えない）。
- シーンは `scene/` ブランチでしか編集しない。このブランチは `Village` に `VillageBuilder` を付けない。`VillageBuilder.GetOrCreate()` が、シーンの `Village` を見つけて付ける（Bootstrap #34 から呼ぶ想定）。`Village` が無いサンドボックスでは、立方体を作ってそこに付ける。

## 使い方
- Play 時に `VillageBuilder.GetOrCreate()` を呼ぶと、村ができる。
- `SetDelivered("village", true)` で「届いた後の状態」、`false` で「足りない状態」にする。始まりは足りない状態。
  - 足りない状態: 畑の作物がない。村人は `Idle`。
  - 届いた後: 作物（`Carrot` `Turnip` `Pumpkin`）が現れる。村人が `Victory` を 1 回やって `Idle` に戻る。

## アニメーション（村人）
段階を踏んで決めた。
1. **あるか**: `Casual_Male.fbx`（Ultimate Animated Character Pack）に、17 種のクリップがある（`CharacterArmature|Idle` `Walk` `Walk_Carry` `PickUp` `Victory` `Defeat` など）。確認は FBX の中の名前を読んだ。建物（Farm Buildings）と食べ物（Ultimate Food Pack）は、動きのない既定のクリップだけ。
2. **場面に合うか**: 飢饉の村で、村人は食べ物を待つ。届いたら喜ぶ。
   - 使う: `Idle`（待つ。ループ）、`Victory`（喜ぶ。1 回）。
   - 今は使わない: `Walk` `Run`（どこかへ行ってしまう）、`Defeat` `Death`（暗すぎる）、`Punch` `SwordSlash` `Shoot_OneHanded` `Roll` `RecieveHit`（戦闘）、`Jump` `SitDown` `StandUp`（意味がない）。
   - 後で使える: `Walk_Carry`（物を運ぶ）、`PickUp`（収穫）。
3. **実装**: 生成メニューが、村人の Prefab に `Villager.controller`（`Idle` → `Delivered` トリガーで `Victory` → 終わったら `Idle`）を付ける。`Idle` はループにする（FBX の取り込み設定を変える）。`VillageBuilder.SetDelivered` が `Delivered` トリガーを引く。
- **未確認**: 実際の動きの見た目（`Victory` が喜びに見えるか、長さは適切か）は Unity で見ていない。

## Prefab を作る（最初の 1 回）
素材（FBX）をそのまま `Resources` に置けないので、Unity のメニュー **MouthOfGod > Village > Generate Prefabs** で、
`Assets/Resources/Prefabs/Village/` に Prefab と `Villager.controller` を作る。できた Prefab をコミットすれば、以後は Play するだけで動く。
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
- フィールドの一辺 10 m とした配置の座標。ミニチュアへの縮め方は、シーンの `Village` の大きさから決まる。
- 村人は 1 人（`Casual_Male`）。アニメーションはまだ付けていない。
- `House_Sample` の Prefab は、位置が大きくずれたまま保存されている（Transform が (-7, -5.8, 7.8)）。置き方がおかしければ Prefab 側を直す。
