---
name: issue-hygiene
description: 週に 1 回、open の Issue を点検して、閉じられるものを閉じ、重複・担当なし・止まっているもの・題の分かりにくいものを 1 つの Issue にまとめる。定期実行のルーティンから呼ばれる。「Issue を整理して」と言われたときにも使う。
---

# issue-hygiene

open の Issue を点検して、(1) 根拠があって安全なものはその場で直し、(2) 人の判断が要るものは、1 つの Issue「Issue 整理（週次）」にまとめる。確認は取らず、最後まで実行する。**報告は短く。全体で 40 行以内。**

## 前提
- 統合・クローズ候補・担当の割り当て・マイルストーンの変更は、**自分では行わない**（提案だけ）。人が決める。例外は下の「その場で直すもの」だけ。
- 対象外: ラベル `sub-goal`（子が閉じると自動で閉じる）・`roadmap`・`activity`・`hygiene` の Issue、`optional`（任意）の Issue（重複と題だけ見る）。
- 担当の役割は Leading / Support / Follower（CONTRIBUTING）。担当「なし」は、Leading がいないこと。

## 手順

### 1. 材料を集める
- open の Issue 全部: `gh issue list --state open --limit 300 --json number,title,labels,milestone,assignees,updatedAt,body`
- 各 Issue の Blocked by: `gh api repos/{owner}/{repo}/issues/N/dependencies/blocked_by`
- マイルストーンの期限: `gh api repos/{owner}/{repo}/milestones`
- 直近 7 日に閉じた Issue と、マージした PR（`gh issue list --state closed --search "closed:>=YYYY-MM-DD"`、`gh pr list --state merged`）

### 2. その場で直すもの（根拠が事実で、元に戻せるものだけ）
1. **閉じる**: 「できたと言える状態」または「完了条件」のチェックボックスが**すべて `[x]`** で、先に終わっている必要がある Issue（Blocked by）がすべて閉じている Issue。コメントに根拠（どのチェックが済みか）を書いて、`completed` で閉じる。チェックが 1 つでも空なら閉じない。
2. **種類ラベルを付ける**: 種類ラベル（`enhancement`・`decision`・`documentation`・`making/hardware`・`assets`・`test`・`submission`・`logistics`）が 1 つも無い Issue に、題と本文から**明らか**なものを 1 つ付ける。迷うものは付けず、報告に回す。

### 3. 人の判断が要るもの（提案だけ。各項目 1 行）
最大件数を守る。当てはまるものが無い節は書かない。
1. **重複・統合の候補**（最大 5 組）: 題・本文・Blocked by（同じ親、同じ子）が重なる Issue の組。「#A と #B: 〇〇が重なる。#A に統合して #B を閉じる案」と書く。
2. **担当（Leading）がいない**（最大 8 件）: 期限が 14 日以内、または過ぎたマイルストーンの、open の作業・決めること。
3. **止まっている**（最大 8 件）: 最終更新が 14 日以上前で、期限が近い、または過ぎたマイルストーンの Issue。先に終わっている必要があるものが未完了なら、それを書く（動かせない理由）。
4. **題が分かりにくい**（最大 5 件）: 題が 50 文字を超える、または、何をするか（動詞）が分からない。題の案を 1 つ付ける。
5. **決める系で、方針が出て残りが作業**（最大 5 件）: `decision` の Issue のうち、最新のコメントに方針があり、未チェックが作業（図を作る、試す、書く）だけのもの。「閉じて、#N の『やること』に移す案」と書く（`mtg-sync` と同じ基準）。
6. **ラベル・マイルストーンの不備**（最大 5 件）: マイルストーンが無い、種類ラベルが迷う、`due-oct` と期限が食い違う。
7. **Blocked by の不整合**（最大 5 件）: 閉じた Issue や `deprecated` を指している、本文の「つながり」と設定が食い違っている。

### 4. 報告する Issue を更新する
- 題「Issue 整理（週次）」、ラベル `hygiene` の open な Issue を探す（`gh issue list --label hygiene --state open`）。無ければ、本文を書いて作る（担当は付けない）。
- **本文を、毎回、今回の内容で置き換える**（過去の分は残さない。履歴は Issue の編集履歴と、閉じた Issue に残る）。構成:
  1. 冒頭: 「この Issue は Claude が毎週更新します。M/D 更新。」と、数の一行（open N 件、今回その場で直した M 件）
  2. **その場で直したもの**: 閉じた Issue・付けたラベル（番号つき）
  3. **人の判断が要るもの**: 手順 3 の節を、そのまま。各行の頭に Issue 番号。どの人が決めるかは書かない（Leading を決めるのが最初のアクション）
- 同じ指摘が 3 週続いた Issue は、行の末尾に「（3 週目）」と付ける（優先度の目安）。

### 5. 報告する
実行したことを 3 行以内で出力する（その場で直した数、人の判断が要る数、更新した Issue の URL）。失敗した手順があれば、止まらずに続け、報告に書く。
