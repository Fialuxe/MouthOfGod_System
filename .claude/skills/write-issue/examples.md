# 例

## 良い例（このリポジトリの #356）

題: エンディングを作る

```markdown
今は、本編が終わると体験がそのまま止まります。体験者は、HMD を外してよいのか分かりません。

巫女のセリフは、チュートリアルと同じ `MikoLineBook` と合図の仕組みで足せます。動画は、プロローグの `PrologueVideo` で流せます。

メモ: どんな終わり方がよさそうでしょうか？ 巫女が 2〜3 行話して終える形が、いちばん簡単です。動画にする、村の全体を引きで見せる、などの形もあります。終わりの印象をどうしたいかで、選び方が変わりそうです。
```

## 良い例（Bevy #26584）

題: Remove full_ui testbed

```markdown
`full_ui` continues to get updates from various code migrations, but its really minimal and `ui` is where all active development happens. We've added a lot of scenes to `ui` since and nothing tested in `full_ui` is missing there.

We should remove `full_ui` to reduce refactoring churn and make it clear where the actual ui testbed is.
```

## 悪い例（以前のこのリポジトリの書き方）

題: 【小ゴール】#141 のために、プロローグとエンディングを神様の演出で見られ、通してつながるようにする

```markdown
> 🤖 この Issue は Claude（AI）が下書きしました。おかしな所があれば、直すか閉じてください。

## 何をするか
...
## なぜやるか
...
## やること
- [ ] ...
## できたと言える状態
- [ ] ...
```

この例には、次の問題があります。

- 先頭に断り書きがあります。
- 見出しが多く、何をするかが本文に埋もれています。
- チェックボックスで、Issue が作業の一覧になっています。
- 題に飾りの括弧があり、別の Issue に寄りかかっています。
