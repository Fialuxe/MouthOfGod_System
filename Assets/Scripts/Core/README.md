# Core

全体で共有する、小さな基盤。機能どうしをつなぐ ID・イベントのデータ・interface だけを置く。ロジックは置かない。他の asmdef に依存しない（`MouthOfGod.Core` の `references` は空）。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `TextureId` / `FoodId` / `FieldId` / `CueId` | 文字列を包んだ ID（`readonly struct`）。`enum` にしないための型。`null` と空文字は、どちらも空 ID（`None`、`IsEmpty`）。大文字小文字は区別する。型が違えば、同じ文字列でも別物 |
| `Spawned` / `Delivered` / `RequestIssued` / `RequestClosed` / `CueRequested` | 機能をまたぐイベントが運ぶデータ（`readonly struct`）。値を持つだけ。デバイスの入力は `IDevice` のイベントで、ここには置かない |
| `Devices` | 置き場: いま使える `IDevice`（`WhenReady`・`Current`）。デバイスの実装が `Register` する |
| `Fields` | 置き場: いまシーンにあるフィールド（`All`・`Added`・`Removed`・`Find`）。フィールドが有効になると自分で `Register` する。ID が空・重なりは警告して登録しない |
| `IFieldRequests` | 1 つのフィールドの欲しい物（`Open`・`Resolve`・`Expire`・`Restart`・`Issued`・`Closed`）。いつもの実装は Village の `FieldStages`（独自の作りでも、これを実装すればつながる）。使う側は `Fields` から受け取る。分担は [docs/requests.md](../../../docs/requests.md) |
| `IDevice` | 触感デバイスの入力。`Current`（いまの食感）、`TextureChanged(TextureId)`、`Confirmed(TextureId)`（決定したときの食感を運ぶ）、`Bitten`。実装は Device 側 |

型とイベントの流れは [docs/architecture.md](../../../docs/architecture.md) §3。

## 使い方

他の機能は、`MouthOfGod.Core` だけを参照してよい。ID は文字列で作る。

```csharp
var food = new FoodId("apple");
var field = new FieldId("village");
var delivered = new Delivered(food, field, accepted: true);
```

### デバイスの入力を受け取る

`Devices.WhenReady` で `IDevice` を受け取る。デバイス（`MockDevice` など）が後から用意されても届く。Device の asmdef を参照する必要はない。

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

- `WhenReady` には、ラムダではなくメソッド（上の `Attach`）を渡す。ラムダは `CancelWhenReady` で取り消せない。
- いまの食感だけ読みたいときは `Devices.Current?.Current`（デバイスがまだ無ければ null）。
- デバイスは 1 台の前提。2 台目の `Register` は受け付けない（警告を出して `false` を返す）。登録は起動時に 1 回の前提で、途中の差し替えは、すでに受け取った側に知らせない。
- `Devices` もイベントも、メインスレッドから使う。
- イベントの順番と意味（`Current` を更新してから `TextureChanged` を出す、など）は `IDevice.cs` のコメント。

## 決めたこと

- 空 ID は、`null` と空文字を同じにそろえた（`default` も空）。
- 大文字小文字は区別する（`Ordinal` で比べる）。
- イベントの型の購読の仕組み（バス）は置かない（流れが追えなくなる。architecture.md §3）。
- 機能をまたいで使う「もの」は、置き場（`Devices`・`Fields`）で受け渡す。全機能を参照する組み立て役（Bootstrap）は置かない（2026-10-15）。置き場を足すのは、そういう「もの」が増えたときだけで、足したら上の表に書く。

## テスト

`Tests/` に EditMode のテストがある（Window > General > Test Runner > EditMode）。
