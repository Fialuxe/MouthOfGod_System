# Tutorial

チュートリアル 1〜4 の進行です。**チュートリアルごとに、コルーチンで流れを書く「人形劇スクリプト」を 1 つ**書きます。`TutorialRunner` が、それを順に動かします。スクリプトは合図を出すだけです。見せ方は、合図を受けたフィールドや UI が決めます。

状態: 案（#50）。T1〜T4 は仮の中身が入りました（10/25）。Unity の Play では、まだ確かめていません。決めた経緯と採らなかった案は、[docs/decisions.md](../../../docs/decisions.md) の 2026-10-19 にあります。

## 部品

| 部品 | 役割 |
|---|---|
| `TutorialScript` | 人形劇スクリプトの土台です。継いだクラスを、チュートリアルごとに 1 つ書きます（`T1Fork.cs` …）。`Phase`（場面の名前）と、`Play()`（流れのコルーチン）を書きます |
| `TutorialRunner` | 子の `TutorialScript` を、Hierarchy の並び順に 1 つずつ動かします。無効にしたスクリプトは飛ばすので、特定のチュートリアルだけを試せます。例外が出たら、そのスクリプトだけをやめて次へ進みます。`Begin()` で始め、`Stop()` でやめます |
| `TutorialMemory` | チュートリアルのあいだで引き継ぐ「覚えたこと」です。名前だけを持ちます。`Has(名前)`・`Set(名前)` で使います |
| `DeliveryWait` / `ConfirmWait` / `ForkWait` / `SpawnWait` / `TextureChangeWait` / `StandWait` / `AnyWait` | `yield return` で待つものです。時間切れも付けられます。待てるのは次の 7 つです。1 つ目から 6 つ目は、物が届く、デバイスで決定する、フォークで刺す・口に含む、物が生まれる、食感が切り替わる、その場所に立つ、です。7 つ目は、渡した待ちのうち、どれかが終わったら終わる待ちです |
| `Say(id)` | 巫女にセリフを言わせます。合図 `miko.say.<id>` を出します。文は、そのチュートリアルの Prefab に付けた `MikoLineBook` に書きます |
| `TutorialHistory` | チュートリアルが始まってから体験者がしたことの記録です。`TutorialRunner` が、チュートリアルごとに作ります。待ちは、この記録を使って、先回りした操作も数えます（下の「先回りした操作」） |
| `AlreadyDone(種類)` | その操作を、もう先回りして済ませているかを返します。済んでいる段の案内のセリフを飛ばすときに使います |

### 先回りした操作

体験者は、案内のセリフより先に動くことがあります。そこで、待ちは次の決まりで操作を数えます。各チュートリアルのスクリプトで、特別に書く必要はありません。

- 待ちは、そのチュートリアルが始まってから起きて、まだ使われていない操作を数えます。もう起きていれば、待ちはすぐ終わります。
- 待ちが終わると、その種類の操作は使ったことにします。同じリンゴ 1 つで、2 回進むことはありません。
- 前のチュートリアルで起きた操作は数えません。
- 記録するのは次の 5 つです。
  1. フォークで雲を刺した
  2. かけらの付いた先を口に含んだ
  3. 食感が変わった
  4. デバイスで決定した
  5. 食べ物が生まれた
- 物の届きと立つ場所は、記録しません。立つ場所は、待ち始めたときにもう立っていれば、すぐ終わります。

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

## プロローグとチュートリアル 1〜4

台本は [シナリオブック](../../../docs/scenario-book.md) の S1・S3〜S6 です。セリフの id・合図の名前・待ち時間は、すべて Inspector で変えられます。合図やセリフを空にすると、出しません。

| ファイル | 場面（Phase） | すること | 覚えること |
|---|---|---|---|
| `PrologueVideo` | `prologue` | 導入の動画を流し、「立ち上がって、スタッフの指示を聞いて」と伝える | — |
| `T1Fork` | `fork` | 雲を刺す → 口に含む → 食感が 3 回変わる → ボタンで決める | `fork-learned` |
| `T2Spawn` | `spawn` | 影を見ながら、リンゴを生む。違う物なら、やり直す | `food-born`・`texture-shadow-seen` |
| `T3Recognize` | `recognize` | 海に魚の欲しい物を出し、魚を生む。欲しい物は出したままにする | `request-seen` |
| `T4Deliver` | `deliver` | 海の前まで歩き、魚を届ける。違う場所なら「ここではない」 | `delivered-by-standing` |

どのチュートリアルも、待ちに **Give Up Seconds** があります。その時間を過ぎると、できていなくても次へ進みます。ブースで止まらないようにするためです。

### 導入の動画（プロローグ）

動画は、シーンを切り替えずに、同じシーンの中の画面で流します。理由は、[決めたことの記録](../../../docs/decisions.md) の 2026-10-25 にあります。

- 導入の動画（約 1 分）は、`Assets/StreamingAssets/` に置きます。`Prologue` の Prefab の `VideoScreen` の **Streaming Assets File** に、ファイル名（例: `Begin_KeltPiano.mp4`）を書きます。短い動画なら、**Clip** に入れてもかまいません。
- 画面は、体験者の正面 3 m 先に出ます。大きさは、水平の視野の 100 度を覆う幅です（**View Degrees**）。視界をほぼ覆います。
- 流している間は、周りを暗い球で囲みます（**Dark Surround**）。村もミニチュアも見えません。
- 画面は空間に固定します。頭に付けると、頭と一緒に動いて酔いやすいためです。大きく向きを変えたときだけ、ゆっくり正面へ回り込みます。
- ほかに隠す物・出す物があれば、**Hide While Playing** と **Show While Playing** に入れます。
- 試すときは、Play 中に `VideoScreen` を右クリックし、**Skip** で飛ばせます。

### チュートリアルの Prefab

プロローグとチュートリアル 1〜4 は、1 つずつ Prefab にしています。置き場所は `Assets/Resources/Prefabs/Tutorial/` です。担当者は、自分のチュートリアルの Prefab だけを直します。ほかの人と同じファイルを直さないので、Scene Fusion を待たずに並列で作業できます。

| Prefab | 中身 |
|---|---|
| `Prologue` | `PrologueVideo`、子の `VideoScreen`、セリフ |
| `T1Fork` | `T1Fork`、セリフ |
| `T2Spawn` | `T2Spawn`、お供え物の仮の見た目（赤い球）、セリフ |
| `T3Recognize` | `T3Recognize`、セリフ |
| `T4Deliver` | `T4Deliver`、セリフ |

Prefab がまだ無いときは、メニュー **MouthOfGod > Tutorial > Generate Tutorial Prefabs** で作ります。既にある Prefab は上書きしません。

秒数・合図の名前・セリフは、Prefab を開いて直します。シーンに置いた Prefab の上で直すと、そのシーンだけの上書きになり、ほかのシーンには反映されません。

ただし、シーンの物は Prefab に入れられません。次の値だけは、シーンに置いた Prefab の上で入れます。
- セリフの **Point Target**（指さす物）と **Move To**（行き先）
- `VideoScreen` の **Hide While Playing** と **Show While Playing**（動画のあいだ隠す物・出す物）
- `T2Spawn` のお供え物の位置。体験者の前に浮かぶように動かします

### シーンへの置き方

1. 空の GameObject に `TutorialRunner` を付けます。
   - Play で自動で始めるときは、`TutorialRunner` の **Begin On Start** を入れます。**Wait Until Worn** が入っていれば、Quest をかぶるまで待ちます。
2. その子に、`Prologue`・`T1Fork`・`T2Spawn`・`T3Recognize`・`T4Deliver` の Prefab を、この順でドラッグします。この順に動きます。
3. 上に書いた、シーンの物だけを入れます。
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
| `prologue.start` / `prologue.end` | 導入の動画を流し始める・終わった | `CueReaction` で付ける |

## まだないもの

- 台本にないセリフ（`t2-remember-short`・`t2-hint-pierce`・`t2-hint-mouth`・`t3-wrong`・`t4-here`）は、仮の文です。
- セリフの声は、まだありません。字幕だけが出ます。
- 導入の動画は、`Assets/StreamingAssets/` に 2 本の候補があります。`Begin_KeltPiano.mp4` と `Begin_PureKelt.mp4` です。今は `Prologue` の Prefab で `Begin_KeltPiano.mp4` を流しています。どちらにするかは、まだ決まっていません。動画が無いときは、プロローグは最後のセリフだけを言って進みます。

## テスト

テストは `Tests/` にあります（EditMode）。`TutorialRunnerTests` は、順に動かす決まり・例外・覚えたこと・合図を確かめます。`TutorialWaitTests` は、待ちの決まりを確かめます。`T1ForkTests`〜`T4DeliverTests` は、各チュートリアルの流れを確かめます。時間切れは、EditMode では時間が進まないので確かめていません。Core の `Phases`・`Cues` は `Core/Tests/` にあります。
