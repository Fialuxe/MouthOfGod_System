# Food

デバイスで決定した食感から、対応する食べ物を引いて、口の位置に生み、前へ飛ばす。食感と食べ物の対応は、`FoodTable`（表）で持つ。

> 状態: 案（#31）。Unity では未確認（コンパイルと EditMode のテストは、Unity の代わりの簡単な土台で確かめただけ）。名前と細部は仮。決めた経緯と、採らなかった案は [docs/decisions.md](../../../docs/decisions.md)（2026-10-19）。

## 部品

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FoodTable` | 共通の表は `FoodSpawner` と同じオブジェクト。専用の表は、そのフィールドの Prefab の一番上 | 「食感 → 食べ物の Prefab」の表。**Field Id が空なら共通の表**、フィールドの ID（`village` / `mountain` / `sea` / `grassland`）なら、そのフィールドの専用の表。有効になると、自分で `FoodTables` に登録する |
| `FoodSpawner` | シーンに 1 つ | 決定した食感（`IDevice.Confirmed`）で表を引き、**Spawn Point** の位置に食べ物を生む。**Launch Speed** が 0 より大きく、食べ物に Rigidbody があれば、Spawn Point の前向きに初速を与える。そのあとの動きは、食べ物の Rigidbody（Mass など）しだい |
| `FoodTables` | （置き場。付けない） | いまある `FoodTable` の掲示板。`Resolve(FieldId, TextureId)` で引く。そのフィールドの表 → 共通の表 → なし、の順 |
| `FoodItemTag` | （`FoodSpawner` が、生んだ食べ物に付ける） | Core の `IFoodItem` を実装し、どの食べ物かを渡す。届け先の判定などは、Food を参照せずに `IFoodItem` で読む |

参照するのは Core だけ。

## 使い方

1. シーンの空のオブジェクトに `FoodSpawner` と `FoodTable` を付ける。`FoodTable` の Field Id は空のまま。
2. 食べ物の Prefab を `Assets/Resources/Prefabs/Food/` に置く。**Prefab のファイル名が、食べ物の ID（`FoodId`）になる**。`FieldStep` の欲しい物（Food）と同じ文字にする。飛ばしたいときは、Prefab に Rigidbody を付ける（Mass・Drag で動きが変わる）。いまある `carrot`・`pumpkin`・`sheep`・`pig`・`apple`・`fish`・`cow` は、メニューの **MouthOfGod > Food > Generate Prefabs**（`Editor/FoodPrefabGenerator.cs`）で素材から作ったものです。大きさを変えるときは、そのスクリプトの表を直し、Prefab を消してから作り直します。村人の吹き出しに出すアイコンは、メニューの **MouthOfGod > Food > Generate Icons**（`Editor/FoodIconGenerator.cs`）で、Prefab を写して `Assets/Resources/Icons/Food/` に作ります。Prefab を直したら、アイコンも作り直します。
3. `FoodTable` の Rows に、1 行ずつ足す。Texture に食感の ID、Food に Prefab をドラッグする。デバイスなしで試すときの食感は、`MockDevice` の `placeholder-a` `placeholder-b` `placeholder-c`。
4. フィールドごとに変えたいときだけ、そのフィールドの Prefab に `FoodTable` を付け、Field Id にフィールドの ID を入れ、変えたい食感の行だけ足す。ほかの食感は共通の表が使われる。
5. **共通の表はなくてもよい**（なければ、専用の表にない食感は何も生まれない）。

| やりたいこと | すること |
|---|---|
| 食感を増やす・減らす | 共通の表の行を足す、または消す |
| フィールドごとに変える | そのフィールドの Prefab に `FoodTable` を付ける |
| 共通に戻す | そのフィールドの `FoodTable` を外す |
| 食べ物を足す | Prefab を置き、表に 1 行足す |
| 全体を見る | `FoodTable` を右クリックして「対応一覧を出す」（再生中は、フィールド × 食感の表が Console に出る） |

## 「体験者の前のフィールド」

食べ物は、立っている場所のフィールドと食感で決まる。このフィールドを判定する仕組みは、まだない。立つ位置の取り方は #64（届ける操作）、移動は #80 で扱う（#62 は 10/13 に閉じて、残りをその 2 つに移した）。判定を作る側が、`FoodSpawner.CurrentField`（`Func<FieldId>`）に関数を入れる。入れるまでは、いつも「なし」で、共通の表だけを使う。

## 起動時の警告

`FoodSpawner` の `Start` で、次の食い違いを Console に出す。

- 食感が空の行、Prefab が空の行。
- 同じ表に、同じ食感の行が 2 つある（先の行を使う）。
- 専用の表にある食感が、共通の表にない（共通の表があるときだけ）。

食べ物を引けなかったときは、決定のたびに警告を出す。

`FieldStep` の欲しい物が、そのフィールドで引けるかの突き合わせは、まだない。Food は Village を参照できないため。置き場は [#31](https://github.com/Fialuxe/MouthOfGod_System/issues/31) で相談中。

## 作らないもの

- 口の位置の目印（`MouthAnchor`）を置くこと。#53 は 10/13 に閉じて、残りの作業（ずれの扱い、目印を置く、見やすさの確認）が #31 に移った。まだ置いていない。
- 体験者の前のフィールドの判定（立つ位置の取り方は #64、移動は #80）。届け先の判定（#63）。
- 間違えたとき（欲しい物と違う食べ物が届いたとき）の反応と、採点。`IFieldRequests` の `Received` の知らせを受ける側が足す。
- 影の表示（UI の仕事）。`FoodSpawner.Preview(TextureId)` が、生まずに Prefab を引く。

## テスト

`Tests/`（EditMode）。表の引き方・登録・警告（`FoodTablesTests`）と、生む処理・デバイスの受け取り（`FoodSpawnerTests`）。
