# Core

全体で共有する、小さな基盤です。機能どうしをつなぐ ID・イベントのデータ・interface だけを置きます。ロジックは置きません。

決めた経緯は [docs/decisions.md](../../../docs/decisions.md)（2026-10-13、2026-10-15、2026-10-19）にあります。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `TextureId` / `FoodId` / `FieldId` / `CueId` / `PhaseId` | 文字列を包んだ ID です（`readonly struct`）。`enum` にしないための型です。`null`・空文字・`default` は、どれも空 ID（`None`、`IsEmpty`）です。大文字小文字は区別します（`Ordinal` で比べます）。型が違えば、同じ文字列でも別物です |
| `Spawned` / `Delivered`（`DeliveryKind` 付き）/ `RequestIssued` / `RequestClosed` / `CueRequested` | 機能をまたぐイベントのデータです（`readonly struct`）。値を持つだけです。デバイスの入力は `IDevice` のイベントなので、ここには置きません |
| `Devices` | 置き場です。いま使える `IDevice` を渡します（`WhenReady`・`Current`）。デバイスの実装が `Register` します |
| `Forks` | 置き場です。いま使える `IFork` を渡します（`WhenReady`・`Current`）。Fork の `ForkTip` が `Register` します。形は `Devices` と同じです |
| `Cues` | 合図（`CueRequested`）の置き場です。Tutorial が `Request(CueId)` します。Village や UI は `Requested` を購読し、見せ方を決めます。出す側は、誰が受けるかを知りません |
| `Phases` | いまの場面（`PhaseId`）の置き場です。チュートリアルの名前などが入ります。決める側（Tutorial・Game）が `Set` します。使う側は `Current` で読み、`Changed` で知らせを受けます |
| `Fields` | いまシーンにあるフィールドの置き場です（`All`・`Added`・`Removed`・`Find`）。フィールドは、有効になると自分で `Register` します。ID が空か重なっていれば、警告して登録しません |
| `FoodSpawners` | 置き場です。いま使える `IFoodSpawner`（食べ物を生む所）を渡します（`WhenReady`・`Current`）。Food の `FoodSpawner` が `Register` します。形は `Devices` と同じです |
| `Standing` | 体験者がいま前に立っているフィールドの置き場です（`Field`・`DropPoint`・`Changed`）。Village の `StandArea` が `Set` します。Food は、このフィールドの表で食べ物を引きます |
| `IFieldRequests` | 1 つのフィールドの欲しい物です（`Open`・`Receive`・`Expire`・`Restart`・`Issued`・`Closed`・`Received`）。同時に複数出ていることがあります。欲しい物ごとに番号（`Request`）が付きます。いつもの実装は Village の `FieldStages` です。独自の作りでも、これを実装すればつながります。使う側は `Fields` から受け取ります。分担は [docs/requests.md](../../../docs/requests.md) にあります |
| `IFoodItem` | 生まれた食べ物が持ちます。`Food`（食べ物の ID）を返します。届け先の判定などで使います。Food を参照せずに、食べ物の情報を読めます。実装は Food の `FoodItemTag` です |
| `IDevice` | 触感デバイスの入力です。`Current`（いまの食感）、`TextureChanged(TextureId)`、`Confirmed(TextureId)`、`Bitten` があります。`Confirmed` は、決定したときの食感を運びます。実装は Device 側です |
| `IFork` | 体験者が持つフォークです。`IsLoaded` は、先に雲のかけらが付いていることです。`InMouth` は、先が口の中にあることです。イベントは `Pierced`・`MouthEntered`・`MouthLeft`・`Emptied` です。`Emptied` は、決定してかけらがなくなったときです。実装は Fork 側です |
| `IFoodSpawner` | 食べ物を生む所です。`Spawned`（生まれた）と `Preview(TextureId)`（その食感なら何が生まれるか）を持ちます。チュートリアルは生まれた物を待ち、影の UI は生まれる物を引きます。実装は Food の `FoodSpawner` です |

## ほかの機能とのつなぎ方

- 他の機能は、`MouthOfGod.Core` だけを参照してよいです。Core は、他の asmdef に依存しません（`references` は空です）。
- 機能をまたいで使う「もの」は、置き場（`Devices`・`Fields` など）で受け渡します。
- 置き場を足すのは、そういう「もの」が増えたときだけです。足したら、上の表に書きます。
- イベントの購読の仕組み（バス）は置きません。流れが追えなくなるためです。
- 全機能を参照する組み立て役（Bootstrap）も置きません。理由は decisions.md の 2026-10-15 にあります。
- 型とイベントの流れは [docs/architecture.md](../../../docs/architecture.md) §3 にあります。

## 使い方

### ID を作る

ID は文字列で作ります。

```csharp
var food = new FoodId("apple");
var field = new FieldId("village");
var delivered = new Delivered(food, field, DeliveryKind.Requested);
```

### デバイスの入力を受け取る

`Devices.WhenReady` で `IDevice` を受け取ります。デバイス（`MockDevice` など）が後から用意されても届きます。Device の asmdef を参照する必要はありません。

```csharp
using MouthOfGod.Core;
using UnityEngine;

public sealed class FoodSpawner : MonoBehaviour
{
    IDevice _device;

    void OnEnable() => Devices.WhenReady(Attach);

    void OnDisable()
    {
        Devices.CancelWhenReady(Attach); // まだ届いていなければ取り消す
        if (_device != null) _device.Confirmed -= Spawn;
        _device = null;
    }

    void Attach(IDevice device)
    {
        _device = device;
        device.Confirmed += Spawn;
    }

    void Spawn(TextureId texture)
    {
        if (texture.IsEmpty) return; // 噛む前の決定
        // texture に対応する食べ物を生む
    }
}
```

- `WhenReady` には、ラムダではなくメソッド（上の `Attach`）を渡します。ラムダは `CancelWhenReady` で取り消せないためです。
- いまの食感だけ読むときは、`Devices.Current?.Current` を使います。デバイスがまだ無ければ null です。
- デバイスは 1 台の前提です。2 台目の `Register` は、警告を出して `false` を返します。
- 登録は、起動時に 1 回の前提です。途中で差し替えても、すでに受け取った側には知らせません。
- `Devices` もイベントも、メインスレッドから使います。
- イベントの順番と意味は、`IDevice.cs` のコメントにあります。たとえば、`Current` を更新してから `TextureChanged` を出します。

## テスト

`Tests/` に EditMode のテストがあります（Window > General > Test Runner > EditMode）。
