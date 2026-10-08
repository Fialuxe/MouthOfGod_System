# VR / MR 環境のセットアップ（Meta Quest + Quest Link + MRUK）

PC に Quest を接続し、Unity の Play 中に PC 側で描画する構成。Meta XR SDK の **Mixed Reality Utility Kit（MRUK）** を使い、部屋（床・壁・机など）の情報を扱えるようにする。

> **状態**: パッケージの追加までは PR で済んでいる。Unity で行う設定（手順 3〜5）と、その結果の `ProjectSettings/` のコミットは、**Unity を開いて実機で確認した人が行う**。この手順書は公式ドキュメントを実機で確認したものではなく、一般的な手順。画面や項目名は、実際の表示に合わせて直してよい。

## 入っているパッケージ（`Packages/manifest.json`）
| パッケージ | バージョン | 役割 |
|---|---|---|
| `com.meta.xr.mrutilitykit` | 207.0.0 | MRUK 本体（部屋・アンカーの扱い） |
| `com.meta.xr.sdk.core` | 207.0.0 | Meta XR Core SDK（`OVRCameraRig`、パススルー、コントローラー入力） |
| `com.unity.inputsystem` | 1.19.0 | 既存。`activeInputHandler` は Input System のみ |

- どちらも Unity 6000.0 対応で、Unity のパッケージレジストリで取得できる（2026-10 時点で確認）。
- **MRUK と Core のバージョンは必ず揃える**（MRUK 207.0.0 は Core 207.0.0 に依存）。上げるときは両方を同時に上げる。
- OpenXR / XR Interaction Toolkit は、今は使わない。Meta XR SDK の機能（パススルー、MRUK）を使う方針のため。

## 手順
1. **PC 側**: Meta Quest Link アプリを入れ、Quest を接続する。
2. **Unity を開く**: `git pull` 後、初回はパッケージ解決に時間がかかる。
3. `Edit > Project Settings > XR Plug-in Management` で、PC タブの **OpenXR** を有効にし、`OpenXR` の項目で **Meta XR Feature**（ある場合）を有効にする。（Core SDK 207 は OpenXR 経由で動く。Play 時のログに `Meta.XR.OpenXRFeatures` / `XRGeneralSettings` が出ていれば、この経路で動いている。）`Project Validation`（`Meta XR > Project Setup Tool` など）に出る項目を直す。
4. **シーンにリグを置く**: `GameObject > Meta XR > Building Blocks` から **Camera Rig** と **Passthrough** を追加する。MRUK を使うには、**MRUK** の Building Block も追加する。
   - 動作確認用のシーンは `Assets/Scenes/Sandbox/<名前>_vr-test.unity`（CONTRIBUTING.md「サンドボックスシーン」）。`Main.unity` には置かない。
5. Quest を接続したまま Play し、頭とコントローラーが追従することを確認する。
6. 生成された `ProjectSettings/` や `Assets/Oculus/` などの差分を、**この PR のブランチにコミット**する。

## Quest Link でパススルーを使うための前提
エラー `Failed to initialize Insight Passthrough ... Error Failure_NotInitialized` が出たときは、次を確認する（[Meta 公式: Passthrough over Quest Link](https://developers.meta.com/horizon/documentation/unity/unity-passthrough-use-over-link/)）。

1. **対応機種**: Quest 3 / 3S / 2 / Pro。
2. **バージョン**: Quest 本体が v37.0 以上、**Meta Horizon Link** PC アプリが v37.0 以上。Meta XR Core SDK も v37 以上（今回は 207 なので満たす）。
3. **Link アプリのベータ設定**: `Developer Runtime Features` を有効にしたあと、**`Passthrough over Meta Quest Link`** を有効にする（前者を有効にすると後者が出る）。**変更したら Unity を再起動する**。
4. Link アプリとヘッドセットの両方で、**開発者アカウントでサインイン**している。
5. カラーのパススルーには、**2 Gbps 以上の USB-C ケーブル**が必要。Link アプリの USB 速度テストで確認できる。
6. 上記を満たしても出るときは、`OVRManager` の `Passthrough Support`（Supported / Required）と `Insight Passthrough` の設定を確認する。

## Link でつまずいたとき
### 「システムのハードウェアが Link に対応していないため、Link の機能を利用できません」と出る
- ノート PC（Intel 内蔵 GPU + NVIDIA の Optimus 構成。RTX 5060 Laptop で確認）で出た。Link アプリのログ（`%LOCALAPPDATA%\Oculus\Service_*.txt`）に `oculus_error code=-3006 ... Error reading vendor and device Id` と `overall_compat=FAIL` が残る。
- NVIDIA ドライバは要件を満たし、Link が実際に使う GPU も RTX だった。**Link アプリを再起動したら消えた**。根本原因は特定できていない。
- 出たら、まず Link アプリ（と必要なら PC）を再起動する。それでも出るときは、ログの `-3006` を確認する。

### Unity で Play しても、ヘッドセットが Link のロード表示のまま進まない
- **Game ビューのタブを前面に出す**（Scene ビューが前面だと、XR のカメラが描画されず、Link にフレームが届かない）。Play 後に Game ビュー内を一度クリックしてもよい。
- 症状の見分け方: Unity のログ（`Editor.log`）で OpenXR セッションが `READY` のまま進まず、Link のログの `num_completed_app_frames=0` になる。Link には接続できているのに、アプリのフレームが 0 枚という状態。
- 確認済みで原因ではなかったもの: OpenXR ランタイム（Meta になっている）、XR Plug-in Management（OpenXR 有効）、`Run In Background`。

## 既知の警告（Quest Link 上で出るが、無視してよいと考えているもの）
Play 時に次の警告が大量に出る。いずれも `LogWarning` で、エラーではない。**頭・コントローラーの追従に問題がなければ、機能への影響はないと考えている**（実機では未確認）。

| 警告 | 原因（コードを読んで確認したもの） |
|---|---|
| `XR_ERROR_ACTIONSET_NOT_ATTACHED ... xrGetDeviceSampleRateFB` が**毎フレーム**出る | `OVRManager.LateUpdate()` が、**非推奨の `OVRHaptics.Process()`** を毎フレーム呼び、その中で `Config.Load()` が毎回ハプティクス情報を取りに行く。Link のランタイム側でハプティクスのアクションセットが紐づいていないため失敗する。SDK 側の挙動で、このプロジェクトのコードが原因ではない |
| `failed to get function pointer for 'xrRequestSceneCaptureFB'` | Link のランタイムが、その拡張関数を実装していない。**Link 上ではシーンキャプチャ（部屋のスキャン）を使えない**ことを示唆する（MRUK の部屋データの取得方法は別途要確認） |
| `failed to get function pointer for 'xrAgenticRegisterExternalToolMETAX1'` | 実験的な機能で、ランタイムが未対応 |
| `Local Dimming feature is not supported` | 機種またはランタイムが未対応の機能 |

- `Library/PackageCache/` の中のコードは、直接書き換えない（Git に入らず、再インポートで消える）。
- Console の警告表示を外す（フィルタ）か、SDK のアップデートを待つ。

## Quest Link で確認できたこと（Quest 3、エディタの Play）
- **パススルー映像の表示**: 映る（上の前提を満たした場合）。
- **パススルーカメラの RGB フレームの取得**: 取れる。Meta XR の `Meta.XR.PassthroughCameraAccess`（MRUK パッケージ内）を使い、`GetColors()` / `GetTexture()` で画素データを読めた。
  - 条件: Link の `Developer Runtime Features` が有効で、Meta Horizon Link が v85 以上（`PassthroughCameraAccess.IsSupported` が `True` になる）。
  - 結果: 解像度 1280×960、開始直後から `IsPlaying` が `True`。サンプルした画素のほぼすべてが黒でなく、フレームごとにタイムスタンプも進んだ（実際の映像が更新されている）。
  - 検証は、使い捨ての最小スクリプトで行い、コミットはしていない。Android 実機では `horizonos.permission.HEADSET_CAMERA` の許可が要る。

## 要確認（実機で確かめること）
- **部屋の情報（MRUK の Scene データ）を、エディタの Play 中にどう得るか**: MRUK は、ユーザーがスキャンした部屋の壁・床・家具などの形を、アンカーとして返す。Link ではシーンキャプチャ（部屋のスキャン）が使えないため、エディタ用の仮の部屋（Prefab / JSON）を使う仕組みで足りるかを確認する。
- **コントローラーの追従**: 警告は出るが問題ないと考えている。実機で確認する。
- **アプリのビルド**: 今は PC 描画（Link）を前提にしている。Quest 単体ビルド（Android）にする場合は、別途設定が要る。

## 位置取得について
コントローラーや部屋の情報で、プレイヤーの位置や指す場所を取得する。実装は `Assets/Scripts/Tracking/` に置く（CONTRIBUTING.md「6. スクリプト方針」）。旧プロジェクト MealBeBack は Vive トラッカーを使っていたが、今回は Quest が前提。
