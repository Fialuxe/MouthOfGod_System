# Village

ミニチュアの村のフィールドと、フィールドが段階的に戻る仕組み。いまは「村」のフィールドだけ（草原・海・山は別に作る。定義は [docs/fields.md](../../../docs/fields.md)）。

> 状態: Unity では一部だけ確認（配置・段階の切り替え・悩む印）。部品に分けた後の形（Village.prefab）は未確認。配置の数字・倍率は仮。

## 部品（4 つのフィールドで共通に使う）
段階の仕組みは村専用ではない。山・海・草原の担当も、自分のフィールドに同じ部品を付ければ使える（コードは要らない）。

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FieldStages` | フィールドの一番上 | いまの段階（0 = 飢饉 〜 `maxStage`）を持つ。`Advance()`（1 段階進める）・`SetStage(n)`。変わると子の部品に知らせ、C# の `StageChanged` と、Inspector でつなぐ `onStageChanged`（UnityEvent。音・光など）を出す。**Play 中に Inspector の Stage を動かすと、その段階に切り替わる**（確認用） |
| `StageVisibility` | 置き物 1 つ 1 つ（手で置いた物にも） | 何段目から何段目まで見えるか（`untilStage` が -1 なら最後まで） |
| `VillagerMood` | 村人（村人の Prefab に入っている） | 何段目で元気になるか。悩んでいる間は頭の上の印を出し、ゆっくり立つ ⇄ ときどきうなだれる、を人ごとにばらばらの間隔でくり返す。段階が上がると、元気な人が喜ぶ |
| `SurfaceAnchor` | フィールドの Prefab の一番上 | シーンの台（縦が潰れた立方体）の上面に乗せ、親の倍率を打ち消す。Prefab の中は 1 = 1 m のまま。編集中のシーンでも効く |

段階のふるまいの決まり（`Stages`）は Unity に依存しないので、テストしている。

### ほかの機能とのつなぎ方
- 「届いたら 1 段階進める」は、Core の `Delivered`（#30 / PR #227）を受ける所が `FieldStages.Advance()` を呼ぶ。Bootstrap（#34）がつなぐ。Game や Tutorial は Village を直接呼ばない（[docs/architecture.md](../../../docs/architecture.md)）。
- 何回届けたら 1 段階進むか、どのフィールドが戻るかは、本編（#16 など）で決める。
- 段階が変わったときの音・光は、`FieldStages` の `onStageChanged` に Inspector からつなぐ。

## シーンとのつながり
`Assets/Scenes/Main.unity` の `MinitureBase > Content > Village` は、立方体の地面（スケール 0.54, 0.2, 0.54。親の倍率をかけた見かけの大きさも縦に潰れている）。
- `Village.prefab` を、この `Village`（地面）の**子**に置く。`SurfaceAnchor` が上面に乗せ、ゆがみを打ち消す。
- 位置と大きさは、**実物のミニチュアの寸法（メートル）**。台は 1.08 m 四方、村はその中の 0.54 m 四方。村の中は、中心から ±0.27 m。
- 地面の見た目はシーンのまま。
- 共有シーン（`Main`）は `scene/` ブランチでしか編集しない。確かめるのはサンドボックスで。

## 使い方
1. Unity のメニュー **MouthOfGod > Village > Generate Prefabs** で、置き物・村人の Prefab を作る（下の「Prefab を作る」）。
2. **MouthOfGod > Village > Bake Draft Village Prefab** で、下書きの配置から `Assets/Resources/Prefabs/Village/Village.prefab` を作る。
3. サンドボックスのシーンで、`Village`（地面）の子に `Village.prefab` を置く。
4. Play して、`Village.prefab` の `FieldStages` の **Stage** を 0〜4 で動かして確かめる。
5. 配置を直すときは、**`Village.prefab` を開いて手で動かす**。段階で出入りさせたい物には `StageVisibility` を付ける。

## 下書き（`Editor/Draft/`）
`Editor/Draft/VillageLayout.cs` は、村の配置をコードで書いた最初の下書き。`Editor/Draft/VillageBaker.cs` が、ここから `Village.prefab` を作る（Bake）。
- Bake は最初の 1 回の補助。Bake の後は Prefab が正。下書きを Prefab と同じに保つ必要はない。
- もう一度 Bake すると、Prefab を手で直した分は消える（上書きの前に確かめるダイアログが出る）。
- Editor の中なので、展示のビルドには入らない。

## 段階
| 段階 | 見た目 | 元気になる村人 |
|---|---|---|
| 0 飢饉 | 畑に枯れた茎、木は茶色、家畜はいない。村人は全員うなだれ、頭の上に暗い縦線 | — |
| 1 | 畑に芽が出る | 畑の 2 人（`Worker_Male` `Worker_Female`） |
| 2 | 作物（にんじん・かぶ・かぼちゃ）が実り、木が緑に戻る | 井戸の人（`Casual_Female`） |
| 3 | 羊が戻る | 羊の番の長老（`OldClassy_Male`） |
| 4 | 豚も戻る | 戸口の人（`Casual_Male`）。全員が元気 |

段階が上がるたびに、元気な村人が `Victory` を 1 回やって喜ぶ。何がいつ戻るかは、`Village.prefab` の中の `StageVisibility` と `VillagerMood` で決める（下書きでは `FromStage` `UntilStage` `CalmFromStage`）。

## アニメーション（村人）
段階を踏んで決めた。
1. **あるか**: `Casual_Male.fbx`（Ultimate Animated Character Pack）に、17 種のクリップがある（`CharacterArmature|Idle` `Walk` `Walk_Carry` `PickUp` `Victory` `Defeat` など）。確認は FBX の中の名前を読んだ。「頭を抱える」動きは無い。
2. **場面に合うか**: 飢饉の村で、村人は困っている。届いて困りごとが解けたら元気になり、届くたびに喜ぶ。
   - 使う: `Idle`（元気。ループ。悩んでいる間は 0.6 倍でゆっくり）、`Defeat`（ときどきうなだれる。最後の姿勢で止まる）、`Victory`（喜ぶ。1 回）。
   - 悩んでいる間ずっとうなだれたままだと、時間が止まったように見えた。なので、ゆっくり立つ ⇄ うなだれる、を人ごとにばらばらの間隔でくり返す。
   - 使わない: `Walk` `Run`（どこかへ行ってしまう）、`Death`（暗すぎる）、`Punch` `SwordSlash` `Shoot_OneHanded` `Roll` `RecieveHit`（戦闘）、`PickUp`（拾う物が無いと不自然）、`Jump` `SitDown` `StandUp`（意味がない）。
   - 後で使える: `Walk_Carry`（物を運ぶ）。頭を抱える動きが要るなら、Mixamo などの外の動きを試す（村人の取り込みを Humanoid にする必要がある）。
3. **実装**: 生成メニューが `Villager.controller` を作り、村人の Prefab に付ける。
   - 状態: `Idle` ⇄ `Worried`（`Worried` の bool）、`Worried` ⇄ `Slump`（`Slump` の bool。`Defeat`）、`Idle` → `Victory`（`Delivered` のトリガー）→ `Idle`。
   - 村人の Prefab には、`VillagerMood` と、頭の骨の子に悩んでいる印 `WorryMark`（暗い縦線 3 本）が入っている。
   - `VillagerMood` が `Worried` `Slump` と印を切り替え、段階が上がったら `Delivered` を引く。
- **未確認**: 実際の動きの見た目（`Defeat` が最後にうなだれたままで止まるか、立ち上がってしまうか。印の位置と大きさ）は Unity で見ていない。

## Prefab を作る（最初の 1 回）
素材（FBX）をそのまま `Resources` に置けないので、Unity のメニュー **MouthOfGod > Village > Generate Prefabs** で、
`Assets/Resources/Prefabs/Village/` に Prefab と `Villager.controller` を作る。できた Prefab をコミットすれば、以後は Play するだけで動く。
既にある Prefab は飛ばす。ただし村人 5 人と `Villager.controller` は毎回作り直す。
Prefab が無い物は、仮の箱で置く（警告が出る）。この手順は開発中の補助で、Prefab をコミットしたら不要。

## 置き物（素材の探し方は [docs/quaternius-assets.md](../../../docs/quaternius-assets.md)）
| 置き物 | 素材 | 出る段階 |
|---|---|---|
| `House_Sample`（4 軒。道か広場に正面を向ける） | 既存の Prefab（Medieval Village MegaKit の組み立て例）。家の素材はこれだけ | 常に |
| `Well` `Windmill` `Barn` `Fence` `ChickenCoop` | Farm Buildings | 常に |
| `Wagon` `Crate` | Medieval Village MegaKit（`Prop_Wagon` `Prop_Crate`） | 常に |
| `Sheep` → `Pig` | Farm Animals | 3〜 → 4〜 |
| `Farmland`（畑: 土の板と畝 4 本）、`Path`（道: 土の板）、`Plaza`（広場: 円盤） | 素材に無いので、生成メニューが箱・円柱・球で作る（マテリアルも） | 常に |
| `Tree_Bare`（葉が茶色い木）→ `Tree`（緑の木） | 同上 | 0〜1 → 2〜 |
| `Crop_Withered`（枯れた茎）→ `Crop_Sprout`（芽） | 同上 | 0 → 1 |
| `Casual_Male` `Casual_Female` `OldClassy_Male` `Worker_Male` `Worker_Female`（困っている人: 村人 5 人） | Ultimate Animated Character Pack。アニメーションは `Casual_Male` のクリップと `Villager.controller` を共有（骨組みが同じ） | 常に |
| `Carrot` `Turnip` `Pumpkin`（畑の作物。畝 1 本に 4 つずつ） | Ultimate Food Pack | 2〜 |

## 配置の考え方（村らしく見せるため）
実際の村の成り立ちと、ミニチュアの村（ジオラマ）の作り方を軽く調べて、次の決まりにした。
1. **中心に広場と井戸**。見る人の目が最初に行く所を 1 つ作る。道は広場で交わり、村の外（隣のフィールド）へ抜ける（行き止まりにしない）。
2. **家は道か広場に正面を向ける**。裏は外側へ。角度と道からの下がり具合を少しずつ変え、一直線にそろえない。
3. **道の突き当たりに目印**（風車）。
4. **畑・納屋・家畜は外側**。家の集まりと畑を細い道でつなぐ。
5. **外周に木**を置いて端をやわらげる。詰め込みすぎず、広場のまわりは空ける。
6. **村人は、何かをしている場所にいる**。1 か所に集めない（井戸で水をくむ、戸口に立つ、羊の番、畑仕事）。向きも、しているものの方に向ける。

道はまっすぐ通さず、少し曲げる（曲がった道は長く見え、作った感じが減る）。

## 配置を変えるには
`Village.prefab` を開いて手で動かす。配置図（#79）が決まったら、Prefab を直す。
（下書きの `Editor/Draft/VillageLayout.cs` では、大きさを置き物ごとの `Size`＝見た目の一番長い辺（メートル）で決めていた。）

## 仮に決めたこと
- 配置の座標と大きさ（実物の寸法）。家 10 cm、風車 11 cm、納屋 10 cm、畑 15 cm（柵 5 cm を 3 辺に 8 枚）、井戸 5 cm、村人 6.5 cm、作物 2.5〜3 cm、大通りの幅 3.5 cm、小道 2.2 cm、広場の直径 13 cm。VR で見て決める。
- 村人は 5 人、大きさ 6.5 cm（家 10 cm に対して実際より大きく誇張）。
- `House_Sample` の玄関の向きは確かめていない。家が道に背を向けていたら、Prefab の中で家を 180° 回す（下書きなら `HouseFrontYaw` を 180 にして Bake し直す）。
- 素材の根元の回転（FBX の軸を直す X -90° など）は残し、その上から Y の向きを付ける。
- `House_Sample` の Prefab は、位置が大きくずれたまま保存されている（Transform が (-7, -5.8, 7.8)）。外形で位置を合わせるので、置き方には影響しない。
