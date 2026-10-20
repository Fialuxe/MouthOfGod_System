# Food

デバイスで決定した食感から、食べ物を引きます。口の位置に生み、前へ飛ばします。食感と食べ物の対応は、表（`FoodTable`）で持ちます。

状態: 案（#31）。名前と細部は仮です。決めた経緯と採らなかった案は、[docs/decisions.md](../../../docs/decisions.md) の 2026-10-19 にあります。

## 部品

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `FoodTable` | 共通の表は、`FoodSpawner` と同じオブジェクトです。専用の表は、そのフィールドの Prefab の一番上です | 「食感 → 食べ物の Prefab」の表です。**Field Id が空なら共通の表**です。フィールドの ID（`village` / `mountain` / `sea` / `grassland`）なら、そのフィールドの専用の表です。有効になると、自分で `FoodTables` に登録します |
| `FoodSpawner` | シーンに 1 つ | 決定した食感（`IDevice.Confirmed`）で表を引きます。**Spawn Point** の位置に食べ物を生みます。**Launch Speed** が 0 より大きく、食べ物に Rigidbody があれば、前向きに初速を与えます。向きは Spawn Point の前です。そのあとの動きは、食べ物の Rigidbody（Mass など）しだいです |
| `FoodTables` | （置き場です。付けません） | いまある `FoodTable` の掲示板です。`Resolve(FieldId, TextureId)` で引きます。そのフィールドの表、共通の表、なし、の順に探します |
| `FoodItemTag` | （`FoodSpawner` が、生んだ食べ物に付けます） | Core の `IFoodItem` を実装し、どの食べ物かを渡します。届け先の判定などは、Food を参照せずに `IFoodItem` で読みます |

## ほかの機能とのつなぎ方

- 参照するのは Core だけです。
- 食べ物は、立っている場所のフィールドと食感で決まります。このフィールドを判定する仕組みは、まだありません。
- 判定を作る側が、`FoodSpawner.CurrentField`（`Func<FieldId>`）に関数を入れます。入れるまでは、いつも「なし」です。そのため、共通の表だけを使います。
- 立つ位置の取り方は、#64（届ける操作）で扱います。移動は #80 で扱います。#62 は 10/13 に閉じました。
- 影の表示は UI の仕事です。`FoodSpawner.Preview(TextureId)` を使うと、生まずに Prefab を引けます。
- 欲しい物と違う食べ物が届いたときの反応と採点は、受ける側が足します。受ける側は、`IFieldRequests` の `Received` を受け取ります。

## 使い方

1. シーンの空のオブジェクトに、`FoodSpawner` と `FoodTable` を付けます。`FoodTable` の Field Id は空のままにします。
2. 食べ物の Prefab を `Assets/Resources/Prefabs/Food/` に置きます。**ファイル名が、食べ物の ID（`FoodId`）になります**。`FieldStep` の欲しい物（Food）と同じ文字にします。
3. `FoodTable` の Rows に、1 行ずつ足します。Texture に食感の ID を入れ、Food に Prefab をドラッグします。デバイスなしで試すときの食感は、`MockDevice` の `placeholder-a` `placeholder-b` `placeholder-c` です。
4. フィールドごとに変えたいときだけ、そのフィールドの Prefab に `FoodTable` を付けます。Field Id にフィールドの ID を入れ、変えたい食感の行だけ足します。ほかの食感は、共通の表が使われます。
5. **共通の表はなくてもよいです**。なければ、専用の表にない食感では何も生まれません。

| やりたいこと | すること |
|---|---|
| 食感を増やす・減らす | 共通の表の行を足すか、消します |
| フィールドごとに変える | そのフィールドの Prefab に `FoodTable` を付けます |
| 共通に戻す | そのフィールドの `FoodTable` を外します |
| 食べ物を足す | Prefab を置き、表に 1 行足します |
| 全体を見る | `FoodTable` を右クリックし、「対応一覧を出す」を選びます。再生中は、フィールド × 食感の表が Console に出ます |

### 食べ物の Prefab とアイコンを作る

- 飛ばしたいときは、Prefab に Rigidbody を付けます。Mass と Drag で動きが変わります。
- いまある `carrot`・`pumpkin`・`sheep`・`pig`・`apple`・`fish`・`cow` は、素材から作りました。メニューは **MouthOfGod > Food > Generate Prefabs** です（`Editor/FoodPrefabGenerator.cs`）。
- 大きさを変えるときは、そのスクリプトの表を直します。Prefab を消してから、作り直します。
- 村人の吹き出しのアイコンは、**MouthOfGod > Food > Generate Icons** で作ります（`Editor/FoodIconGenerator.cs`）。Prefab を写して、`Assets/Resources/Icons/Food/` に作ります。既にあるアイコンは上書きしません。
- 手で描いたアイコンを使うときは、食べ物の ID と同じ名前の PNG を、そのフォルダに置きます。
- Prefab を直してアイコンを作り直すときは、Project でその Prefab を選びます。右クリックの **MouthOfGod > Food > Generate Icon From Selected** を使います。

### 起動時の警告

`FoodSpawner` の `Start` で、次の食い違いを Console に出します。

- 食感が空の行と、Prefab が空の行。
- 同じ表に、同じ食感の行が 2 つある。先の行を使います。
- 専用の表にある食感が、共通の表にない。共通の表があるときだけ出します。

食べ物を引けなかったときは、決定のたびに警告を出します。

## まだないもの

- `FieldStep` の欲しい物が、そのフィールドで引けるかは、まだ確かめていません。Food は Village を参照できないためです。置き場は [#31](https://github.com/Fialuxe/MouthOfGod_System/issues/31) で相談中です。
- 口の位置の目印は、まだ置いていません。目印の名前は `MouthAnchor` です。#53 は 10/13 に閉じました。残りの作業は #31 で扱います。ずれの扱い、目印を置くこと、見やすさの確認が残っています。
- 届け先の判定は、まだありません。#63 で進めます。

## テスト

`Tests/`（EditMode）にあります。`FoodTablesTests` は、表の引き方・登録・警告を確かめます。`FoodSpawnerTests` は、生む処理とデバイスの受け取りを確かめます。
