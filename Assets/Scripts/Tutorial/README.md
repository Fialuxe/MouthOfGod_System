# Tutorial

チュートリアル 1〜4 の進行。**チュートリアルごとに、コルーチンで流れを書く「人形劇スクリプト」を 1 つ**書き、`TutorialRunner` が順に動かす。見せ方は書かない。合図を出すだけで、見せ方は、合図を受けたフィールドや UI が決める。

> 状態: 案（#50）。枠組みだけで、T1〜T4 の中身はまだない（フォークの判定 #40 など、待つ相手が未完成のため）。Unity では未確認（コンパイルと EditMode のテストは、Unity の代わりの簡単な土台で確かめただけ。`Begin`・`Stop` は未確認）。決めた経緯と、採らなかった案は [docs/decisions.md](../../../docs/decisions.md)（2026-10-19）。

参照するのは Core だけ。

## 部品

| 部品 | 役割 |
|---|---|
| `TutorialScript` | 人形劇スクリプトの土台。継いだクラスを、チュートリアルごとに 1 つ書く（`T1.cs` …）。`Phase`（場面の名前）と `Play()`（流れ。コルーチン）を書く |
| `TutorialRunner` | 子の `TutorialScript` を Hierarchy の並び順に 1 つずつ動かす。無効にしたスクリプトは飛ばす（特定のチュートリアルだけを試せる）。例外が出たら、そのスクリプトだけをやめて次へ進む。`Begin()` で始め、`Stop()` でやめる |
| `TutorialMemory` | チュートリアルのあいだで引き継ぐ「覚えたこと」。名前だけ。`Has(名前)`・`Set(名前)` |
| `DeliveryWait` / `ConfirmWait` / `AnyWait` | `yield return` で待つもの。物の届き、デバイスでの決定、どれか早いほう。時間切れも付けられる |

## 人形劇スクリプトの書き方

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

- 書くのは、合図（`Cue`）、待つ条件（`WaitFor…`）、分岐だけ。フィールドの中身は知らない。
- そのチュートリアルにしかない物（浮かぶお供え物、影など）は、このクラスの `[SerializeField]` に持たせてよい。Prefab は `Assets/Resources/Prefabs/Tutorial/` に置く。
- 場面の名前（`Phase`）は、番号ではなく、やることを表す名前にする。一覧は [docs/progress-keys.md](../../../docs/progress-keys.md)。
- 始めるのは `TutorialRunner.Begin()`。いつ呼ぶか（台の前に立ったとき、など）は、呼ぶ側が決める。全部終わると `Finished` が出て、場面は空に戻る。次の場面（本編）は、`Finished` を受けた側が決める。

## フィールド・UI 側（合図を受ける）

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

合図の名前は、フィールド名から始める（`sea.glow`、`village.cheer`）。

## まだないもの

- T1〜T4 の中身。待つ相手が未完成: 雲を刺す・口に含む判定（#40）、食べ物が生まれた知らせを受ける Core の置き場（`FoodSpawner.Spawned` は Food の中にある）。使う側ができたときに足す。
- `Begin`・`Stop` の Unity での確認。

## テスト

`Tests/`（EditMode）。順に動かす決まり・例外・覚えたこと・合図（`TutorialRunnerTests`）と、待ちの決まり（`TutorialWaitTests`）。Core の `Phases`・`Cues` は `Core/Tests/`。
