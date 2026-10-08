# VR 環境のセットアップ（Meta Quest + Quest Link）

PC に Quest を有線／Air Link で接続し、Unity の Play 中に PC 側で描画する構成。OpenXR を使う。

> **状態**: パッケージの追加までは PR で済んでいる。Unity で行う設定（手順 3〜5）と、その結果の `ProjectSettings/` のコミットは、**Unity を開いて実機で確認した人が行う**。この手順書は一般的な内容で、実機での確認はまだ。

## 入っているパッケージ（`Packages/manifest.json`）
| パッケージ | バージョン | 役割 |
|---|---|---|
| `com.unity.xr.openxr` | 1.18.0 | OpenXR 本体（`com.unity.xr.management` も依存で入る） |
| `com.unity.xr.interaction.toolkit` | 3.6.1 | コントローラー操作・掴む・UI 操作 |
| `com.unity.inputsystem` | 1.19.0 | 入力（既存。`activeInputHandler` は Input System のみ） |

Meta 固有の機能（パススルー、ハンドトラッキングなど）が必要になったら、`com.unity.xr.meta-openxr` を追加する。AR Foundation など依存が増えるため、今は入れていない。

## 手順
1. **PC 側**: Meta Quest Link アプリを入れ、Quest を接続する。アプリの `設定 > 一般` で「Meta Quest Link を OpenXR ランタイムとして設定」を有効にする。
2. **Unity を開く**: `git pull` 後、初回はパッケージ解決に時間がかかる。
3. `Edit > Project Settings > XR Plug-in Management`
   - **PC（Windows アイコンのタブ）** で `OpenXR` にチェックを入れる。
   - `OpenXR` の項目で、`Enabled Interaction Profiles` に **Oculus Touch Controller Profile** を追加する。
   - `Project Validation` に出る警告を `Fix All` で直す。
4. **リグを用意する**: `Window > Package Manager > XR Interaction Toolkit > Samples` で **Starter Assets** を Import し、`XR Origin (XR Rig)` の Prefab をシーンに置く。
   - 動作確認用のシーンは `Assets/Scenes/Sandbox/<名前>_vr-test.unity`（CONTRIBUTING.md「サンドボックスシーン」）。`Main.unity` には置かない。
5. Quest を接続したまま Play し、頭とコントローラーが追従することを確認する。
6. 生成された `ProjectSettings/XRSettings.asset`、`Assets/XR/`、`Assets/Samples/` などの差分を、**この PR のブランチにコミット**する。

## 位置取得について
ゲームでは、コントローラーの位置でプレイヤーの位置・指す場所を取得する想定（README）。実装は `Assets/Scripts/Tracking/`（CONTRIBUTING.md「6. スクリプト方針」）に置く。旧プロジェクト MealBeBack は Vive トラッカーを使っていたが、今回は Quest のコントローラーが前提。
