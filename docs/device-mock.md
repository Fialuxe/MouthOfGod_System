# デバイスなしで開発・動作確認する（MockDevice）

触感デバイスを持っていなくても、Play するだけで「噛む → 食感が変わる → 決定」を試せる。Quest が無くても、キーボードだけで試せる。
ゲームは `IDevice`（`Assets/Scripts/Core/IDevice.cs`）だけを見ているので、実機ができたら差し替えるだけで済む。
実機のつなぎ方と必要な環境は #47（仕様が決まってから）。

> 状態: Unity エディタでのコンパイル・Play（キーボード）・EditMode テストは確認済み。Quest（コントローラ・ハンドトラッキング）では未確認。操作の割り当てと数値は暫定。

## 手順
1. Quest で試すときは、[vr-setup.md](vr-setup.md) のとおり Quest Link で Play できる状態にする。キーボードだけなら不要（ヘッドセットをつながずに Play してよい）。
2. Play する。`MockDevice` は自動で作られる（シーンに置く必要も、Inspector の設定もない。実機のデバイスが登録されていれば作られない）。棒は原点（0, 0, 0）に出る。見えないときは Hierarchy の `MockDevice` を選んで、Scene ビューで探す。
3. キーボードのときは、Game ビューを一度クリックしてから押す（フォーカスが無いとキーが届かない）。
4. 試す。

| やること | Quest（ハンドトラッキング） | Quest コントローラ | キーボード |
|---|---|---|---|
| 棒（デバイス）を持つ | 右手を握る | 右グリップ | G を押している間 |
| 噛む（押している間） | 左手を握る | 右トリガー | Space |
| 決定（実機の「ハードのボタン 1 つ」の代わり） | 左手のつまみ | 右 A ボタン | Enter |

- 棒の先端に、デバイスの内容量（0〜100 %）が出る。`MockDevice` の `Content Percent`（Inspector のスライダー、またはコードの `ContentPercent`）で動かす。内容量を誰がどう変えるか（ゲームからの指令など）は未定なので、Mock は値を出すだけ。
- 握っている間、棒は手に付いてくる。離すとその場に残る。手（`RightHandAnchor`）が無いシーンでは、G を押しても動かない。
- 噛み始めると `Bitten` が出る。噛んでいる間は、既定 1.5 秒ごとに `TextureChanged` が出る。決定すると `Confirmed` が出て、そのときの食感が付く。キャンセルはない。
- 実機の決定は、デバイスに付くハードのボタン 1 つです。Mock では、右 A ボタン、左手のつまみ、Enter で代わりにします。右手で握るのは、棒を手に持つ見た目のためだけです。

### 動いているかの見分け方
- 棒の先端の 1 行目: いまの食感（まだ噛んでいなければ `-`）。噛んでいる間は `* placeholder-a` のように `*` が付く。決定すると 1 秒だけ `OK: placeholder-b` が出る。2 行目は内容量（%）。
- Console: `[MockDevice] Bitten`、`[MockDevice] TextureChanged: placeholder-a`、`[MockDevice] Confirmed: placeholder-b`（噛む前の決定は `(噛む前)`）が出る。うるさいときは `MockDevice` の `Log Events` を切る。

## 自分のコードで受け取る
`Devices.WhenReady` で `IDevice` を受け取る（Device の asmdef は参照しない）。Mock でも実機でも、同じコードで動く。例は [Core/README.md](../Assets/Scripts/Core/README.md) の「デバイスの入力を受け取る」。

- `Confirmed` は、決定したときの食感（`TextureId`）を運ぶ。噛む前の決定なら空の ID。
- いまの食感は `Current`。

## 変えたいとき
- `MockDevice` の Inspector で、噛む・決定の操作（Input Action のバインド）、切替間隔、食感 ID を変える。
- `MockDeviceModel` の Inspector で、握る操作、握りの強さ、手の位置（空なら `RightHandAnchor` を探す）を変える。
- 設定を残したいときは、メニュー **MouthOfGod > Device > Create MockDevice Prefab** で Prefab（`Assets/Resources/Prefabs/Device/MockDevice.prefab`）を作り、その Inspector で変えてコミットする。Play すると、Mock はこの Prefab から作られる（既にあれば上書きしない）。
- シーンに置きたいときは、Prefab を置く（置いたものが使われる。Prefab より優先）。`MockDevice` コンポーネントだけを足すと、棒の見た目（`MockDeviceModel`）が付かない。
- 食感 ID の既定は `placeholder-a` 〜 `placeholder-f` の 6 つ。実際の ID は #31 / #54 で決まる。

## まだ確かめていないこと
- Quest + Link での動作（右トリガー・右 A ボタン・握り）。
- OpenXR の Hand Interaction Profile を有効にしたときの `<HandInteraction>{RightHand}/graspValue` の名前。違えば Inspector で直す。
- 左手の握り・つまみ（`<HandInteraction>{LeftHand}/graspValue`・`pinchValue`）は、まだ実機で確かめていません。左手のつまみ（手のひらを自分に向けた形）は、Quest のシステムメニューと重なることがあります。
- ボタンを付ける場所は、まだ決めていません。候補は、デバイスか Quest コントローラです（#7）。
- 噛む動きは検出しないことに、10/14 に決まりました。

コードは [Assets/Scripts/Device/README.md](../Assets/Scripts/Device/README.md)。入力の流れは [architecture.md](architecture.md) の「デバイス連携」。
