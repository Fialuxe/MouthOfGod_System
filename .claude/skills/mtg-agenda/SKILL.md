---
name: mtg-agenda
description: 次の MTG の議題 Issue を作る。MTG の 2 日前（水曜）に、定期実行のルーティンから呼ばれる。「次の MTG の議題を作って」と言われたときにも使う。
---

# mtg-agenda

次の MTG（既定: 次の金曜）の議題 Issue を 1 つ作る。確認は取らず、最後まで実行する。文字数は少なく、全体で 70 行以内。

## 手順

### 1. 日付を決める
MTG は毎週金曜。今日より後で最も近い金曜を `M/D` にする（指示があればそれに従う）。同じ日付の議題 Issue（題に `M/D MTG` を含む open な Issue）が既にあれば、**新しく作らず**、本文を更新して終わる。

### 2. 材料を集める
- `mtg-share`・`mtg-decide` が付いた open の Issue（`gh issue list`）
- 前回の MTG 以降に、閉じた Issue とマージした PR（`gh issue list --state closed --search "closed:>=YYYY-MM-DD"`、`gh pr list --state merged`）。前回の日付は、直近の `docs/minutes/*.md` のファイル名
- 直近の議事録 `docs/minutes/*.md` の「アクション」（旧「次にやる人」）と「未解決・次回」（旧「確認してほしいこと」）
- **アクションの Issue ごとの状況**（前回のアクションに書かれた Issue 番号それぞれについて）: `gh issue view N --json state,stateReason,closedAt,assignees,comments`（コメントは前回の MTG の日付以降のもの）と、その Issue を参照している PR（`gh pr list --state all --search "N in:body"`）
- マイルストーンの期限（`gh api repos/{owner}/{repo}/milestones`）。締切が 14 日以内のもの
- README「今の区切り」と、`roadmap` ラベルの Issue

### 3. Issue を作る
- 題: `M/D MTG の議題と議事録`
- ラベル: `mtg-share`、`claude-proposal`
- 本文（日本語。冒頭に「この Issue は Claude が下書きしました。足したいことは、MTG の前に書き足してください。」）:
  1. **共有すること**: 前回以降に終わったこと（Issue・PR を番号つきで）、見てほしいもの
  2. **決めること**: `mtg-decide` の Issue を、**締切の近い順**に表で（番号・決めること・なぜ今）
  3. **前回のアクションの結果**（Check。前回の「アクション」の表を、そのまま並べる）: 先頭に「N 件中 M 件完了」の 1 行。表の列は 誰が / 何を / 期限 / Issue / 結果。**結果は事実だけで判定する**:
     - ✅ 完了: Issue が `completed` で閉じている、またはそれを `Closes` する PR がマージ済み
     - ⏹ 取りやめ: Issue が `not_planned` で閉じている
     - 🔄 進行中: open で、前回の MTG 以降にコメント・PR・コミットがある
     - ⚠ 期限切れ: open で、期限（今日）を過ぎている。動きがあっても期限切れが優先
     - ⏳ 未着手: open で、期限前、動きがない
     - ❓ 判定できない: Issue 番号が書かれていない、期限が「未定」など。推測で ✅ にしない
     並び順は ⚠ → ⏳ → 🔄 → ✅・⏹。⚠ と ⏳ の行には、一言で「続ける／担当を替える／やめる」を MTG で決めてもらう、と添える。これを「決めること」の先頭に 1 行で繰り返さない（二重に書かない）
  4. **お知らせ**: あれば、流し見できる程度に
  5. 末尾に、CONTRIBUTING の「MTG が終わったら」の 4 手順は書かない（mtg-sync が行う）
- 何も材料がない節は省く。#183 や #186 などの既存の議題 Issue は変更しない。

### 4. 報告する
作った Issue の URL を 1 行で出力する。
