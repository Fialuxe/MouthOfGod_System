# Device

触感デバイスの入力を、ゲームが使える形（`IDevice` のイベント）にする。いまあるのは、デバイスなしで動かす `MockDevice` だけ。実機との通信（#47）は含まない。

担当: （未定）

## 目的

Meta Quest のコントローラ（ゲームパッド・キーボードも可）だけで「噛む → 食感が変わる → 決定」を試せるようにする。実機ができたら、ゲーム側は `IDevice` を見ているので、差し替えるだけで済む。

## 公開 API

| 型 | 役割 |
|---|---|
| `MockDevice`（`MonoBehaviour`、`IDevice`） | キー入力を `Bitten` / `TextureChanged(TextureId)` / `Confirmed(TextureId)` に変える。有効になると `Devices` に登録する |

`IDevice` の中身と、使う側の受け取り方（`Devices.WhenReady`）は `Core/README.md` を参照。`MockDevice.GetOrCreate()` でスクリプトから作れる（既定の操作割り当て付き）。Play すると自動で用意する。

Inspector で変えられる値:

| 項目 | 既定 | 意味 |
|---|---|---|
| `textureIds` | `placeholder-a` `placeholder-b` `placeholder-c` | 噛んでいる間に順に切り替わる食感 ID（プレースホルダー。実際の ID に差し替える） |
| `switchIntervalSeconds` | 1.5 | 食感が切り替わる間隔（秒） |
| `biteAction` | Quest 右トリガー / Space / ゲームパッド A | 噛む操作（`InputAction`。Inspector でバインドを変更できる） |
| `confirmAction` | Quest 右 A ボタン / Enter / ゲームパッド Start | 決定操作（同上） |

## キー操作（要確認: 暫定案）

| キー | 動作 |
|---|---|
| 右トリガー(Quest) / A / Space を押す | 噛み始め。`Bitten` を出し、続けて最初の食感の `TextureChanged` を出す |
| 同上を押し続ける | 間隔ごとに次の食感へ切り替わる（最後の次は最初に戻る） |
| 同上を離す | 噛むのをやめる。切替が止まる。次に押すと最初の食感から |
| 右 A ボタン(Quest) / Start / Enter | 決定。いまの食感を付けて `Confirmed` を出す（噛んでいなくても出る。噛む前なら空の ID。離した後なら最後の食感） |

キャンセルはない。決定はボタン操作。

## 構成

- `MockDevice.cs`: キー入力を読む `MonoBehaviour`（新 Input System の `InputAction`（Quest は `<XRController>{RightHand}`）のみ）。
- `MockDeviceLogic.cs`: 入力・Unity に依存しない中身（経過時間 → 食感の切替、決定）。テスト対象。
- `Tests/`: EditMode のテスト（`MouthOfGod.Device.Tests`）。

## 決めたこと（要確認）

- 割り当て（Quest 右トリガー・右 A ボタンなど）、切替間隔（1.5 秒）は暫定。
- 噛み始めで、最初の食感をすぐ通知する。
- 食感 ID の空欄は通知しない。切替間隔は 0.05 秒未満にならない。
