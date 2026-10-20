# Fork

体験者が持つフォークで、雲を刺したか、先を口に含んだかを判定します。チュートリアル 1 の「雲を刺す → 口に含む」（シナリオブック S3）を進めるために使います。

> 状態: 案（#40 の残り）。Unity では未確認です。半径などの数字は、すべて仮の値です。

参照するのは Core だけです。ほかの機能は、Core の `Forks` から `IFork` を受け取ります。

## 部品

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `ForkTip` | フォークの先の位置に置いた、空のオブジェクト | 雲に触れたら「刺した」、口に近づいたら「口に入った」とします。有効になると、自分で `Forks` に登録します。先の当たり判定（小さな球のトリガー）と、動かない Rigidbody は、起動時に自分で付けます |
| `Cloud` | 刺せる雲の見た目（Collider を付ける） | 刺されると隠れ、`Respawn Seconds` たつと戻ります |

## 判定の決まり

- **刺す:** フォークの先の当たり判定が、`Cloud` の Collider に入ったときです。先に雲のかけらが付いている間は、ほかの雲に触れても刺しません。
- **口に含む:** 先と口の距離で決めます。`Mouth Enter Radius`（6 cm）以内で入り、`Mouth Exit Radius`（9 cm）より離れると出ます。2 つの半径を変えているのは、境目で入る・出るをくり返さないためです。
- **口の位置:** `Mouth` に入れた Transform です。`FoodSpawner` の Spawn Point と同じ `MouthAnchor` を入れます。空なら、Main Camera から `Mouth Offset` だけずらした所を使います。
- **かけらがなくなる:** デバイスで決定したときです。食べ物が生まれるのと同じときにそろえています。先が口の中にあるかは見ません。

## 使い方

1. フォークの位置を取るコントローラーの、手のアンカーの子に、空のオブジェクトを作ります。名前は `ForkTip` にし、フォークの先の位置まで動かします。`ForkTip` を付けます。
2. `ForkTip` の `Mouth` に、Main Camera の子の `MouthAnchor` を入れます。
3. 刺した雲のかけらの見た目があれば、`ForkTip` の子に置き、`Loaded Piece` に入れます。刺している間だけ表示されます。
4. 雲の見た目に、Collider と `Cloud` を付けます。

コントローラーを左右どちらの手に付けるかは、まだ決まっていません。手のアンカーを変えれば、どちらでも動きます。

## Quest なしで試す

Play 中に、キーで代わりの操作ができます。展示では使いません。

| キー | 操作 |
|---|---|
| P | 雲を刺す |
| M（押している間） | 先を口に入れる |

噛む（Space）と決定（Enter）は、`MockDevice` のキーです。

## チュートリアルから待つ

人形劇スクリプト（`TutorialScript`）では、`WaitForPierce` と `WaitForMouth` で待ちます。

```csharp
ForkWait pierce = WaitForPierce(timeoutSeconds: 10f);
yield return pierce;
if (pierce.TimedOut)
{
    // 10 秒たっても刺せないときは、雲を寄せる合図を出す（S3）。合図の名前はまだない
}

yield return WaitForMouth();
```

`WaitForMouth` は、かけらが付いた先を口に含んだときに終わります。かけらなしで口に入れても、数えません。

## まだないもの

- 雲を光らせる、近くへ寄せるなどの見せ方。合図（`Cues`）を受ける側が足します。
- チュートリアル 1 の人形劇スクリプト（#51）。

## テスト

`Tests/`（EditMode）の `ForkLogicTests` で、刺す・口・食べたの決まりを確かめます。Core の `Forks` は `Core/Tests/ForksTests`、待ちは `Tutorial/Tests/TutorialWaitTests` で確かめます。
