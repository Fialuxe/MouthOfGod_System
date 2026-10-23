# Tutorial

チュートリアル 1〜4 の進行です。**チュートリアルごとに、コルーチンで流れを書く「人形劇スクリプト」を 1 つ**書きます。`TutorialRunner` が、それを順に動かします。スクリプトは合図を出すだけです。見せ方は、合図を受けたフィールドや UI が決めます。

状態: 案（#50）。枠組みだけで、T1〜T4 の中身はまだありません。決めた経緯と採らなかった案は、[docs/decisions.md](../../../docs/decisions.md) の 2026-10-19 にあります。

## 部品

| 部品 | 役割 |
|---|---|
| `TutorialScript` | 人形劇スクリプトの土台です。継いだクラスを、チュートリアルごとに 1 つ書きます（`T1.cs` …）。`Phase`（場面の名前）と、`Play()`（流れのコルーチン）を書きます |
| `TutorialRunner` | 子の `TutorialScript` を、Hierarchy の並び順に 1 つずつ動かします。無効にしたスクリプトは飛ばすので、特定のチュートリアルだけを試せます。例外が出たら、そのスクリプトだけをやめて次へ進みます。`Begin()` で始め、`Stop()` でやめます |
| `TutorialMemory` | チュートリアルのあいだで引き継ぐ「覚えたこと」です。名前だけを持ちます。`Has(名前)`・`Set(名前)` で使います |
| `DeliveryWait` / `ConfirmWait` / `ForkWait` / `AnyWait` | `yield return` で待つものです。物の届き、デバイスでの決定、フォークの動き、どれか早いほう、を待ちます。フォークは、雲を刺す（`WaitForPierce`）と口に含む（`WaitForMouth`）です。時間切れも付けられます |

## ほかの機能とのつなぎ方

- 参照するのは Core だけです。
- 始めるのは `TutorialRunner.Begin()` です。いつ呼ぶか（台の前に立ったとき、など）は、呼ぶ側が決めます。
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

## まだないもの

- T1〜T4 の中身は、まだありません。待つ相手が、まだそろっていません。
- 食べ物が生まれたことを知らせる仕組みは、まだ Core にありません。今は、Food の中の `FoodSpawner.Spawned` がその役をしています。使う側ができたときに足します。

## テスト

テストは `Tests/` にあります（EditMode）。`TutorialRunnerTests` は、順に動かす決まり・例外・覚えたこと・合図を確かめます。`TutorialWaitTests` は、待ちの決まりを確かめます。Core の `Phases`・`Cues` は `Core/Tests/` にあります。
