# Tracking

VR と位置の取得です。いまは、パススルーカメラの映像から AprilTag を検出します。タグの位置に、オブジェクトを合わせます。

検証の結果は #25 に書きます。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `AprilTagDetectionService` | シーンに **1 つだけ**あります。`AprilTagPlace` が必要なときに自動で作るので、手で置かなくてよいです。カメラの RGB フレームから AprilTag を検出します。検出した姿勢は、ワールド座標で公開します（`IAprilTagSource`）。カメラ映像の取り込みと検出は重いので、ここで 1 回だけ行います。タグごとには行いません |
| `AprilTagSettings` | 検出の設定（タグのサイズなど）です。`Assets/Resources/AprilTagSettings.asset` に保存します。なければ、コードの既定値で動きます |
| `AprilTagRecalibrateInput` | タグの位置を取り直す入力です。Quest の左コントローラーの Y ボタンを 2 秒長押しするか、キーボードの R です。サービスが自動で付けます |
| `HandHeldPose` | 手（ハンドトラッキング）の位置に、オフセットを足して、手に持ったものの先の位置に自分を動かします。フォークなら、`ForkTip` のオブジェクトかその親に付けます。手は OVRCameraRig の `RightHandAnchor` から読みます。先を口に当てて C キーを押すと、オフセットを測り直します（開発中の入力。Quest の手で押せる入力は未定） |
| `HandHeldTip` | 手の姿勢とオフセットから、先の姿勢を出す計算です。ぶれのならしと、オフセットの測り直しも持ちます（Unity のオブジェクトに依存しません） |
| `AprilTagPlace` | **タグ 1 枚につき 1 つ**、シーンに置きます。指定した ID のタグの位置に、対象の Transform を合わせます。対象の既定は自分自身です |
| `PoseTracker` | 追従モードの、平滑化と「見つけた／見失った」の判定です。Unity のオブジェクトに依存しません |
| `PoseCalibrator` | 固定モードの、観測の収集と、外れ値を除いた平均です（同上） |
| `AprilTagGeometry` | 内部パラメータの換算と、座標変換です（同上） |
| `AprilTagObservation` / `IAprilTagSource` | 検出結果と、その供給元のインターフェースです |

## 外に見せる API

他の機能が使うのは、次の 3 つだけです。

- `AprilTagPlace` の **Transform**: 子オブジェクトにするか、位置を読みます。タグを見失っても、最後の位置に残ります。
- `AprilTagPlace.IsTracked`: 追従モードでは、いまタグが見えているかです。固定モードでは、位置が固定済みかです。
- `AprilTagPlace.Found` / `Lost`: 見つけたとき、一定時間見えなくなったときのイベントです。表示の切り替えなどは、受け取った側で行います。

`AprilTagDetectionService` と `IAprilTagSource` は、この機能の内部です。他の機能からは直接使いません。

## 使い方

1. タグごとに GameObject を作り、`AprilTagPlace` を付けます。`Tag Id` を設定します。位置を合わせたいオブジェクトは、その子にします。**これだけでよいです**。検出サービスは自動で作られます。
2. タグのサイズを、`AprilTagSettings.asset` の `Tag Size Meters` に入れます（下記）。
3. Quest Link で Play します。Game ビューを前面にします（[docs/vr-setup.md](../../../docs/vr-setup.md)）。
4. タグを動かしたら、位置を取り直します（固定モードのとき）。左コントローラーの Y ボタンを 2 秒長押しするか、R キーを押します。

### タグのサイズ（`Tag Size Meters`）

使うタグは `tagStandard41h12` だけです。使っているライブラリの制約です。画像は [AprilRobotics/apriltag-imgs](https://github.com/AprilRobotics/apriltag-imgs) の `tagStandard41h12/` にあります。

このタグは 9×9 マスでできています。

- 一番外側の 1 周は、データのマスです（白黒が混ざります）。
- その内側に、**黒い枠が 1 周**あります。枠の外側の辺は 7 マス分です。
- 黒い枠の内側は、白い 1 周と、中央の 3×3 のデータです。

`Tag Size Meters` には、**黒い枠の内側の縁で囲まれた正方形の一辺**を入れます。5 マス分なので、1 マスの一辺 × 5 で求めます。枠の外側の辺（7 マス分）でも、全体（9 マス分）でもありません。

| 1 マスの一辺 | `Tag Size Meters` |
|---|---|
| 1 cm | 0.05 |
| 1.5 cm | 0.075 |
| 2 cm | 0.10 |

ここがずれると、検出はできても、距離（奥行き）と位置がその割合でずれます。定規で 1 マスを測るのが確実です。

### ぶれを抑える

`AprilTagPlace` の項目で切り替えます。値はシーンに保存されます。既定はどちらもオフです。

| 設定 | 効果 | 使いどころ |
|---|---|---|
| `Lock After Calibration` | 観測を `Calibration Samples` 個集めます。外れ値を除いた平均の位置に置き、**以後は動かしません**。取り直しは、左コントローラーの Y ボタンの 2 秒長押しか R キーです。コードからは `AprilTagPlace.RecalibrateAll()` です。開発中は、コンポーネントの `⋮` メニューの `Recalibrate` でもできます | タグが動かないとき（机に置いたまま使うとき）。最も安定します |
| `Keep Normal Vertical` | タグ面の法線を鉛直にそろえ、傾き（ピッチ・ロール）を捨てます。法線まわりの回転（向き）は残します。`Tag Normal Axis` は、タグ面に垂直な軸です（既定は Z） | 机や床など、水平な面のタグ。壁のタグではオフにします |
| `Smoothing Seconds` | 追従モードの平滑化です。大きいほどなめらかですが、遅れます | 追従モードのとき |

- 固定モードの進み具合は、`CalibrationProgress`（0〜1）と `IsCalibrating` で読めます。固定が終わると `Found` が呼ばれます。
- `Recalibrate()` を呼ぶと、まず `Lost` が呼ばれます。集め直したあとに `Found` が呼ばれます。集め直す間、対象は直前の位置に残ります。
- 固定中は、タグが隠れても、動かしても、位置は変わりません。タグを動かしたら `Recalibrate()` で取り直します。
- 集めている間は、頭をできるだけ動かしません。

## 前提と注意

- Link では、Developer Runtime Features を有効にします。Meta Horizon Link は v85 以上が要ります。
- 検出結果の向きの軸は、AprilTag ライブラリの規約に従います。向きが期待と違うときは、`Local Euler Offset` で合わせます。
- 向きは、2 つのオフセットで合わせます。`Local Euler Offset` は、タグの座標系の回転です。`World Euler Offset` は、ワールドの軸の回転で、Y が鉛直軸まわりの向きです。ワールドの回転は、タグの位置を軸に全体を回します。回したあとの向きに、位置のオフセットとローカルの回転を足します。ワールドの回転が 0 なら、これまでと同じ動きです。
- **Terrain のように回せないものが子にあるとき**は、`Align Rig Instead` をオンにします。`OVRCameraRig`（ユーザー側）を動かして、観測したタグを対象のいまのワールド姿勢に重ねます。対象は動かしません。`MinitureBase` に付ければ、子は何も動かないので、Terrain も Static の子も影響を受けません。
  - 固定モード（`Lock After Calibration`）と `Keep Normal Vertical` を、自動で有効にします。固定が終わったときに 1 回だけリグを合わせます。取り直す（左 Y の長押し、R キー）と、もう一度合わせます。
  - リグは、位置と鉛直軸まわりの向きだけ動かします。傾きは変えません。
  - 対象の向きは、Y が上、前（Z）が水平になるようにします。タグを机に寝かせると、タグの Z（面の法線）が上を向くので、`Local Euler Offset` に X = -90 などを入れて合わせます。
  - `Follow Rotation` をオフにすると、向きは合わせず、位置だけ合わせます。
  - Quest を再センタリングすると、合わせた位置がずれます。そのときは取り直します。
- `PassthroughCameraAccess.GetCameraPose()` は、ドキュメント上はワールド座標のカメラ姿勢を返します。`OVRCameraRig` を原点から動かしたり回したりするシーンもあります。そのときにタグの位置がずれないかを、実機で確かめます。
- 診断ログ（`[AprilTag]`）に、検出回数・処理時間・検出した ID が出ます。
- サービスと設定は自動で用意されます。シーンに検出用のオブジェクトを置く必要はありません。手で置いたものが残っていても、そのまま使われます。設定は `AprilTagSettings` から読みます。

## テスト

`Tests/` に EditMode のテストがあります（Window > General > Test Runner > EditMode）。
