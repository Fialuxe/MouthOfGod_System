# Tutorial

チュートリアル 1〜4 の進行です。**チュートリアルごとに、コルーチンで流れを書く「人形劇スクリプト」を 1 つ**書きます。`TutorialRunner` が、それを順に動かします。スクリプトは合図を出すだけです。見せ方は、合図を受けたフィールドや UI が決めます。

状態: 案（#50）。T1〜T4 は仮の中身が入りました（10/25）。Unity の Play では、まだ確かめていません。決めた経緯と採らなかった案は、[docs/decisions.md](../../../docs/decisions.md) の 2026-10-19 にあります。

## 部品

| 部品 | 役割 |
|---|---|
| `TutorialScript` | 人形劇スクリプトの土台です。継いだクラスを、チュートリアルごとに 1 つ書きます（`T1Fork.cs` …）。`Phase`（場面の名前）と、`Play()`（流れのコルーチン）を書きます |
| `TutorialRunner` | 子の `TutorialScript` を、Hierarchy の並び順に 1 つずつ動かします。無効にしたスクリプトは飛ばすので、特定のチュートリアルだけを試せます。例外が出たら、そのスクリプトだけをやめて次へ進みます。`Begin()` で始め、`Stop()` でやめます |
| `TutorialMemory` | チュートリアルのあいだで引き継ぐ「覚えたこと」です。名前だけを持ちます。`Has(名前)`・`Set(名前)` で使います |
| `DeliveryWait` / `ConfirmWait` / `ForkWait` / `SpawnWait` / `TextureChangeWait` / `StandWait` / `AnyWait` | `yield return` で待つものです。時間切れも付けられます。待てるのは、次の 7 つです。
物の届き、デバイスでの決定、フォークの動き、物が生まれる（`WaitForSpawn`）、食感の切り替わり（`WaitForTextureChanges`）、その場所に立つ（`WaitForStanding`）、どれか早いほう |
| `Say(id)` | 巫女にセリフを言わせます。合図 `miko.say.<id>` を出します。文は、シーンの Miko の LineBook に書きます |

## ほかの機能とのつなぎ方

- 参照するのは Core だけです。
- 始めるのは `TutorialRunner.Begin()` です。いつ呼ぶか（台の前に立ったとき、など）は、呼ぶ側が決めます。
- `TutorialRunner` の **Begin On Start** を入れると、Play で自動で始まります。**Begin Delay Seconds** で、始めるまで待ちます。
- 全部終わると `Finished` が出て、場面は空に戻ります。次の場面（本編）は、`Finished` を受けた側が決めます。
- 合図を受けるフィールドや UI は、Core の `Cues.Requested` を購読します（下の例）。

### 人形劇スクリプトの書き方

```csharp
public sealed class T4Deliver : TutorialScript
{
    public override PhaseId Phase => new PhaseId("deliver");

    public override IEnumerator Play()
    {
        Cue("sea.glow");                                       // 海を光らせる。光らせ方は海の担当
        DeliveryWait first = WaitForDelivery(timeoutSeconds: 20f);
        yield return first;                                    // 届くか、20 秒たつまで待つ

        if (first.TimedOut) Cue("sea.hint");
        else if (first.Result.Value.Field == new FieldId("sea") && first.Result.Value.WasRequested) Cue("sea.cheer");
        else Cue("sea.notHere");

        Memory.Set("delivered-to-the-right-place");            // 次のチュートリアルが名前で読める
    }
}
```

- 書くのは、合図（`Cue`）、待つ条件（`WaitFor…`）、分岐だけです。フィールドの中身は知りません。
- そのチュートリアルにしかない物は、このクラスの `[SerializeField]` に持たせてよいです。浮かぶお供え物や影などです。Prefab は `Assets/Resources/Prefabs/Tutorial/` に置きます。
- 場面の名前（`Phase`）は、番号ではなく、やることを表す名前にします。一覧は [docs/progress-keys.md](../../../docs/progress-keys.md) にあります。

### フィールド・UI 側（合図を受ける）

```csharp
public sealed class SeaReaction : MonoBehaviour
{
    void OnEnable()  => Cues.Requested += OnCue;
    void OnDisable() => Cues.Requested -= OnCue;

    void OnCue(CueRequested request)
    {
        if (request.Cue == new CueId("sea.glow")) StartCoroutine(Glow());   // 見せ方は、フィールドの担当が自由に決める
    }
    // そのフィールドだけの特別な反応が要るときは、Phases.Current == new PhaseId("deliver") のように、名前で読んでよい
}
```

合図の名前は、フィールド名から始めます（`sea.glow`、`village.cheer`）。

## チュートリアル 1〜4

台本は [シナリオブック](../../../docs/scenario-book.md) の S3〜S6 です。セリフの id・合図の名前・待ち時間は、すべて Inspector で変えられます。合図やセリフを空にすると、出しません。

| ファイル | 場面（Phase） | すること | 覚えること |
|---|---|---|---|
| `T1Fork` | `fork` | 雲を刺す → 口に含む → 食感が 3 回変わる → ボタンで決める | `fork-learned` |
| `T2Spawn` | `spawn` | 影を見ながら、リンゴを生む。違う物なら、やり直す | `food-born`・`texture-shadow-seen` |
| `T3Recognize` | `recognize` | 海に魚の欲しい物を出し、魚を生む。欲しい物は出したままにする | `request-seen` |
| `T4Deliver` | `deliver` | 海の前まで歩き、魚を届ける。違う場所なら「ここではない」 | `delivered-by-standing` |

どのチュートリアルも、待ちに **Give Up Seconds** があります。その時間を過ぎると、できていなくても次へ進みます。ブースで止まらないようにするためです。

### シーンへの置き方

1. 空の GameObject に `TutorialRunner` を付けます。
2. その子に、`T1Fork`・`T2Spawn`・`T3Recognize`・`T4Deliver` を、この順で 1 つずつ置きます。
3. シーンの Miko を選び、メニュー **MouthOfGod > Miko > Add Tutorial Lines To Selected LineBook** を実行します。22 行のセリフが LineBook に入ります。このメニューは一時的なものなので、入れ終えたら消します。
4. `CloudRing` のオブジェクトに、`CloudCues`（Fork）を足します。雲が光る・寄ってくる見せ方です。
5. 影を出す `FoodShadow`（UI）を、シーンに 1 つ置きます。
6. フィールドごとに、立つ場所の `StandArea`（Village）を置きます。置き方は [Village/README.md](../Village/README.md) にあります。
7. ほかの合図への反応は、`CueReaction`（UI）で付けます。コードは要りません。

### 出す合図

| 合図 | 意味 | いま受けるもの |
|---|---|---|
| `miko.say.<id>` / `miko.follow` / `miko.stay` | 巫女のセリフと動き | Miko |
| `cloud.glow` / `cloud.approach` / `cloud.calm` | 雲を光らせる・寄せる・戻す | `CloudCues` |
| `shadow.show` / `shadow.faint` / `shadow.emphasize` / `shadow.hide` | 影を出す・薄く出す・強く出す・消す | `FoodShadow` |
| `<field>.footprint` / `<field>.footprint-off` | 立つ場所の足あとの印を出す・消す | `StandArea` |
| `<field>.glow` / `<field>.glow-off` / `<field>.cheer` / `<field>.shake-head` | フィールドを光らせる・消す、村人が喜ぶ・首を振る | `CueReaction` で付ける |
| `offering.show` / `offering.hide` | お供え物のリンゴを見せる・消す | `CueReaction` で付ける（`T2Spawn` の Offering に入れてもよい） |
| `device.button.glow` / `device.button.glow-strong` / `device.button.glow-off` | ボタンの位置を光らせる | `CueReaction` で付ける |
| `staff.alert` | スタッフに知らせる | `CueReaction` で付ける |

## まだないもの

- 台本にないセリフ（`t2-remember-short`・`t2-hint-pierce`・`t2-hint-mouth`・`t3-wrong`・`t4-here`）は、仮の文です。
- セリフの声は、まだありません。字幕だけが出ます。

## テスト

テストは `Tests/` にあります（EditMode）。`TutorialRunnerTests` は、順に動かす決まり・例外・覚えたこと・合図を確かめます。`TutorialWaitTests` は、待ちの決まりを確かめます。`T1ForkTests`〜`T4DeliverTests` は、各チュートリアルの流れを確かめます。時間切れは、EditMode では時間が進まないので確かめていません。Core の `Phases`・`Cues` は `Core/Tests/` にあります。
