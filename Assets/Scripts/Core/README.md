# Core

全体で共有する、小さな基盤。機能どうしをつなぐ ID・イベントのデータ・interface だけを置く。ロジックは置かない。他の asmdef に依存しない（`MouthOfGod.Core` の `references` は空）。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `TextureId` / `FoodId` / `FieldId` / `CueId` | 文字列を包んだ ID（`readonly struct`）。`enum` にしないための型。`null` と空文字は、どちらも空 ID（`None`、`IsEmpty`）。大文字小文字は区別する。型が違えば、同じ文字列でも別物 |
| `Spawned` / `Delivered` / `RequestIssued` / `CueRequested` | 機能をまたぐイベントが運ぶデータ（`readonly struct`）。値を持つだけ。デバイスの入力は `IDevice` のイベントで、ここには置かない |
| `IDevice` | 触感デバイスの入力（`TextureChanged`、`Confirmed`、`Bitten`）。実装は Device 側 |

型とイベントの流れは [docs/architecture.md](../../../docs/architecture.md) §3。

## 使い方

他の機能は、`MouthOfGod.Core` だけを参照してよい。ID は文字列で作る。

```csharp
var food = new FoodId("apple");
var field = new FieldId("village");
var delivered = new Delivered(food, field, accepted: true);
```

## 決めたこと

- 空 ID は、`null` と空文字を同じにそろえた（`default` も空）。
- 大文字小文字は区別する（`Ordinal` で比べる）。
- イベントの型の購読の仕組み（バス）は、まだ置かない。必要になるまで採用しない（architecture.md §3）。

## テスト

`Tests/` に EditMode のテストがある（Window > General > Test Runner > EditMode）。
