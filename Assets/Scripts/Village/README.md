# Village

ミニチュアの村のフィールドと、フィールドが段階的に戻る仕組み。いまは「村」のフィールドだけ（草原・海・山は別に作る。定義は [docs/fields.md](../../../docs/fields.md)）。

> 状態: Unity では一部だけ確認（配置・悩む印、Bake とテスト）。ステップに分けた後の Play での見た目は未確認。配置の数字・倍率は仮。

## 部品（4 つのフィールドで共通に使う）
戻り方の仕組みは村専用ではない。山・海・草原の担当も、自分のフィールドに同じ部品を付ければ使える（コードは要らない）。作り方・確かめ方・分担は [docs/requests.md](../../../docs/requests.md)。

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FieldStages` | フィールドの一番上 | **Field Id に自分のフィールドの ID を入れる**（初期値は空。`village` / `mountain` / `sea` / `grassland`）。有効になると Core の `Fields` に登録される。下の `FieldStep` を **Hierarchy の並び順**に使う（番号は持たない）。欲しい物は `Open()` で出し、届いた物は `Receive(food)` で受け取ります。頼まれた物だけが戻ります。待ちきれなかった物は `Expire(番号)` で、はじめに戻す物は `Restart()` で扱います。同時に出せる欲しい物の数は **Max Open Requests**（仮に 2）。Core の `IFieldRequests` の実装で、いつ出すかは Game・Tutorial が決める。開発中は右クリックメニュー（Open Next Want・Deliver Wanted Food・Complete Next Step・Restart）で確かめる |
| `FieldStep` | `Steps` の下の、ステップごとの空のオブジェクト | 戻り方の 1 段です。1 段ごとに、欲しい物（`Food`）を決め、困りごとが解ける人（`Helped`。1 人目の頭の上に吹き出し）と、済むと消える物（`Hide When Done`）を指定します。**このオブジェクトの子が、済むと出る物**。音・光は `On Done` につなぐ |
| `VillagerMood` | 村人（村人の Prefab に入っている） | 悩んでいる間は、頭の上に印を出します。村人は、ゆっくり立つ動きと、ときどきうなだれる動きを、人ごとにばらばらの間隔でくり返します。名前のあるステップがすべて済むと元気になる。ステップが済むたびに、元気な人が喜ぶ |
| `SurfaceAnchor` | フィールドの Prefab の一番上 | シーンの台（縦が潰れた立方体）の上面に乗せ、親の倍率を打ち消す。Prefab の中は 1 = 1 m のまま。編集中のシーンでも効く |
| `DeliveryZone` | フィールドの一番上（Is Trigger の Collider と一緒に） | 食べ物が落ちる場所。入った食べ物の ID を Core の `IFoodItem` で読み、そのフィールドの `Receive(food)` を呼ぶ。届け先は **Field Id**（空なら、親の `FieldStages`）。頼まれた物が届いたら食べ物を消す（**Consume Requested**）。それ以外の物は残す（**Consume Others**）。同じ食べ物を 2 回は届けない。`Village.prefab` には、Bake が村の上を覆う箱と一緒に付ける |

ステップの進み具合の決まり（`StepProgress`）は Unity に依存しないので、テストしている。

### ほかの機能とのつなぎ方
- 食べ物が落ちる場所（`DeliveryZone`）は、何が落ちても Core の `IFieldRequests.Receive(food)` を呼ぶ。頼まれた物ならそのステップが済む。
- 頼まれていない物では、ステップは進まない。代わりに `Received` の知らせ（`Own` か `Foreign`）だけが出る。この知らせを受けて、反応を足せる。たとえば、2 本目のにんじんで村が豊作になる反応や、村に豚が来て世界がいびつになる反応を足せる。
- Game や Tutorial は Village を直接呼ばない。Core の `Fields`（置き場）から `IFieldRequests` として受け取る。
- 独自の演出は、ステップの子の物に付けたスクリプトで足せる。出たときに動かすなら `OnEnable` を使う。または `FieldStages.StepDone` から足す。共通の部品は直さない。

## シーンとのつながり
`Assets/Scenes/Main.unity` の `MinitureBase > Content > Village` は、立方体の地面（スケール 0.54, 0.2, 0.54。親の倍率をかけた見かけの大きさも縦に潰れている）。
- `Village.prefab` を、この `Village`（地面）の**子**に置く。`SurfaceAnchor` が上面に乗せ、ゆがみを打ち消す。
- 位置と大きさは、**実物のミニチュアの寸法（メートル）**。台は 1.08 m 四方、村はその中の 0.54 m 四方。村の中は、中心から ±0.27 m。
- 地面の見た目はシーンのまま。
- 共有シーン（`Main`）は `scene/` ブランチでしか編集しない。動作の確認は、サンドボックスで行う。

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
| はじめ（飢饉） | 畑に枯れた茎、木は茶色、家は屋根が無い。家畜はいない。村人は全員うなだれ、頭の上に暗い縦線 | — |
| 1 Pumpkin（欲しい物 〔pumpkin〕） | 畝 1 本（畑の西端）にかぼちゃが実る。木 1 本が緑に戻る。家 1 軒の屋根が直る | 畑の入り口の人（`Worker_Male`） |
| 2 Carrot（〔carrot〕） | 畝 2 本目ににんじんが実る。木 1 本と家 1 軒が戻る | 井戸の人（`Casual_Female`） |
| 3 Turnip（〔turnip〕） | 畝 3 本目にかぶが実る。木 1 本と家 1 軒が戻る | 畑の中の人（`Worker_Female`） |
| 4 Tomato（〔tomato〕） | 畝 4 本目（海側）にトマトが実る。最後の木と家が戻る | 戸口の人（`Casual_Male`）。全員が元気 |

どのステップの欲しい物も、農作物です。1 ステップごとに戻るものは、畝 1 本・木 1 本・家 1 軒・村人 1 人です。村は北が山、東が海のフィールドに接します。そのため、北の端には何も置かず、畑は東の端から離しています。`turnip` と `tomato` の食べ物は、Unity のメニュー MouthOfGod > Food > Generate Prefabs と Generate Icons で作ります。

ステップが済むたびに、元気な村人が `Victory` を 1 回やって喜ぶ。何がいつ戻るかは、`Village.prefab` の `Steps` の下の並びと各 `FieldStep` で決めます。下書きでは `AppearsAt` `GoneAt` `HelpedAt` と `VillageLayout.Steps` が、その役目です。〔 〕の欲しい物の ID は仮（#31）。

## アニメーション（村人）
段階を踏んで決めた。
1. **あるか**: `Casual_Male.fbx`（Ultimate Animated Character Pack）に、17 種のクリップがある（`CharacterArmature|Idle` `Walk` `Walk_Carry` `PickUp` `Victory` `Defeat` など）。FBX の中の名前を読んで確認した。「頭を抱える」動きは無い。
2. **場面に合うか**: 飢饉の村で、村人は困っている。届いて困りごとが解けたら元気になり、届くたびに喜ぶ。
   - 使う: `Idle`（元気。ループ。悩んでいる間は 0.6 倍でゆっくり）、`Defeat`（ときどきうなだれる。最後の姿勢で止まる）、`Victory`（喜ぶ。1 回）。
   - 悩んでいる間ずっとうなだれたままだと、時間が止まったように見えた。なので、ゆっくり立つ動きとうなだれる動きを、人ごとにばらばらの間隔でくり返す。
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
既にある Prefab は飛ばす。ただし村人 5 人（`OldClassy_Male` を含む）と `Villager.controller` は毎回作り直す。
Prefab が無い物は、仮の箱で置く（警告が出る）。この手順は開発中の補助で、Prefab をコミットしたら不要。

## 置き物（素材の探し方は [docs/quaternius-assets.md](../../../docs/quaternius-assets.md)）
| 置き物 | 素材 | 出るステップ |
|---|---|---|
| `House_Ruined`（屋根が無い家）→ `House_Sample`（屋根がある家）。4 軒。道か広場に正面を向ける | `House_Sample` は既存の Prefab（Medieval Village MegaKit の組み立て例）。`House_Ruined` は、生成メニューが `House_Sample` から `Roof_` で始まる部品を外して作る | 0 → 1〜4（1 軒ずつ） |
| `Well` `Barn` `Fence` | Farm Buildings | 常に（`Windmill` `ChickenCoop` の Prefab は残るが、いまの配置では使わない） |
| `Wagon` `Crate` | Medieval Village MegaKit（`Prop_Wagon` `Prop_Crate`） | 常に |
| `Farmland`（畑: 土の板と畝 4 本）、`Path`（道: 土の板）、`Plaza`（広場: 円盤） | 素材に無いので、生成メニューが箱・円柱・球で作る（マテリアルも） | 常に |
| `Tree_Bare`（葉が茶色い木）→ `Tree`（緑の木）。4 本 | 同上 | 0 → 1〜4（1 本ずつ） |
| `Crop_Withered`（枯れた茎） | 同上 | 0 → 1〜4（畝 1 本ずつ消える。`Crop_Sprout`（芽）の Prefab は残るが、いまの配置では使わない） |
| `Casual_Male` `Casual_Female` `Worker_Male` `Worker_Female`（困っている人: 村人 4 人。`OldClassy_Male` の Prefab は作るが、いまの配置では使わない） | Ultimate Animated Character Pack。アニメーションは `Casual_Male` のクリップと `Villager.controller` を共有（骨組みが同じ） | 常に |
| `Pumpkin` `Carrot` `Turnip` `Tomato`（畑の作物。畝 1 本に 3 つずつ。この順に畝の西から東へ） | Ultimate Food Pack | 1〜4（畝 1 本ずつ） |
| `Fx_Smoke`（屋根の煙）、`Lantern`（ランタン）、`Flowerbed`（花壇） | 煙は Kenney の Particle Pack（CC0）の絵で作る粒。ランタンと花壇は、箱・球で作る（光って見えるが、ライトは使わない） | 1〜4（直った家ごと） |

## 演出（出方・煙・音）
- **出方**: ステップの子の物は、出るとき小さい所から「ぽんっ」と大きくなる（`PopInEffect`）。`Steps` の下に物を置くだけで効く。出方を自分で作る物には `SkipPopIn` を付ける。ステップごとにやめるには、`FieldStep` の `Pop In` を外す。
- **喜びの粒と音**: `StepDoneFx`（Village の根に付く）が、ステップが済むと、助かった村人の頭の上に星の粒を出し、短い音を鳴らす。粒は `Fx_Joy`、音は `Assets/ArtisticResources/ThirdParty/Kenney/Audio/` の `jingles_PIZZI02`。聞いて合わなければ、Inspector の `Done Clip` を `jingles_PIZZI00`〜`05` の別のものにする。
- **作り方**: 粒の Prefab は `MouthOfGod > Village > Generate Effects`（`Generate Prefabs` からも呼ばれる）。作り直すときは、`Fx_*.prefab` を消してから実行する。
- **素材**: Kenney の Particle Pack と Music Jingles（どちらも CC0。ライセンスは各フォルダの `License.txt`）。
- **Quest 向け**: 粒の数は 20 まで。衝突・軌跡・ライトは使わない。

## 配置の考え方（村らしく見せるため）
村の配置は、実際の村の成り立ちとミニチュアの村（ジオラマ）の作り方をもとに、次の決まりにします。
1. **中心に広場と井戸**。見る人の目が最初に行く所を 1 つ作る。道は広場で交わり、村の外（隣のフィールド）へ抜ける（行き止まりにしない）。
2. **家は道か広場に正面を向ける**。裏は外側へ。角度と道からの下がり具合を少しずつ変え、一直線にそろえない。
3. **北は山に接するので、何も置かない**（風車・家畜・北へ延びる道は置かない）。
4. **畑・納屋・家畜は外側**。家の集まりと畑を細い道でつなぐ。
5. **外周に木**を置いて端をやわらげる。詰め込みすぎず、広場のまわりは空ける。
6. **村人は、何かをしている場所にいる**。1 か所に集めない（井戸で水をくむ、戸口に立つ、畑仕事）。向きも、しているものの方に向ける。

道はまっすぐ通さず、少し曲げる（曲がった道は長く見え、作り物らしさが薄れる）。

## 配置を変えるには
`Village.prefab` を開いて手で動かす。配置図（#79）が決まったら、Prefab を直す。
（下書きの `Editor/Draft/VillageLayout.cs` では、大きさを置き物ごとの `Size`＝見た目の一番長い辺（メートル）で決めていた。）

## 仮に決めたこと
- 配置の座標と大きさは、実物の寸法で書きます。仮の大きさは、家と納屋が 10 cm、畑が 15 cm、井戸が 5 cm、村人が 8.5 cm、作物が 5〜6 cm です。畑の柵は 5 cm のものを 3 辺に 8 枚並べます。大通りの幅は 3.5 cm、小道は 2.2 cm、広場の直径は 13 cm です。最終的な大きさは VR で見て決めます。
- 村人は 4 人、大きさ 8.5 cm（家 10 cm に対して実際より大きく誇張）。
- `House_Sample` の玄関の向きは確かめていない。家が道に背を向けていたら、Prefab の中で家を 180° 回す（下書きなら `HouseFrontYaw` を 180 にして Bake し直す）。
- 素材の根元の回転（FBX の軸を直す X -90° など）は残し、その上から Y の向きを付ける。
- `House_Sample` の Prefab は、位置が大きくずれたまま保存されている（Transform が (-7, -5.8, 7.8)）。外形で位置を合わせるので、置き方には影響しない。
