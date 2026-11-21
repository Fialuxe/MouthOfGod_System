---
name: unity-csharp-style
description: Unity / C# のコードを書く・直す・レビューする時の可読性ルール。命名 (PascalCase / camelCase / _camelCase)、説明的なメソッド名 (Execute や Say 単体の禁止)、System.Action によるイベントと On プレフィックス、購読ハンドラの Subject_Event 命名、4スペースと Allman 波括弧を強制する。.cs ファイルの作成・編集・リファクタリング・レビュー時は必ず使う。
---

# unity-csharp-style: 読める Unity / C# を書く

このスキルは次の2文書に基づく。

- Unity: Naming and code style tips for C# scripting in Unity
- Google C# Style Guide

2文書が食い違う点は、このファイルの決定に従う。
決定の一覧は末尾の「出典との差分」にある。

コードを書いた後、末尾の「提出前チェックリスト」を必ず実行する。

---

## 1. 書式

### 1.1 インデントと波括弧

- インデントはスペース4つにする
- タブを使わない
- 波括弧は Allman スタイルにする (開き括弧の前で改行する)
- `if` `for` `foreach` `while` は、1行でも必ず波括弧を付ける
- `else` `catch` `finally` は閉じ括弧の次の行に書く

Bad:

```csharp
if (isDead) return;
if (hp <= 0) {
  Die();
} else {
  Heal();
}
```

Good:

```csharp
if (_isDead)
{
    return;
}

if (_currentHealth <= 0)
{
    Die();
}
else
{
    RegenerateHealth();
}
```

### 1.2 空白と行

- 1行の長さは 120 文字以内にする
- 1行に1文だけ書く
- 1文に1つの代入だけ書く
- 1行に1つの変数宣言だけ書く
- `if` `for` `while` の後に空白を1つ入れる
- カンマの後に空白を1つ入れる
- 二項演算子の前後に空白を1つ入れる
- 括弧の内側に空白を入れない
- 単項演算子と値の間に空白を入れない

Bad:

```csharp
int x=0,y=0;
for(int i=0;i<count;i++){ Spawn( i ); }
```

Good:

```csharp
int spawnedCount = 0;
int failedCount = 0;

for (int i = 0; i < enemyCount; i++)
{
    SpawnEnemyAtIndex(i);
}
```

### 1.3 using とファイル

- `using` はファイル先頭の namespace の外に書く
- `System` 系を先頭にし、残りはアルファベット順にする
- 1ファイルに MonoBehaviour は1つだけにする
- ファイル名は MonoBehaviour のクラス名と完全に一致させる
- ファイル名とフォルダ名は PascalCase にする

ファイル名とクラス名がずれると Unity はコンポーネントを追加できない。

### 1.4 属性

- 属性は対象メンバーの上の行に1つずつ書く

Bad:

```csharp
[SerializeField] [Range(0f, 10f)] private float _moveSpeed;
```

Good:

```csharp
[SerializeField]
[Range(0f, 10f)]
private float _moveSpeed = 5f;
```

---

## 2. 命名の基本

### 2.1 大文字小文字の表

| 対象 | 規則 | 例 |
|---|---|---|
| クラス、struct | PascalCase | `PlayerController` |
| インターフェース | `I` + PascalCase | `IDamageable` |
| メソッド | PascalCase | `ApplyDamageToTarget` |
| public / protected プロパティ | PascalCase | `CurrentHealth` |
| public フィールド | PascalCase (原則使わない) | `MaxHealth` |
| private / protected / internal フィールド | `_camelCase` | `_currentHealth` |
| `[SerializeField] private` フィールド | `_camelCase` | `_moveSpeed` |
| static private フィールド | `_camelCase` | `_instanceCount` |
| const (全アクセス修飾子) | PascalCase | `MaxEnemyCount` |
| static readonly | PascalCase (public) / `_camelCase` (private) | `DefaultSpawnOffset` |
| enum 型と値 | PascalCase | `WeaponType.Sword` |
| event | PascalCase | `DoorOpened` |
| デリゲート型 | PascalCase | `DamageCalculator` |
| ローカル変数 | camelCase | `nearestEnemy` |
| 引数 | camelCase | `damageAmount` |
| namespace | PascalCase、`_` なし | `MyGame.Combat` |

Inspector は先頭の `_` を除いて表示する。
`_moveSpeed` は Inspector で「Move Speed」と表示される。

### 2.2 略語

- 略語は1単語として扱う
- 先頭だけ大文字にする

| Bad | Good |
|---|---|
| `UIManager` | `UiManager` |
| `HTTPClient` | `HttpClient` |
| `playerID` | `playerId` |

例外: Unity API の名前 (`GetComponentInChildren` など) は変えない。

### 2.3 禁止する名前

- 1文字の名前 (ループ変数 `i` `j` と数式の `x` `y` `t` を除く)
- 型名を付けるハンガリアン記法 (`strName` `iCount` `fSpeed`)
- `m_` `k_` `s_` プレフィックス
- 意味のない略語 (`plyr` `mgr` `tmp` `val` `obj` `data2`)
- 試作用の名前 (`Test1` `NewScript` `Foo` `Hoge`)
- 冗談やダジャレ
- 日本語やその他の Unicode 文字
- クラス名の繰り返し (`Player` クラスの `PlayerScore` は `Score` にする)

### 2.4 品詞

| 対象 | 品詞 | 例 |
|---|---|---|
| クラス | 名詞、名詞句 | `EnemySpawner` |
| インターフェース | 形容詞 (能力) | `IDamageable` `IInteractable` |
| メソッド | 動詞 + 目的語 | `SpawnEnemyWave` |
| bool を返すメソッド | 疑問形 (`Is` `Has` `Can`) | `IsGameOver()` `HasKey()` |
| フィールド、プロパティ | 名詞 | `_remainingAmmo` |
| bool フィールド、プロパティ | `is` `has` `can` `should` + 状態 | `_isGrounded` `CanJump` |
| enum | 単数形の名詞 | `WeaponType` |
| `[Flags]` enum | 複数形の名詞 | `AttackModes` |
| event | 動詞句 (次の章) | `DoorOpened` |

bool の名前は肯定形にする。

| Bad | Good |
|---|---|
| `_isNotVisible` | `_isVisible` |
| `_disableJump` | `_canJump` |
| `ScrollableX` | `CanScrollHorizontally` |

---

## 3. 説明的な命名 (厳格ルール)

メソッド名だけで「何を」「何に」「どの条件で」がわかる必要がある。
呼び出し側を読まずに理解できる名前にする。

### 3.1 単体使用を禁止する動詞

次の動詞は単体でメソッド名にしない。
必ず目的語か条件を付ける。

| 禁止 | 理由 | 置き換え例 |
|---|---|---|
| `Execute()` | 何を実行するか不明 | `ExecuteDashAttack()` |
| `Run()` | 何を走らせるか不明 | `RunSpawnCountdown()` |
| `Do()` | 何をするか不明 | `ApplyKnockbackToTarget()` |
| `Process()` | 何を処理するか不明 | `ProcessPendingDamageQueue()` |
| `Handle()` | 何を扱うか不明 | `HandleJumpInput()` |
| `Say()` | 何を言うか不明 | `SaySpecifiedMessage(string message)` |
| `Show()` | 何を表示するか不明 | `ShowGameOverPanel()` |
| `Set()` | 何を設定するか不明 | `SetVolumeForChannel(...)` |
| `Get()` | 何を取得するか不明 | `GetNearestEnemyInRange(...)` |
| `Init()` / `Setup()` | 何を初期化するか不明 | `InitializeInventorySlots()` |
| `Manage()` | 何を管理するか不明 | 責務を分けて具体名にする |
| `Check()` | 何を確認し何を返すか不明 | `IsTargetInSight()` |
| `Change()` | 何をどう変えるか不明 | `SwitchToNextWeapon()` |
| `Calc()` / `Calculate()` | 何を計算するか不明 | `CalculateFallDamage(...)` |
| `Apply()` | 何を何に適用するか不明 | `ApplyDamageToTarget(...)` |
| `Reset()` | 何を戻すか不明 (Unity の `Reset` メッセージと衝突) | `ResetPlayerPosition()` |

例外: Unity のメッセージ関数 (`Awake` `Start` `Update` `FixedUpdate` `LateUpdate` `OnEnable` `OnDisable` `OnDestroy` `OnTriggerEnter` など) はそのまま使う。

### 3.2 名前に入れる情報

次の順で必要な情報を足す。

1. 動詞 (何をするか)
2. 目的語 (何に対してか)
3. 条件・手段・範囲 (どの条件で、何を使って)

| 段階 | 名前 |
|---|---|
| 曖昧 | `Spawn()` |
| 目的語あり | `SpawnEnemy()` |
| 条件あり | `SpawnEnemyAtRandomSpawnPoint()` |

条件を名前に入れすぎて 40 文字を超える場合は、メソッドの責務が多すぎる。
メソッドを分割する。

### 3.3 Bad / Good

Bad:

```csharp
public class Npc : MonoBehaviour
{
    public void Say(string s)
    {
        _text.text = s;
    }

    public void Execute()
    {
        Move();
        Attack();
    }

    public bool Check()
    {
        return Vector3.Distance(transform.position, _player.position) < 5f;
    }
}
```

Good:

```csharp
public class Npc : MonoBehaviour
{
    private const float DetectionRadius = 5f;

    public void SaySpecifiedMessage(string message)
    {
        _speechBubbleText.text = message;
    }

    public void ChaseAndAttackPlayer()
    {
        MoveTowardPlayer();
        AttackPlayerIfInRange();
    }

    public bool IsPlayerWithinDetectionRadius()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        return distanceToPlayer < DetectionRadius;
    }
}
```

### 3.4 引数で意味を伝える (Google C# Style Guide)

呼び出し側で引数の意味がわからない場合、次の順で直す。

**(a) bool 引数を enum にする**

Bad:

```csharp
SpawnEnemy(true, false);
```

Good:

```csharp
public enum SpawnFormation
{
    Scattered,
    Clustered,
}

SpawnEnemy(SpawnFormation.Clustered, EnemyAlertState.Unaware);
```

**(b) マジックナンバーを名前付き定数にする**

Bad:

```csharp
if (_currentHealth < 20)
{
    PlayLowHealthWarning();
}
```

Good:

```csharp
private const int LowHealthThreshold = 20;

if (_currentHealth < LowHealthThreshold)
{
    PlayLowHealthWarning();
}
```

**(c) 複雑な式を名前付きローカル変数にする**

Bad:

```csharp
if (_rigidbody.velocity.y < 0f && !Physics.Raycast(transform.position, Vector3.down, 1.1f))
{
    StartFallAnimation();
}
```

Good:

```csharp
bool isMovingDown = _rigidbody.velocity.y < 0f;
bool isGroundBelow = Physics.Raycast(transform.position, Vector3.down, GroundCheckDistance);

if (isMovingDown && !isGroundBelow)
{
    StartFallAnimation();
}
```

**(d) 呼び出し側で名前付き引数を使う**

リテラルを渡す場合は名前付き引数を使う。

Bad:

```csharp
PlayExplosion(transform.position, 3f, 0.5f);
```

Good:

```csharp
PlayExplosion(transform.position, radius: 3f, durationSeconds: 0.5f);
```

**(e) 引数が多い場合は設定用の struct / class にまとめる**

引数が5個以上になったら、設定用の型を作る。

```csharp
public readonly struct ExplosionSettings
{
    public float Radius { get; }
    public float DurationSeconds { get; }
    public int Damage { get; }

    public ExplosionSettings(float radius, float durationSeconds, int damage)
    {
        Radius = radius;
        DurationSeconds = durationSeconds;
        Damage = damage;
    }
}
```

### 3.5 単位を名前に入れる

単位が型からわからない数値は、名前に単位を入れる。

| Bad | Good |
|---|---|
| `_delay` | `_delaySeconds` |
| `_angle` | `_angleDegrees` |
| `_timeout` | `_timeoutMilliseconds` |

---

## 4. イベントとデリゲート (厳格ルール)

Unity 公式ガイドの「Events and event handlers」に従う。
3つの名前を区別する。

| 役割 | 名前の形 | 例 |
|---|---|---|
| event 本体 | 動詞句 (過去分詞 / 現在分詞) | `DoorOpened` |
| 発火メソッド (発行側) | `On` + event 名 | `OnDoorOpened()` |
| 購読ハンドラ (購読側) | 発行元名 + `_` + event 名 | `GameEvents_DoorOpened()` |

### 4.1 event 本体の命名

- 状態が変わった後の event は過去分詞にする (`DoorOpened`)
- 状態が変わる前の event は現在分詞にする (`OpeningDoor`)
- `On` を event 本体に付けない
- `Event` を末尾に付けない

| Bad | Good | 理由 |
|---|---|---|
| `OnDoorOpen` | `DoorOpened` | `On` は発火メソッド用 |
| `DoorOpenEvent` | `DoorOpened` | `Event` は冗長 |
| `DoorOpen` | `DoorOpened` | 前か後かがわからない |
| `Opened` | `DoorOpened` | 何が開いたかわからない |

### 4.2 型は System.Action を使う

- 引数なしは `Action` を使う
- 引数ありは `Action<T>` を使う (最大16個)
- 独自のデリゲート型は、戻り値が必要な場合だけ定義する
- `event` キーワードを必ず付ける

`event` を付けないと、外部から `=` で上書きや直接 `Invoke` ができてしまう。

Bad:

```csharp
public delegate void DoorDelegate();
public DoorDelegate doorOpened;          // event なし、独自デリゲート、camelCase
public Action<int> onScore;              // event なし
```

Good:

```csharp
using System;

public event Action DoorOpened;
public event Action<int> PointsScored;
```

引数が3個以上になる場合は、読み取り専用の struct を渡す。

```csharp
public readonly struct DamageEventArgs
{
    public int Amount { get; }
    public Vector3 HitPoint { get; }
    public GameObject Attacker { get; }

    public DamageEventArgs(int amount, Vector3 hitPoint, GameObject attacker)
    {
        Amount = amount;
        HitPoint = hitPoint;
        Attacker = attacker;
    }
}

public event Action<DamageEventArgs> DamageReceived;
```

`System.EventArgs` を継承した class も使える。
1つのプロジェクトでは struct か EventArgs のどちらかに統一する。

### 4.3 発火メソッド

- 発火メソッドは `On` + event 名にする
- `?.Invoke()` で呼ぶ
- event の `Invoke` は発火メソッドの中だけで行う

Bad:

```csharp
public void Open()
{
    _isOpen = true;
    if (DoorOpened != null)
    {
        DoorOpened();
    }
}
```

Good:

```csharp
public void OpenDoor()
{
    _isOpen = true;
    OnDoorOpened();
}

public void OnDoorOpened()
{
    DoorOpened?.Invoke();
}
```

発火メソッドの名前が Unity のメッセージ関数と衝突しないようにする。
`OnEnable` `OnDisable` `OnDestroy` `OnTriggerEnter` `OnCollisionEnter` `OnValidate` `OnApplicationQuit` などは使わない。
衝突する場合は event 名を変える。
例: `Enabled` event ではなく `DoorActivated` event にする。

### 4.4 購読ハンドラ

- ハンドラ名は「発行元の名前 + `_` + event 名」にする
- 発行元の名前は、購読側から見えるフィールド名か型名を使う
- ラムダ式で購読しない (解除できないため)
- `OnEnable` で購読し、`OnDisable` で解除する

Bad:

```csharp
private void Start()
{
    _door.DoorOpened += () => _audioSource.Play();   // 解除できない
    _door.DoorOpened += PlaySound;                   // 何の event か名前でわからない
}
```

Good:

```csharp
public class DoorSound : MonoBehaviour
{
    [SerializeField]
    private Door _door;

    [SerializeField]
    private AudioSource _audioSource;

    private void OnEnable()
    {
        _door.DoorOpened += Door_DoorOpened;
        _door.DoorClosed += Door_DoorClosed;
    }

    private void OnDisable()
    {
        _door.DoorOpened -= Door_DoorOpened;
        _door.DoorClosed -= Door_DoorClosed;
    }

    private void Door_DoorOpened()
    {
        _audioSource.PlayOneShot(_openClip);
    }

    private void Door_DoorClosed()
    {
        _audioSource.PlayOneShot(_closeClip);
    }
}
```

static なイベントハブの場合は型名を使う。

```csharp
private void OnEnable()
{
    GameEvents.DoorOpened += GameEvents_DoorOpened;
}

private void OnDisable()
{
    GameEvents.DoorOpened -= GameEvents_DoorOpened;
}

private void GameEvents_DoorOpened()
{
    ShowDoorOpenedHint();
}
```

ハンドラの中身が2文を超える場合は、処理を具体名のメソッドに出す。
ハンドラは「何が起きたか」、出したメソッドは「何をするか」を表す。

### 4.5 完全な例

発行側:

```csharp
using System;
using UnityEngine;

public class Door : MonoBehaviour
{
    public event Action OpeningDoor;
    public event Action DoorOpened;
    public event Action DoorClosed;

    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public void OpenDoor()
    {
        if (_isOpen)
        {
            return;
        }

        OnOpeningDoor();
        _isOpen = true;
        OnDoorOpened();
    }

    public void CloseDoor()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        OnDoorClosed();
    }

    public void OnOpeningDoor()
    {
        OpeningDoor?.Invoke();
    }

    public void OnDoorOpened()
    {
        DoorOpened?.Invoke();
    }

    public void OnDoorClosed()
    {
        DoorClosed?.Invoke();
    }
}
```

### 4.6 UnityEvent との使い分け

- コード間の通知は `event Action` を使う
- デザイナーが Inspector で接続する必要がある場合だけ `UnityEvent` を使う
- `UnityEvent` のフィールドも event と同じ動詞句で命名する (`_doorOpened`)

---

## 5. フィールドとプロパティ

- public フィールドを作らない
- Inspector に出す値は `[SerializeField] private` にする
- 外部から読む値は読み取り専用プロパティで公開する
- 1行の読み取り専用プロパティは `=>` を使う
- それ以外は `{ get; set; }` を使う
- `= 0` `= false` `= null` の明示的な初期化を書かない (既定値と同じため)
- アクセス修飾子は必ず明示する (`private` を省略しない)
- `const` にできるものは `const` にする
- `const` にできないものは `readonly` にする

Bad:

```csharp
public float speed = 5;
public int hp;
bool dead = false;
```

Good:

```csharp
[SerializeField]
private float _moveSpeed = 5f;

private int _currentHealth;
private bool _isDead;

public int CurrentHealth => _currentHealth;
public bool IsDead => _isDead;
```

### 5.1 シリアライズフィールドの改名

`[SerializeField]` フィールドを改名すると、Inspector の設定値が失われる。
改名時は必ず `[FormerlySerializedAs]` を付ける。

```csharp
using UnityEngine.Serialization;

[SerializeField]
[FormerlySerializedAs("speed")]
private float _moveSpeed = 5f;
```

---

## 6. メンバーの並び順

クラス内は次の順に並べる。

1. ネストした型、enum、デリゲート型
2. event
3. const、static readonly
4. `[SerializeField]` フィールド
5. その他のフィールド
6. プロパティ
7. Unity のメッセージ関数 (`Awake` `OnEnable` `Start` `Update` `FixedUpdate` `LateUpdate` `OnDisable` `OnDestroy` の順)
8. public メソッド
9. private メソッド
10. イベントハンドラ (`Subject_Event`)

同じグループ内は public、internal、protected、private の順にする。
修飾子は `public protected internal private new abstract virtual override sealed static readonly extern unsafe volatile async` の順に書く。

---

## 7. その他のルール

### 7.1 var

- 右辺で型が明らかな場合だけ `var` を使う (`var enemy = new Enemy();`)
- 組み込み型 (`int` `float` `bool` `string`) には使わない
- メソッドの戻り値で型がわからない場合は使わない

Bad:

```csharp
var count = 3;
var result = GetTargets();
```

Good:

```csharp
int count = 3;
List<Enemy> visibleEnemies = GetVisibleEnemiesInRange(DetectionRadius);
var spawnPoints = new List<Transform>();
```

### 7.2 ラムダと LINQ

- 2文を超えるラムダは名前付きメソッドにする
- 再利用するラムダは名前付きメソッドにする
- LINQ はメソッド構文を使う (クエリ構文を使わない)
- 長い LINQ チェーンは名前付き変数に分ける
- `Update` など毎フレーム呼ばれる処理で LINQ を使わない (GC アロケーションが発生するため)

### 7.3 コレクション

- 引数は必要最小限の型にする (`IReadOnlyList<T>` など)
- サイズが変わるものは `List<T>` を使う
- サイズが固定のものは配列を使う
- 複数の値を返す場合は `Tuple<>` ではなく名前付きの型を作る
- 反復中に削除する場合は `RemoveAll(predicate)` を使う

### 7.4 out と ref

- `out` 引数は最後に置く
- `ref` は入力値を置き換える必要がある場合だけ使う
- 失敗しうる取得は `bool TryGet...(..., out T value)` の形にする

```csharp
public bool TryGetNearestEnemy(out Enemy nearestEnemy)
{
    ...
}
```

### 7.5 struct と class

- 原則として class を使う
- 小さく短命な値型 (`Vector3` のようなもの) だけ struct にする

### 7.6 拡張メソッド

- 元のクラスを変更できない場合だけ作る
- 迷ったら作らない

### 7.7 コメント

- 自明でないクラスには、目的と使い方を `/// <summary>` で書く
- 自明でないメソッドには、動詞で始まるコメントを書く (例: 「指定半径内で最も近い敵を返す。」)
- 引数が null を許すか、戻り値が null になるかを書く
- 名前で説明できることをコメントで書かない
- 複雑な処理は、その処理の直前にコメントを書く

Bad:

```csharp
// プレイヤーを動かす
public void Execute() { ... }
```

Good:

```csharp
/// <summary>
/// 目標地点へ移動速度に従って1フレーム分移動する。目標に到達済みなら何もしない。
/// </summary>
public void MovePlayerTowardTarget()
{
    ...
}
```

### 7.8 namespace

- namespace は2階層以内にする (`MyGame.Combat`)
- `_` や記号を使わない

---

## 8. 総合例

Bad:

```csharp
using UnityEngine;
public class enemy : MonoBehaviour {
    public float spd=3;
    public int m_hp = 100;
    public System.Action onDie;
    GameObject p;
    void Start(){ p = GameObject.Find("Player"); }
    void Update() {
        if(Vector3.Distance(transform.position,p.transform.position)<10) Move();
    }
    void Move(){ transform.position=Vector3.MoveTowards(transform.position,p.transform.position,spd*Time.deltaTime); }
    public void Damage(int d, bool b) {
        m_hp-=b?d*2:d;
        if(m_hp<=0){ onDie(); Destroy(gameObject); }
    }
}
```

問題点:

- クラス名が camelCase
- Allman スタイルでない、波括弧の省略
- public フィールド、`m_` プレフィックス
- event キーワードなし、event 名に `On`
- 1文字の名前 (`p` `d` `b`)
- 曖昧なメソッド名 (`Move` `Damage`)
- bool 引数
- マジックナンバー
- null チェックなしの発火

Good:

```csharp
using System;
using UnityEngine;

public enum DamageMultiplier
{
    Normal,
    Critical,
}

public class Enemy : MonoBehaviour
{
    public event Action EnemyDied;

    private const int CriticalDamageFactor = 2;
    private const float ChaseStartDistance = 10f;

    [SerializeField]
    private float _moveSpeed = 3f;

    [SerializeField]
    private int _maxHealth = 100;

    [SerializeField]
    private Transform _playerTransform;

    private int _currentHealth;

    public int CurrentHealth => _currentHealth;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    private void Update()
    {
        if (IsPlayerWithinChaseDistance())
        {
            MoveTowardPlayer();
        }
    }

    public void TakeDamage(int damageAmount, DamageMultiplier multiplier)
    {
        int appliedDamage = multiplier == DamageMultiplier.Critical
            ? damageAmount * CriticalDamageFactor
            : damageAmount;

        _currentHealth -= appliedDamage;

        if (_currentHealth <= 0)
        {
            OnEnemyDied();
            Destroy(gameObject);
        }
    }

    public void OnEnemyDied()
    {
        EnemyDied?.Invoke();
    }

    private bool IsPlayerWithinChaseDistance()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        return distanceToPlayer < ChaseStartDistance;
    }

    private void MoveTowardPlayer()
    {
        float maxStepDistance = _moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _playerTransform.position, maxStepDistance);
    }
}
```

購読側:

```csharp
using UnityEngine;

public class ScoreBoard : MonoBehaviour
{
    private const int PointsPerEnemyKill = 100;

    [SerializeField]
    private Enemy _enemy;

    private int _totalScore;

    private void OnEnable()
    {
        _enemy.EnemyDied += Enemy_EnemyDied;
    }

    private void OnDisable()
    {
        _enemy.EnemyDied -= Enemy_EnemyDied;
    }

    private void Enemy_EnemyDied()
    {
        AddPointsToTotalScore(PointsPerEnemyKill);
    }

    private void AddPointsToTotalScore(int points)
    {
        _totalScore += points;
    }
}
```

---

## 9. 提出前チェックリスト

コードを書いた後、変更したすべての `.cs` ファイルで確認する。
違反があれば直してから完了とする。

### 書式

- [ ] インデントがスペース4つである
- [ ] 波括弧が Allman スタイルである
- [ ] 1行の `if` にも波括弧がある
- [ ] 1行1文、1行1宣言である
- [ ] 属性が1行に1つである

### 命名

- [ ] クラス、メソッド、プロパティ、enum、event、const が PascalCase である
- [ ] ローカル変数と引数が camelCase である
- [ ] private フィールドが `_camelCase` である
- [ ] `m_` `k_` `s_` やハンガリアン記法がない
- [ ] bool が `Is` `Has` `Can` などで始まる肯定形である
- [ ] 略語が1単語扱いである (`UiManager`)

### 説明的な命名

- [ ] `Execute` `Run` `Do` `Process` `Handle` `Say` `Set` `Get` `Init` `Check` が単体で使われていない
- [ ] メソッド名に目的語がある
- [ ] bool 引数がない (enum にした)
- [ ] マジックナンバーがない (const にした)
- [ ] リテラル引数に名前付き引数を使った
- [ ] 単位が名前に入っている (`Seconds` `Degrees`)

### イベント

- [ ] event に `event` キーワードがある
- [ ] event 型が `Action` / `Action<T>` である
- [ ] event 名が動詞句 (`DoorOpened`) で、`On` と `Event` を含まない
- [ ] 発火メソッドが `On` + event 名で、`?.Invoke()` を使う
- [ ] 発火メソッド名が Unity のメッセージ関数と衝突しない
- [ ] 購読ハンドラが `Subject_EventName` である
- [ ] ラムダで購読していない
- [ ] `OnEnable` で購読し `OnDisable` で解除している

### Unity 固有

- [ ] ファイル名と MonoBehaviour のクラス名が一致する
- [ ] public フィールドがない (`[SerializeField] private` にした)
- [ ] 改名したシリアライズフィールドに `[FormerlySerializedAs]` がある
- [ ] `Update` 内で LINQ や `GameObject.Find` を呼んでいない

---

## 出典との差分

このスキルの決定と出典の差分を示す。

| 項目 | Unity ガイド | Google C# Style Guide | このスキル |
|---|---|---|---|
| インデント | 規定なし (別記事) | スペース2つ | スペース4つ |
| 波括弧 | 規定なし (別記事) | 改行しない (K&R) | 改行する (Allman) |
| private フィールド | camelCase または `m_` | `_camelCase` | `_camelCase` |
| 定数 | `k_` + PascalCase | 修飾子で命名を変えない | PascalCase |
| 略語 | 規定なし | 1単語扱い (`MyRpc`) | 1単語扱い |
| event の型 | `Action` 推奨 | 規定なし | `Action` |
| event 発火 | `On` + event 名 | `?.Invoke()` | 両方を採用 |
| 購読ハンドラ | `Subject_Event` | 規定なし | `Subject_Event` |
| namespace とフォルダ | 一致させる | 一致を強制しない | 一致を強制しない |
