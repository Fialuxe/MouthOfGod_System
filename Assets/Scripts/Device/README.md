# Device

触感デバイスの入力を、ゲームが使える形（`IDevice` のイベント）にする。いまあるのは、デバイスなしで動かす `MockDevice` だけだ。実機との通信（#47）は含まない。

担当: （未定）

## 目的

Meta Quest のコントローラだけで、「噛む → 食感が変わる → 決定」を試せるようにする。ゲームパッド・キーボードでも試せる。ゲーム側は `IDevice` を見ている。そのため、実機ができたら差し替えるだけで済む。

## 公開 API

| 型 | 役割 |
|---|---|
| `MockDevice`（`MonoBehaviour`、`IDevice`） | キー入力を `Bitten` / `TextureChanged(TextureId)` / `Confirmed(TextureId)` に変える。有効になると `Devices` に登録する |

`IDevice` の中身と、使う側の受け取り方（`Devices.WhenReady`）は、`Core/README.md` を参照する。

スクリプトからは、`MockDevice.GetOrCreate()` で作れる。このメソッドは、次の順に探して使う。

1. シーンに置いたもの
2. Prefab（`Assets/Resources/Prefabs/Device/MockDevice.prefab`）
3. 既定の操作割り当てで作ったもの

Play すると、Mock は自動で用意される。ビルドでも同じだ。ただし、実機のデバイスがすでに `Devices` に登録されていれば作らない。自動で作った Mock は、シーンが変わっても残る。

そのため実機（#47）は、通信がつながる前に登録する。登録は、`Awake` か `OnEnable` で `Devices.Register` を呼んで行う。`Start` や接続後に登録すると、Mock が先に登録されてしまう。

Inspector で変えられる値:

| 項目 | 既定 | 意味 |
|---|---|---|
| `textureIds` | `placeholder-a` `placeholder-b` `placeholder-c` | 噛んでいる間に、順に切り替わる食感 ID。いまはプレースホルダーなので、実際の ID に差し替える |
| `switchIntervalSeconds` | 1.5 | 食感が切り替わる間隔（秒） |
| `biteAction` | Quest 右トリガー / Space / ゲームパッド A | 噛む操作（`InputAction`）。Inspector でバインドを変更できる |
| `confirmAction` | Quest 右 A ボタン / Enter / ゲームパッド Start | 決定操作（同上） |

## キー操作（要確認: 暫定案）

| キー | 動作 |
|---|---|
| 右トリガー(Quest) / A / Space を押す | 噛み始める。まず `Bitten` を出す。続けて、最初の食感の `TextureChanged` を出す |
| 同上を押し続ける | 間隔ごとに、次の食感へ切り替わる。最後の食感の次は、最初に戻る |
| 同上を離す | 噛むのをやめる。切替が止まる。次に押すと、最初の食感から始まる |
| 右 A ボタン(Quest) / Start / Enter | 決定する。いまの食感を付けて `Confirmed` を出す。噛んでいなくても出る。噛む前なら、空の ID を付ける。離した後なら、最後の食感を付ける |

キャンセルはない。決定はボタン操作で行う。

## 構成

- `MockDevice.cs`: キー入力を読む `MonoBehaviour`。新 Input System の `InputAction` だけを使う。Quest の入力は `<XRController>{RightHand}` で読む。
- `MockDeviceLogic.cs`: 入力にも Unity にも依存しない中身の処理を持つ。経過時間に応じた食感の切替と、決定を扱う。テスト対象にする。
- `Tests/`: EditMode のテスト（`MouthOfGod.Device.Tests`）を置く。
- `Editor/MockDevicePrefabGenerator.cs`: メニュー「MouthOfGod/Device/Create MockDevice Prefab」から、既定の設定で Prefab を作る。既にあれば上書きしない。

## 決めたこと（要確認）

- 割り当て（Quest 右トリガー・右 A ボタンなど）と切替間隔（1.5 秒）は暫定だ。
- 噛み始めたら、最初の食感をすぐ通知する。
- 食感 ID が空欄なら、通知しない。
- 切替間隔の最小値は、0.05 秒だ。

手順（デバイスなしで試す方法）は、[docs/device-mock.md](../../../docs/device-mock.md) に書いた。`MockDeviceModel` は、棒の見た目と先端の内容量（%）を表示する。握ると手に付く動きも担う。この動きは見た目だけだ。
