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
3. `Edit > Project Settings > XR Plug-in Management` で、PC タブの **Oculus**（または Meta XR の表示）を有効にする。`Project Validation`（`Meta XR > Project Setup Tool` など）に出る項目を直す。
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

## 要確認（実機で確かめること）
- **Quest Link 経由でパッケージの機能がどこまで使えるか**: パススルーを Link で使うには、Meta Quest Link アプリ側の設定が必要な場合がある。
- **部屋の情報（Scene）を、エディタの Play 中にどう得るか**: MRUK には、エディタ用に Prefab / JSON の仮の部屋を使う仕組みがある、と理解している。実際にどれが使えるか確認する。
- **アプリのビルド**: 今は PC 描画（Link）を前提にしている。Quest 単体ビルド（Android）にする場合は、別途設定が要る。

## 位置取得について
コントローラーや部屋の情報で、プレイヤーの位置や指す場所を取得する。実装は `Assets/Scripts/Tracking/` に置く（CONTRIBUTING.md「6. スクリプト方針」）。旧プロジェクト MealBeBack は Vive トラッカーを使っていたが、今回は Quest が前提。
