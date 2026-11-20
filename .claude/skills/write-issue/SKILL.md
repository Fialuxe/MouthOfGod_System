---
name: write-issue
description: このリポジトリに GitHub の Issue を作る・題や本文を直す。題は作業を動詞で書き、本文は短い文章とコードの場所だけにする。「Issue を作って」「Issue にして」「タスクを Issue に起こして」と言われたとき、ほかのスキルやエージェントが Issue を作るときに使う。
---

# write-issue

このリポジトリの Issue は、OSS の Issue のように素っ気なく書きます。手本は Bevy と Misskey の Issue です。どちらも、題で作業が分かり、本文は数行の文章とコードの場所だけです。

Issue を作る前に `.claude/hooks/issue-check/` のフックが本文を確かめます。決まりを破ると、`gh issue create` が止まります。

## 手順

1. **事実を集める。** 関わるコードを読み、ファイルと行を確かめます。Issue の中身が、今のコードで済んでいないかも確かめます。済んでいれば、Issue にしません。
2. **似た Issue を探す。** `gh issue list --state open` で、同じ作業の Issue がないか見ます。あれば、新しく作らずにそちらにコメントします。
3. **下書きを書く。** 一時フォルダに、題と本文を別々のファイルで書きます。書き方は下の「決まり」と `rules/` に従います。
4. **人に見せる。** 人に頼まれて作るときは、投稿する前に下書きを見せます。定期実行から作るときは、見せずに投稿してよいです。
5. **投稿する。** `gh issue create --title "<題>" --body-file <本文のファイル> --label claude-proposal` で作ります。マイルストーンがあれば `--milestone` で付けます。何件作るときも、1 件ずつ、ファイルのパスを直接書いたコマンドで作ります。ループや変数でパスを組み立てると、フックが本文を読めず、確かめずに通してしまうからです。
6. **報告する。** 作った Issue の番号と題を、1 行ずつ返します。

## 決まり

| 決まり | ファイル |
|---|---|
| 題は、作業を動詞で終える 1 文にする | `rules/title.md` |
| 本文は、見出しなしの短い文章と、コードの場所だけにする | `rules/body.md` |
| 作り方が 1 つに決まらない所は「メモ:」で書き、問いかけで終える | `rules/memo.md` |
| 断り書き・ラベル・マイルストーン・人の名前の扱い | `rules/meta.md` |

良い例と悪い例は `examples.md` にあります。

## 元にしたもの

構成は、tartinerlabs/skills の `github-issues`（MIT ライセンス）を元にしました。https://github.com/tartinerlabs/skills/tree/main/plugins/workflow/skills/github-issues

チェックボックスを使わない決まりと、題を自然な言葉で書く決まりは、そこから取りました。「Agent brief」の節は取り入れていません。このプロジェクトでは、やることや完了条件の節を Issue に書かないからです。
