# Scripts

スクリプトはすべてここに置く。機能ごとにフォルダを切り、フォルダの中の分け方は担当者が自由に決めてよい。
方針の詳細は [CONTRIBUTING.md「6. スクリプト方針」](../../CONTRIBUTING.md) を参照。
機能どうしのつなぎ方と、避けること（アンチパターン）は [docs/architecture.md](../../docs/architecture.md) を参照。

| フォルダ | 役割 | 担当 |
|---|---|---|
| `Core/` | 全体で共有する小さな基盤（ID の型 `TextureId` `FoodId` `FieldId` `CueId`、イベントのデータ、約束（`IDevice`・`IFork`・`IFieldRequests`）と受け取り口（`Devices`・`Forks`・`Fields`））。他のフォルダに依存しない | |
| `Device/` | 触感デバイス・Arduino との通信。いまは、キーボードで動く `MockDevice`（`IDevice` の擬似実装） | |
| `Fork/` | 体験者が持つフォークで、雲を刺したか・先を口に含んだかの判定（`ForkTip`）と、刺せる雲（`Cloud`・`CloudRing`）。フォークの位置の取り方は知らない | |
| `Food/` | 食感 → 食べ物の対応（`FoodTable`）と、決定したときに食べ物を生んで飛ばす `FoodSpawner` | |
| `Tracking/` | VR・位置取得 | |
| `Tutorial/` | チュートリアル 1〜4 の進行。チュートリアルごとの人形劇スクリプト（`TutorialScript`）と、順に動かす `TutorialRunner` | |
| `Game/` | 本編の進行、スコア、判定 | |
| `Village/` | ミニチュアの村・フィールド・村人。フィールドが戻る部品と欲しい物（`FieldStages`・`FieldStep`・`VillagerMood`）と、村のフィールドの `Village.prefab` | |
| `UI/` | 画面表示。村人の欲しい物の吹き出し `RequestBubbles`（#59・#270）。Play で自動で置かれます。食べ物のアイコンがあれば絵を、なければ英語の名前を出します。大きさ・高さ・透け方・色は、`Assets/Resources/RequestBubbleSettings.asset` を Inspector で直します。Play 中に直しても、すぐ効きます | |
| `DevTools/` | Quest なしで試すための道具です。キーボードとマウスで動く頭（`DesktopHead`）、状態の表示（`TutorialDebugHud`）、テスト用シーンを作るメニューがあります。展示では使いません | |

フォルダを増やしたら、この表に 1 行足す。

## 名前が多くて分からないとき（まずここ）

### それぞれ、ゲームの何のためにあるのか

体験の中心は、次のくり返しです。困っている人の吹き出しを見ます。その場所の前で、口から物を生みます。物が届くと、その場所が戻ります。名前は、この流れのどこを受け持つかで分かれています。

| 名前 | ゲームの上で何のためにあるか | あなたが作る？ |
|---|---|---|
| `FieldStages` | **1 つの場所（村・山・海・草原）が、届くたびに戻っていく**ことを受け持つ。場所の一番上に 1 つ付ける | 付けるだけ（コードは書かない） |
| `FieldStep` | **戻り方の 1 段**（「芽が出る」「羊が戻る」）。欲しい物・助かる人・消える物を持ち、子に置いた物が届くと出る | 付けるだけ |
| `VillagerMood` | **困っている人の気持ちと動き**（うなだれる・元気になる・喜ぶ） | 村人の Prefab に入っている |
| `Fields` | **いまある場所の一覧**。場所は自分でここに名乗り出る。ほかの人は、ここから場所を受け取る | 使うだけ |
| `IFieldRequests` | **場所に頼めることの約束**（「次の欲しい物を出して」「この物が届いた」「やり直して」）。`Fields` から受け取る場所は、すべてこの約束を守っている | 使うだけ（作るのは、場所の仕組みごと独自にする人だけ） |
| `RequestIssued` / `RequestClosed` | **「吹き出しが出た」「吹き出しが終わった」の知らせ**の中身（何を・どこで・届いたか） | 受け取るだけ |
| `Devices` / `IDevice` | **フォーク（触感デバイス）の中身**。噛んだ・食感が変わった・決定した、を知らせる。実機が無ければ Mock が代わる | 使うだけ（作るのは Device の担当だけ） |
| `Forks` / `IFork` | **フォークの先の位置**。雲を刺した・先を口に含んだ、を知らせる。噛む・決定するは `IDevice` が知らせる | 使うだけ（作るのは Fork の担当だけ） |
| `FoodId` / `FieldId` / `TextureId` / `CueId` | **名前札**（`carrot`、`sea` など）。文字列を、取り違えないように包んだもの | 使うだけ |

`I` で始まる名前（`IFieldRequests`・`IDevice`）は「約束」（interface）。**ほとんどの人は使うだけで、自分で作らない。** 中身を知らなくても使えるようにするためにある（村の作りが変わっても、本編や吹き出しのコードは直さなくてよい）。

### あなたの担当で、何をすればいいか

| 担当 | やること | 触る名前 |
|---|---|---|
| 場所（山・海・草原・村） | 場所を組み立てる。**コードは書かない**。手順と村の例は [docs/requests.md「フィールドの作り方」](../../docs/requests.md) | `FieldStages`・`FieldStep` |
| 吹き出し（UI） | 「出た」で吹き出しを出し、「終わった」で消す | `Fields`・`RequestIssued`・`RequestClosed` |
| 本編（Game） | いつ・どこに欲しい物を出すかを決める | `Fields`・`IFieldRequests` の `CanOpen`・`Open()`・`Expire()` |
| チュートリアル | 台本どおりに「海に魚」を出し、届くのを待つ | `Fields.Find`・`Open(food)`・`Closed` |
| 物が落ちた場所の判定 | 生まれた物を、体験者がいる場所に渡す（何であっても） | `Fields.Find`・`Receive(food)` |
| 採点・反応 | 届いた物が何だったか（頼まれた・このフィールドの物・よその物）で、点や反応を付ける | `Fields`・`Received`・`Delivered.Kind` |

コードの例（そのまま写して始められる）:

```csharp
using MouthOfGod.Core;
using UnityEngine;

// 吹き出し（UI）: どの場所の吹き出しも、出たら出し、終わったら消す
public sealed class RequestBubbles : MonoBehaviour
{
    void OnEnable()
    {
        foreach (var field in Fields.All) Watch(field); // もうある場所
        Fields.Added += Watch;                           // あとから来た場所
        Fields.Removed += Unwatch;
    }

    void OnDisable()
    {
        Fields.Added -= Watch;
        Fields.Removed -= Unwatch;
        foreach (var field in Fields.All) Unwatch(field);
    }

    void Watch(IFieldRequests field) { field.Issued += Show; field.Closed += Hide; }
    void Unwatch(IFieldRequests field) { field.Issued -= Show; field.Closed -= Hide; }

    void Show(RequestIssued r) { /* r.Anchor（困っている人の頭）の上に、r.Food の吹き出しを出す */ }
    void Hide(RequestClosed r) { /* r.Field の吹き出しを消す。r.Resolved が true なら喜ぶ、false ならがっかり */ }
}
```

```csharp
// 本編（Game）: 同時に maxOpen 個まで、出せる場所に欲しい物を出す（待ち時間の Expire は別に書く）
void Update()
{
    var open = 0;
    foreach (var field in Fields.All) if (field.IsOpen) open++;
    foreach (var field in Fields.All)
    {
        if (open >= maxOpen) break;
        if (field.CanOpen && field.Open()) open++;
    }
}
```

```csharp
// 採点（決まりは未定）: どのフィールドに何が届いたかを数える。吹き出しと同じく Fields.All と Added で各フィールドの Received を購読する
void OnReceived(Delivered d)
{
    switch (d.Kind)
    {
        case DeliveryKind.Requested: score += 10; break; // 頼まれた物
        case DeliveryKind.Own: score += 1; break;        // 2 本目のにんじん（豊作）
        case DeliveryKind.Foreign: break;                // 村に豚（いびつな世界）
    }
}
```

```csharp
// チュートリアル 3: 海の困っている人に、魚を欲しがらせる
var sea = Fields.Find(new FieldId("sea"));
if (sea != null && sea.Open(new FoodId("fish"))) sea.Closed += OnSeaClosed; // 届いたら次のステップへ
```

```csharp
// 物が落ちた場所の判定: 生まれた物を、何であっても、体験者がいる場所に渡す（いる場所の受け取り方は、作るときに Core に足す）
Fields.Find(currentField)?.Receive(spawned.Food); // 戻るのは頼まれた物のときだけ。何だったかは Received で知らせる
```

もっと詳しく: 考え方・流れ・約束の全部は [docs/requests.md](../../docs/requests.md)（「これは何か」「API 仕様」）。

## 実装する前に（役割ごとに知っておくこと）

なぜこうなったか、何を考えたかは [docs/decisions.md「2026-10-13 コードの型…」](../../docs/decisions.md) にある。ここは、自分の仕事に要ることだけ。

**全員**
- 共有シーンは `Assets/Scenes/Main.unity` の 1 つ。編集は Scene Fusion のセッションの中だけ。機能の動作確認は、自分の `Assets/Scenes/Sandbox/` のシーンで行う。
- 同じファイルを取り合わない。`.unity` `.prefab` `.asset` は Git でマージできない。PR が同じファイルでぶつかったら、分け方が悪いサイン。
- Play したあとに、手でつなぐ設定を残さない（CONTRIBUTING §6）。

**実装担当（C#）**
- 自分の機能のフォルダ `Assets/Scripts/<機能>/` の中だけで作る。他の機能のクラスを直接参照しない。つなぐのは `Core` の interface かイベント（参照してよいのは自分と `Core` だけ。破ると PR に警告が出る）。
- 食感・食べ物・フィールドを `enum` にしない。ID は文字列（`TextureId` `FoodId` `FieldId`）。
- 「チュートリアル 1 ならこう」という分岐を、共通のコードに書かない。進行は `TutorialRunner` が共通で、中身はチュートリアルごとの人形劇スクリプト（`TutorialScript`。コルーチン）。フィールドの見せ方は、合図（`Cues`）を受けて、フィールドの担当が決める。
- チュートリアルのあいだで引き継ぐものは、`TutorialMemory` の名前だけを見る（T2 のことは見ない）。名前を足すときは [progress-keys.md](../../docs/progress-keys.md) に 1 行足す。
- 型と、データの置き場所: [CONTRIBUTING「型の名前とデータの置き場所」](../../CONTRIBUTING.md#型の名前とデータの置き場所)。型の一覧とイベントの流れ: [docs/architecture.md](../../docs/architecture.md)。

**素材担当**
- モデル・テクスチャ・音は、`Assets/Resources/` の外（`Assets/ArtisticResources/` など）に置く。タグ（食感・置き場所）を素材に埋め込まない。データ側で持つ。
- Prefab は `Assets/Resources/Prefabs/<機能>/` に置く（フォルダ名はスクリプトとそろえる）。
- 食べ物を足すときは、Prefab を `Assets/Resources/Prefabs/Food/` に置き（ファイル名が ID）、`FoodTable` に 1 行足す。全員で触る共有のリストは作らない。

**企画・シーン担当**
- データ（ScriptableObject）は `Assets/Resources/Data/<種類>/` に、1 件 1 アセットで作る。そのチュートリアルにしかない物の Prefab は `Assets/Resources/Prefabs/Tutorial/` に置く。
- フィールドの定義（山・海・草原・村、足りない状態と届いた後の状態）は [docs/fields.md](../../docs/fields.md)。
- 覚えたこと・場面の名前は、[progress-keys.md](../../docs/progress-keys.md) の一覧にあるものだけ使う。

**レビュワー**
- 見るのは、(1) 他の機能を直接参照していないか（警告）、(2) `enum` や直書きの ID が入っていないか、(3) 同じファイルを複数の PR が触っていないか、(4) 記録の名前が一覧と合っているか。
