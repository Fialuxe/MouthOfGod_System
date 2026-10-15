# Village

ミニチュアの村のフィールドと、フィールドが段階的に戻る仕組み。いまは「村」のフィールドだけ（草原・海・山は別に作る。定義は [docs/fields.md](../../../docs/fields.md)）。

> 状態: Unity では一部だけ確認（配置・悩む印、Bake とテスト）。ステップに分けた後の Play での見た目は未確認。配置の数字・倍率は仮。

## 部品（4 つのフィールドで共通に使う）
戻り方の仕組みは村専用ではない。山・海・草原の担当も、自分のフィールドに同じ部品を付ければ使える（コードは要らない）。作り方・確かめ方・分担は [docs/requests.md](../../../docs/requests.md)。

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FieldStages` | フィールドの一番上 | **Field Id に自分のフィールドの ID を入れる**（初期値は空。`village` / `mountain` / `sea` / `grassland`）。有効になると Core の `Fields` に登録される。下の `FieldStep` を **Hierarchy の並び順**に使う（番号は持たない）。欲しい物を出す `Open()`・届いた物を受け取る `Receive(food)`（頼まれた物だけ戻る）・待ちきれなかった `Expire()`・はじめに戻す `Restart()`。Core の `IFieldRequests` の実装で、いつ出すかは Game・Tutorial が決める。開発中は右クリックメニュー（Open Next Want・Deliver Wanted Food・Complete Next Step・Restart）で確かめる |
| `FieldStep` | `Steps` の下の、ステップごとの空のオブジェクト | 戻り方の 1 段。欲しい物（`Food`）、困りごとが解ける人（`Helped`。1 人目の頭の上に吹き出し）、済むと消える物（`Hide When Done`）。**このオブジェクトの子が、済むと出る物**。音・光は `On Done` につなぐ |
| `VillagerMood` | 村人（村人の Prefab に入っている） | 悩んでいる間は頭の上の印を出し、ゆっくり立つ ⇄ ときどきうなだれる、を人ごとにばらばらの間隔でくり返す。名前のあるステップがすべて済むと元気になる。ステップが済むたびに、元気な人が喜ぶ |
| `SurfaceAnchor` | フィールドの Prefab の一番上 | シーンの台（縦が潰れた立方体）の上面に乗せ、親の倍率を打ち消す。Prefab の中は 1 = 1 m のまま。編集中のシーンでも効く |

ステップの進み具合の決まり（`StepProgress`）は Unity に依存しないので、テストしている。

### ほかの機能とのつなぎ方
- 物が落ちた場所を決める所（DeliveryZone）が、何が落ちても Core の `IFieldRequests.Receive(food)` を呼ぶ。頼まれた物ならそのステップが済む。頼まれていない物は戻らず、`Received`（`Own`・`Foreign`）の知らせだけ出る（豊作・いびつな世界などの反応は、それを受けて足す）。Game や Tutorial は Village を直接呼ばず、Core の `Fields`（置き場）から `IFieldRequests` として受け取る。
- フィールドごとの独自の演出は、ステップの子の物に付けたスクリプト（出たときの `OnEnable`）か、`FieldStages.StepDone` から足す。共通の部品は直さない。

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
4. Play して、`Village.prefab` の `FieldStages` の右クリックメニュー（Open Next Want → Deliver Wanted Food、または Complete Next Step）で確かめる。
5. 配置を直すときは、**`Village.prefab` を開いて手で動かす**。ステップで出したい物は、そのステップ（`Steps` の下）の子に入れる。消したい物は、そのステップの `Hide When Done` に入れる。

## 下書き（`Editor/Draft/`）
`Editor/Draft/VillageLayout.cs` は、村の配置をコードで書いた最初の下書き。`Editor/Draft/VillageBaker.cs` が、ここから `Village.prefab` を作る（Bake）。
- Bake は最初の 1 回の補助。Bake の後は Prefab が正。下書きを Prefab と同じに保つ必要はない。
- もう一度 Bake すると、Prefab を手で直した分は消える（上書きの前に確かめるダイアログが出る）。
- Editor の中なので、展示のビルドには入らない。

## ステップ
| ステップ（`Steps` の下の並び） | 見た目 | 元気になる村人 |
|---|---|---|
| はじめ（飢饉） | 畑に枯れた茎、木は茶色、家畜はいない。村人は全員うなだれ、頭の上に暗い縦線 | — |
| 1 Sprout（欲しい物 〔carrot〕） | 畑に芽が出る | 畑の 2 人（`Worker_Male` `Worker_Female`） |
| 2 Crops（〔pumpkin〕） | 作物（にんじん・かぶ・かぼちゃ）が実り、木が緑に戻る | 井戸の人（`Casual_Female`） |
| 3 Sheep（〔sheep〕） | 羊が戻る | 羊の番の長老（`OldClassy_Male`） |
| 4 Pig（〔pig〕） | 豚も戻る | 戸口の人（`Casual_Male`）。全員が元気 |

ステップが済むたびに、元気な村人が `Victory` を 1 回やって喜ぶ。何がいつ戻るかは、`Village.prefab` の `Steps` の下の並びと、各 `FieldStep` で決める（下書きでは `AppearsAt` `GoneAt` `HelpedAt` と `VillageLayout.Steps`）。〔 〕の欲しい物の ID は仮（#31）。

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
   - `VillagerMood` が `Worried` `Slump` と印を切り替え、ステップが済んだら `Delivered` を引く。
- **未確認**: 実際の動きの見た目（`Defeat` が最後にうなだれたままで止まるか、立ち上がってしまうか。印の位置と大きさ）は Unity で見ていない。

## Prefab を作る（最初の 1 回）
素材（FBX）をそのまま `Resources` に置けないので、Unity のメニュー **MouthOfGod > Village > Generate Prefabs** で、
`Assets/Resources/Prefabs/Village/` に Prefab と `Villager.controller` を作る。できた Prefab をコミットすれば、以後は Play するだけで動く。
既にある Prefab は飛ばす。ただし村人 5 人と `Villager.controller` は毎回作り直す。
Prefab が無い物は、仮の箱で置く（警告が出る）。この手順は開発中の補助で、Prefab をコミットしたら不要。

## 置き物（素材の探し方は [docs/quaternius-assets.md](../../../docs/quaternius-assets.md)）
| 置き物 | 素材 | 出るステップ |
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
