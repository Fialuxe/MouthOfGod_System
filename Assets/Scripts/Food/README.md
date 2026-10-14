# Food

噛んで決定した食感から、生む食べ物を決める。いまあるのは、その**対応表**（`FoodCatalog`）と、食べ物 1 つ分のデータ（`FoodDefinition`）だけ。口の位置に食べ物を生む処理（FoodSpawner）は、まだ無い（#31 の第 2 弾）。

担当: （未定）

## これがあると、何が違うか

- **無いと**: 「`hard` ならリンゴ」のような対応を、FoodSpawner のコードの中に書くことになる。食べ物を 1 つ足すたびにコードを直し、全員が同じファイルを取り合う。
- **あると**: 食べ物を足す人は、アセットを 1 つ作るだけ（コードも共有のファイルも触らない）。食感の重複は、起動時にエラーで分かる。

ただし、**これ単体では画面に何も出ない**。使う側（FoodSpawner）とデータ（アセット）がそろって、はじめて「決定するとリンゴが口の位置に出る」になる。

## 流れの中での位置

```
体験者が噛む → 決定ボタン
  ↓ IDevice.Confirmed("hard")          … Device（Mock か実機）
FoodSpawner（まだ無い）
  ↓ catalog.Resolve("hard") → "apple"   … ★ ここ（FoodCatalog）
  ↓ apple の Prefab を口の位置に出す
  ↓ Spawned("apple")
Tutorial・Village・UI が「リンゴが生まれた」を受け取る
```

## 誰が、いつ使うか

| 誰が | いつ | 何をする |
|---|---|---|
| 素材・データを作る人 | 食べ物を 1 つ足すとき | Create メニューの `MouthOfGod/Food Definition` で、1 つの食べ物につき 1 つアセットを作る（ID・表示名・Prefab・対応する食感）。置き場所は `Assets/Resources/Data/Foods/` |
| FoodSpawner（コード） | ゲームの起動時に 1 回 | `FoodCatalog.LoadFromResources()` で、そのフォルダのアセットを全部読んで対応表を作る。食感の重複はここでエラー |
| FoodSpawner（コード） | 決定のたびに | `Resolve(食感)` で食べ物を引く |
| Tutorial・Village・UI | 使わない | 何が生まれたか（`Spawned`）を受け取るだけ |

## 公開 API

| 型 | 役割 |
|---|---|
| `FoodDefinition`（ScriptableObject） | 食べ物 1 つ分: `FoodId`・表示名・Prefab・分類・対応する食感（`TextureId`）。食べ物と生物は同じ型 |
| `FoodCatalog` | 食感 → 食べ物の対応表。`Build`（重複は例外）、`Resolve(TextureId)`（無ければ `FoodId.None`）、`LoadFromResources()` |

## まだ足りないこと（FoodSpawner までに）

- `Resolve` は食べ物の ID しか返さないので、生む Prefab が引けない。`FoodDefinition` を返せるようにする。
- `LoadFromResources` で 1 件も読めなかったとき、何も出さない（フォルダが無い・パスの打ち間違いに気づけない）。
- 場所ごとに違う物を出すときは、デバイスが場所ごとに別の食感 ID（例: `sea-hard`）を出す案がある。その場合も、この表は 1 食感 → 1 食べ物のままでよい。

## テスト

`Tests/` に EditMode のテストがある（Window > General > Test Runner > EditMode）。ScriptableObject を作るので、Unity の中で走らせる。
