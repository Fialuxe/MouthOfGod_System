# デバイスなしで開発・動作確認する（MockDevice）

触感デバイスを持っていなくても、Quest + Link で Play するだけで「噛む → 食感が変わる → 決定」を試せる。
ゲームは `IDevice`（`Assets/Scripts/Core/IDevice.cs`）だけを見ているので、実機ができたら差し替えるだけで済む。
実機のつなぎ方と必要な環境は #47（仕様が決まってから）。

> 状態: Unity では未確認。操作の割り当てと数値は暫定。

## 手順
1. [vr-setup.md](vr-setup.md) のとおり Quest Link で Play できる状態にする。
2. Play する。`MockDevice` は自動で作られる（シーンに置く必要も、Inspector の設定もない）。
3. 試す。

| やること | Quest（ハンドトラッキング） | Quest コントローラ | キーボード |
|---|---|---|---|
| 棒（デバイス）を持つ | 右手を握る | 右グリップ | G を押している間 |
| 噛む（押している間） | — | 右トリガー | Space |
| 決定（実機の「ハードのボタン 1 つ」の代わり） | — | 右 A ボタン | Enter |

- 棒の先端に、デバイスの内容量（0〜100 %）が出る。`MockDevice` の `Content Percent`（Inspector のスライダー、またはコードの `ContentPercent`）で動かす。内容量を誰がどう変えるか（ゲームからの指令など）は未定なので、Mock は値を出すだけ。
- 握っている間、棒は手に付いてくる。離すとその場に残る。
- 噛み始めで `Bitten`、噛んでいる間は一定間隔（既定 1.5 秒）で `TextureChanged`、決定で `Confirmed` が出る。キャンセルはない。
- 決定は、デバイスに付くハードのボタン 1 つ。手の動きでは決定しない。Mock では、そのボタンの代わりに右 A ボタン / Enter を使う。握るのは、棒を手に持つ見た目のためだけ。

## 変えたいとき
- `MockDevice` の Inspector で、噛む・決定の操作（Input Action のバインド）、切替間隔、食感 ID を変える。
- `MockDeviceModel` の Inspector で、握る操作、握りの強さ、手の位置（空なら `RightHandAnchor` を探す）を変える。
- 自動で作られる `MockDevice` に設定を残したいときは、シーンに手で置く。`MockDevice.GetOrCreate()` は、あれば置いたものを使う。
- 食感 ID の既定は `placeholder-a/b/c`。実際の ID は #31 / #54 で決まる。

## まだ確かめていないこと
- Unity でのコンパイル、Quest + Link での動作（握り・先端の数字）。
- OpenXR の Hand Interaction Profile を有効にしたときの `<HandInteraction>{RightHand}/graspValue` の名前。違えば Inspector で直す。
- Link 上で、Editor の Game ビューにフォーカスが要るか（キーボードの場合）。
- 新デバイスが噛みを検出するのか、ボタンはデバイスか Quest コントローラか（#7 で人に確認していた点）。

コードは [Assets/Scripts/Device/README.md](../Assets/Scripts/Device/README.md)。入力の流れは [architecture.md](architecture.md) の「デバイス連携」。
