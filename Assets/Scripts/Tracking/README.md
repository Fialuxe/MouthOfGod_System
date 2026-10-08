# Tracking

VR・位置取得。いまは、パススルーカメラの映像から AprilTag を検出し、タグの位置にオブジェクトを合わせる機能（Issue #25）。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `AprilTagDetectionService` | シーンに **1 つだけ**存在する（`AprilTagPlace` が必要なときに自動で作るので、手で置かなくてよい）。カメラの RGB フレームを読み、AprilTag を検出して、ワールド座標の姿勢を公開する（`IAprilTagSource`）。カメラの読み出しと検出は重いので、タグごとではなくここで 1 回だけ行う |
| `AprilTagSettings` | 検出の設定（タグのサイズなど）。`Assets/Resources/AprilTagSettings.asset` に保存する。なければコードの既定値で動く |
| `AprilTagRecalibrateInput` | タグの位置の取り直しの入力（Quest コントローラーの A / X ボタン、キーボードの R）。サービスが自動で付ける |
| `AprilTagPlace` | **タグ 1 枚につき 1 つ**、シーンに置く。指定した ID のタグの位置に、対象の Transform（既定は自分自身）を合わせる |
| `PoseTracker` | 追従モードの、平滑化と「見つけた／見失った」の判定（Unity のオブジェクトに依存しない） |
| `PoseCalibrator` | 固定モードの、観測の収集と、外れ値を除いた平均（同上） |
| `AprilTagGeometry` | 内部パラメータの換算と座標変換（同上） |
| `AprilTagObservation` / `IAprilTagSource` | 検出結果と、その供給元のインターフェース |

## 外に見せる API

他の機能が使うのは、次の 3 つだけ。

- `AprilTagPlace` の **Transform**: 子オブジェクトにするか、位置を読む。タグを見失っても、最後の位置に残る。
- `AprilTagPlace.IsTracked`: 追従モードでは、いまタグが見えているか。固定モードでは、位置が固定済みか。
- `AprilTagPlace.Found` / `Lost`: 見つけた／一定時間見えなくなったときのイベント。表示の切り替えなどは、受け取った側で行う。

`AprilTagDetectionService` と `IAprilTagSource` は、この機能の内部。他の機能からは直接使わない。

## 使い方

1. タグごとに GameObject を作り、`AprilTagPlace` を付ける。`Tag Id` を設定する。位置を合わせたいオブジェクトは、その子にする。**これだけでよい**（検出サービスは自動で作られる）。
2. タグのサイズを `Assets/Resources/AprilTagSettings.asset` の `Tag Size Meters` に入れる（下記）。
3. Quest Link で Play する（Game ビューを前面にする。`docs/vr-setup.md` を参照）。
4. タグを動かしたときは、コントローラーの A / X ボタン、またはキーボードの R で、位置を取り直す（固定モードのとき）。

### タグのサイズ（`Tag Size Meters`）

使うタグは `tagStandard41h12` のみ（使っているライブラリの制約）。画像は [AprilRobotics/apriltag-imgs](https://github.com/AprilRobotics/apriltag-imgs) の `tagStandard41h12/` にある。

このタグは 9×9 マスでできている。

- 一番外側の 1 周は、データのマス（白黒が混ざる）。
- その内側に、**黒い枠が 1 周**ある（外側の辺が 7 マス分）。
- 黒い枠の内側は、白い 1 周と、中央の 3×3 のデータ。

`Tag Size Meters` に入れるのは、**黒い枠の内側の縁で囲まれた正方形（5 マス分）の一辺**。1 マスの一辺 × 5 で求める。黒い枠の外側の辺（7 マス分）でも、全体（9 マス分）でもない。

| 1 マスの一辺 | `Tag Size Meters` |
|---|---|
| 1 cm | 0.05 |
| 1.5 cm | 0.075 |
| 2 cm | 0.10 |

ここがずれると、検出はできても、距離（奥行き）と位置がその割合でずれる。定規で 1 マスを測るのが確実。

## ぶれを抑える

`AprilTagPlace` の項目（値はシーンに保存される）で切り替える。既定はどちらもオフ。

| 設定 | 効果 | 使いどころ |
|---|---|---|
| `Lock After Calibration` | 観測を `Calibration Samples` 個集め、外れ値を除いて平均した位置に置き、**以後は動かさない**。取り直すときは、A / X ボタンか R キー（またはコードから `AprilTagPlace.RecalibrateAll()`。開発中は、コンポーネントの `⋮` メニューの `Recalibrate` でも実行できる） | タグが動かない（机に置いたまま使う）とき。最も安定する |
| `Keep Normal Vertical` | タグ面の法線を鉛直に揃え、傾き（ピッチ・ロール）を捨てる。法線まわりの回転（向き）は残す。`Tag Normal Axis` は、タグ面に垂直な軸（既定は Z） | 机・床など、水平な面に置いたタグ。壁のタグではオフ |
| `Smoothing Seconds` | 追従モードの平滑化。大きいほどなめらかだが遅れる | 追従モードのとき |

- 固定モードの進み具合は `CalibrationProgress`（0〜1）と `IsCalibrating` で読める。固定が終わると `Found` が呼ばれる。`Recalibrate()` を呼ぶと、`Lost` が呼ばれ、集め直したあとに `Found` が呼ばれる。集め直している間、対象は直前の位置に残る。
- 固定中は、タグが隠れても、動かしても、位置は変わらない。タグを動かしたら `Recalibrate()` で取り直す。
- 集めている間は、頭をできるだけ動かさない。

## 前提と注意

- Link では、Developer Runtime Features が有効で、Meta Horizon Link が v85 以上であること。
- 検出結果の向きの軸は、AprilTag ライブラリの規約に従う。向きが期待と違うときは、`Local Euler Offset` で合わせる。
- `PassthroughCameraAccess.GetCameraPose()` は、ドキュメント上は「ワールド座標のカメラ姿勢」を返す。`OVRCameraRig` を原点から動かす・回すシーンでは、タグの位置がずれないか実機で確認する（未確認）。
- 診断ログ（`[AprilTag]`）に、検出回数・処理時間・検出した ID が出る。検証の結果は Issue #25 に書く。
- サービスと設定は自動で用意されるので、シーンに検出用のオブジェクトを置く必要はない。過去に手で置いたものが残っていても、そのまま使われる（設定は `AprilTagSettings` から読む）。

## テスト

`Tests/` に EditMode のテストがある（Window > General > Test Runner > EditMode）。
