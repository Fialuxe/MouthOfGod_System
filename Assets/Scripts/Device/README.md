# Device

触感デバイスの入力を、ゲームが使える形（`IDevice` のイベント）にします。いまあるのは、デバイスなしで動かす `MockDevice` だけです。

状態: 案。割り当てと切替間隔は暫定です。実機との通信は #47 で扱い、ここには含みません。

担当: （未定）

Meta Quest のコントローラだけで、「噛む → 食感が変わる → 決定」を試せます。ゲームパッドとキーボードでも試せます。ゲーム側は `IDevice` を見ています。そのため、実機ができたら差し替えるだけで済みます。

## 部品

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `MockDevice`（`MonoBehaviour`、`IDevice`） | 自動で用意されます。シーンに置いてもかまいません | キー入力を `Bitten` / `TextureChanged(TextureId)` / `Confirmed(TextureId)` に変えます。有効になると `Devices` に登録します。入力は新 Input System の `InputAction` だけで読みます。Quest の入力は `<XRController>{RightHand}` です |
| `MockDeviceLogic` | （コードの中で使います） | 入力にも Unity にも依存しない中身です。経過時間に応じた食感の切替と、決定を扱います |
| `MockDeviceModel` | 棒の見た目 | 棒の見た目と、先端の内容量（%）を表示します。握ると手に付く動きも扱います。この動きは見た目だけです |

`IDevice` の中身と、使う側の受け取り方（`Devices.WhenReady`）は、[Core/README.md](../Core/README.md) にあります。

Inspector で変えられる値は次のとおりです。

| 項目 | 既定 | 意味 |
|---|---|---|
| `textureIds` | `placeholder-a` 〜 `placeholder-f`（6 つ） | 噛んでいる間に、順に切り替わる食感 ID です。いまはプレースホルダーです。実際の ID に差し替えます。a〜d は各フィールドの食べ物、e はリンゴ、f は魚に使います（docs/decisions.md 2026-10-25） |
| `switchIntervalSeconds` | 1.5 | 食感が切り替わる間隔（秒）です。最小値は 0.05 秒です |
| `biteAction` | Quest 右トリガー / 左手を握る / Space / ゲームパッド A | 噛む操作（`InputAction`）です。Inspector でバインドを変えられます |
| `confirmAction` | Quest 右 A ボタン / 左手のつまみ / Enter / ゲームパッド Start | 決定操作です（同上） |

## ほかの機能とのつなぎ方

Play すると、Mock は自動で用意されます。ビルドでも同じです。ただし、実機のデバイスが `Devices` に登録済みなら作りません。自動で作った Mock は、シーンが変わっても残ります。

そのため実機（#47）は、通信がつながる前に登録します。`Awake` か `OnEnable` で `Devices.Register` を呼びます。`Start` や接続後に登録すると、Mock が先に登録されてしまいます。

スクリプトからは、`MockDevice.GetOrCreate()` で作れます。次の順に探して使います。

1. シーンに置いたもの
2. Prefab（`Assets/Resources/Prefabs/Device/MockDevice.prefab`）
3. 既定の操作割り当てで作ったもの

Prefab は、メニュー **MouthOfGod > Device > Create MockDevice Prefab** で作れます（`Editor/MockDevicePrefabGenerator.cs`）。既定の設定で作ります。既にあれば上書きしません。

## キー操作（暫定）

| キー | 動作 |
|---|---|
| 右トリガー(Quest) / 左手を握る / A / Space を押す | 噛み始めます。まず `Bitten` を出します。続けて、最初の食感の `TextureChanged` をすぐ出します |
| 同上を押し続ける | 間隔ごとに、次の食感へ切り替わります。最後の次は、最初に戻ります |
| 同上を離す | 噛むのをやめ、切替が止まります。次に押すと、最初の食感から始まります |
| 右 A ボタン(Quest) / 左手のつまみ / Start / Enter | 決定します。いまの食感を付けて `Confirmed` を出します。噛んでいなくても出ます。噛む前なら、空の ID を付けます。離した後なら、最後の食感を付けます |

- キャンセルはありません。決定はボタン操作で行います。
- 食感 ID が空欄なら、通知しません。

デバイスなしで試す手順は、[docs/device-mock.md](../../../docs/device-mock.md) にあります。

## テスト

`Tests/` に EditMode のテスト（`MouthOfGod.Device.Tests`）があります。主に `MockDeviceLogic` を確かめます。
