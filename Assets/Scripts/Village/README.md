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
| `DeliveryZone` | フィールドの一番上（Is Trigger の Collider と一緒に） | 食べ物が落ちる場所です。入った食べ物の ID を Core の `IFoodItem` で読み、そのフィールドの `Receive(food)` を呼びます。届け先は **Field Id** です（空なら、親の `FieldStages`）。頼まれた物が届いたら、食べ物を消します（**Consume Requested**）。それ以外の物は、煙を出して、だんだん小さくなって消えます（**Vanish Others**。外すと残ります）。同じ食べ物は 2 回届けません。`Village.prefab` には、Bake が村の上を覆う箱と一緒に付けます |
| `StandArea` | フィールドごとに 1 つ。体験者が立つ床の上（フィールドの子か、シーン） | 体験者が、そのフィールドの前に立っているかを判定します。頭が箱に入ると、Core の `Standing` にフィールドを入れます。出ると外します。口から生まれた物は、ここで決めたフィールドに落ちます。詳しくは下の「立つ場所を置く」です |

`FieldStages` の主な API は次のとおりです。

- `Open()`: 欲しい物を出します。
- `Receive(food)`: 届いた物を受け取ります。戻るのは、頼まれた物だけです。
- `Expire(番号)`: 待ちきれなかった欲しい物を終えます。
- `Restart()`: はじめに戻します。
- `StepDone`: ステップが済んだ知らせです。

## 立つ場所を置く（StandArea）

体験者は、物を届けたいフィールドの前まで歩きます。そこで顔を向けて狙いを定め、欲しがっている人のところへ物を飛ばします。物がどのフィールドに届いたかは、物が落ちた所の `DeliveryZone` が決めます。正しい物が届いても、違う物が届いても、フィールドはそれぞれに応じて反応します。

`StandArea` は、体験者が「どのフィールドの前に立っているか」を決めます。使い道は 2 つです。
- チュートリアルが「村の前に立って」「海の前まで歩いて」と案内して、立つのを待ちます。
- 食べ物を生むときに、そのフィールド専用の「食感 → 食べ物」の表を選びます。

`StandArea` では、フィールドごとに次の 2 つを決めます。

| 決めること | 中身 | `StandArea` の欄 |
|---|---|---|
| 立つ場所 | 床の上の見えない箱です。体験者の頭がこの箱に入ると、そのフィールドの前に立っていることになります | **Center**・**Size** |
| 足あとの印 | 立つ場所を体験者に教える、床の印です。チュートリアルが「ここに立って」と案内するときに出ます | **Footprint** |

```
  ┌──────────────┐
  │ フィールドの台（DeliveryZone） │  ← 物が落ちたら、このフィールドに届く
  └──────────────┘
      ┌─────┐
      │ 立つ場所  │  頭がこの箱に入ると「このフィールドの前」
      │  👣 印   │
      └─────┘
       体験者
```

置き方は、次の手順です。

1. フィールドごとに、空のオブジェクトを 1 つ作ります。体験者が立つ床の上に置きます。
2. フィールドの Prefab（`FieldStages` がある物）の子に置くと、**Field Id は空のまま**で使えます。シーンに置くときは、Field Id にフィールドの ID を入れます。
3. **Center** と **Size** で、立つ場所の箱を決めます。値は、このオブジェクトのローカル座標です。台の 1 辺の前に、人が立つ広さにします（例: 幅 0.8 m、奥行き 0.6 m）。
4. 箱は Scene ビューに線で出ます。選ぶと、塗りつぶしで出ます。頭が入っている間は、緑になります。
5. **Ignore Height** は、ふつうは入れたままにします。床の広さだけで判定します。
6. **Margin** は、出たと判定するまでの余白です。境目で、立つ場所がちらつかないようにします。
7. **Head** は、ふつうは空のままです。空なら、Main Camera を頭として使います。
8. 床の印（足あと）を出すときは、その物を **Footprint** に入れます。合図 `<field>.footprint` で出て、`<field>.footprint-off` で消えます。体験者が立つと、消えます（**Hide Footprint On Arrive**）。Play の始めは、消えています。

- 箱は、このオブジェクトと一緒に動きます。台ごと動いても（AprilTag で合わせても）、ずれません。
- 箱が重なる所では、あとから入った場所が勝ちます。そこを出ると、まだ入っている場所に戻ります。
- ヘッドセットなしで試すときは、`StandArea` を右クリックします。**Stand Here** で、そこに立ったことにします。**Leave** で、立つのをやめます。

## ほかの機能とのつなぎ方

- いつ欲しい物を出すかは、Game と Tutorial が決めます。
- Game や Tutorial は、Village を直接呼びません。Core の `Fields`（置き場）から、`IFieldRequests` として受け取ります。
- 物が落ちた場所を決める所（DeliveryZone）は、何が落ちても `IFieldRequests.Receive(food)` を呼びます。頼まれた物なら、そのステップが済みます。
- 頼まれていない物では、フィールドは戻りません。`Received`（`Own`・`Foreign`）の知らせだけが出ます。この知らせを受けて、豊作やいびつな世界などの反応を足します。
- 届いたときの標準の反応は、`DeliveryFx` が出します。`FieldStages` があれば、Play で自動で付きます。何も入れなくても、次の反応が出ます。
  - 欲しい物が届いた: 助かった村人の頭の上に喜びの粒（`Fx_Joy`）が出て、村人が喜ぶ動き（Victory）をします。**Done Clip** を入れると、音が鳴ります。
  - 違う物が届いた（頼まれていない物・よその物）: 困っている村人の Animator に、トリガー `Missed` を送ります。届いた物は、`DeliveryZone` が煙を出して消します。
  - 村人の動きを変えるときは、`Villager.controller` の `Missed` ステートの Motion を差し替えます。コードは直しません。今は、うなだれる動き（Defeat）を速く再生しています。
  - 今の `Villager.controller` に `Missed` が無いときは、メニュー **MouthOfGod > Village > Add Missed To Villager Controller** で足します。
  - 独自の反応は、`DeliveryFx` の **On Requested** と **On Missed** につなぎます。
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

`Tests/`（EditMode）にあります。ステップの進み具合の決まり（`StepProgress`）は Unity に依存しないので、`StepProgressTests` で確かめます。ほかに `FieldStagesTests` と `VillageLayoutTests` があります。立つ場所の判定は、`StandAreaTests` で確かめます。
