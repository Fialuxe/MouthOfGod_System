# Device

触感デバイスの入力を、ゲームが使える形（`IDevice` のイベント）にします。いまあるのは、デバイスなしで動かす `MockDevice` だけです。実機との通信（#47）は含みません。

担当: （未定）

## 目的

Meta Quest のコントローラだけで、「噛む → 食感が変わる → 決定」を試せるようにします。ゲームパッド・キーボードでも試せます。ゲーム側は `IDevice` を見ています。そのため、実機ができたら差し替えるだけで済みます。

## 公開 API

| 型 | 役割 |
|---|---|
| `MockDevice`（`MonoBehaviour`、`IDevice`） | キー入力を `Bitten` / `TextureChanged(TextureId)` / `Confirmed(TextureId)` に変えます。有効になると `Devices` に登録します |

`IDevice` の中身と、使う側の受け取り方（`Devices.WhenReady`）は、`Core/README.md` を参照してください。

スクリプトからは、`MockDevice.GetOrCreate()` で作れます。このメソッドは、次の順に探して使います。

1. シーンに置いたもの
2. Prefab（`Assets/Resources/Prefabs/Device/MockDevice.prefab`）
3. 既定の操作割り当てで作ったもの

Play すると、Mock は自動で用意されます。ビルドでも同じです。ただし、実機のデバイスがすでに `Devices` に登録されていれば作りません。自動で作った Mock は、シーンが変わっても残ります。

そのため実機（#47）は、通信がつながる前に登録します。登録は、`Awake` か `OnEnable` で `Devices.Register` を呼んで行います。`Start` や接続後に登録すると、Mock が先に登録されてしまいます。

Inspector で変えられる値:

| 項目 | 既定 | 意味 |
|---|---|---|
| `textureIds` | `placeholder-a` `placeholder-b` `placeholder-c` | 噛んでいる間に、順に切り替わる食感 ID。いまはプレースホルダーなので、実際の ID に差し替えます |
| `switchIntervalSeconds` | 1.5 | 食感が切り替わる間隔（秒） |
| `biteAction` | Quest 右トリガー / Space / ゲームパッド A | 噛む操作（`InputAction`）。Inspector でバインドを変更できます |
| `confirmAction` | Quest 右 A ボタン / Enter / ゲームパッド Start | 決定操作（同上） |

## キー操作（要確認: 暫定案）

| キー | 動作 |
|---|---|
| 右トリガー(Quest) / A / Space を押す | 噛み始めます。まず `Bitten` を出します。続けて、最初の食感の `TextureChanged` を出します |
| 同上を押し続ける | 間隔ごとに、次の食感へ切り替わります。最後の食感の次は、最初に戻ります |
| 同上を離す | 噛むのをやめます。切替が止まります。次に押すと、最初の食感から始まります |
| 右 A ボタン(Quest) / Start / Enter | 決定します。いまの食感を付けて `Confirmed` を出します。噛んでいなくても出ます。噛む前なら、空の ID を付けます。離した後なら、最後の食感を付けます |

キャンセルはありません。決定はボタン操作で行います。

## 構成

- `MockDevice.cs`: キー入力を読む `MonoBehaviour` です。新 Input System の `InputAction` だけを使います。Quest の入力は `<XRController>{RightHand}` で読みます。
- `MockDeviceLogic.cs`: 入力にも Unity にも依存しない中身の処理を持ちます。経過時間に応じた食感の切替と、決定を扱います。テスト対象にします。
- `Tests/`: EditMode のテスト（`MouthOfGod.Device.Tests`）を置きます。
- `Editor/MockDevicePrefabGenerator.cs`: メニュー「MouthOfGod/Device/Create MockDevice Prefab」から、既定の設定で Prefab を作ります。既にあれば上書きしません。

## 決めたこと（要確認）

- 割り当て（Quest 右トリガー・右 A ボタンなど）と切替間隔（1.5 秒）は暫定です。
- 噛み始めたら、最初の食感をすぐ通知します。
- 食感 ID が空欄なら、通知しません。
- 切替間隔の最小値は、0.05 秒です。

手順（デバイスなしで試す方法）は、[docs/device-mock.md](../../../docs/device-mock.md) に書きました。`MockDeviceModel` は、棒の見た目と先端の内容量（%）を表示します。握ると手に付く動きも、`MockDeviceModel` が扱います。この動きは見た目だけです。
