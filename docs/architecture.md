# 設計方針

> **状態: 案（レビュー待ち）**。Issue #29（全体設計）の内容を、リポジトリに残すために移したものです。名前や細部は仮で、決まっていないことは #29 の「人間に決めてほしいこと」にあります。直すときは、この文書を PR で直してください。

スクリプトの置き場所と書き方の基本は [CONTRIBUTING.md「6. スクリプト方針」](../CONTRIBUTING.md#6-スクリプト方針) にあります。この文書は、その上にある「機能どうしをどうつなぐか」と「避けること」をまとめます。

## 方針（1 行）

**「Core の小さな型・interface」と「ScriptableObject のデータ」を中心にし、機能は互いを知らず、一方向のイベントの連鎖でつなぐ。`GameManager` は作らない。**

## GDC 前に守ること、後でよいこと

締切（応募は 10/30）も大事なので、全部を最初から完璧にはしない。ただし、**後から直すと全体の作り直しになるもの**は、GDC 前から守る。そうしないと、いつか大きな問題になる。

| GDC 前から守る（後から直しにくい） | 後でよい（あとから足せる） |
|---|---|
| 中央の進行クラス（`GameFlow` / `GameManager`）を作らない | 全機能に asmdef を置く（まず `Core` と `Device`） |
| 共有シーンは 1 つにし、Scene Fusion で一緒に編集する | 完了条件をクラスやアセットに分ける（`switch` が複雑になってから） |
| Device は `IDevice` 越しに使い、`MockDevice` だけで動く | データの検証ツール、エディタ拡張 |
| 機能どうしは直接参照せず、イベントか interface でつなぐ | ScriptableObject のイベントチャンネルなどの仕組み |
| Play したあとに手作業が要らない（CONTRIBUTING） | テストの網羅 |
| 食べ物などのデータは 1 件 1 アセット | |

## 1. 同じファイルを取り合わない

いちばん大事な目的です。

**何が問題か。** Unity の `.unity`（シーン）、`.prefab`、`.asset`（ScriptableObject）は Git でマージがほぼできない（CONTRIBUTING 第 3 章）。2 人が別々に同じものを触ると片方を捨てることになるので、**同じファイルを別々に触る作業は並行できず、順番待ちになる。**（シーンは例外で、Scene Fusion で一緒に編集する。下記） C# のファイルは Git でマージできるので、待ちはそれほど生まれない。ただし、たとえばチュートリアル 1 と 2 が同じ `GameFlow` クラスの中に書かれていると、片方の変更がもう片方を壊しやすく、どちらを直すにも全体を読む必要がある（結合が強い）。

そこで、作業の単位とファイルの単位をそろえる。

- **共有シーンは 1 つ（例: `Main`）にし、分けない。** 村全体と全フィールドを 1 つのシーンに置き、複数人で同時に編集するときは Scene Fusion を使う（CONTRIBUTING 第 3 章）。シーンを分けると、加算ロードや位置合わせのつなぎ、他の人のシーンと合わせる手間が増え、このチームには重い。シーンの外で別々に作業したいものは、データ（下の 3 つ）やコードに出す。
- **1 チュートリアル = 1 つの人形劇スクリプト。** チュートリアルごとに 1 つのファイル（`Assets/Scripts/Tutorial/T1.cs`、`T2.cs`）を書き、そのチュートリアルにしかない物（浮かぶお供え物、影など）の Prefab は `Assets/Resources/Prefabs/Tutorial/` に置く。チュートリアル 2 を直す人は、チュートリアル 1 のファイルを開かない。
- **1 件 = 1 つのまとまり。** 食べ物は Prefab 1 つ（ファイル名が食べ物の ID）、フィールド・ステップなどは、1 件ごとに別の Prefab かアセットにする。全部を 1 つの巨大なリストやファイルに入れない。
- **「食感 → 食べ物」の対応表は、全員で触る 1 つのリストにしない。** 全フィールド共通の表（`FoodTable`。0 個か 1 個）と、フィールドごとの専用の表（そのフィールドの Prefab に付ける `FoodTable`）に分ける。食べ物を足す人は、Prefab を置いて、自分のフィールドの表に 1 行足す。食感の重複などは、起動時に警告する（2026-10-19 の [決めたことの記録](decisions.md)。作りは [Food/README.md](../Assets/Scripts/Food/README.md)）。
- **中央の進行クラスを作らない。** `GameFlow` や `GameManager` の中に「チュートリアル 1 ならこう、2 ならこう」と書かない。進行を担う `TutorialRunner` は**どのチュートリアルでも同じコード**で、中身（流れ）は、チュートリアルごとの人形劇スクリプト（`TutorialScript`）に書く。
- **PR が同じファイルでぶつかったら、分け方を見直すサイン**として扱う。

> 未確認: ScriptableObject を Scene Fusion で同時に編集したときの挙動は、#43 で確かめる。

### `switch` で分けるか、ファイルを分けるか

その時々で決める。種類（例: ステップの完了条件の `Kind`）が少なく、各 `case` が数行で済むうちは、`enum` と `switch` でよい（#33、#21 の `Narration` もこれ）。次のどれかになったら、種類ごとのクラスやアセットに分ける。

- 1 つの `case` が長くなった、または種類ごとに自分の状態（タイマーなど）を持ち始めた
- 複数の人が、同じ `switch` に別々の `case` を足すようになった
- `switch` が複数の場所に散らばり、種類を 1 つ足すたびに何か所も直す

### シーンを 1 つにするときの約束

- シーンを編集するのは、**Scene Fusion のセッションの中だけ**。セッションの外で、別々の人が別々の時間に同じシーンを編集してコミットしない（Git ではマージできない）。セッションのホストだけがコミットする（CONTRIBUTING 第 2 章 B・第 3 章）。
- 機能を作るときの動作確認は、自分のサンドボックスのシーン（`Assets/Scenes/Sandbox/`）で行い、共有シーンには入れない。
- フィールドは、AprilTag で合わせた村の基準（`VillageRoot`）の子として置く。同じシーンなので、エディタで普通に親子にできる。
- Scene Fusion が使えなくなったとき（席が足りない、不安定）の対処は、CONTRIBUTING「席が足りない・接続できない場合」に従う。シーンを分けるのは、その時に決める。採用しなかった「フィールドごとにシーンを分ける案」（加算ロード、実行時に村の基準へつなぐ方法、利点とコスト）は [決めたことの記録](decisions.md) に残してある。

## 2. 機能と依存の向き

```
Core        型（TextureId / FoodId / FieldId）、小さな interface、イベントの型。ロジックは載せない。誰にも依存しない
 ↑
Device      IDevice の実装（Serial / Mock）                      Core のみ
Tracking    AprilTag・位置                                       Core のみ（現状は Core も不要）
Food        食感→食べ物の対応、生成（FoodTable / FoodSpawner）     Core のみ
Village     Field・DeliveryZone・村人の要求・村の変化              Core のみ（位置合わせは「親 Transform が動く」ことにだけ依存）
UI          表示。Core のイベントを購読                           Core のみ
 ↑
Tutorial    チュートリアルの進行（人形劇スクリプト）               Core のみ。Game とは互いに依存しない
Game        本編（時間・スコア）                                  Core のみ
```

全機能を参照する組み立て役（Bootstrap）は置かない（2026-10-15。[決めたことの記録](decisions.md)）。

- 横の機能どうし（Device ↔ Food ↔ Village ↔ UI）は**相互参照を禁止**し、asmdef で強制する。
- 参照できるのは「下の層だけ」。Tutorial が Device や Village のクラスを直接叩かない。
- 機能どうしは、Core の置き場でつながる。**機能をまたいで使う「もの」（デバイス・フィールドなど）は、Core の置き場に自分で登録し、使う側はそこから受け取る。** やりとりの中身は、その「もの」の interface とイベントで行う。置き場は「もの」にだけ作り、知らせ 1 つ 1 つには作らない。いまある置き場は `Devices` と `Fields`（一覧は Core の README）。 Inspector での手配線に頼らない（CONTRIBUTING「最終的に Inspector／Editor の操作が要らないシステムにする」）。

## 3. 流れはイベントの連鎖にする

```
Device   --TextureChanged / Confirmed-->  Food(FoodSpawner)  --Spawned-->  UI / Tutorial
Game / Tutorial --IFieldRequests.Open()--> Village(FieldStages)  --RequestIssued-->  UI(吹き出し)
プレイヤーが、生まれた物を DeliveryZone へ運ぶ
DeliveryZone --IFieldRequests.Receive(food)--> Village(FieldStages: 頼まれた物なら村が変わる)  --Delivered(Kind)--> Game(採点) / 反応
Village(FieldStages) --RequestClosed(Resolved?)--> UI(吹き出しを消す) / Tutorial / Game
```

- Tutorial は Device を直接見ない（`MockDevice` に差し替えても動く）。
- 欲しい物は、フィールドが「何が欲しいか・届いたら何が変わるか」を持ち、「いつ・どこに出すか」は Game（本編）と Tutorial が決める。分担と流れは [requests.md](requests.md)。
- Tutorial から村や UI への指示（「この場所を光らせる」など）は、Tutorial が Core の `Cues.Request(CueId)` で合図を出し、Village や UI が `Cues.Requested` を購読する（依存の向きを保つため）。見せ方は、受けた側が決める。合図の名前はフィールド名から始める（[requests.md](requests.md)）。
- フィールドや UI が「いまのチュートリアル」で動きを分けたいときは、Core の `Phases.Current`（`PhaseId`。`fork` `spawn` のような名前）を読んでよい。番号ではなく名前にする。ふつうの反応は合図で足りるので、`if` はそのフィールドだけの特別な反応に使う。
- 機能の中は C# の `event`。機能をまたぐものは、Core の interface とイベントで、相手は Core の置き場から受け取る。ScriptableObject のイベントチャンネルや、知らせの通り道（イベントバス）は採用しない（流れが追えなくなり、返事や順番の約束を守らせにくい）。
- `IDevice` は `Devices` から（`Devices.WhenReady`。どのデバイスを登録するかは Device 側が決める）、フィールドは `Fields` から（`Fields.All`・`Fields.Added`）受け取る。どのシーンでも、置くだけでつながる。
- 置き場を足してよいのは、機能をまたいで使う「もの」が増えたときだけ。いまある置き場は `Devices`・`Fields`・`Cues`・`Phases`（いまの場面）。候補は「体験者がいまいるフィールド」で、使う側を作るときに足す。置き場が膨らんだら、持ち主に聞くだけの窓口（ファサード）で包んでもよい。ただし、状態や判断は持たせない（`GameFlow` と同じになる）。置き場は static なので、Play のたびに空にする（`Reset`、`RuntimeInitializeOnLoadMethod`）。

### 型とイベント（案。名前は仮）

| 用途 | 型・イベント | 置き場所 |
|---|---|---|
| 食感 | `TextureId`（論理 ID。物理量は Device の内側で ID に変換する） | Core |
| 入力 | `IDevice`: `Current`、`TextureChanged`、`Confirmed`（決定したときの食感を運ぶ）、`Bitten` | Core（interface）／ Device（実装） |
| 食べ物 | `FoodId`、`FoodTables.Resolve(FieldId, TextureId)` → 食べ物の Prefab、`FoodSpawner.Spawned`、生んだ物の `IFoodItem`（どの食べ物かを渡す） | Core（`FoodId`・`Spawned`・`IFoodItem`）／ Food |
| 届け先 | `FieldId`、`IFieldRequests.Receive(FoodId)` → `DeliveryKind`、`Delivered`（`Kind` 付き） | Core ／ Village |
| 欲しい物（要求） | `Fields`（置き場）、`IFieldRequests`（`Open`・`Receive`・`Expire`・`Restart`）、`RequestIssued`（`Anchor` 付き）、`RequestClosed` | Core（interface・イベント）／ Village（`FieldStages`・`FieldStep`） |
| 進行 | `TutorialRunner`・`TutorialScript`（人形劇スクリプト）、`Phases`（いまの場面）、`Cues`（合図。`CueRequested` を運ぶ） | Tutorial ／ Core |

- 食感・食べ物を `enum` で Core に直書きしない。追加のたびに全機能が再コンパイルされ、素材担当が触れなくなる。ID は文字列にする（決定。CONTRIBUTING「型の名前とデータの置き場所」）。

### チュートリアルのあいだでデータを引き継ぐ

チュートリアル 1〜4 は、互いを知らない（T3 のコードに「T2 では」と書かない）。それでも、「T2 で覚えた食感の対応」のようなことは、次のチュートリアルに引き継ぐ必要がある。そこで、**引き継ぐものを、名前の付いた「覚えたこと」にして、`TutorialRunner` が持つ `TutorialMemory` で読み書きする。** Core の interface は要らない（2026-10-19 の [決めたことの記録](decisions.md)。前の案の `IProgress` はやめた）。

- 人形劇スクリプトは、`Memory.Set("texture-mapping-learned")` で覚え、`Memory.Has("texture-mapping-learned")` で読む。T3 は、T2 のことではなく、**名前だけ**を見る。T2 を直しても、名前を変えなければ、T3 は壊れない。
- 名前は「事実の名前」にする（`t2-done` のように、特定のチュートリアルを指す名前は避ける）。値を持たせるのは必要になってから。
- **名前は、1 か所の一覧に書く**（`docs/progress-keys.md`、足す PR で一緒に足す）。一覧にない名前を、コードに書かない。名前が人によってずれると、見えない結合になる。
- `TutorialRunner.Begin()` で空にする。次の体験者に引き継がない。
- 保存（電源が切れても続きから）が必要になるまでは、メモリに持つだけでよい。

## 4. 素材・データ・実装を分ける

| 区分 | 持つもの | 主な担当 |
|---|---|---|
| 素材（Art） | モデル、テクスチャ、アニメーション、SE、Particle | 素材担当 |
| Prefab | 食べ物・生物、フィールド、村人の見た目と Collider | 素材担当 ＋ シーン担当 |
| データ（ScriptableObject など） | `FieldDefinition`（欲しい物はフィールドの Prefab の `FieldStep` に持つ）。「食感 → 食べ物」の表は `FoodTable`（Prefab に付ける部品） | 企画・シーン担当（実装は読むだけ） |
| シーン | 村・フィールドの配置、AprilTag の位置 | シーン担当（Scene Fusion） |
| コード | 表の参照、完了条件の評価、判定、Device 通信、起動時の組み立て | 実装担当 |

素材にタグ（食感・置き場所）を埋め込まず、データ側で持つ。素材を差し替えても、コードとシーンを触らずに済む。

### データ（アセット）の作り方（LLM エージェントが実装するとき）

ScriptableObject や Prefab の `.asset` / `.prefab` は、GUID と参照を含む YAML なので、**エージェントが手で書かない**（壊れても気づきにくい）。作業している人の状況に合わせて、次のどれかで作る。

1. **人に頼む**: エディタでの手順（メニュー、作る場所、入れる値）を具体的に書いて、人に作ってもらう。数が少ないとき。
2. **Unity の MCP を使う**: エディタを操作できる MCP がつながっていれば、それで作る。
3. **一時的なエディタメニューを作る**: `Editor/` に「データを作る」メニューのスクリプトを書き、人に 1 回実行してもらう。**作り終えたらスクリプトは消す**（PR に「一時的なもの」と書き、同じ PR かすぐ後の PR で削除する）。

どれで作っても、Play したときにデータを確かめる（足りない参照、重複した ID をエラーにする）。

## 5. デバイス連携（`Device/`）: 旧プロジェクトの反省

旧プロジェクト [MealBeBack](https://github.com/Fialuxe/MealBeBack) のシリアル通信まわりで良くなかった点と、今回の方針です。

| 旧プロジェクトで気になった点 | 方針 |
|---|---|
| ゲームの状態（`UserChoice` など）を、通信のクラスが持っている | 通信は**バイトと行の送受信だけ**。ゲームの意味づけは上位に任せる |
| ポート名が `COM3` 固定で、開けないと無効化されて再試行しない | 設定から渡す。自動検出と再接続を行う |
| 駆動中に次のコマンドを送ると状態がずれる、という規約を呼び出し側が守る必要がある | **ファサード側でキューイングし、駆動中は送らない**ことを保証する |
| 2 台前提（`A` / `B`）で、同じ役割のクラスが 2 つある | 台数を固定しない。役割は 1 つのクラスに集約する |
| 終了時の `Thread.Sleep` で送信を待つ | 終了処理は非同期に完了を待つ |

- 層を分ける: **トランスポート**（シリアルの読み書き・別スレッド）→ **プロトコル**（コマンドの組み立てと解釈）→ **ファサード**（ゲームから呼ぶ API）。
- ゲームは `interface IDevice` だけを見る。実機の実装と、**キーボードなどで動く擬似実装（Mock）**を用意し、デバイスなしで開発・動作確認できるようにする（#7、#46）。

### 入力の流れと、デバイスなしで試すキー操作

噛み始め → 食感が時間で切り替わる → ボタンで決定、の順に `IDevice` のイベントが出る。

1. 噛み始め: `Bitten`
2. 噛んでいる間: 一定間隔で `TextureChanged(TextureId)`（噛み始めの直後に最初の食感も出る）
3. ボタンで決定: `Confirmed(TextureId)`。決定したときの食感を運ぶ（キャンセルはない）

`MockDevice`（`Device/`）では、Quest 右コントローラのトリガー（キーボードなら Space）を押し続けると噛み、右 A ボタン（Enter）で決定する（暫定。操作の割り当て・間隔・食感 ID は `Device/README.md` と Inspector）。手順は [device-mock.md](device-mock.md)。

## 6. 避けること（アンチパターン）

| 避けること | なぜ困るか | 代わりに |
|---|---|---|
| `GameManager` / `GameFlow`（や、全部をつなぐ Bootstrap）が Device・Village・UI・各チュートリアルを全部持つ | 全員がそのファイルを編集するので順番待ちになる。1 か所の変更が全体を壊す | 機能ごとに分け、イベントでつなぐ。相手は Core の置き場から受け取る |
| チュートリアルごとの処理を、共通クラスの `switch` や `if` で分ける | チュートリアルを足す・直すたびに共通ファイルを編集し、互いを壊しやすい | `TutorialRunner` は共通、中身はチュートリアルごとの人形劇スクリプト |
| 全部のデータを 1 つのアセットに入れる | マージできないので、同時に編集できない | 1 件 1 アセット |
| 対応表などの「目次」を、手で編集するリストにする | 何かを足すたびに全員が同じファイルを触る | 1 件ずつのアセットを集めて作る |
| 共有シーンを、Scene Fusion のセッションの外で別々に編集してコミットする | Git でマージできず、どちらかの作業が消える | 同時に編集するときは Scene Fusion。動作確認はサンドボックスで |
| エージェントが `.asset` / `.prefab` の YAML を手で書く | GUID や参照が壊れても気づきにくい | 人に頼む、MCP、一時的なエディタメニュー（使ったら消す） |
| Device がゲームの状態（旧 `UserChoice`）を持つ | 通信とゲームの都合が絡み、Mock に差し替えられない | Device は入力を ID とイベントにするだけ |
| Tutorial が Village の Field などを直接叩く | 依存の向きが崩れ、片方だけ直せなくなる | Core の `Cues`（合図）や `Phases` 経由 |
| 食感・食べ物を `enum` で Core に直書き | 追加のたびに全体が再コンパイル。素材担当が触れない | 文字列 ID か ScriptableObject |
| `static` なシングルトンの乱用、`FindObjectOfType` / `GameObject.Find` | どこから使われているか追えない。テストで差し替えられない | Core の置き場（`Devices`・`Fields`）から受け取る。置き場は Core の README の一覧にあるものだけ |
| 機能をまたぐ `[SerializeField]` の手配線 | Play の前に手作業が要る。付け忘れで動かない | 自分で Core の置き場に登録し、使う側はそこから受け取る |
| 定数（ポート名、しきい値）をコードに埋め込む | 調整のたびにコードを直す | ScriptableObject か設定から渡す |

## 7. レビューのときのチェック項目

- [ ] 共有シーンの変更は、Scene Fusion のセッションの中で行い、ホストがコミットしたものか
- [ ] この変更は、別の人が作業中の Prefab・データを触っていないか。触るなら、分けられないか
- [ ] 「GDC 前から守る」の表を満たしているか
- [ ] 一時的なエディタメニューを使ったなら、消してあるか（または消す予定が PR に書いてあるか）
- [ ] asmdef の参照が、第 2 章の向きになっているか。機能どうしの直接参照がないか
- [ ] 対応表や定数が ScriptableObject になっているか
- [ ] `MockDevice` だけで動くか
- [ ] Play の前に手作業が要らないか
- [ ] `static` を使うなら、Domain Reload なしでも状態が残らないよう `ResetStatics` があるか
- [ ] public API が必要最小限か

## 関連
- #29 全体設計（この文書のもと。未確定事項）
- #6 実行用シーンとフォルダ構成、#20 村のレイアウトとフィールド、#34 Bootstrap（起動とシーンの読み込み。組み立て役は置かないことにした）、#43 Scene Fusion の作業ルール
- #18 デバイス連携の設計、#46 デバイスの仕組みと Mock、#47 実機のデバイスとつなぐ
