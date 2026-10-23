# 村の作り込み

村のフィールド（`Village.prefab`）を作る人のための資料です。部品の使い方は [Assets/Scripts/Village/README.md](../Assets/Scripts/Village/README.md) にあります。ここには、配置・素材・見た目・寸法の決め方を書きます。

この資料は、Village の README から移しました（2026-10-20）。数字と倍率は、どれも仮です。

## シーンと寸法

`Assets/Scenes/Main.unity` の `MinitureBase > Content > Village` は、立方体の地面です。

- スケールは (0.54, 0.2, 0.54) です。親の倍率をかけた見かけの大きさも、縦に潰れています。
- `Village.prefab` は、この地面の子に置きます。`SurfaceAnchor` が上面に乗せ、ゆがみを打ち消します。
- 位置と大きさは、**ミニチュアそのものの寸法**で決めます。単位はメートルです。
- 台は 1.08 m 四方です。村は、その中の 0.54 m 四方です。
- 村の中は、中心から ±0.27 m です。

## 配置の考え方（村らしく見せるため）

次の決まりは、実際の村の成り立ちを参考にしています。ミニチュアの村（ジオラマ）の作り方も参考にしています。

1. **中心に広場と井戸**を置きます。見る人の目が、最初に行く所を 1 つ作ります。道は広場で交わり、村の外（隣のフィールド）へ抜けます。行き止まりにはしません。
2. **家は、道か広場に正面を向けます**。裏は外側に向けます。家ごとに、角度を少しずつ変えます。道から引っ込む距離も、少しずつ変えます。一直線にはそろえません。
3. **北は山に接するので、何も置きません**。風車・家畜・北へ延びる道は置きません。
4. **畑・納屋・家畜は外側**に置きます。家の集まりと畑を、細い道でつなぎます。
5. **外周に木**を置いて、端をやわらげます。詰め込みすぎず、広場のまわりは空けます。
6. **村人は、何かをしている場所に**置きます。1 か所に集めません。井戸で水をくむ、戸口に立つ、畑仕事などです。向きも、しているものの方に向けます。

道はまっすぐ通さず、少し曲げます。曲がった道は長く見え、作り物らしさが薄れるためです。

## 配置を変えるには

`Village.prefab` を開いて、手で動かします。配置図（#79）が決まったら、Prefab を直します。

下書きの `Editor/Draft/VillageLayout.cs` では、大きさを置き物ごとの `Size` で決めていました。`Size` は、見た目の一番長い辺（メートル）です。

## 置き物と素材

素材の探し方は [quaternius-assets.md](quaternius-assets.md) にあります。

| 置き物 | 素材 | 出るステップ |
|---|---|---|
| `House_Ruined`（屋根が無い家）→ `House_Sample`（屋根がある家）。4 軒。道か広場に正面を向けます | `House_Sample` は既存の Prefab です（Medieval Village MegaKit の組み立て例）。`House_Ruined` は、生成メニューが `House_Sample` から `Roof_` で始まる部品を外して作ります | 0 → 1〜4（1 軒ずつ） |
| `Well` `Barn` `Fence` | Farm Buildings | 常に。`Windmill` `ChickenCoop` の Prefab は残りますが、いまの配置では使いません |
| `Wagon` `Crate` | Medieval Village MegaKit（`Prop_Wagon` `Prop_Crate`） | 常に |
| `Farmland`（畑: 土の板と畝 4 本）、`Path`（道: 土の板）、`Plaza`（広場: 円盤） | 素材に無いので、生成メニューが箱・円柱・球で作ります（マテリアルも） | 常に |
| `Tree_Bare`（葉が茶色い木）→ `Tree`（緑の木）。4 本 | 同上 | 0 → 1〜4（1 本ずつ） |
| `Crop_Withered`（枯れた茎） | 同上 | 0 → 1〜4（畝 1 本ずつ消えます）。`Crop_Sprout`（芽）の Prefab は残りますが、いまの配置では使いません |
| `Casual_Male` `Casual_Female` `Worker_Male` `Worker_Female`（困っている村人 4 人） | Ultimate Animated Character Pack です。骨組みが同じなので、`Casual_Male` のクリップと `Villager.controller` を共有します。`OldClassy_Male` の Prefab も作りますが、いまの配置では使いません | 常に |
| `Pumpkin` `Carrot` `Turnip` `Tomato`（畑の作物。畝 1 本に 3 つずつ。この順に畝の西から東へ） | Ultimate Food Pack | 1〜4（畝 1 本ずつ） |
| `Fx_Smoke`（屋根の煙）、`Lantern`（ランタン）、`Flowerbed`（花壇） | 煙は Kenney の Particle Pack（CC0）の絵で作る粒です。ランタンと花壇は、箱・球で作ります（光って見えますが、ライトは使いません） | 1〜4（直った家ごと） |

## ステップ

| ステップ（`Steps` の下の並び） | 見た目 | 元気になる村人 |
|---|---|---|
| はじめ（飢饉） | 畑に枯れた茎があり、木は茶色で、家は屋根が無い状態です。家畜はいません。村人は全員うなだれ、頭の上に暗い縦線が出ます | — |
| 1 Pumpkin（欲しい物 〔pumpkin〕） | 畝 1 本（畑の西端）にかぼちゃが実ります。木 1 本が緑に戻ります。家 1 軒の屋根が直ります | 畑の入り口の人（`Worker_Male`） |
| 2 Carrot（〔carrot〕） | 畝 2 本目ににんじんが実ります。木 1 本と家 1 軒が戻ります | 井戸の人（`Casual_Female`） |
| 3 Turnip（〔turnip〕） | 畝 3 本目にかぶが実ります。木 1 本と家 1 軒が戻ります | 畑の中の人（`Worker_Female`） |
| 4 Tomato（〔tomato〕） | 畝 4 本目（海側）にトマトが実ります。最後の木と家が戻ります | 戸口の人（`Casual_Male`）。これで全員が元気です |

どのステップの欲しい物も、農作物です。1 ステップごとに戻るものは、畝 1 本・木 1 本・家 1 軒・村人 1 人です。村は北が山、東が海のフィールドに接します。そのため、北の端には何も置かず、畑は東の端から離しています。`turnip` と `tomato` の食べ物は、Unity のメニュー MouthOfGod > Food > Generate Prefabs と Generate Icons で作ります。

- ステップが済むたびに、元気な村人が `Victory` を 1 回やって喜びます。
- 何がいつ戻るかは、`Village.prefab` の `Steps` の下の並びと、各 `FieldStep` で決めます。
- 下書きでは、`AppearsAt` `GoneAt` `HelpedAt` と `VillageLayout.Steps` で決めていました。
- 〔 〕の欲しい物の ID は仮です（#31）。

## 村人のアニメーション

次の 3 段で決めました。

### 1. あるか

`Casual_Male.fbx`（Ultimate Animated Character Pack）には、17 種のクリップがあります。`CharacterArmature|Idle` `Walk` `Walk_Carry` `PickUp` `Victory` `Defeat` などです。FBX の中の名前を読んで確かめました。「頭を抱える」動きはありません。

### 2. 場面に合うか

飢饉の村で、村人は困っています。欲しい物が届くと、村人の困りごとが解けます。すると元気になります。届くたびに喜びます。

- 使う: `Idle` は元気なときです（ループ）。悩んでいる間は、0.6 倍でゆっくり動かします。
- 使う: `Defeat` は、ときどきうなだれる動きです。最後の姿勢で止まります。
- 使う: `Victory` は喜ぶ動きです（1 回）。
- 悩んでいる間ずっとうなだれたままだと、時間が止まったように見えました。そこで、ゆっくり立つ動きと、うなだれる動きをくり返します。間隔は人ごとに違います。
- 使わない: `Walk` `Run` は、どこかへ行ってしまうためです。`Death` は暗すぎます。
- 使わない: `Punch` `SwordSlash` `Shoot_OneHanded` `Roll` `RecieveHit` は、戦闘の動きです。
- 使わない: `PickUp` は、拾う物が無いと不自然です。`Jump` `SitDown` `StandUp` は、意味がありません。
- 後で使える: `Walk_Carry`（物を運ぶ）です。
- 頭を抱える動きが要るなら、Mixamo などの外の動きを試します。その場合は、村人の取り込みを Humanoid にする必要があります。

### 3. 実装

生成メニューが `Villager.controller` を作り、村人の Prefab に付けます。

- 状態は、`Idle` ⇄ `Worried`（bool の `Worried`）です。
- `Worried` ⇄ `Slump`（bool の `Slump`）があります。`Slump` は `Defeat` を使います。
- `Idle` → `Victory`（トリガーの `Delivered`）→ `Idle` で喜びます。
- 村人の Prefab には、`VillagerMood` が入っています。頭の骨の子に、悩んでいる印 `WorryMark`（暗い縦線 3 本）も入っています。
- `VillagerMood` が、`Worried` `Slump` と印を切り替えます。ステップが済んだら、`Delivered` を引きます。

見て確かめること: `Defeat` が、最後にうなだれたままで止まるか、立ち上がってしまうか。印の位置と大きさも見ます。

## Prefab を作る（最初の 1 回）

素材（FBX）は、そのまま `Resources` に置けません。そこで、メニュー **MouthOfGod > Village > Generate Prefabs** を使います。

- `Assets/Resources/Prefabs/Village/` に、Prefab と `Villager.controller` を作ります。
- できた Prefab をコミットすれば、以後は Play するだけで動きます。
- 既にある Prefab は飛ばします。ただし、村人 5 人（`OldClassy_Male` を含む）と `Villager.controller` は毎回作り直します。
- Prefab が無い物は、仮の箱で置きます。そのときは警告が出ます。
- この手順は開発中の補助です。Prefab をコミットしたら要りません。

## 演出（出方・煙・音）

- **出方**: ステップの子の物は、出るとき小さい所から「ぽんっ」と大きくなります（`PopInEffect`）。`Steps` の下に物を置くだけで効きます。出方を自分で作る物には、`SkipPopIn` を付けます。ステップごとにやめるには、`FieldStep` の `Pop In` を外します。
- **喜びの粒と音**: `StepDoneFx`（Village の根に付きます）が、ステップが済むと、助かった村人の頭の上に星の粒を出し、短い音を鳴らします。粒は `Fx_Joy` です。音は `Assets/ArtisticResources/ThirdParty/Kenney/Audio/` の `jingles_PIZZI02` です。聞いて合わなければ、Inspector の `Done Clip` を `jingles_PIZZI00`〜`05` の別のものにします。
- **作り方**: 粒の Prefab は、メニューの `MouthOfGod > Village > Generate Effects` で作ります（`Generate Prefabs` からも呼ばれます）。作り直すときは、`Fx_*.prefab` を消してから実行します。
- **素材**: Kenney の Particle Pack と Music Jingles です（どちらも CC0。ライセンスは各フォルダの `License.txt`）。
- **Quest 向け**: 粒の数は 20 までにします。衝突・軌跡・ライトは使いません。

## 下書きと Bake（`Editor/Draft/`）

`Editor/Draft/VillageLayout.cs` は、村の配置をコードで書いた最初の下書きです。`Editor/Draft/VillageBaker.cs` が、ここから `Village.prefab` を作ります（Bake）。

- Bake は、最初の 1 回の補助です。Bake の後は、Prefab が正です。下書きを Prefab と同じに保つ必要はありません。
- もう一度 Bake すると、Prefab を手で直した分は消えます。上書きの前に、確かめるダイアログが出ます。
- Editor の中なので、展示のビルドには入りません。

## 仮に決めたこと

- 配置の座標と大きさは、実物の寸法で書きます。VR で見て決め直します。
  - 家と納屋は 10 cm、井戸は 5 cm です。
  - 畑は 15 cm です。柵は 5 cm で、3 辺に 8 枚です。
  - 村人は 8.5 cm、作物は 5〜6 cm です。
  - 大通りの幅は 3.5 cm、小道は 2.2 cm、広場の直径は 13 cm です。
- 村人は 4 人で、大きさは 8.5 cm です。家 10 cm に対して、実際より大きく誇張しています。
- `House_Sample` の玄関の向きは、確かめていません。家が道に背を向けていたら、Prefab の中で家を 180° 回します。下書きなら、`HouseFrontYaw` を 180 にして Bake し直します。
- 素材の元の回転は残します。FBX の軸を直すための回転で、X -90° などです。その上から、Y の向きを付けます。
- `House_Sample` の Prefab は、位置が大きくずれたまま保存されています（Transform が (-7, -5.8, 7.8)）。外形で位置を合わせるので、置き方には影響しません。
