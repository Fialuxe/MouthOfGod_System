---
name: csharp-comment-style
description: C# / Unity のコメント規約。クラスコメント、メソッドの XML ドキュメントコメント (/// の summary タグなど)、フィールドの不変条件、実装内の補足コメント、TODO の書き方を、Google C# / C++ Style Guide と Microsoft Learn の XML ドキュメントタグ規約に基づいて強制する。自明なコメントを禁止し、Why / Usage / Invariants だけを書かせる。.cs ファイルの作成・編集・レビュー時、「コメントを書いて」「ドキュメントコメント」「XMLコメント」「summary」と言われた時は必ず使う。
---

# csharp-comment-style: 名前で書けないことだけをコメントにする

このスキルは次の4文書に基づく。

- Google C# Style Guide: Comments
- Google C++ Style Guide: Comments (C# 版にない詳細を補う)
- Microsoft Learn: XML documentation comments
- Microsoft Learn: Recommended XML tags for C# documentation comments

命名の規約は `unity-csharp-style` スキルに従う。
このスキルはコメントだけを扱う。

---

## 0. 原則

最良のコードは、コメントがなくても読める。
コメントを書く前に、名前と型で表現できないか考える。
表現できるなら、コメントではなく名前を直す。

コメントに書くのは次の4種類だけである。

| 種類 | 内容 | 例 |
|---|---|---|
| Why | なぜそうするのか。なぜ他の方法を選ばなかったのか | 「Discrete では高速時に床をすり抜けるため Continuous にする。」 |
| Usage | どう使うのか。いつ使い、いつ使わないのか | 「シーン開始時に1回だけ呼ぶ。」 |
| Invariants | 前提条件、不変条件、特殊値、寿命 | 「-1 は未割り当てを表す。」 |
| Contract | 入出力、null の可否、参照の保持、例外、性能 | 「`target` が null の場合は何もしない。」 |

次は書かない。

- コードを日本語に訳しただけの説明 (What の繰り返し)
- 名前と型を見ればわかること
- 変更履歴、作者名、日付 (Git が持つ)
- コメントアウトしたコード

### 判定手順

コメントを書く時、次の順で判定する。

1. このコメントはコードの直訳か → 直訳なら削除する
2. 名前を変えれば不要になるか → なるなら名前を直す
3. メソッドに切り出せば不要になるか → なるなら切り出す
4. Why / Usage / Invariants / Contract のどれかか → どれかなら書く
5. どれでもない → 書かない

---

## 1. 書き方の共通ルール

### 1.1 記号

- ドキュメントコメントは `///` を使う
- `/** */` を使わない (行頭の `*` の扱いが不安定なため)
- 実装内コメントは `//` を使う
- `/* */` を使わない
- `//` の後に空白を1つ入れる

Bad:

```csharp
//敵を生成する
/* 一時的に無効化 */
```

Good:

```csharp
// プールが空の時だけ新規生成する。Instantiate は GC を発生させるため。
```

### 1.2 文体

- 完全な文で書く
- 文末を「。」で終える (英語なら `.` で終える)
- 主語は省略してよい
- 体言止めにしない (`/// 敵の生成` ではなく `/// 敵を生成する。`)
- 言語はプロジェクトの既存コメントに合わせる
- 判断できない場合は日本語で書く
- 1ファイルの中で日本語と英語を混ぜない

行末の短いコメントだけは、句点を省略してよい。
省略する場合は、ファイル内で統一する。

### 1.3 XML として正しく書く

`///` コメントは XML として解析される。
XML が壊れているとコンパイラが警告を出す。

- 本文に `<` `>` を書く時は `&lt;` `&gt;` にする
- ジェネリック型の `cref` は `{}` を使う (`cref="List{T}"`)
- タグを必ず閉じる
- タグのない `///` の文章を書かない (IntelliSense に表示されない)

Bad:

```csharp
/// 値が 0 < x < 10 の時だけ有効。
/// <see cref="List<Enemy>"/> を返す。
```

Good:

```csharp
/// <summary>
/// 値が 0 &lt; x &lt; 10 の時だけ有効にする。
/// </summary>
/// <returns>生存している敵の <see cref="List{T}"/>。</returns>
```

---

## 2. クラスコメント

### 2.1 ルール

- 自明でないクラスには、必ずクラスコメントを書く
- `/// <summary>` に目的を1〜2文で書く
- 使い方と注意点は `/// <remarks>` に書く
- いつこのクラスを使うべきかがわかるように書く
- 使う時の注意点を書く
- スレッドや実行順序の前提があれば書く
- 短い使用例があれば `<example>` と `<code>` で書く
- 名前だけで目的が明らかなクラスは `<summary>` だけでよい

クラスコメントに書く内容:

| 項目 | 書く場所 | 例 |
|---|---|---|
| 目的 | `<summary>` | 何を担当するクラスか |
| 使い方 | `<remarks>` | どのオブジェクトに付けるか、何を設定するか |
| 注意点 | `<remarks>` | シーンに1つだけ置く、Awake 前に参照しない |
| 実行順序 | `<remarks>` | `[DefaultExecutionOrder]` の理由 |
| 使用例 | `<example>` | 典型的な呼び出し |

### 2.2 Bad / Good

Bad:

```csharp
/// <summary>
/// EnemySpawner クラス。
/// </summary>
public class EnemySpawner : MonoBehaviour
```

理由: クラス名を繰り返しただけである。

Bad:

```csharp
// 敵を出すやつ
// 作成: tanaka 2026/10/01
// 修正: suzuki 2026/11/03
public class EnemySpawner : MonoBehaviour
```

理由: `///` でない。体言止めである。作者と日付は Git が持つ。

Good:

```csharp
/// <summary>
/// ウェーブ定義に従って、敵をオブジェクトプールから出現させる。
/// </summary>
/// <remarks>
/// <para>
/// シーンに1つだけ配置する。
/// 複数置くと同じウェーブが二重に出現する。
/// </para>
/// <para>
/// 出現位置は <see cref="_spawnPoints"/> から順番に選ぶ。
/// ランダムにしないのは、レベルデザイナーが難易度を位置で調整するためである。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// _enemySpawner.StartWave(waveIndex: 0);
/// </code>
/// </example>
public class EnemySpawner : MonoBehaviour
```

Good (自明なクラス):

```csharp
/// <summary>
/// 敵1体分のダメージ情報を保持する。
/// </summary>
public readonly struct DamageInfo
```

---

## 3. メソッドコメント (XML ドキュメント)

### 3.1 書く対象

- ほぼすべてのメソッドに書く
- private メソッドにも書く (自明でない場合)
- 省略してよいのは、単純で自明なメソッドだけである

省略してよい例:

- 値を返すだけのプロパティ
- `Awake` `Start` `Update` などの Unity メッセージ関数 (処理内容が名前通りの場合)
- 名前と引数だけで契約がすべてわかるメソッド

### 3.2 summary の書き方

- 「このメソッドは」が主語になる文で書く
- 動詞で終える (「〜する。」「〜を返す。」)
- 命令形にしない
- 何をするかと、どう使うかを書く
- どう実装しているかを書かない (実装は 5 章の実装コメントに書く)

| Bad | Good | 理由 |
|---|---|---|
| `/// ファイルを開け。` | `/// ファイルを開く。` | 命令形 |
| `/// ファイルオープン処理` | `/// ファイルを開く。` | 体言止め |
| `/// for ループで配列を走査して最小値を探す。` | `/// 最も近い敵を返す。` | 実装の説明 |
| `/// GetNearestEnemy メソッド。` | `/// 指定半径内で最も近い敵を返す。` | 名前の繰り返し |

### 3.3 必ず書く契約

メソッドコメントには、該当する次の項目を必ず書く。

| 項目 | タグ | 書く条件 |
|---|---|---|
| 各引数の意味 | `<param>` | 引数があれば必ず |
| 戻り値の意味 | `<returns>` | void 以外なら必ず |
| 型引数の意味 | `<typeparam>` | ジェネリックなら必ず |
| null の可否 | `<param>` / `<returns>` | 参照型の引数・戻り値がある |
| 引数の参照を保持するか | `<remarks>` | 引数をフィールドに保存する |
| out / コレクション引数を上書きか追記か | `<param>` | 出力用の引数がある |
| 投げる例外 | `<exception>` | 例外を投げる |
| 性能上の注意 | `<remarks>` | GC 発生、O(n²)、毎フレーム呼ぶと重い |
| 呼び出しのタイミング | `<remarks>` | Awake 後に限る、メインスレッドに限る |

本文中で引数に触れる時は `<paramref name="..."/>` を使う。
型引数に触れる時は `<typeparamref name="..."/>` を使う。
`true` `false` `null` は `<see langword="..."/>` を使う。

### 3.4 Bad / Good

Bad:

```csharp
/// <summary>
/// 敵を取得する
/// </summary>
/// <param name="r">r</param>
/// <returns>敵</returns>
public Enemy GetNearestEnemy(float r)
```

理由: 句点がない。引数の説明が名前の繰り返しである。null を返すかがわからない。

Bad:

```csharp
/// <summary>
/// 半径 r 以内の敵を FindObjectsOfType で全部取ってきて、
/// foreach で距離を計算し、一番小さいものを返します。
/// </summary>
public Enemy GetNearestEnemy(float r)
```

理由: 実装の説明である。実装を変えるとコメントが嘘になる。

Good:

```csharp
/// <summary>
/// 指定半径内で、このオブジェクトに最も近い生存中の敵を返す。
/// </summary>
/// <param name="searchRadius">探索半径 (メートル)。0 以下の場合は常に <see langword="null"/> を返す。</param>
/// <returns>最も近い敵。半径内に敵がいない場合は <see langword="null"/>。</returns>
/// <remarks>
/// シーン内の全敵を走査するため O(n) である。
/// <see cref="Update"/> から毎フレーム呼ばず、0.2 秒間隔などで呼ぶ。
/// </remarks>
public Enemy GetNearestAliveEnemy(float searchRadius)
```

### 3.5 null と参照保持の例

```csharp
/// <summary>
/// 指定した敵を追跡対象として登録する。
/// </summary>
/// <param name="target">
/// 追跡する敵。<see langword="null"/> を渡すと追跡を解除する。
/// </param>
/// <remarks>
/// <paramref name="target"/> への参照を保持する。
/// 対象が Destroy された場合、次の <see cref="LateUpdate"/> で自動的に解除する。
/// </remarks>
public void SetChaseTarget(Enemy target)
```

### 3.6 出力引数の例

```csharp
/// <summary>
/// 視界内にいる敵を <paramref name="results"/> に追加する。
/// </summary>
/// <param name="results">
/// 結果の格納先。既存の要素は消さずに末尾へ追加する。
/// GC を避けるため、呼び出し側で使い回すリストを渡す。
/// </param>
/// <returns>追加した敵の数。</returns>
public int CollectVisibleEnemies(List<Enemy> results)
```

### 3.7 例外の例

```csharp
/// <summary>
/// インベントリの指定スロットにアイテムを入れる。
/// </summary>
/// <param name="slotIndex">0 から始まるスロット番号。</param>
/// <param name="item">入れるアイテム。<see langword="null"/> は不可。</param>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="slotIndex"/> がスロット数以上の場合。
/// </exception>
/// <exception cref="ArgumentNullException">
/// <paramref name="item"/> が <see langword="null"/> の場合。
/// </exception>
public void PutItemIntoSlot(int slotIndex, Item item)
```

### 3.8 Try パターンの例

```csharp
/// <summary>
/// 指定 ID のセーブデータを読み込む。
/// </summary>
/// <param name="saveSlotId">セーブスロットの ID。</param>
/// <param name="saveData">読み込んだデータ。失敗時は <see langword="default"/>。</param>
/// <returns>読み込みに成功した場合は <see langword="true"/>。</returns>
public bool TryLoadSaveData(int saveSlotId, out SaveData saveData)
```

### 3.9 ジェネリックの例

```csharp
/// <summary>
/// 指定した型のコンポーネントをプールから取り出す。
/// </summary>
/// <typeparam name="TComponent">取り出すコンポーネントの型。</typeparam>
/// <returns>
/// 有効化済みの <typeparamref name="TComponent"/>。
/// プールが空の場合は新規生成して返すため、<see langword="null"/> を返さない。
/// </returns>
public TComponent RentFromPool<TComponent>() where TComponent : Component
```

### 3.10 オーバーライドとインターフェース実装

- オーバーライドには、オーバーライド固有の内容だけを書く
- 基底と同じ説明を繰り返さない
- 固有の内容がなければ `/// <inheritdoc/>` だけを書く

Bad:

```csharp
/// <summary>
/// ダメージを受ける。
/// </summary>
/// <param name="damageAmount">ダメージ量。</param>
public override void TakeDamage(int damageAmount)
```

理由: 基底クラスのコメントの繰り返しである。

Good (固有の内容なし):

```csharp
/// <inheritdoc/>
public override void TakeDamage(int damageAmount)
```

Good (固有の内容あり):

```csharp
/// <inheritdoc/>
/// <remarks>
/// シールド中はダメージを半分にする。
/// </remarks>
public override void TakeDamage(int damageAmount)
```

### 3.11 コンストラクタ

- 「インスタンスを生成する」とだけ書かない
- 引数の所有権や、生成時の副作用があれば書く
- 自明なら省略する

Bad:

```csharp
/// <summary>
/// DamageInfo の新しいインスタンスを生成する。
/// </summary>
public DamageInfo(int amount, GameObject attacker)
```

Good:

```csharp
/// <param name="amount">ダメージ量。負の値は回復として扱う。</param>
/// <param name="attacker">攻撃したオブジェクト。環境ダメージの場合は <see langword="null"/>。</param>
public DamageInfo(int amount, GameObject attacker)
```

---

## 4. フィールドとプロパティのコメント

### 4.1 ルール

- 各メンバーの目的が明らかである必要がある
- 型と名前で目的が伝わるなら、コメントを書かない
- 型と名前で表現できない次の情報だけを書く

| 書く情報 | 例 |
|---|---|
| 特殊値 (番兵値) | 「-1 は未割り当てを表す。」 |
| 値の範囲 | 「0 以上 `_maxHealth` 以下を保つ。」 |
| メンバー間の関係 | 「`_spawnPoints` と同じ長さを保つ。」 |
| 寿命 | 「シーン遷移で破棄される。」 |
| 単位 (名前に入れられない場合) | 「ワールド座標系のメートル。」 |
| なぜ static / global か | 「シーンをまたいで保持するため static にする。」 |

### 4.2 Bad / Good

Bad:

```csharp
// 体力
private int _currentHealth;

// 移動速度
[SerializeField]
private float _moveSpeed = 5f;

// 敵のリスト
private List<Enemy> _enemies;
```

理由: すべて名前の繰り返しである。削除する。

Good:

```csharp
private int _currentHealth;

[SerializeField]
private float _moveSpeed = 5f;

// 0 以上 _maxHealth 以下を保つ。直接代入せず ApplyHealthChange を通す。
private int _currentHealth;

// 現在ロックオンしている敵の、_trackedEnemies 内のインデックス。
// -1 はロックオンなしを表す。
private int _lockedEnemyIndex = -1;

// _spawnPoints と同じ長さを保つ。i 番目の出現位置の使用回数を表す。
private int[] _spawnPointUsageCounts;

// シーン遷移後もスコアを保持するため static にする。
private static int _accumulatedScore;
```

### 4.3 Inspector に出すフィールド

`[SerializeField]` フィールドの説明は、Inspector で見る人のために `[Tooltip]` に書く。
`///` や `//` は Inspector に表示されない。

- デザイナーが調整する値には `[Tooltip]` を付ける
- `[Tooltip]` には単位と調整の目安を書く
- 不変条件はコード向けに `//` で書く
- 同じ内容を `[Tooltip]` と `//` に重複して書かない

Bad:

```csharp
// ジャンプ力
[SerializeField]
private float _jumpForce = 8f;
```

Good:

```csharp
[SerializeField]
[Tooltip("ジャンプ時に加える上向きの力 (N)。8 で約 2m 跳ぶ。")]
[Min(0f)]
private float _jumpForce = 8f;
```

### 4.4 public プロパティ

外部に公開するプロパティは `<summary>` を書く。
値の意味が `<summary>` で足りない場合は `<value>` を使う。

```csharp
/// <summary>
/// 現在の体力の割合を取得する。
/// </summary>
/// <value>0 から 1 の値。1 が満タン。</value>
public float HealthRatio => (float)_currentHealth / _maxHealth;
```

### 4.5 event

event には、いつ発火するかを書く。
発火の前後関係と引数の意味を書く。

```csharp
/// <summary>
/// 体力が 0 になった直後、Destroy の前に発火する。
/// </summary>
/// <remarks>
/// 購読側はこのフレーム内なら <see cref="Enemy"/> のプロパティを参照できる。
/// </remarks>
public event Action EnemyDied;
```

---

## 5. 実装内コメント (メソッドの中)

### 5.1 ルール

- 難しい処理、複雑な処理、自明でない処理の直前にコメントを書く
- コメントは対象のコードの上の行に書く
- 処理が何をしているかではなく、なぜそうするかを書く
- 他の方法を採用しなかった理由を書く
- 長いメソッドでは、処理の段階の概要を書いてよい
- メソッドコメント (summary) の内容を繰り返さない

### 5.2 自明なコメントの禁止

次のコメントは禁止する。
見つけたら削除する。

```csharp
i++;                         // i を増やす
_currentHealth -= damage;    // 体力からダメージを引く
if (_isDead)                 // 死んでいたら
{
    return;                  // 抜ける
}
transform.position = target; // 位置を設定
Destroy(gameObject);         // オブジェクトを破棄
```

### 5.3 段階的な改善

C++ Style Guide の考え方に従い、次の順で直す。

**Bad: コードの直訳**

```csharp
// リストの中に enemy があるか探す
if (!_processedEnemies.Contains(enemy))
{
    ApplyPoisonDamage(enemy);
}
```

**Better: 意図を書く**

```csharp
// 同じ敵に毒を二重にかけないよう、処理済みの敵は飛ばす。
if (!_processedEnemies.Contains(enemy))
{
    ApplyPoisonDamage(enemy);
}
```

**Best: 名前で表し、コメントを不要にする**

```csharp
if (!IsAlreadyPoisoned(enemy))
{
    ApplyPoisonDamage(enemy);
}
```

### 5.4 Why を書く例

Good:

```csharp
// Unity の == 演算子は Destroy 済みのオブジェクトを null と判定する。
// ?. はこの判定を通らないため、ここでは明示的に比較する。
if (_target != null)
{
    _target.TakeDamage(_damage);
}
```

```csharp
// FixedUpdate で読むと入力を取りこぼすため、Update で記録して FixedUpdate で使う。
_isJumpRequested |= Input.GetButtonDown("Jump");
```

```csharp
// Physics.Raycast は自身のコライダーにも当たるため、Player レイヤーを除外する。
int groundLayerMask = ~LayerMask.GetMask("Player");
```

```csharp
// 浮動小数点の誤差で 1.0 をわずかに超えることがあるため丸める。
float normalizedTime = Mathf.Clamp01(_elapsedSeconds / _durationSeconds);
```

```csharp
// 逆順に回すのは、ループ中に要素を削除してもインデックスがずれないためである。
for (int i = _activeBullets.Count - 1; i >= 0; i--)
{
    ...
}
```

### 5.5 処理段階の概要

長い処理では、段階の見出しとしてコメントを書いてよい。
ただし、段階ごとにメソッドへ切り出せないか先に検討する。

```csharp
private void RebuildNavigationGrid()
{
    // 1. 障害物のある地点を通行不可にする。
    // 2. 通行不可地点から 1 マス以内をコスト 2 倍にする。
    //    壁際を歩くと見た目が不自然になるためである。
    // 3. 到達できない孤立地点を通行不可にする。
    MarkObstacleCellsAsBlocked();
    IncreaseCostNearBlockedCells();
    BlockUnreachableCells();
}
```

### 5.6 引数の意味が呼び出し側でわからない場合

コメントで補う前に、次の順で直す。
コメントは最後の手段である。

1. 同じリテラルを複数箇所で使うなら、名前付き定数にする
2. bool 引数を enum にする
3. 引数が多いなら、設定用の struct / class にまとめる
4. 複雑な式を名前付きローカル変数にする
5. C# の名前付き引数を使う

C++ の `/*name=*/value` 形式のコメントは使わない。
C# では名前付き引数で同じことができる。

Bad:

```csharp
SpawnEnemy(prefab, true, 3, /* delay */ 0.5f);
```

Good:

```csharp
SpawnEnemy(prefab, SpawnFormation.Clustered, count: 3, delaySeconds: 0.5f);
```

### 5.7 行末コメント

- 行末コメントは短い補足だけにする
- コードと `//` の間に空白を2つ入れる
- 複数行の行末コメントを揃えるためにコードの空白を調整しない

```csharp
private const float GravityScale = 2.5f;  // 落下を速くしてジャンプを軽く感じさせる
```

---

## 6. TODO コメント

### 6.1 形式

```csharp
// TODO(識別子): 内容。期限か条件。
```

- `TODO` は大文字で書く
- 括弧の中に識別子を書く (Issue 番号、担当者名、設計資料のリンク)
- 「後でやる」TODO には、具体的な日付か、実行する条件を書く
- TODO は修正の約束ではない

### 6.2 Bad / Good

Bad:

```csharp
// TODO
// todo: 直す
// あとでリファクタ
// FIXME
```

理由: 誰が、何を、いつ直すかがわからない。

Good:

```csharp
// TODO(#128): ボス戦の第2形態を追加したら、この固定値を BossPhaseData から読む。
// TODO(takuma): Unity 6.3 に上げたら FindObjectsByType の非ソート版に置き換える。
// TODO(#204): 2026-12 のマイルストーンまでにセーブデータの暗号化を入れる。
```

`FIXME` `HACK` `XXX` は使わない。
すべて `TODO` に統一する。

---

## 7. 書かないコメント

次は書かない。
見つけたら削除する。

| 種類 | 理由 | 代わり |
|---|---|---|
| 変更履歴 (`// 2026/11/01 修正`) | Git が持つ | コミットメッセージ |
| 作者名 (`// by tanaka`) | Git が持つ | `git blame` |
| コメントアウトしたコード | 読み手が残す理由を推測できない | 削除する。必要なら Git から戻す |
| 閉じ括弧のコメント (`} // end if`) | メソッドが長すぎる兆候 | メソッドを分割する |
| 区切り線 (`// ==========`) | 構造を示さない | クラスを分割する、メンバー順を整える |
| `#region` | 長すぎるクラスを隠す | クラスを分割する |
| 感想や謝罪 (`// 汚いけど許して`) | 情報がない | Why を書く、または直す |

---

## 8. 総合例

Bad:

```csharp
// プレイヤー
// 作成者: tanaka
public class PlayerHealth : MonoBehaviour
{
    // 最大HP
    [SerializeField] private int _maxHealth = 100;
    // 現在HP
    private int _currentHealth;
    // 無敵時間
    private float _invincibleTimer;

    // ダメージ
    public void TakeDamage(int damage)
    {
        // 無敵なら
        if (_invincibleTimer > 0) return; // 抜ける
        // HPを減らす
        _currentHealth -= damage;
        // 0以下なら
        if (_currentHealth <= 0)
        {
            _currentHealth = 0; // 0にする
            Die(); // 死ぬ
        }
        _invincibleTimer = 1.0f; // 無敵時間を設定
        // Debug.Log("damage " + damage);
    }
}
```

Good:

```csharp
/// <summary>
/// プレイヤーの体力と、被弾後の無敵時間を管理する。
/// </summary>
/// <remarks>
/// 体力の変更は必ず <see cref="TakeDamage"/> か <see cref="RestoreHealth"/> を通す。
/// UI は <see cref="HealthChanged"/> を購読して表示を更新する。
/// </remarks>
public class PlayerHealth : MonoBehaviour
{
    /// <summary>
    /// 体力が変化した直後に発火する。引数は変化後の体力。
    /// </summary>
    public event Action<int> HealthChanged;

    private const float InvincibleDurationSeconds = 1.0f;

    [SerializeField]
    [Tooltip("最大体力。難易度 Normal の基準は 100。")]
    [Min(1)]
    private int _maxHealth = 100;

    // 0 以上 _maxHealth 以下を保つ。
    private int _currentHealth;

    private float _remainingInvincibleSeconds;

    /// <summary>
    /// ダメージを受ける。無敵時間中は何もしない。
    /// </summary>
    /// <param name="damageAmount">受けるダメージ量。0 以下の場合は何もしない。</param>
    /// <remarks>
    /// 被弾後 <see cref="InvincibleDurationSeconds"/> 秒は無敵になる。
    /// 多段ヒットの攻撃で一瞬で倒されないようにするためである。
    /// </remarks>
    public void TakeDamage(int damageAmount)
    {
        if (_remainingInvincibleSeconds > 0f || damageAmount <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(_currentHealth - damageAmount, 0);
        OnHealthChanged();

        if (_currentHealth == 0)
        {
            Die();
            return;
        }

        _remainingInvincibleSeconds = InvincibleDurationSeconds;
    }
}
```

---

## 9. 提出前チェックリスト

変更した `.cs` ファイルで、次をすべて確認する。
違反があれば直してから完了とする。

### 原則

- [ ] コードの直訳コメントがない
- [ ] 名前と型の繰り返しだけのコメントがない
- [ ] 各コメントが Why / Usage / Invariants / Contract のどれかである
- [ ] 名前やメソッド抽出で消せるコメントを、名前やメソッド抽出で消した

### 書式

- [ ] ドキュメントコメントが `///` である (`/** */` でない)
- [ ] 実装内コメントが `//` である (`/* */` でない)
- [ ] 完全な文で、文末が「。」である
- [ ] 体言止め、命令形がない
- [ ] XML が正しい (タグが閉じている、`<` `>` をエスケープした)
- [ ] ジェネリックの `cref` に `{}` を使った

### クラス

- [ ] 自明でないクラスに `<summary>` がある
- [ ] 使い方と注意点が必要なクラスに `<remarks>` がある
- [ ] クラス名を繰り返しただけの `<summary>` がない

### メソッド

- [ ] 自明でないメソッドに `<summary>` がある
- [ ] `<summary>` が動詞で終わり、実装を説明していない
- [ ] すべての引数に `<param>` があり、`name` が引数名と一致する
- [ ] void 以外に `<returns>` がある
- [ ] 参照型の引数と戻り値に、null の可否を書いた
- [ ] 引数の参照を保持する場合、そう書いた
- [ ] 投げる例外に `<exception>` がある
- [ ] 性能の注意がある場合、`<remarks>` に書いた
- [ ] オーバーライドが基底の説明を繰り返していない (`<inheritdoc/>` を使った)
- [ ] コンストラクタに「インスタンスを生成する」だけのコメントがない

### フィールド

- [ ] 名前の繰り返しだけのフィールドコメントがない
- [ ] 特殊値、範囲、メンバー間の関係を書いた
- [ ] Inspector で調整する値に `[Tooltip]` がある
- [ ] event に発火タイミングを書いた

### 実装内

- [ ] 難しい処理の直前に Why のコメントがある
- [ ] メソッドの `<summary>` を繰り返していない
- [ ] `/*name=*/value` ではなく名前付き引数を使った

### TODO と不要なコメント

- [ ] TODO が `TODO(識別子): 内容。期限か条件。` の形式である
- [ ] `FIXME` `HACK` `XXX` がない
- [ ] 変更履歴、作者名、コメントアウトしたコードがない
- [ ] `} // end if` や区切り線、`#region` がない

---

## 出典との対応

| このスキルの章 | 主な出典 |
|---|---|
| 0. 原則 | Google C# Comments (導入)、C++ Implementation Comments: Don'ts |
| 1.1 記号 | C++ Comment Style、Microsoft: XML documentation comments (`/** */` の規則) |
| 1.2 文体 | C++ Punctuation, Spelling, and Grammar、Microsoft: Recommended tags (完全な文で書く) |
| 1.3 XML | Microsoft: XML documentation comments (エスケープ、cref の `{}`) |
| 2. クラス | Google C# Class Comments、C++ Class Comments |
| 3. メソッド | Google C# Method Comments、C++ Function Comments、Microsoft: Recommended tags |
| 4. フィールド | Google C# Class Members / Variable Comments、C++ Variable Comments |
| 5. 実装内 | Google C# Explanatory Comments、C++ Implementation Comments |
| 6. TODO | C++ TODO Comments |

C++ の次の規約は、C# / Unity では次のように置き換えた。

| C++ の規約 | このスキル | 理由 |
|---|---|---|
| ファイル先頭のライセンス文 | プロジェクトの方針に従う | Unity プロジェクトでは通常不要 |
| 宣言と定義のコメントを分ける | `<summary>` と実装内コメントに分ける | C# にヘッダーファイルがない |
| `/*name=*/value` 引数コメント | 名前付き引数 | C# の言語機能で代替できる |
| ポインタ引数の null | 参照型引数の null | C# にポインタが通常ない |
