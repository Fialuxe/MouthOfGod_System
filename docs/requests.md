# 欲しい物（要求）の仕組みと、分担

> 状態: 案（PR #251）。Unity では EditMode のテストと、`Village.prefab` の作り直し（Bake）まで確認。Play での見た目は未確認。名前と細部は仮。
>
> 読む順番: 何のための仕組みかは「これは何か」、フィールドを作る人は「フィールドの作り方」、ほかの機能を作る人は「API 仕様」。

山・海・草原・村の 4 つのフィールドと、チュートリアル・本編・吹き出しを、**別々の人が同時に作れる**ようにするための約束です。言葉は [fields.md](fields.md) にそろえます（「要求」「困りごと」は、**欲しい物**）。

## これは何か（考え方）

### ゲームの上で起きること

体験者は神様です。飢饉の村のまわりに、山・海・草原・村の 4 つのフィールドがあり、それぞれに困っている人がいます。困っている人は、頭の上の吹き出しで欲しい物を見せます。体験者はそのフィールドの前まで歩き、口から欲しい物を生み出します。届くと、そのフィールドの一部が戻り（畑に芽が出る、羊が戻る）、困っていた人が元気になって喜びます。これを 2 分くり返し、届けた分だけ村が豊かになります（[シナリオブック](scenario-book.md) S5〜S8）。

### なぜ仕組みが要るのか

この体験は、次の 3 つの問いに答え続けることでできています。

| 問い | 例 | 答える所 |
|---|---|---|
| 何が欲しいか、誰が欲しがるか | 村の畑の人が、にんじんを | フィールド（`FieldStep` のデータ） |
| いつ、どのフィールドに出すか | 最初の 30 秒は 1 つずつ、あとは同時に最大 2 つ。T3 では海に魚 | 本編（Game）とチュートリアル（Tutorial） |
| 届いたら何が変わるか | 芽が出て、畑の人が喜ぶ | フィールド（`FieldStep` の子・消す物・助かる人） |

**3 つを別々の所が答える**のが要点です。

- 全部を 1 か所（`GameManager` のようなクラス）に書くと、4 つのフィールドやチュートリアルを直すたびに全員が同じファイルを触り、並行して作れなくなります（[architecture.md](architecture.md) §1）。
- 逆に、フィールドがそれぞれ勝手に欲しい物を出すと、4 つがばらばらに吹き出しを出して、体験者が追いきれません。
- だから「いつ」だけを 1 か所に集め、「何が欲しいか」と「何が変わるか」はフィールドに分けます。

分けた所どうしが決まった言葉で話すための約束が、`IFieldRequests`（呼ぶ側が使う窓口）と、`RequestIssued` / `RequestClosed`（出た・終わったの知らせ）です。中身は下の「API 仕様」にあります。

### Unity の上でどう動くのか

この仕組みで使っている Unity の道具は、次のものだけです。

| Unity の言葉 | 何か | この仕組みでは |
|---|---|---|
| GameObject | シーンや Prefab の中の「物」。位置を持ち、親子に並べられる | 家・村人・芽・ステップ |
| コンポーネント（MonoBehaviour） | GameObject に付けるスクリプト。付けた物の上で動く | `FieldStages`・`FieldStep`・`VillagerMood` |
| Hierarchy | 物の親子と並び順 | ステップの順番そのもの |
| Prefab | 物の組み合わせを保存したファイル。シーンに置ける | `Village.prefab` が、村のフィールド 1 つ |
| `SetActive` | 物を出す・消す。消えた物は描かれず、付いたスクリプトも止まる | ステップの子を出す、枯れた茎を消す |
| `Start` | Play を始めてから、最初のフレームの前に 1 回呼ばれる | はじめの見た目にする（全部のステップがまだ） |
| Animator | アニメーションの状態を切り替える | 村人の悩む・うなだれる・喜ぶ |
| C# の `event` | 「起きたら知らせて」と登録しておく仕組み。知らせる側は、誰が聞いているかを知らない | `Issued` と `Closed` |
| interface | 「このメンバーを持っている」という約束だけの型 | `IFieldRequests`。中身が `FieldStages` でも独自の作りでも、同じように使える |
| asmdef | スクリプトのまとまり。どのまとまりを参照してよいかを決める | Game・UI・Tutorial は Village を参照できない。Core だけを見る |

フィールドの「状態」は、どこかに表で書かれているのではありません。**物が出ているか消えているか（`SetActive`）として、目に見える形で表れます。** `FieldStages` が覚えているのは「どのステップが済んだか」だけで（`StepProgress`）、それが変わるたびに見た目に写し直します。

### 1 回届けるときに、実際に起きること

1. **Play を始める**: `FieldStages.Start` が、その時点の進み具合を見た目に写す（ふつうは、全部のステップがまだの見た目）。ステップの子は消え、`Hide When Done` の物（枯れた茎）は出て、`Helped` の人は悩む。
2. **出す**: 本編（Game）が、そのフィールドの `CanOpen` を見て `Open()` を呼ぶ。
3. `FieldStages` が、まだ済んでいない最初のステップを選んで `Issued`（にんじん・村・畑の人の頭）を出す。吹き出し（UI）がそれを聞いて、頭の上に吹き出しを出す。
4. **届く**: 体験者が村の前でにんじんを生み出す。DeliveryZone が「村に、にんじんが落ちた」と判断して `Resolve(にんじん)` を呼ぶ。
5. `FieldStages` がそのステップを「済み」にして、見た目を写し直す。芽が出て、枯れた茎が消え、畑の 2 人が元気になり、元気な人がみんな喜ぶ（`Victory`）。
6. **終わりを知らせる**: `Closed`（`Resolved = true`）が出る。UI は吹き出しを消し、Game はスコアを足して、すぐ次の `Open()` を呼べる。そのあと、音・光（`On Done`）とフィールドごとの演出（`StepDone`）が動く。

違う物（かぼちゃ）が届いたら、`Resolve` は false を返し、何も変わりません。20 秒たっても届かなければ、Game が `Expire()` を呼び、`Closed`（`Resolved = false`）が出ます。

### なぜ、これで動くと言えるのか

- 上の流れ（出す → 届く → 見た目が変わる → 次を出す）と、違う物は受け付けない・待ちきれない・やり直す・ステップを途中に挟む、を EditMode のテスト（`StepProgressTests`・`FieldStagesTests`）で確かめている。テストは、Unity の中で実際に GameObject を作って動かしている。
- `Village.prefab` は Bake で作り直し、ステップ・欲しい物・助かる人・消す物が入っていることを確かめた。
- **まだ確かめていないこと**: Play での見た目（アニメーションの切り替え、戻る順番が目で見て自然か）。PR #251 の「Unity で確かめること」の手順で見る。

## まとめると（1 行で）

**フィールドは「何が欲しいか」と「届いたら何が変わるか」だけを持つ。「いつ・どこに出すか」は、本編（Game）とチュートリアル（Tutorial）が決める。** 両者をつなぐのは Core の `IFieldRequests` だけ。

- フィールドの担当は、時間や他のフィールドのことを考えなくてよい。
- 4 つのフィールドがばらばらに動かず、調子がそろうのは、出す順番と間隔を 1 か所（本編なら Game）で決めるから。

## フィールドの作り方（コードは要らない）

戻り方は、**Hierarchy の並び順**で決まります。番号はどこにも書きません。

```
Village                 ← FieldStages（フィールドに 1 つ。ID は village / mountain / sea / grassland）
├─ 家・井戸・村人…       ← どのステップにも入らない物は、はじめからずっとある
└─ Steps
   ├─ 1 Sprout           ← FieldStep: 欲しい物 carrot / 困りごとが解ける人 畑の 2 人 / 消す物 枯れた茎
   │    └─ 芽 ×16        ← このステップが済むと出る（ステップの子）
   ├─ 2 Crops            ← FieldStep: pumpkin / 井戸の人 / 茶色い木・芽
   │    └─ 作物・緑の木
   ├─ 3 Sheep
   │    └─ 羊
   └─ 4 Pig
        └─ 豚
```

`FieldStep` が持つのは 3 つだけです。

| 項目 | 意味 |
|---|---|
| Food | 欲しい物の FoodId（吹き出しに出る物）。空なら欲しい物は出さない |
| Helped | このステップで困りごとが解ける人（`VillagerMood`）。**1 人目の頭の上に吹き出し**。名前のあるステップがすべて済むと、悩むのをやめて元気になる |
| Hide When Done | このステップが済むと消える物（どこに置いてあってもよい） |

ほかに、済んだときの音・光を Inspector でつなぐ `On Done` があります。

- **ステップを足す・挟む**: `Steps` の下に空のオブジェクトを作って `FieldStep` を付け、好きな位置へドラッグし、出す物をその子に入れる。ほかのステップは直さない。
- **消す・並べ替える**: 消すか、ドラッグするだけ。
- 村人のアニメーション（悩む・うなだれる・喜ぶ）は `VillagerMood` の中にあり、ステップの作りとは関係なく動く。
- 変わる物（木・動物・作物など）は、普通のオブジェクトとして置く。Terrain の機能で塗った木や草は、ステップで出し入れできない（地形そのものは変えない方針。[decisions.md](decisions.md) 2026-10-13）。

### 1 人で確かめる

サンドボックスのシーンで Play し、`FieldStages` の右クリックメニューを使う（開発中の確認用。展示では Game・Tutorial が呼ぶ。ブースではキーボードも Inspector も使わない）。

| メニュー | すること |
|---|---|
| Open Next Want | 次のステップの欲しい物を出す（Console に `wants ...`） |
| Deliver Wanted Food | 出ている欲しい物が届いたことにする（見た目が変わり、村人が喜ぶ） |
| Complete Next Step | 欲しい物を通さずに、次のステップを済ませる |
| Restart | はじめに戻す |

## 流れ

```
                      ┌─ Game（本編）: 最初の 30 秒は 1 つずつ、あとは同時に最大 2 つ。20 秒で Expire
  いつ・どこに出すか ─┤
                      └─ Tutorial（T3・T4）: 「海に出す」をステップのデータで指定
              │ IFieldRequests.Open() / Expire() / CanOpen    （Core。Bootstrap がフィールドの数だけ渡す）
              ▼
  フィールド（いつもは FieldStages。独自の作りでもよい）
              │ RequestIssued(Food, Field, Anchor)   → UI: 吹き出しを Anchor の上に出す
              │
  プレイヤーが、そのフィールドの前で物を生み出す（届く）
              │ IFieldRequests.Resolve(food)         ← 届いたかを決める所（DeliveryZone）
              ▼
  ステップが済む: 子が出る・消す物が消える・村人が元気になって喜ぶ
  RequestClosed(Resolved = true)                     → UI: 吹き出しを消す / Game: スコア / Tutorial: 次へ
```

待ちきれなかったとき（Game が `Expire()`）は `RequestClosed(Resolved = false)` が出て、ステップは済まない。

## API 仕様

ほかの機能とフィールドの合わせ目の約束です。**この節と `Assets/Scripts/Core/IFieldRequests.cs` のコメントを正とします。** 形（メンバー・意味・イベントの順番）を変えるときは、使う側すべてに効くので別の PR にし、この節も一緒に直します。

### 共通の前提

- すべてメインスレッドから呼ぶ。イベントは、呼び出しの中ですぐに（同期で）出る。
- 1 つのフィールドに、出ている欲しい物は**最大 1 つ**。
- 1 つのフィールドでは、`Issued` と `Closed` が**必ず交互に**出る（`Issued` → `Closed` → `Issued` …）。
- **状態を変えてから知らせる**。イベント（`Closed`・`StepDone`・`On Done`）の中から、同じフィールドの `Open` などをすぐ呼んでよい（新しい状態で動く）。見た目を写し直している最中（出し入れした物の `OnEnable` の中）の `Open` は、`Closed` を出す前なので false になる。
- フィールドの ID は `village` / `mountain` / `sea` / `grassland`（[fields.md](fields.md)）。大文字と小文字は区別する。

### `IFieldRequests`（Core）

| メンバー | 呼ぶ人 | すること | 戻り値・出るイベント |
|---|---|---|---|
| `Field` | 誰でも | このフィールドの ID | — |
| `IsOpen` | 誰でも | 欲しい物が出ているか | — |
| `CanOpen` | Game・Tutorial | 次を出せるか（出ているものが無く、まだ欲しい物が残っている。`Food` が空のステップは数えない） | — |
| `Open()` | Game・Tutorial | 次の欲しい物を出す | 出せたら true で、`Issued` が 1 回。出ている・残りが無いなら false で、イベントは出ない |
| `Resolve(food)` | DeliveryZone | 物がこのフィールドに届いた | 出ている物と同じなら true で、見た目が変わってから `Closed`（`Resolved = true`）。違う物・出ていないなら false で、何も変わらない |
| `Expire()` | Game（待ち時間切れ） | 出ている欲しい物を、届かないまま終える | 出ていれば `Closed`（`Resolved = false`）。出ていなければ何もしない |
| `Restart()` | Bootstrap（体験の終わり） | はじめに戻す | 見た目を戻してから、出ていれば `Closed`（`Resolved = false`） |
| `Issued` | UI・Game・Tutorial が聞く | 欲しい物が出た | `RequestIssued` |
| `Closed` | UI・Game・Tutorial が聞く | 欲しい物が終わった | `RequestClosed` |

### イベントのデータ（Core）

| 型 | 中身 |
|---|---|
| `RequestIssued` | `Food`（欲しい物）、`Field`、`Anchor`（吹き出しを出す場所の Transform。ミニチュアと一緒に動くので位置ではなく Transform。無ければ null） |
| `RequestClosed` | `Food`、`Field`、`Resolved`（true = 届いた、false = 待ちきれなかった・やり直した） |

### `FieldStages`（Village。いつもの実装）だけにあるもの

Village の中と、フィールドの担当のスクリプトから使います。Game・UI・Tutorial は使いません（`IFieldRequests` だけを見る）。

| メンバー | すること |
|---|---|
| `StepDone`（event、済んだ `FieldStep`） | ステップが済んだ。見た目が変わり、`Closed` が出た後に出る（`FieldStep` の `On Done` も同じとき）。フィールドごとの演出を足すのに使う |
| `Restarted`（event） | はじめに戻した。`Closed` の後に出る |
| `Steps`・`DoneCount`・`IsFullyRecovered`・`IsDone(step)`・`Current` | 読むだけ |
| 右クリックメニュー（Open Next Want など） | 開発中の確認用。展示では使わない |

### `FieldStep` の決まり

- `FieldStages` の子孫に置く（ふつうは `Steps` の下）。`FieldStep` の中に `FieldStep` を入れない（入れると、それも 1 つのステップとして数える）。
- 並び順は、Play を始めて最初に使ったときに 1 回だけ読む。Play 中に足した・動かしたステップは、次の Play から効く。
- `Food` が空のステップは、`Open` で飛ばされる（開発中の Complete Next Step でだけ済ませられる）。
- `Hide When Done` に入れた物と、ステップの子の出し入れは `FieldStages` が受け持つ。ほかのスクリプトから `SetActive` しない（次に見た目を写し直すときに戻される）。
- 新しいフィールドでは、`FieldStages` の `Field Id` を自分のフィールドの ID にする（既定は `village`）。

### 独自の実装で守ること

`IFieldRequests` を自分で実装するときも、上の「共通の前提」と表の約束を守る。特に、同時に 1 つ、`Issued` と `Closed` が交互、状態を変えてから `Closed`、の 3 つ。守れば、本編・チュートリアル・吹き出しは中身を知らずに動く。

## フィールドの中の決まり

| 決まり | 理由 |
|---|---|
| 1 回届く = 1 ステップ | 戻り方が目に見えて分かる |
| 次に出すのは、まだ済んでいない**最初の**ステップ | 草原の「鶏 → 豚 → 牛 → 象」のように、戻り方の物語を担当が決められる（fields.md） |
| 1 つのフィールドに、**同時に出る欲しい物は 1 つ** | 吹き出しが重ならない |
| 済んだかは、ステップごとに持つ（何番目まで、とは数えない） | 順番を自由にしたくなっても、「次にどれを出すか」（`StepProgress.NextIndex`）の 1 か所を変えるだけで済む |

プレイヤーが**どのフィールドから**助けるかは自由（吹き出しは同時に最大 2 つ出ていて、どちらに応えるかはプレイヤー次第）。

## 誰が何を作るか

| 担当 | 作るもの | 触るファイル | 待たなくてよいもの |
|---|---|---|---|
| フィールド（山・海・草原・村、各 1 人） | 上の「フィールドの作り方」 | 自分のフィールドの Prefab かサンドボックスだけ | 本編・チュートリアル・吹き出し |
| 本編（Game、#81） | いつ・どのフィールドに出すか（同時の数、待ち時間、難しさ）。`CanOpen` を見て `Open`、時間切れで `Expire` | Game のコードとデータ | フィールドの中身。テストでは `IFieldRequests` の偽物を使う |
| チュートリアル（Tutorial、#60・#50） | ステップのデータに「どのフィールド（`FieldId`）に出すか」を書き、`Open` を呼ぶ。`Closed` で次へ | Tutorial のコードとデータ | 同上 |
| 吹き出し（UI、#59） | `Issued` で `Anchor` の上に出し、`Closed` で消す（`Resolved` で喜ぶ・がっかりを分ける） | UI のコード・Prefab | 同上 |
| 届いたかを決める所（DeliveryZone） | 物がそのフィールドに落ちたら、そのフィールドの `IFieldRequests.Resolve(food)`。戻り値を `Delivered` の `Accepted` に使う | DeliveryZone のコード | — |
| 組み立て（Bootstrap、#34） | シーンの `IFieldRequests` を全部集め、Game・Tutorial・UI に渡す。体験の終わりに各フィールドを `Restart()` | Bootstrap のコード | — |

- Game・Tutorial・UI・DeliveryZone・Bootstrap は、`FieldStages` を直接使わない（Bootstrap も集めるときに `IFieldRequests` として扱う）。Village のクラスを直接参照しない（asmdef で禁止。[architecture.md](architecture.md) §2）。見るのは Core の `IFieldRequests`・`RequestIssued`・`RequestClosed` だけ。
- `IFieldRequests` の形（メンバーと意味）を変えるときは、使う側すべてに効くので、別の PR にする。

## 4 人が別々の作りにしても、合わせられるか

合わせられる。合わせ目（統合の境目）は `IFieldRequests` の 1 つだけで、その内側はフィールドごとに自由。

| どこまで独自にするか | やり方 | ほかへの影響 |
|---|---|---|
| 見た目の演出だけ（魚が船の上で跳ねる、動物が歩き回る） | ステップの子の物に自分のスクリプトを付ける。済んで出たとき（`OnEnable`）に動き出す。または `FieldStages.StepDone` を受ける | なし |
| 音・光 | `FieldStep` の `On Done` に Inspector でつなぐ | なし |
| フィールドの仕組みごと（ステップでは表せない、独自の進み方） | 自分の MonoBehaviour で `IFieldRequests` を実装する（Core だけを参照）。`FieldStages` は使わなくてよい | なし。Bootstrap が `IFieldRequests` として集めるので、本編・チュートリアル・吹き出しはそのまま使える |

本編の出し方（Game）も、`IFieldRequests` しか見ないので、フィールドの作りが変わっても直さなくてよい。

## 足りないもの（この仕組みの外で作る）

- 吹き出し（#59）、届いたかを決める所（DeliveryZone）、組み立て（#34）、本編の出し方（#81）。
- 待ちきれなかったときの村人の反応（がっかり）。今の `VillagerMood` は「済んだか」しか見ない。`RequestClosed(Resolved = false)` を見て反応させるのは、そのとき足す。
- 欲しい物の ID（村の carrot・pumpkin・sheep・pig）は仮。食べ物の ID は、食感と食べ物の対応（#31）で決まる。

## 採用しなかった案

- 全体で 1 つの要求の並び（`RequestSet`）を順に出す（PR #251 の最初の形）: 4 人が同じアセットを触る。どの村人の上に出すか分からない。本編の「同時に最大 2 つ・20 秒で消える」を表せない。
- 段階を番号で持つ（main にあった `StageVisibility` の「何段目から何段目まで」、`VillagerMood` の `calmFromStage`）: 途中に段階を挟むと、後ろの番号を全部ずらす必要があり、ずらし忘れても気づけない。
