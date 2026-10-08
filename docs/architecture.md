# 設計方針

> **状態: 案（レビュー待ち）**。Issue #29（全体設計）の内容を、リポジトリに残すために移したものです。名前や細部は仮で、決まっていないことは #29 の「人間に決めてほしいこと」にあります。直すときは、この文書を PR で直してください。

スクリプトの置き場所と書き方の基本は [CONTRIBUTING.md「6. スクリプト方針」](../CONTRIBUTING.md#6-スクリプト方針) にあります。この文書は、その上にある「機能どうしをどうつなぐか」と「避けること」をまとめます。

## 方針（1 行）

**「Core の小さな型・interface」と「ScriptableObject のデータ」を中心にし、機能は互いを知らず、一方向のイベントの連鎖でつなぐ。`GameManager` は作らない。**

## 1. 別々の作業が、同じファイルを編集しないようにする

いちばん大事な目的です。たとえばチュートリアル 1 とチュートリアル 2 が同じ `GameFlow` クラス（や同じシーン、同じデータファイル）を使っていると、誰かがそれを編集している間、もう一方は編集できません。Unity の `.unity` `.prefab` `.asset` はマージがほぼできない（CONTRIBUTING 第 3 章）ので、**同じファイルを触る作業は、並行できず順番待ちになります。**

そこで、作業の単位とファイルの単位をそろえます。

- **中央の進行クラスを作らない。** `GameFlow` や `GameManager` の中に「チュートリアル 1 ならこう、2 ならこう」という `switch` を書かない。進行を担う `TutorialRunner` は**どのチュートリアルでも同じコード**で、中身はデータ（`TutorialStep`）から読む。
- **1 チュートリアル = 1 まとまりのデータ。** チュートリアルごとにフォルダを分け（例: `Assets/Data/Tutorial/T1/`、`T2/`）、そのステップのアセットはその中に置く。チュートリアル 2 を直す人は、チュートリアル 1 のファイルを開かない。
- **1 件 = 1 アセット。** 食べ物・フィールド・ステップなどは、1 件ごとに別のアセット（`FoodDefinition` など）にする。全部を 1 つの巨大なリストやファイルに入れない。全員が触る「目次」のようなアセット（例: チュートリアルの順番）は、中身を持たず並びだけにして、小さく保つ。
- **1 フィールド = 1 シーン。** 木・川・牧場などのフィールドは別々のシーンにし、起動時に加算ロードする（#20、#34、#6）。
- **種類を足すときは、既存のコードに `case` を足すのではなく、新しいファイルを足す。** たとえばステップの完了条件の種類が増えるとき、共通の `enum` と `switch` を毎回編集するのではなく、条件ごとのクラスやアセットを足せる形が望ましい（案）。
- **PR が同じファイルでぶつかったら、分け方を見直すサイン**として扱う。

> 未確認: ScriptableObject を Scene Fusion で同時に編集したときの挙動は、#43 で確かめる。

## 2. 機能と依存の向き

```
Core        型（TextureId / FoodId / FieldId）、小さな interface、イベントの型。ロジックは載せない。誰にも依存しない
 ↑
Device      IDevice の実装（Serial / Mock）                      Core のみ
Tracking    AprilTag・位置                                       Core のみ（現状は Core も不要）
Food        食感→食べ物の対応、生成（FoodCatalog / FoodSpawner）   Core のみ
Village     Field・DeliveryZone・村人の要求・村の変化              Core のみ（位置合わせは「親 Transform が動く」ことにだけ依存）
UI          表示。Core のイベントを購読                           Core のみ
 ↑
Tutorial    ステップの進行（データ駆動）                          Core のみ。Game とは互いに依存しない
Game        本編（時間・スコア）                                  Core のみ
 ↑
Bootstrap   起動時の組み立て。全機能を参照できる唯一の場所
```

- 横の機能どうし（Device ↔ Food ↔ Village ↔ UI）は**相互参照を禁止**し、asmdef で強制する。
- 参照できるのは「下の層だけ」。Tutorial が Device や Village のクラスを直接叩かない。
- 機能どうしを結びつけるのは Bootstrap だけ。Inspector での手配線に頼らない（CONTRIBUTING「最終的に Inspector／Editor の操作が要らないシステムにする」）。

## 3. 流れはイベントの連鎖にする

```
Device   --TextureChanged / Confirmed-->  Food(FoodSpawner)  --Spawned-->  UI / Tutorial
Village  --RequestIssued-->               UI(吹き出し) / Tutorial
プレイヤーが、生まれた物を DeliveryZone へ運ぶ
Village(DeliveryZone) --Delivered(Accepted?)--> Village(FieldView: 村が変わる) / Tutorial / Game(スコア)
```

- Tutorial は Device を直接見ない（`MockDevice` に差し替えても動く）。
- Tutorial から村や UI への指示（「この場所を光らせる」など）は、Tutorial が Core の `CueRequested(CueId)` を発行し、Village や UI が購読する（依存の向きを保つため）。
- 機能の中は C# の `event`。機能をまたぐものは、Core の interface とイベントを Bootstrap が起動時に渡す。ScriptableObject のイベントチャンネルは、必要になるまで採用しない。

### 型とイベント（案。名前は仮）

| 用途 | 型・イベント | 置き場所 |
|---|---|---|
| 食感 | `TextureId`（論理 ID。物理量は Device の内側で ID に変換する） | Core |
| 入力 | `IDevice`: `TextureChanged`、`Confirmed`、`Bitten` | Core（interface）／ Device（実装） |
| 食べ物 | `FoodId`、`FoodCatalog.Resolve(TextureId)` → `FoodId`、`FoodSpawner.Spawned` | Food |
| 届け先 | `FieldId`、`Field.Accepts(FoodId)`、`DeliveryZone.Delivered(DeliveryResult)` | Village |
| 要求 | `Request`（`FoodId`、`FieldId`）、`RequestIssued` | Village |
| 進行 | `TutorialRunner`、`StepEntered`、`StepCompleted`、`CueRequested` | Tutorial ／ Core |

- 食感・食べ物を `enum` で Core に直書きしない。追加のたびに全機能が再コンパイルされ、素材担当が触れなくなる。ID は文字列か ScriptableObject の参照にする。

## 4. 素材・データ・実装を分ける

| 区分 | 持つもの | 主な担当 |
|---|---|---|
| 素材（Art） | モデル、テクスチャ、アニメーション、SE、Particle | 素材担当 |
| Prefab | 食べ物・生物、フィールド、村人の見た目と Collider | 素材担当 ＋ シーン担当 |
| データ（ScriptableObject） | `FoodCatalog`、`FoodDefinition`、`FieldDefinition`、`RequestSet`、`TutorialStep` | 企画・シーン担当（実装は読むだけ） |
| シーン | 村・フィールドの配置、AprilTag の位置 | シーン担当（Scene Fusion） |
| コード | カタログの参照、完了条件の評価、判定、Device 通信、起動時の組み立て | 実装担当 |

素材にタグ（食感・置き場所）を埋め込まず、データ側で持つ。素材を差し替えても、コードとシーンを触らずに済む。

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

## 6. 避けること（アンチパターン）

| 避けること | なぜ困るか | 代わりに |
|---|---|---|
| `GameManager` / `GameFlow` が Device・Village・UI・各チュートリアルを全部持つ | 全員がそのファイルを編集するので順番待ちになる。1 か所の変更が全体を壊す | 機能ごとに分け、イベントでつなぐ。組み立ては Bootstrap |
| チュートリアルごとの処理を、共通クラスの `switch` や `if` で分ける | チュートリアルを足す・直すたびに共通ファイルを編集する | `TutorialRunner` は共通、中身はチュートリアルごとのデータ |
| 全部のデータを 1 つのアセットやシーンに入れる | 同時に編集できない | 1 件 1 アセット、1 フィールド 1 シーン |
| Device がゲームの状態（旧 `UserChoice`）を持つ | 通信とゲームの都合が絡み、Mock に差し替えられない | Device は入力を ID とイベントにするだけ |
| Tutorial が Village の Field などを直接叩く | 依存の向きが崩れ、片方だけ直せなくなる | Core のイベント（`CueRequested` など）経由 |
| 食感・食べ物を `enum` で Core に直書き | 追加のたびに全体が再コンパイル。素材担当が触れない | 文字列 ID か ScriptableObject |
| `static` なシングルトンの乱用、`FindObjectOfType` / `GameObject.Find` | どこから使われているか追えない。テストで差し替えられない | Bootstrap から渡す |
| 機能をまたぐ `[SerializeField]` の手配線 | Play の前に手作業が要る。付け忘れで動かない | Bootstrap がコードで組み立てる |
| 定数（ポート名、しきい値）をコードに埋め込む | 調整のたびにコードを直す | ScriptableObject か設定から渡す |

## 7. レビューのときのチェック項目

- [ ] この変更は、別の人が作業中のファイル（共通の進行クラス、共有シーン、共有データ）を触っていないか。触るなら、分けられないか
- [ ] asmdef の参照が、第 2 章の向きになっているか。機能どうしの直接参照がないか
- [ ] 対応表や定数が ScriptableObject になっているか
- [ ] `MockDevice` だけで動くか
- [ ] Play の前に手作業が要らないか
- [ ] `static` を使うなら、Domain Reload なしでも状態が残らないよう `ResetStatics` があるか
- [ ] public API が必要最小限か

## 関連
- #29 全体設計（この文書のもと。未確定事項）
- #6 実行用シーンとフォルダ構成、#20 村のレイアウトとフィールド（シーンの分け方）、#34 Bootstrap（起動シーンと加算ロード）、#43 Scene Fusion の作業ルール
- #18 デバイス連携の設計、#46 デバイスの仕組みと Mock、#47 実機のデバイスとつなぐ
