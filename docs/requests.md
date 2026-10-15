# 欲しい物（要求）の仕組みと、分担

> 状態: 案（PR #251）。Unity では EditMode のテストと、`Village.prefab` の作り直し（Bake）まで確認。Play での見た目は未確認。名前と細部は仮。

山・海・草原・村の 4 つのフィールドと、チュートリアル・本編・吹き出しを、**別々の人が同時に作れる**ようにするための約束です。言葉は [fields.md](fields.md) にそろえます（「要求」「困りごと」は、**欲しい物**）。

## 1 行で

**フィールドは「何が欲しいか」と「届いたら何が変わるか」だけを持つ。「いつ・どこに出すか」は、本編（Game）とチュートリアル（Tutorial）が決める。** 両者をつなぐのは Core の `IFieldRequests` だけ。

- フィールドの担当は、時間や他のフィールドのことを考えなくてよい。
- 4 つのフィールドがばらばらに動かず、調子がそろうのは、出す順番と間隔を 1 か所（本編なら Game）で決めるから。

## フィールドの作り方（コードは要らない）

戻り方は、**Hierarchy の並び順**で決まります。番号はどこにも書きません。

```
Village                 ← FieldStages（フィールドに 1 つ。ID は village / mountain / sea / grassland）
├─ 家・井戸・村人…       ← どのステップにも入らない物は、はじめからずっとある
└─ Steps
   ├─ 1 Sprout           ← FieldStep: 欲しい物 carrot / 困りごとが解ける人 畑の 2 人 / 消す物 枯れた茎
   │    └─ 芽 ×16        ← このステップが済むと出る（ステップの子）
   ├─ 2 Crops            ← FieldStep: pumpkin / 井戸の人 / 茶色い木・芽
   │    └─ 作物・緑の木
   ├─ 3 Sheep
   │    └─ 羊
   └─ 4 Pig
        └─ 豚
```

`FieldStep` が持つのは 3 つだけです。

| 項目 | 意味 |
|---|---|
| Food | 欲しい物の FoodId（吹き出しに出る物）。空なら欲しい物は出さない |
| Helped | このステップで困りごとが解ける人（`VillagerMood`）。**1 人目の頭の上に吹き出し**。名前のあるステップがすべて済むと、悩むのをやめて元気になる |
| Hide When Done | このステップが済むと消える物（どこに置いてあってもよい） |

ほかに、済んだときの音・光を Inspector でつなぐ `On Done` があります。

- **ステップを足す・挟む**: `Steps` の下に空のオブジェクトを作って `FieldStep` を付け、好きな位置へドラッグし、出す物をその子に入れる。ほかのステップは直さない。
- **消す・並べ替える**: 消すか、ドラッグするだけ。
- 村人のアニメーション（悩む・うなだれる・喜ぶ）は `VillagerMood` の中にあり、ステップの作りとは関係なく動く。
- 変わる物（木・動物・作物など）は、普通のオブジェクトとして置く。Terrain の機能で塗った木や草は、ステップで出し入れできない（地形そのものは変えない方針。[decisions.md](decisions.md) 2026-10-13）。

### 1 人で確かめる

サンドボックスのシーンで Play し、`FieldStages` の右クリックメニューを使う（開発中の確認用。展示では Game・Tutorial が呼ぶ。ブースではキーボードも Inspector も使わない）。

| メニュー | すること |
|---|---|
| Open Next Want | 次のステップの欲しい物を出す（Console に `wants ...`） |
| Deliver Wanted Food | 出ている欲しい物が届いたことにする（見た目が変わり、村人が喜ぶ） |
| Complete Next Step | 欲しい物を通さずに、次のステップを済ませる |
| Restart | はじめに戻す |

## 流れ

```
                      ┌─ Game（本編）: 最初の 30 秒は 1 つずつ、あとは同時に最大 2 つ。20 秒で Expire
  いつ・どこに出すか ─┤
                      └─ Tutorial（T3・T4）: 「海に出す」をステップのデータで指定
              │ IFieldRequests.Open() / Expire() / CanOpen    （Core。Bootstrap がフィールドの数だけ渡す）
              ▼
  フィールド（いつもは FieldStages。独自の作りでもよい）
              │ RequestIssued(Food, Field, Anchor)   → UI: 吹き出しを Anchor の上に出す
              │
  プレイヤーが、そのフィールドの前で物を生み出す（届く）
              │ IFieldRequests.Resolve(food)         ← 届いたかを決める所（DeliveryZone）
              ▼
  ステップが済む: 子が出る・消す物が消える・村人が元気になって喜ぶ
  RequestClosed(Resolved = true)                     → UI: 吹き出しを消す / Game: スコア / Tutorial: 次へ
```

待ちきれなかったとき（Game が `Expire()`）は `RequestClosed(Resolved = false)` が出て、ステップは済まない。

## フィールドの中の決まり

| 決まり | 理由 |
|---|---|
| 1 回届く = 1 ステップ | 戻り方が目に見えて分かる |
| 次に出すのは、まだ済んでいない**最初の**ステップ | 草原の「鶏 → 豚 → 牛 → 象」のように、戻り方の物語を担当が決められる（fields.md） |
| 1 つのフィールドに、**同時に出る欲しい物は 1 つ** | 吹き出しが重ならない |
| 済んだかは、ステップごとに持つ（何番目まで、とは数えない） | 順番を自由にしたくなっても、「次にどれを出すか」（`StepProgress.NextIndex`）の 1 か所を変えるだけで済む |

プレイヤーが**どのフィールドから**助けるかは自由（吹き出しは同時に最大 2 つ出ていて、どちらに応えるかはプレイヤー次第）。

## 誰が何を作るか

| 担当 | 作るもの | 触るファイル | 待たなくてよいもの |
|---|---|---|---|
| フィールド（山・海・草原・村、各 1 人） | 上の「フィールドの作り方」 | 自分のフィールドの Prefab かサンドボックスだけ | 本編・チュートリアル・吹き出し |
| 本編（Game、#81） | いつ・どのフィールドに出すか（同時の数、待ち時間、難しさ）。`CanOpen` を見て `Open`、時間切れで `Expire` | Game のコードとデータ | フィールドの中身。テストでは `IFieldRequests` の偽物を使う |
| チュートリアル（Tutorial、#60・#50） | ステップのデータに「どのフィールド（`FieldId`）に出すか」を書き、`Open` を呼ぶ。`Closed` で次へ | Tutorial のコードとデータ | 同上 |
| 吹き出し（UI、#59） | `Issued` で `Anchor` の上に出し、`Closed` で消す（`Resolved` で喜ぶ・がっかりを分ける） | UI のコード・Prefab | 同上 |
| 届いたかを決める所（DeliveryZone） | 物がそのフィールドに落ちたら、そのフィールドの `IFieldRequests.Resolve(food)`。戻り値を `Delivered` の `Accepted` に使う | DeliveryZone のコード | — |
| 組み立て（Bootstrap、#34） | シーンの `IFieldRequests` を全部集め、Game・Tutorial・UI に渡す。体験の終わりに各フィールドを `Restart()` | Bootstrap のコード | — |

- Game・Tutorial・UI・DeliveryZone・Bootstrap は、`FieldStages` を直接使わない（Bootstrap も集めるときに `IFieldRequests` として扱う）。Village のクラスを直接参照しない（asmdef で禁止。[architecture.md](architecture.md) §2）。見るのは Core の `IFieldRequests`・`RequestIssued`・`RequestClosed` だけ。
- `IFieldRequests` の形（メンバーと意味）を変えるときは、使う側すべてに効くので、別の PR にする。

## 4 人が別々の作りにしても、合わせられるか

合わせられる。合わせ目（統合の境目）は `IFieldRequests` の 1 つだけで、その内側はフィールドごとに自由。

| どこまで独自にするか | やり方 | ほかへの影響 |
|---|---|---|
| 見た目の演出だけ（魚が船の上で跳ねる、動物が歩き回る） | ステップの子の物に自分のスクリプトを付ける。済んで出たとき（`OnEnable`）に動き出す。または `FieldStages.StepDone` を受ける | なし |
| 音・光 | `FieldStep` の `On Done` に Inspector でつなぐ | なし |
| フィールドの仕組みごと（ステップでは表せない、独自の進み方） | 自分の MonoBehaviour で `IFieldRequests` を実装する（Core だけを参照）。`FieldStages` は使わなくてよい | なし。Bootstrap が `IFieldRequests` として集めるので、本編・チュートリアル・吹き出しはそのまま使える |

本編の出し方（Game）も、`IFieldRequests` しか見ないので、フィールドの作りが変わっても直さなくてよい。

## 足りないもの（この仕組みの外で作る）

- 吹き出し（#59）、届いたかを決める所（DeliveryZone）、組み立て（#34）、本編の出し方（#81）。
- 待ちきれなかったときの村人の反応（がっかり）。今の `VillagerMood` は「済んだか」しか見ない。`RequestClosed(Resolved = false)` を見て反応させるのは、そのとき足す。
- 欲しい物の ID（村の carrot・pumpkin・sheep・pig）は仮。食べ物の ID は #52・#54 で決まる。

## 採用しなかった案

- 全体で 1 つの要求の並び（`RequestSet`）を順に出す（PR #251 の最初の形）: 4 人が同じアセットを触る。どの村人の上に出すか分からない。本編の「同時に最大 2 つ・20 秒で消える」を表せない。
- 段階を番号で持つ（main にあった `StageVisibility` の「何段目から何段目まで」、`VillagerMood` の `calmFromStage`）: 途中に段階を挟むと、後ろの番号を全部ずらす必要があり、ずらし忘れても気づけない。
