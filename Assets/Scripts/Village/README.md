# Village

ミニチュアの村のフィールドと、フィールドが段階的に戻る仕組みです。いまあるのは「村」のフィールドだけです。草原・海・山は別に作ります。定義は [docs/fields.md](../../../docs/fields.md) にあります。

状態: 案。配置の数字と倍率は仮です。決めた経緯は [docs/decisions.md](../../../docs/decisions.md) の 2026-10-15 と 2026-10-20 にあります。

村の作り込み（配置の考え方、置き物と素材、ステップの見た目、アニメーション、寸法、下書き）は [docs/village.md](../../../docs/village.md) にあります。

## 部品（4 つのフィールドで共通に使う）

戻り方の仕組みは、村専用ではありません。山・海・草原の担当も、自分のフィールドに同じ部品を付ければ使えます。コードは要りません。作り方・確かめ方・分担は [docs/requests.md](../../../docs/requests.md) にあります。

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FieldStages` | フィールドの一番上 | Core の `IFieldRequests` の実装です。**Field Id に、自分のフィールドの ID を入れます**（初期値は空です。`village` / `mountain` / `sea` / `grassland`）。有効になると、Core の `Fields` に登録されます。下の `FieldStep` を **Hierarchy の並び順**に使います。番号は持ちません。同時に出せる欲しい物の数は、**Max Open Requests** で決めます。今の値は仮に 2 です |
| `FieldStep` | `Steps` の下の、ステップごとの空のオブジェクト | 戻り方の 1 段です。欲しい物（`Food`）と、困りごとが解ける人（`Helped`）を持ちます。吹き出しは、`Helped` の 1 人目の頭の上に出ます。`Hide When Done` には、済むと消える物を入れます。**このオブジェクトの子が、済むと出る物です**。音や光は `On Done` につなぎます |
| `VillagerMood` | 村人（村人の Prefab に入っています） | 悩んでいる間は、頭の上に印を出します。村人は、ゆっくり立つ動きと、ときどきうなだれる動きをくり返します。間隔は人ごとにばらばらです。名前のあるステップがすべて済むと、元気になります。ステップが済むたびに、元気な人が喜びます |
| `SurfaceAnchor` | フィールドの Prefab の一番上 | シーンの台（縦が潰れた立方体）の上面に乗せ、親の倍率を打ち消します。Prefab の中は 1 = 1 m のままです。編集中のシーンでも効きます |

`FieldStages` の主な API は次のとおりです。

- `Open()`: 欲しい物を出します。
- `Receive(food)`: 届いた物を受け取ります。戻るのは、頼まれた物だけです。
- `Expire(番号)`: 待ちきれなかった欲しい物を終えます。
- `Restart()`: はじめに戻します。
- `StepDone`: ステップが済んだ知らせです。

## ほかの機能とのつなぎ方

- いつ欲しい物を出すかは、Game と Tutorial が決めます。
- Game や Tutorial は、Village を直接呼びません。Core の `Fields`（置き場）から、`IFieldRequests` として受け取ります。
- 物が落ちた場所を決める所（DeliveryZone）は、何が落ちても `IFieldRequests.Receive(food)` を呼びます。頼まれた物なら、そのステップが済みます。
- 頼まれていない物では、フィールドは戻りません。`Received`（`Own`・`Foreign`）の知らせだけが出ます。豊作やいびつな世界などの反応は、それを受けて足します。
- フィールドごとの独自の演出は、共通の部品を直さずに足します。ステップの子の物に付けたスクリプト（出たときの `OnEnable`）か、`FieldStages.StepDone` を使います。

## シーンとのつながり

- `Village.prefab` は、`Assets/Scenes/Main.unity` の `MinitureBase > Content > Village`（地面）の**子**に置きます。`SurfaceAnchor` が上面に乗せ、ゆがみを打ち消します。
- 地面の見た目は、シーンのままです。寸法は [docs/village.md](../../../docs/village.md) の「シーンと寸法」にあります。
- 共有シーン（`Main`）は、`scene/` ブランチでしか編集しません。確かめるのは、サンドボックスで行います。

## 使い方

1. メニュー **MouthOfGod > Village > Generate Prefabs** で、置き物と村人の Prefab を作ります。詳しくは docs/village.md の「Prefab を作る」です。
2. **MouthOfGod > Village > Bake Draft Village Prefab** で、下書きの配置から `Assets/Resources/Prefabs/Village/Village.prefab` を作ります。
3. サンドボックスのシーンで、`Village`（地面）の子に `Village.prefab` を置きます。
4. Play して、`Village.prefab` の `FieldStages` を右クリックし、メニューで確かめます。Open Next Want → Deliver Wanted Food の順か、Complete Next Step を使います。Restart で、はじめに戻ります。
5. 配置を直すときは、**`Village.prefab` を開いて手で動かします**。ステップで出したい物は、そのステップ（`Steps` の下）の子に入れます。消したい物は、そのステップの `Hide When Done` に入れます。

## テスト

`Tests/`（EditMode）にあります。ステップの進み具合の決まり（`StepProgress`）は Unity に依存しないので、`StepProgressTests` で確かめます。ほかに `FieldStagesTests` と `VillageLayoutTests` があります。
