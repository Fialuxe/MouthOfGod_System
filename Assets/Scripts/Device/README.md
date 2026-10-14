# Device

触感デバイスの入力を、ゲームが使える形（`IDevice` のイベント）にする。いまあるのは、デバイスなしで動かす `MockDevice` だけ。実機との通信（#47）は含まない。

担当: （未定）

## 目的

コントローラ（ゲームパッド）またはキーボードだけで「噛む → 食感が変わる → 決定」を試せるようにする。実機ができたら、ゲーム側は `IDevice` を見ているので、差し替えるだけで済む。

## 公開 API

| 型 | 役割 |
|---|---|
| `MockDevice`（`MonoBehaviour`、`IDevice`） | キー入力を `Bitten` / `TextureChanged(TextureId)` / `Confirmed` に変える |

`IDevice` の中身は `Core/` を参照。`MockDevice` は自動では生成しない。シーンへ置くのは Bootstrap（#34）か、手で置く。

Inspector で変えられる値:

| 項目 | 既定 | 意味 |
|---|---|---|
| `textureIds` | `placeholder-a` `placeholder-b` `placeholder-c` | 噛んでいる間に順に切り替わる食感 ID（プレースホルダー。実際の ID に差し替える） |
| `switchIntervalSeconds` | 1.5 | 食感が切り替わる間隔（秒） |
| `biteKey` | Space | 噛むキー |
| `confirmKey` | Start / Enter | 決定キー |

## キー操作（要確認: 暫定案）

| キー | 動作 |
|---|---|
| A ボタン / Space を押す | 噛み始め。`Bitten` を出し、続けて最初の食感の `TextureChanged` を出す |
| A / Space を押し続ける | 間隔ごとに次の食感へ切り替わる（最後の次は最初に戻る） |
| A / Space を離す | 噛むのをやめる。切替が止まる。次に押すと最初の食感から |
| Start / Enter | 決定。`Confirmed` を出す（噛んでいなくても出る） |

キャンセルはない。決定はボタン操作。

## 構成

- `MockDevice.cs`: キー入力を読む `MonoBehaviour`（新 Input System の `Gamepad.current` / `Keyboard.current` のみ）。
- `MockDeviceLogic.cs`: 入力・Unity に依存しない中身（経過時間 → 食感の切替、決定）。テスト対象。
- `Tests/`: EditMode のテスト（`MouthOfGod.Device.Tests`）。

## 決めたこと（要確認）

- 割り当て（A・Start / Space・Enter）、切替間隔（1.5 秒）は暫定。
- 噛み始めで、最初の食感をすぐ通知する。
- 食感 ID の空欄は通知しない。切替間隔は 0.05 秒未満にならない。
