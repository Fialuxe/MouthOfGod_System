# Quaternius の素材パック

`Assets/ArtisticResources/ThirdParty/Quaternius/` に入っている素材の、**どのパックに何があるか**の案内です（素材探しのときに読む）。
置き場所とライセンスのルールは [CONTRIBUTING 第 7 章](../CONTRIBUTING.md#7-外部素材サードパーティのアセット)、提供元・ライセンスの一覧は [CREDITS.md](../CREDITS.md)。すべて CC0 1.0（再配布・商用利用 OK）。

## 全体像

| パック | 何が入っているか | 点数 | このゲームでの使い道（案） |
|---|---|---|---|
| [Ultimate Food Pack](#ultimate-food-pack-oct-2019) | 食べ物・食器・調理器具 | 103 | 食感から「生まれる物」（リンゴ、魚、きのこなど） |
| [Medieval Village MegaKit](#medieval-village-megakit-standard) | 中世の家を組み立てるパーツ（壁・屋根・窓・ドア…） | 176 | 村の家（#115）。`House_Sample` Prefab が組み立て例 |
| [Farm Buildings](#farm-buildings-sept-2018) | 納屋・サイロ・風車・井戸・柵 | 13 | 村の建物、フィールドの飾り |
| [Farm Animals](#farm-animals) | 牛・馬・豚・羊など | 7 | 村の動物、「生まれる物」の候補 |
| [Ultimate Animated Animals](#ultimate-animated-animals-july-2021) | 鹿・キツネ・犬など（アニメーション付き） | 12 | 森・村の動物 |
| [Animated Fish Pack](#animated-fish-pack) | 魚・イルカ・サメなど（アニメーション付き） | 7 | 川（届け先）の「魚」（#15） |

**ないもの**: 村人（人型のキャラクター）、木、川の地形は、これらのパックには入っていない。村人・木・川は、別の素材（Quaternius の別パックや Kenney など。#45）か、仮の形で用意する。

## 共通の構成

どのパックも、**同じモデルを複数の形式で配っている**。Unity で使うときは、どれか 1 つの形式を選ぶ。

| フォルダ | 形式 | 使い方 |
|---|---|---|
| `FBX/` | FBX | **基本はこれ**。Unity が標準で読める。`Animated` と付くパックは、アニメーション入り |
| `OBJ/` | OBJ + `.mtl` | 形だけ（アニメーションなし）。`.mtl` は色の定義 |
| `glTF/` | glTF + `.bin` + テクスチャ | glTFast（`com.unity.cloud.gltfast`）で読む |
| `Textures/` | PNG | MegaKit だけ。パーツ共通のテクスチャ |
| `License*.txt` | テキスト | ライセンスの原文。消さない |
| `Preview.png` / `.jpg` | 画像 | パック全体の見本 |

- `.blend`（Blender の元データ）とプレビューの動画・GIF は、容量のため Git に入れていない（`.gitignore` 済み。手元にあっても無視される）。
- 同じ名前のモデルが `FBX/` と `OBJ/` にあるときは、中身は同じ形。Prefab で混ざらないように、**1 つの Prefab の中では 1 つの形式にそろえる**。
- 各ファイルの隣の `.meta` は Unity が作った管理ファイル。**消さない・名前を変えるときは Unity の中で**（参照が切れる）。

## Ultimate Food Pack (Oct 2019)

`Ultimate Food Pack - Oct 2019/`

- 構成: `FBX/`（103）、`OBJ/`（103 ×（obj + mtl））、`License.txt`、`Preview.png`
- 食べ物と、それに付く物がすべて 1 フォルダに並んでいる（種類ごとのサブフォルダはない）。名前で探す。

| 種類 | 例 |
|---|---|
| 果物・野菜 | `Apple`、`Apple_Green`、`Banana`、`Orange`、`Tomato`、`Carrot`、`Broccoli`、`Pumpkin`、`Turnip`、`Eggplant`、`Avocado`、`Coconut`、`Mushroom`、`Lettuce`、`Pepper_Red` |
| 肉・魚・卵 | `Steak`、`Bacon_Cooked`、`Sausage_Cooked`、`ChickenLeg`、`Fish`、`FishBone`、`Egg_Fried`、`Egg_Whole` |
| パン・主食 | `Bread`、`Bread_Slice`、`Croissant`、`Pancake`、`Waffle`、`Pizza`、`Pizza_Slice` |
| 料理 | `Burger`、`Cheeseburger`、`Hotdog`、`Fries`、`Corndog`、`Sushi_Nigiri1`、`Sushi_Roll1`、`Sashimi_Salmon` |
| おやつ | `Cupcake`、`Donut1`〜`Donut4`、`ChocolateBar`、`IceCream_1`〜`4`、`Popsicle_*` |
| 調味料・瓶 | `KetchupBottle`、`MustardBottle`、`SoySauce`、`Soda`、`Bottle1` |
| 食器・調理器具 | `Plate`、`Fork`、`Knife`、`Spoon`、`Chopsticks`、`FryingPan`、`CookingPot`、`CookingPot_Soup` |

焼いた・焦げた・生などの状態違い（`Bacon_Uncooked` / `_Cooked` / `_Burned` など）がある物もある。

## Medieval Village MegaKit (Standard)

`Medieval Village MegaKit[Standard]/`

- 構成: `FBX/`（176）、`OBJ/`（176 ×（obj + mtl））、`glTF/`（176 + `.bin` + テクスチャ 22 枚）、`Textures/`、`License_Standard.txt`、`Preview.jpg`
- 無料版（Standard）。有料版にだけあるパーツは入っていない。
- **完成した家ではなく、家を組み立てるパーツ**。壁・屋根・床などを並べて 1 軒を作る。組み立て例は `Assets/Resources/Prefabs/Village/House_Sample.prefab`。
- 名前は `<部位>_<素材・形>_<バリエーション>`。部位ごとのパーツ数:

| 部位（名前の頭） | 数 | 中身 |
|---|---|---|
| `Roof_` | 39 | 屋根。`RoundTiles_<幅>x<奥行>`（丸瓦）、`Wooden_*`（板）、`Front_Brick*`（妻側）、`Dormer`（屋根窓）、`Tower_RoundTiles`、`Log`（丸太）など |
| `Prop_` | 23 | 小物。`Chimney`（煙突）、`Crate`、`Wagon`、`WoodenFence_*`、`MetalFence_*`、`Vine*`（つた）、`Brick*`、`Support` |
| `Wall_` | 20 | 壁。`Plaster_*`（漆喰）と `UnevenBrick_*`（レンガ）の 2 系統。`_Door_*`（ドア付き）、`_Window_*`（窓付き）、`Arch`、`BottomCover` |
| `Overhang_` | 20 | 2 階以上の張り出し。`Plaster_*` と `UnevenBrick_*`、`Side_*`（側面）、`Corner`（角） |
| `Stairs_` / `Stair_` | 15 / 4 | 外階段。`Exterior_Straight`、`_Platform`（踊り場）、`_Sides`（手すり付き）など |
| `Floor_` | 12 | 床。`WoodDark` / `WoodLight`（木）、`Brick`、`RedBrick`、`UnevenBrick` |
| `Door_` / `DoorFrame_` | 8 / 4 | ドアとドア枠。`Flat`（四角）と `Round`（アーチ） |
| `Corner_` | 8 | 建物の角。`Exterior_*`（外側）、`Interior_*`（内側） |
| `WindowShutters_` / `Window_` | 8 / 6 | 窓の雨戸と窓 |
| `HoleCover_` | 5 | 床や屋根の穴をふさぐ蓋 |
| `Balcony_` | 4 | バルコニー（`Cross` / `Simple`、`Corner` / `Straight`） |

- `Textures/` には、パーツ共通のテクスチャが 26 枚（レンガ、漆喰、木、屋根瓦、つた、窓など。BaseColor / Normal / Roughness / ORM）。`Normals Godot-Unity/`（6 枚）は Unity・Godot 向けの法線マップ。
- 容量が最も大きいパック。Quest で表示するときは、使うパーツだけを Prefab にして、テクスチャの重さに注意する（#45）。

## Farm Buildings (Sept 2018)

`Farm Buildings - Sept 2018/`

- 構成: `FBX/`（13）、`OBJ/`（13 ×（obj + mtl））、`License.txt`、`Preview.png`
- 中身: `Barn`、`BigBarn`、`SmallBarn`、`OpenBarn`（納屋）、`ChickenCoop`（鶏小屋）、`Silo`、`Silo_House`、`Windmill`、`TowerWindmill`（風車）、`WaterTower`、`Well`（井戸）、`Fence`、`Fence2`（柵）
- 1 つが完成した建物。そのまま置ける。

## Farm Animals

`Farm Animals by @Quaternius/`

- 構成: `FBX/`（7）、`OBJ/`（7 ×（obj + mtl））、`License.txt`
- 中身: `Cow`、`Horse`、`Llama`、`Pig`、`Pug`、`Sheep`、`Zebra`
- ローポリの動物。

## Ultimate Animated Animals (July 2021)

`Ultimate Animated Animals - July 2021/`

- 構成: `FBX/`（12）、`OBJ/`（12 ×（obj + mtl））、`glTF/`（12）、`License.txt`、`Preview.jpg`
- 中身: `Alpaca`、`Bull`、`Cow`、`Deer`、`Donkey`、`Fox`、`Horse`、`Horse_White`、`Husky`、`ShibaInu`、`Stag`、`Wolf`
- アニメーション付き（FBX と glTF。OBJ は形だけ）。

## Animated Fish Pack

`Animated Fish Pack by @Quaternius/`

- 構成: `FBX/`（7）、`OBJ/`（7 ×（obj + mtl））、`License.txt`
- 中身: `Fish1`、`Fish2`、`Fish3`、`Dolphin`、`Manta ray`、`Shark`、`Whale`
- アニメーション付き（FBX）。川に置く「魚」には `Fish1`〜`Fish3` が使える。

## パックを足すとき

1. [CONTRIBUTING 第 7 章](../CONTRIBUTING.md#7-外部素材サードパーティのアセット)に従って置く。
2. [CREDITS.md](../CREDITS.md) に 1 行足す。
3. このファイルに、パックの中身（構成・何があるか）を足す。
