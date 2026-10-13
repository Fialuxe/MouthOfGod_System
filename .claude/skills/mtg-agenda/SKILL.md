---
name: mtg-agenda
description: 次の MTG の議題 Issue を作る。MTG の 2 日前（水曜）に、定期実行のルーティンから呼ばれる。「次の MTG の議題を作って」と言われたときにも使う。
---

# mtg-agenda

次の MTG（既定: 次の金曜）の議題 Issue を 1 つ作る。確認は取らず、最後まで実行する。文字数は少なく、全体で 80 行以内。

## 手順

### 1. 日付を決める
MTG は毎週金曜。今日より後で最も近い金曜を `M/D` にする（指示があればそれに従う）。同じ日付の議題 Issue（題に `M/D MTG` を含む open な Issue）が既にあれば、**新しく作らず**、本文を更新して終わる。

### 2. 材料を集める
- `mtg-share`・`mtg-decide` が付いた open の Issue（`gh issue list`）
- 前回の MTG 以降に、閉じた Issue とマージした PR（`gh issue list --state closed --search "closed:>=YYYY-MM-DD"`、`gh pr list --state merged`）。前回の日付は、直近の `docs/minutes/*.md` のファイル名
- 直近の議事録 `docs/minutes/*.md` の「アクション」（旧「次にやる人」）、「未解決・次回」（旧「確認してほしいこと」）、「Issue にしたほうがよさそうなもの」
- **アクションの Issue ごとの状況**（前回のアクションに書かれた Issue 番号それぞれについて）: `gh issue view N --json state,stateReason,closedAt,assignees,comments`（コメントは前回の MTG の日付以降のもの）と、その Issue を参照している PR（`gh pr list --state all --search "N in:body"`）
- マイルストーンの期限（`gh api repos/{owner}/{repo}/milestones`）。締切が 14 日以内のもの
- README「今の区切り」と、`roadmap` ラベルの Issue

### 3. Issue を作る
- 題: `M/D MTG の議題と議事録`
- ラベル: `mtg-share`、`claude-proposal`
- 担当（Assignee）: **全員**（`gh api repos/{owner}/{repo}/assignees --jq '.[].login'` の全員。議題 Issue と議事録は、全員で見るもので、Leading・Support は置かない）
- 本文（日本語。冒頭に「この Issue は Claude が下書きしました。足したいことは、MTG の前に書き足してください。」）:
  1. **共有すること**: 前回以降に終わったこと（Issue・PR を番号つきで）、見てほしいもの
  2. **決めること**: `mtg-decide` の Issue を、**締切の近い順**に表で（番号・決めること・なぜ今）
  3. **前回のアクションの結果**（Check。前回の「アクション」の表を、そのまま並べる）: 先頭に「N 件中 M 件完了」の 1 行。表の列は Leading / Support / 何を / 期限 / Issue / 結果。**結果は事実だけで判定する**:
     - ✅ 完了: Issue が `completed` で閉じている、またはそれを `Closes` する PR がマージ済み
     - ⏹ 取りやめ: Issue が `not_planned` で閉じている
     - 🔄 進行中: open で、前回の MTG 以降にコメント・PR・コミットがある
     - ⚠ 期限切れ: open で、期限（今日）を過ぎている。動きがあっても期限切れが優先
     - ⏳ 未着手: open で、期限前、動きがない
     - ❓ 判定できない: Issue 番号が書かれていない、期限が「未定」など。推測で ✅ にしない
     並び順は ⚠ → ⏳ → 🔄 → ✅・⏹。⚠ と ⏳ の行には、一言で「続ける／担当を替える／やめる」を MTG で決めてもらう、と添える。これを「決めること」の先頭に 1 行で繰り返さない（二重に書かない）
  4. **決めることの候補**（下書き。人が膨らませるための土台。最大 5 件）: まだ `mtg-decide` が付いていない話のうち、次の MTG で決めたほうがよいもの。種類は、(a) 前回の「未解決・次回」と「Issue にしたほうがよさそうなもの」、(b) 担当が「未定」のもの、(c) ⚠ 期限切れで、続けるかやめるかが分かれるもの、(d) 期限が 14 日以内のマイルストーンを止めている、答えの出ていない問い（Issue の「残る問い」「決めること」の未チェック項目）。1 件を次の形で、3 行以内で書く:
     - **何を決めるか**（「〜を決める」の形。Issue 番号があれば付ける）
     - **選択肢**（2〜3 個。根拠のある案だけ。無理に作らない）と、**Claude の仮の推し**（あれば 1 つ。根拠の Issue・発言つき）
     - **担当案**（Leading 候補。その Issue や関連 Issue で動いている人など、根拠があるときだけ。無ければ書かない）
     決めるのは人。ここに書いたものを、そのまま `mtg-decide` にはしない（MTG の前に、人が足し引きして、決めるものに `mtg-decide` を付ける）。
  5. **お知らせ**: あれば、流し見できる程度に
  6. 末尾に、CONTRIBUTING の「MTG が終わったら」の 4 手順は書かない（mtg-sync が行う）
- 何も材料がない節は省く。#183 や #186 などの既存の議題 Issue は変更しない。

### 4. 報告する
作った Issue の URL を 1 行で出力する。
