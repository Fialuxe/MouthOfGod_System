# 見た目の方針と素材

ミニチュアの村の見た目と、素材をどこから持ってくるかの方針（#19）。シーンを作る人（#6、#115）は、まずここを読む。決まっていないことは「未決」と書き、どこで決めるかを付けた。

## 決めたこと

| 項目 | 方針 |
|---|---|
| トーン | **ローポリ**。素材は Quaternius を軸にする（パックどうしで色調・形がそろっている） |
| 神様の視点 | プレイヤーは、目の前のミニチュアの村を見下ろしながら、**周りを歩いて**届け先を選ぶ（README「体験のイメージ」） |
| 素材の調達 | **無料で再配布できる素材**（CC0・MIT）を使う。public リポジトリなので、Asset Store の素材など再配布できないものは入れない（[CONTRIBUTING 第 7 章](../CONTRIBUTING.md#7-外部素材サードパーティのアセット)） |
| 素材の置き場所と一覧 | `Assets/ArtisticResources/ThirdParty/<提供元>/<パック名>/`。入れたものは [CREDITS.md](../CREDITS.md)、Quaternius の中身は [quaternius-assets.md](quaternius-assets.md) |

## 何にどの素材を使うか

| 映すもの | 素材 | 状態 |
|---|---|---|
| 建物（家・納屋・風車・井戸など） | Quaternius Medieval Village MegaKit、Farm Buildings。組み立て例は `House_Sample` Prefab | 入れた |
| 食べ物（お供え物・生まれる物） | Quaternius Ultimate Food Pack | 入れた |
| 動物（牛・羊など） | Quaternius Ultimate Animated Animals、Farm Animals | 入れた |
| 魚 | Quaternius Animated Fish Pack | 入れた |
| 村人 | Quaternius Ultimate Animated Character Pack（52 体、歩く・座る・物を運ぶなどのアニメーション付き） | 入れた |
| 川・海の水 | Uber Stylized Water（MIT、URP）。`Prefabs/Water Template/` のテンプレートから選ぶ。暗い色のもの以外は使いやすい | 入れた |
| 山・地面 | Unity の Terrain で作る案 | 案 |
| 木 | 未決（候補: Kenney Nature Kit、CC0） | 未決 |
| 人のアバター | 用意する案（素材は未定） | 案 |

### 重さについて
水はきちんと描くと重くなる。VR（Link）で 72 fps 以上を保つため、水は透過・反射・コースティクスを切ったテンプレート（Clear など）から試し、足りなければ足す。シーン全体の目安（一般的な値で、実測はしていない）: 10〜30 万ポリゴン以下、マテリアルは共通化して 20〜30 個以内。

## 採用しなかった候補

| 候補 | 理由 |
|---|---|
| Low Poly Medieval Peasants（Asset Store） | Asset Store の素材は public リポジトリで再配布できない |
| Meshy で生成してリグを付ける | Quaternius のパックで足りた。AI 生成素材の規約も調べていない |
| KayKit Adventurers、100Avatars、Villager NPC（村人） | Quaternius のパックで足り、色調もそろう |
| PixelatedWater Shader（川） | 有料（最低 $2）で、ライセンスの記載がなく再配布できるか分からない |
| KayKit Medieval Hexagon（川・地形） | Terrain を使わない場合の候補。Terrain で作る案なので見送り |

## 未決（決める場所）

- 色数・ライティング・時間帯: 村を組むとき（#115）に、素材を並べて決める
- 木の素材: 村を組むとき（#115）
- 参考画像・ムードボードの置き場所: 必要になったら決める。それまでは Issue のコメントに貼る
