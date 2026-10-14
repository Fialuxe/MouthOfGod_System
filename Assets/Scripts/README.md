# Scripts

スクリプトはすべてここに置く。機能ごとにフォルダを切り、フォルダの中の分け方は担当者が自由に決めてよい。
方針の詳細は [CONTRIBUTING.md「6. スクリプト方針」](../../CONTRIBUTING.md) を参照。
機能どうしのつなぎ方と、避けること（アンチパターン）は [docs/architecture.md](../../docs/architecture.md) を参照。

| フォルダ | 役割 | 担当 |
|---|---|---|
| `Core/` | 全体で共有する小さな基盤（ID の型 `TextureId` `FoodId` `FieldId` `CueId`、イベントのデータ、`IDevice`）。他のフォルダに依存しない | |
| `Device/` | 触感デバイス・Arduino との通信。いまは、キーボードで動く `MockDevice`（`IDevice` の擬似実装） | |
| `Tracking/` | VR・位置取得 | |
| `Tutorial/` | チュートリアル 1〜4 の進行 | |
| `Game/` | 本編の進行、スコア、判定 | |
| `Village/` | ミニチュアの村・フィールド・村人 | |
| `UI/` | 画面表示 | |

フォルダを増やしたら、この表に 1 行足す。

## 実装する前に（役割ごとに知っておくこと）

なぜこうなったか、何を考えたかは [docs/decisions.md「2026-10-13 コードの型…」](../../docs/decisions.md) にある。ここは、自分の仕事に要ることだけ。

**全員**
- 共有シーンは `Assets/Scenes/Main.unity` の 1 つ。編集は Scene Fusion のセッションの中だけ。機能の動作確認は、自分の `Assets/Scenes/Sandbox/` のシーンで行う。
- 同じファイルを取り合わない。`.unity` `.prefab` `.asset` は Git でマージできない。PR が同じファイルでぶつかったら、分け方が悪いサイン。
- Play したあとに、手でつなぐ設定を残さない（CONTRIBUTING §6）。

**実装担当（C#）**
- 自分の機能のフォルダ `Assets/Scripts/<機能>/` の中だけで作る。他の機能のクラスを直接参照しない。つなぐのは `Core` の interface かイベント（参照してよいのは自分と `Core` だけ。破ると PR に警告が出る）。
- 食感・食べ物・フィールドを `enum` にしない。ID は文字列（`TextureId` `FoodId` `FieldId`）。
- 「チュートリアル 1 ならこう」という分岐を、共通のコードに書かない。進行は `TutorialRunner` が共通で、中身は `TutorialStep` のデータ。
- チュートリアルのあいだで引き継ぐものは、`IProgress` の記録の名前だけを見る（T2 のことは見ない）。記録を足すときは [progress-keys.md](../../docs/progress-keys.md) に 1 行足す。
- 型と、データの置き場所: [CONTRIBUTING「型の名前とデータの置き場所」](../../CONTRIBUTING.md#型の名前とデータの置き場所)。型の一覧とイベントの流れ: [docs/architecture.md](../../docs/architecture.md)。

**素材担当**
- モデル・テクスチャ・音は、`Assets/Resources/` の外（`Assets/ArtisticResources/` など）に置く。タグ（食感・置き場所）を素材に埋め込まない。データ側で持つ。
- Prefab は `Assets/Resources/Prefabs/<機能>/` に置く（フォルダ名はスクリプトとそろえる）。
- 食べ物を足すときは、`FoodDefinition` を 1 つ足すだけ。共有のリストは触らない。

**企画・シーン担当**
- データ（ScriptableObject）は `Assets/Resources/Data/<種類>/` に、1 件 1 アセットで作る。チュートリアルは `Data/Tutorial/T1/`、`T2/` と分ける。
- フィールドの定義（山・海・草原・村、足りない状態と届いた後の状態）は [docs/fields.md](../../docs/fields.md)。
- 引き継ぎの記録の名前は、[progress-keys.md](../../docs/progress-keys.md) の一覧にあるものだけ使う。

**レビュワー**
- 見るのは、(1) 他の機能を直接参照していないか（警告）、(2) `enum` や直書きの ID が入っていないか、(3) 同じファイルを複数の PR が触っていないか、(4) 記録の名前が一覧と合っているか。
