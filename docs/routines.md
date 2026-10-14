# Claude のルーティン（定期実行）の設定と引き継ぎ

自動化のうち、Claude が動くものは、claude.ai の **ルーティン**（https://claude.ai/code/routines）で動く。ルーティンは、**作ったアカウントに紐づく**（別のアカウントには引き継げない）。そのため、設定をここに書いておき、誰でも作り直せるようにする。

## いま動いているルーティン

| 名前 | いつ（日本時間） | 何をする | 指示文（そのまま貼る） |
|---|---|---|---|
| MTG の議題 | 毎週水曜 9:05（リポジトリは `MouthOfGod_System` と、カレンダーを読むために非公開の `MouthOfGod_transcripts`） | 金曜の MTG の議題 Issue を作る（前回のアクションの結果、決めることの候補つき） | `mtg-agenda スキルに従って、次の MTG（金曜）の議題 Issue を作ってください。確認は取らず、最後まで実行すること。` |
| 週次の Issue 整理 | 毎週火曜 8:47 | 完了した Issue を閉じ、重複・担当なし・止まっているものを「Issue 整理（週次）」にまとめる | `issue-hygiene スキルに従って、Fialuxe/MouthOfGod_System の open の Issue を整理してください。確認は取らず、最後まで実行すること。` |

| 議事録（クラウド） | 時刻なし（API で呼ぶ） | クラウドの文字起こしの後に呼ばれ、議事録の PR を作る | `mtg-sync スキルの「クラウドで動かすとき」に従って、渡された date と name の MTG の議事録を作ってください。確認は取らず、最後まで実行すること。` |
| 開発（dev-loop） | 1 日 2〜3 回（例: 10:13・15:13・21:13 JST）。最初は週 2 回から | PM が open の Issue から「Claude だけで実装できる小さな 1 件」を選び、engineer が実装し、reviewer が確かめて、PR にする（マージは人） | `dev-loop スキルに従って、open の Issue から 1 件を進めてください。確認は取らず、最後まで実行すること。` |
| コンフリクトの調査 | 時刻なし（API で呼ぶ） | GitHub Actions の「Conflict check」が、`main` が進んだとき・PR が更新されたときに open の PR を git で調べ、**新しくぶつかった PR があるときだけ**呼ぶ。merge-doctor が原因と解消の案を調べ、PR に 1 回だけコメントする（解消は人） | `merge-check スキルに従って、渡された prs の PR のコンフリクトを調べ、PR にコメントしてください。確認は取らず、最後まで実行すること。` |

- 「議事録（クラウド）」は、リポジトリを **`Fialuxe/MouthOfGod_System` と `Fialuxe/MouthOfGod_transcripts`（非公開）の 2 つ**付け、スケジュールは付けず、**API トリガー**を足す。トークンを生成したら、GitHub の Secrets `MTG_ROUTINE_URL`（`https://api.anthropic.com/v1/claude_code/routines/trig_…/fire`）と `MTG_ROUTINE_TOKEN`（`sk-ant-oat01-…`）に入れる。トークンは一度しか表示されない。
- 「開発（dev-loop）」は、リポジトリ `Fialuxe/MouthOfGod_System` を付ける。サブエージェントの定義は `.claude/agents/`（pm・engineer・reviewer）。**マージ・Issue のクローズはしない**（PR を作るまで）。1 回の実行で PR は 1 件まで。選ぶ条件は `.claude/agents/pm.md` にある（`docs/`・`tools/`・`.github/`・Unity の新規 `.cs`・`.asmdef` と、その `.meta` だけ。既存のシーン・プレハブ・`decision` などは除外）。
- 「コンフリクトの調査」は、リポジトリ `Fialuxe/MouthOfGod_System` を付け、スケジュールは付けず、**API トリガー**を足す。トークンを生成したら、GitHub の Secrets `CONFLICT_ROUTINE_URL`（`https://api.anthropic.com/v1/claude_code/routines/trig_…/fire`）と `CONFLICT_ROUTINE_TOKEN` に入れる。Secrets が無いあいだは、Actions が PR に警告を出すだけ（ルーティンは呼ばない）。1 回に渡す PR は 5 件まで。同じコンフリクト（同じベース・同じファイルの組）は、PR のコメントの目印 `<!-- merge-doctor:… -->` で二度報告しない
- 作ったアカウント: 代表者の個人の Claude アカウント（研究室のアカウントは、GitHub の連携ができなかったため、使っていない）
- 議事録づくり（`mtg-sync`）は、下の「議事録（クラウド）」のルーティンが動かす。ルーティンが動かないとき・未設定のときは、**PC の見張り**（`watch.ps1`）が引き継ぐ。詳しくは [mtg-automation.md](mtg-automation.md)

## スキルは、リポジトリにある

3 つのスキル（`mtg-agenda`・`mtg-sync`・`issue-hygiene`）は、リポジトリの `.claude/skills/` にある。ルーティンに**リポジトリを足せば、スキルは自動で読み込まれる**。ルーティンに入れるのは、上の指示文だけ。

## 作り直す手順（5 分）

1. claude.ai にログインして、GitHub を連携する: [claude.ai/connect-github](https://claude.ai/connect-github)。**GitHub には、リポジトリの持ち主（Fialuxe）でログインしておく**。Claude の GitHub アプリを、`MouthOfGod_System` に入れる（非公開の `MouthOfGod_transcripts` も使うなら、これも）
2. https://claude.ai/code/routines で、新しいルーティンを作る
3. **リポジトリに `Fialuxe/MouthOfGod_System` を足す**（ここを忘れると、スキルが読み込まれず、失敗する）
4. 上の表の「指示文」を貼り、「いつ」を設定する
5. 一度、手で実行して、結果（議題 Issue、「Issue 整理（週次）」）が出ることを確かめる
6. **古いアカウントのルーティンを、止めるか消す**（二重に動くと、議題の Issue が二重にできる）

## 注意

- **ルーティンを、外部のツール（API など）から作ると、リポジトリが設定されない**ことがある。その場合は、画面で、リポジトリを足す
- ルーティンは、作った人の Claude の使用量を使う。文字起こしの議事録づくり（長い文章を読む）は、使用量が多いので、使用量の上限を確認する
- 録音や文字起こしは、研究室の内部の会議の内容。Claude（外部のサービス）に渡してよいかは、研究室のルールを確認する

## 引き継ぐとき（代表者が研究室を離れるとき）

1. 次の人が、上の手順で、自分のアカウントにルーティンを作る
2. 動いたことを確かめてから、前の人のルーティンを消す
3. この文書の「作ったアカウント」を書き換える
