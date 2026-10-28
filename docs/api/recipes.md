<!-- このページは tools/api_doc/build_md.py が作ります。手で直さないでください。 -->
[入口](README.md) ・ [設計の考え方](design.md) ・ [つながりの図](diagrams.md) ・ [やりたいこと別の手順](recipes.md) ・ [ガイド](guide.md)

「〇〇をしたい」から引く手順です。どの手順も、設計の考え方に沿っています。別の機能のクラスを直接呼ばず、Core の置き場・合図・イベントを使います。

コード例は、手順を説明するための見本です。そのまま入れる前に、自分の場面に合わせて直してください。コード例は、Unity でコンパイルして確かめてはいません。

## 手順の一覧

1. [困っている生き物（例: 猫）を、元気にする](#r-creature-mood)
2. [生き物に、自分で考えて動く振る舞い（AI のような動き）を持たせる](#r-creature-ai)
3. [チュートリアルの中で、イベントを待つ・使う](#r-tutorial-event)
4. [新しいチュートリアルを足す](#r-tutorial-new)
5. [合図に反応させる](#r-react-cue)
6. [新しい食べ物を足す](#r-add-food)
7. [本編で欲しい物を出す](#r-open-request)
8. [カメラのカットを作る](#r-camera-cut)

---

<a id="r-creature-mood"></a>

## 困っている生き物（例: 猫）を、元気にする

このゲームの生き物には、「困っている」と「元気」の 2 つの状態があります。体験者が欲しい物を届けると、困っている生き物が元気になります。村人も海の住人も、同じ仕組みで動いています。猫を足すときも、この仕組みに乗せます。

| 状態 | 見た目（[`VillagerMood`](Village.md#t-villagermood) が Animator で出す） |
|---|---|
| 困っている | 頭の上に印が出ます。肩を落として、ゆっくり立ちます。ときどき、がっくりうなだれます。うなだれる間隔は生き物ごとにばらつくので、全員が同時には動きません |
| 元気 | 印が消え、ふつうに立ちます。ステップが済んだときは、元気な生き物が 1 回喜びます |
| 困っているときに違う物が届いた | がっかりする動き（トリガー `Missed`）を 1 回します。状態は困っているのままです |

**猫を、困っている状態から元気にする手順**

1. 猫に [`VillagerMood`](Village.md#t-villagermood) を付けます。Animator Controller には、村人と同じパラメータ（`Worried`・`Slump` の bool と、`Missed` のトリガー）を用意します。村人の `Villager.controller` を元にすると早いです。
2. 頭の上の印を、Worry Mark に入れます。
3. フィールドの `Steps` の下で、猫を助ける段の [`FieldStep`](Village.md#t-fieldstep) を選び、Helped に猫を入れます。Food に、猫が欲しがる物の ID を入れます。
4. Play すると、猫は困っている状態で始まります。その段の欲しい物が届くと、[`FieldStages`](Village.md#t-fieldstages) が猫を元気にします。Helped に猫の名前がある段がすべて済むまで、猫は困ったままです。
5. 違う物が届いたときのがっかりする動きは、[`DeliveryFx`](Village.md#t-deliveryfx) が送ります。猫の側では何もしません。

**あわせて使える変え方**

- 元気になった段で、猫が新しく現れるようにしたい: 猫を、その段の [`FieldStep`](Village.md#t-fieldstep) の子に置きます。段が済むと、小さい所から「ぽんっ」と出てきます。
- チュートリアルの合図で、猫に何かさせたいとき: 猫か近くの物に [`CueReaction`](UI.md#t-cuereaction) を付けます。Cue に合図の名前（例: `grassland.cat-look`）を入れ、On Cue に Animator のトリガーをつなぎます。チュートリアルからは、`Cue("grassland.cat-look");` で合図を出します。

使う型: [`VillagerMood`](Village.md#t-villagermood)、[`FieldStep`](Village.md#t-fieldstep)、[`FieldStages`](Village.md#t-fieldstages)、[`DeliveryFx`](Village.md#t-deliveryfx)、[`CueReaction`](UI.md#t-cuereaction)

---

<a id="r-creature-ai"></a>

## 生き物に、自分で考えて動く振る舞い（AI のような動き）を持たせる

振る舞いは、ふつうの MonoBehaviour で書きます。特別な枠組みは要りません。守ることは 3 つです。

- 読むのは Core だけにします。場面は [`Phases`](Core.md#t-phases)、体験者の立つ場所は [`Standing`](Core.md#t-standing)、届いた物は `IFieldRequests.Received` から受け取ります。
- `GameObject.Find` や `FindObjectOfType` で、ほかの機能の物を探しません。どこから使われているか追えなくなるためです。
- 受け取りの登録は `OnEnable` で行い、`OnDisable` で外します。

ファイルは、その生き物がいるフィールドの機能（たとえば `Assets/Scripts/Village/`）に置きます。

次の例の猫は、本編のあいだだけ、決めた範囲を歩き回ります。欲しかった物が届くと、喜ぶトリガーを 1 回送ります。

```csharp
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>猫が、本編のあいだ決めた範囲を歩き回る。欲しい物が届くと喜ぶ。</summary>
    public sealed class CatWander : MonoBehaviour
    {
        [Tooltip("歩き回る範囲の半径（m）。フィールドの Prefab の中の寸法")]
        [SerializeField] float radius = 0.08f;
        [Tooltip("歩く速さ（m / 秒）")]
        [SerializeField] float speed = 0.03f;
        [SerializeField] Animator animator;

        Vector3 _home, _target;
        bool _walking;
        IFieldRequests _field;

        void OnEnable()
        {
            _home = transform.localPosition;
            Phases.Changed += OnPhase;
            OnPhase(Phases.Current);
            _field = GetComponentInParent<IFieldRequests>(); // 親のフィールド（FieldStages）
            if (_field != null) _field.Received += OnReceived;
        }

        void OnDisable()
        {
            Phases.Changed -= OnPhase;
            if (_field != null) _field.Received -= OnReceived;
        }

        void OnPhase(PhaseId phase)
        {
            _walking = phase == new PhaseId("main-game");
            if (animator != null) animator.SetBool("Walking", _walking);
            PickTarget();
        }

        void OnReceived(Delivered delivered)
        {
            if (delivered.Kind == DeliveryKind.Requested && animator != null) animator.SetTrigger("Happy");
        }

        void Update()
        {
            if (!_walking) return;
            Vector3 from = transform.localPosition;
            transform.localPosition = Vector3.MoveTowards(from, _target, speed * Time.deltaTime);
            Vector3 dir = _target - from;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-8f)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
            if ((transform.localPosition - _target).sqrMagnitude < 1e-6f) PickTarget();
        }

        void PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * radius;
            _target = _home + new Vector3(r.x, 0f, r.y);
        }
    }
}
```

振る舞いを足すときの考え方です。

- 体験者が前に立ったら寄ってくる: `Standing.Changed` を受け、自分のフィールドの ID と同じなら、目標を体験者の側にします。
- 判断が複雑になったら: 判断の部分を、Unity に依存しないクラス（[`StepProgress`](Village.md#t-stepprogress) のような形）に分けます。EditMode のテストで確かめられます。
- 場面の名前（`main-game` など）は、`docs/progress-keys.md` にある名前だけを使います。

使う型: [`Phases`](Core.md#t-phases)、[`Standing`](Core.md#t-standing)、[`Fields`](Core.md#t-fields)、[`IFieldRequests`](Core.md#t-ifieldrequests)、[`Delivered`](Core.md#t-delivered)、[`DeliveryKind`](Core.md#t-deliverykind)

---

<a id="r-tutorial-event"></a>

## チュートリアルの中で、イベントを待つ・使う

チュートリアルのスクリプト（[`TutorialScript`](Tutorial.md#t-tutorialscript) を継いだクラス）の `Play()` の中で、`yield return` で待ちます。よく使うイベントには、待ちがもう用意されています。

| 待ちたいこと | 書き方 | 終わったあとに読めるもの |
|---|---|---|
| 雲を刺す | `yield return WaitForPierce(秒);` | `TimedOut` |
| かけらの付いた先を口に含む | `yield return WaitForMouth(秒);` | `TimedOut` |
| 食感が n 回切り替わる | `yield return WaitForTextureChanges(n, 秒);` | `Changes` |
| デバイスで決定する | `var w = WaitForConfirm(秒); yield return w;` | `w.Result`（決定した食感） |
| 食べ物が生まれる | `var w = WaitForSpawn("apple", 秒); yield return w;` | `w.Result`（[`Spawned`](Core.md#t-spawned)） |
| フィールドに物が届く | `var w = WaitForDelivery("sea", 秒); yield return w;` | `w.Result`（[`Delivered`](Core.md#t-delivered)。`Kind` で正しいか分かる） |
| 体験者がフィールドの前に立つ | `yield return WaitForStanding("sea", 秒);` | `TimedOut` |
| どれか 1 つが起きる | `var any = WaitForAny(a, b); yield return any;` | `any.Index`（先に終わった待ちの番号） |

- 秒を 0 にすると、時間切れはありません。展示を止めないために、ふつうは時間切れを入れ、`TimedOut` のときの流れも書きます。
- 待ちは、待ち始める前に済んだ操作（先回り）も数えます。早く進める体験者でも、止まりません。先回りした段のセリフを飛ばすときは、`AlreadyDone(TutorialEvent.Pierced)` を見ます。
- ほかの物に何かをさせるときは、`Cue("合図の名前")` と `Say("セリフの id")` を使います。巫女やフィールドのクラスは、直接呼びません。
- 次のチュートリアルに伝えたいことは、`Memory.Set("名前")` で覚えます。名前は `docs/progress-keys.md` に足します。

次の例は、海に魚が届くのを 30 秒待ちます。ほめるか、ヒントを出します。

```csharp
public override IEnumerator Play()
{
    Cue("sea.glow");
    Say("t4-walk");
    var delivery = WaitForDelivery("sea", 30f);
    yield return delivery;
    if (delivery.TimedOut) { Say("t4-walk"); yield break; }
    if (delivery.Result.Value.Kind == DeliveryKind.Requested)
    {
        Say("t4-splendid");
        Memory.Set("delivered-by-standing");
    }
    Cue("sea.glow-off");
}
```

**用意されていないイベントを待ちたいとき**

[`TutorialWait`](Tutorial.md#t-tutorialwait) を継いだ待ちを、`Assets/Scripts/Tutorial/` に足します。コンストラクタで、Core の置き場から相手を受け取り、聞き始めます。条件が満たされたら、`Complete()` を呼びます。聞くのをやめる処理は、`Unsubscribe()` に書きます。次の例は、あるフィールドの欲しい物が 1 つ解決するのを待ちます。

```csharp
using MouthOfGod.Core;

namespace MouthOfGod.Tutorial
{
    /// <summary>そのフィールドの欲しい物が、届いて解決するのを待つ。</summary>
    public sealed class ResolvedWait : TutorialWait
    {
        readonly IFieldRequests _field;

        public ResolvedWait(FieldId field, float timeoutSeconds) : base(timeoutSeconds, null)
        {
            _field = Fields.Find(field);
            if (_field == null) { Cancel(); return; } // フィールドが無ければ、すぐ終える
            _field.Closed += OnClosed;
        }

        /// <summary>解決した欲しい物。時間切れなら null。</summary>
        public RequestClosed? Result { get; private set; }

        void OnClosed(RequestClosed closed)
        {
            if (!closed.Resolved) return;
            Result = closed;
            Complete();
        }

        protected override void Unsubscribe()
        {
            if (_field != null) _field.Closed -= OnClosed;
        }
    }
}
```

使う側は、`var wait = new ResolvedWait(new FieldId("sea"), 30f); yield return wait;` と書きます。`new` で作った待ちは、チュートリアルを途中で止めても自動では取り消されません。`OnAborted()` で `wait.Cancel()` を呼びます。

**待たずに、イベントを受け続けたいとき**

チュートリアルの間ずっと何かを数えたい、のような場合です。`Play()` の始めに購読し、終わりと `OnAborted()` の両方で外します。外し忘れると、次のチュートリアルの間も呼ばれ続けます。

使う型: [`TutorialScript`](Tutorial.md#t-tutorialscript)、[`TutorialWait`](Tutorial.md#t-tutorialwait)、[`DeliveryWait`](Tutorial.md#t-deliverywait)、[`ConfirmWait`](Tutorial.md#t-confirmwait)、[`ForkWait`](Tutorial.md#t-forkwait)、[`SpawnWait`](Tutorial.md#t-spawnwait)、[`TextureChangeWait`](Tutorial.md#t-texturechangewait)、[`StandWait`](Tutorial.md#t-standwait)、[`AnyWait`](Tutorial.md#t-anywait)、[`TutorialMemory`](Tutorial.md#t-tutorialmemory)

---

<a id="r-tutorial-new"></a>

## 新しいチュートリアルを足す

1. `Assets/Scripts/Tutorial/` に、[`TutorialScript`](Tutorial.md#t-tutorialscript) を継いだクラスを 1 つ書きます。`Phase` には、やることを表す名前を返します（例: `new PhaseId("harvest")`）。番号は使いません。
2. 場面の名前を、`docs/progress-keys.md` の表に足します。
3. Prefab を `Assets/Resources/Prefabs/Tutorial/` に作ります。同じ Prefab に [`MikoLineBook`](Miko.md#t-mikolinebook) を付け、このチュートリアルのセリフを書きます。セリフの id は、シーン全体で重ならないようにします。
4. 共有シーンで、その Prefab を [`TutorialRunner`](Tutorial.md#t-tutorialrunner) の子に置きます。並び順が、動く順番です。シーンは Scene Fusion の中で直します。
5. 最後のチュートリアルの場面名を変えたときは、[`GameRunner`](Game.md#t-gamerunner) の After Phase と、[`TutorialRunner`](Tutorial.md#t-tutorialrunner) の Skip Phase も合わせます。

使う型: [`TutorialScript`](Tutorial.md#t-tutorialscript)、[`TutorialRunner`](Tutorial.md#t-tutorialrunner)、[`MikoLineBook`](Miko.md#t-mikolinebook)、[`Phases`](Core.md#t-phases)

---

<a id="r-react-cue"></a>

## 合図に反応させる

**コードを書かない場合**: 反応させたい物に [`CueReaction`](UI.md#t-cuereaction) を付けます。Entries に 1 行足し、Cue に合図の名前、Show・Hide に出し入れする物、On Cue に呼ぶものを入れます。Seconds を入れると、その秒数のあとで元に戻ります。

**コードを書く場合**: `Cues.Requested` を `OnEnable` で購読し、`OnDisable` で外します。名前で自分あての合図かを見分けます。

```csharp
void OnEnable() => Cues.Requested += OnCue;
void OnDisable() => Cues.Requested -= OnCue;

void OnCue(CueRequested request)
{
    if (request.Cue.Value == "grassland.cat-wake") WakeUp();
}
```

合図の名前は、受ける側の名前から始めます（`grassland.`、`miko.`、`cloud.` など）。

使う型: [`CueReaction`](UI.md#t-cuereaction)、[`Cues`](Core.md#t-cues)、[`CueRequested`](Core.md#t-cuerequested)、[`CueId`](Core.md#t-cueid)

---

<a id="r-add-food"></a>

## 新しい食べ物を足す

1. 食べ物の Prefab を `Assets/Resources/Prefabs/Food/` に置きます。ファイル名が食べ物の ID になります（例: `cat_food.prefab` → `cat_food`）。飛ばすには `Rigidbody` が要ります。
2. 大きさは、メニュー `MouthOfGod/Food/Fit Prefab Sizes` でそろえられます。吹き出しのアイコンは `MouthOfGod/Food/Generate Icons` で作ります。
3. どの食感で生まれるかを、自分のフィールドの [`FoodTable`](Food.md#t-foodtable) に 1 行足します。全フィールドで生まれる物だけは、共通の表（[`FoodSpawner`](Food.md#t-foodspawner) の Prefab に付いた表）に足します。
4. フィールドに欲しがらせるなら、その段の [`FieldStep`](Village.md#t-fieldstep) の Food に、同じ ID を書きます。
5. Play すると、表の食い違いが Console に警告として出ます。

使う型: [`FoodTable`](Food.md#t-foodtable)、[`FoodTables`](Food.md#t-foodtables)、[`FoodRow`](Food.md#t-foodrow)、[`FoodSpawner`](Food.md#t-foodspawner)、[`FieldStep`](Village.md#t-fieldstep)

---

<a id="r-open-request"></a>

## 本編で欲しい物を出す

本編が、いつ・どのフィールドに欲しい物を出すかを決めるコードは、まだありません。書くときは、次の形にします。

```csharp
foreach (IFieldRequests field in Fields.All)
{
    if (field.CanOpen) field.Open();   // 出せなければ false が返る
}
```

- 後から置かれたフィールドも受け取るには、`Fields.All` を見たあと、`Fields.Added` を購読します。
- 出した欲しい物が終わった知らせは `Closed` です。`Resolved` が true なら届いて解決、false なら待ちきれずに終わった物です。
- 待ちきれなかった物は、`Expire(番号)` で 1 つずつ終えます。番号は `RequestIssued.Request` です。
- Main シーンには [`GameRunner`](Game.md#t-gamerunner) が置いてあります。どのフィールドも、同時に出せる数をステップの数にそろえています。
- Play 中に、チュートリアルが終わったあとで `MouthOfGod/Dev/Open All Requests (Play mode, after tutorial)` を押すと、全フィールドで欲しい物を出せるだけ出します。

使う型: [`Fields`](Core.md#t-fields)、[`IFieldRequests`](Core.md#t-ifieldrequests)、[`RequestIssued`](Core.md#t-requestissued)、[`RequestClosed`](Core.md#t-requestclosed)、[`GameRunner`](Game.md#t-gamerunner)

---

<a id="r-camera-cut"></a>

## カメラのカットを作る

1. カット用のカメラ（VR の Main Camera とは別のもの）に [`CameraCutPlayer`](CameraWork.md#t-cameracutplayer) を付けます。
2. シーンに空の GameObject を置き、行き先・曲がり方・回る中心の目印にします。
3. Shots にカットを足し、Kind（Line・Curve・Orbit・Hold）、秒数か速さ、Ease、Aim を選び、目印を入れます。
4. カメラを選ぶと、シーンビューに軌道の線が出ます。目印を動かして、線を見ながら直します。
5. Play On Start を入れるか、コードで `Play()` を呼びます。終わったあとの処理は、On Finished につなぎます。

村のカットの見本は、メニュー `MouthOfGod/CameraWork/Add Village Tour` と `Add Village Zoom-Out` で置けます。

使う型: [`CameraCutPlayer`](CameraWork.md#t-cameracutplayer)、[`CameraShot`](CameraWork.md#t-camerashot)、[`ShotKind`](CameraWork.md#t-shotkind)、[`ShotAim`](CameraWork.md#t-shotaim)、[`CameraEase`](CameraWork.md#t-cameraease)
