# Tracking

VR・位置取得。いまは、パススルーカメラの映像から AprilTag を検出し、タグの位置にオブジェクトを合わせる機能（Issue #25）。

担当: （未定）

## 構成

| 型 | 役割 |
|---|---|
| `AprilTagDetectionService` | シーンに **1 つだけ**置く。カメラの RGB フレームを読み、AprilTag を検出して、ワールド座標の姿勢を公開する（`IAprilTagSource`）。カメラの読み出しと検出は重いので、タグごとではなくここで 1 回だけ行う |
| `AprilTagPlace` | **タグ 1 枚につき 1 つ**、シーンに置く。指定した ID のタグの位置に、対象の Transform（既定は自分自身）を合わせる |
| `PoseTracker` | 平滑化と「見つけた／見失った」の判定（Unity のオブジェクトに依存しない） |
| `AprilTagGeometry` | 内部パラメータの換算と座標変換（同上） |
| `AprilTagObservation` / `IAprilTagSource` | 検出結果と、その供給元のインターフェース |

## 外に見せる API

他の機能が使うのは、次の 3 つだけ。

- `AprilTagPlace` の **Transform**: 子オブジェクトにするか、位置を読む。タグを見失っても、最後の位置に残る。
- `AprilTagPlace.IsTracked`: いまタグが見えているか。
- `AprilTagPlace.Found` / `Lost`: 見つけた／一定時間見えなくなったときのイベント。表示の切り替えなどは、受け取った側で行う。

`AprilTagDetectionService` と `IAprilTagSource` は、この機能の内部。他の機能からは直接使わない。

## 使い方

1. シーンに空の GameObject を作り、`AprilTagDetectionService` を付ける（`PassthroughCameraAccess` が自動で付く）。`Tag Size Meters` を、印刷したタグの黒い外枠を含む一辺の長さに合わせる。
2. タグごとに GameObject を作り、`AprilTagPlace` を付ける。`Tag Id` と `Source`（手順 1 のオブジェクト）を設定する。位置を合わせたいオブジェクトは、その子にする。
3. Quest Link で Play する（Game ビューを前面にする。`docs/vr-setup.md` を参照）。

使うタグは `tagStandard41h12` のみ（使っているライブラリの制約）。画像は [AprilRobotics/apriltag-imgs](https://github.com/AprilRobotics/apriltag-imgs) の `tagStandard41h12/` にある。

## 前提と注意

- Link では、Developer Runtime Features が有効で、Meta Horizon Link が v85 以上であること。
- 検出結果の向きの軸は、AprilTag ライブラリの規約に従う。向きが期待と違うときは、`Local Euler Offset` で合わせる。
- `PassthroughCameraAccess.GetCameraPose()` は、ドキュメント上は「ワールド座標のカメラ姿勢」を返す。`OVRCameraRig` を原点から動かす・回すシーンでは、タグの位置がずれないか実機で確認する（未確認）。
- 診断ログ（`[AprilTag]`）に、検出回数・処理時間・検出した ID が出る。検証の結果は Issue #25 に書く。

## テスト

`Tests/` に EditMode のテストがある（Window > General > Test Runner > EditMode）。
