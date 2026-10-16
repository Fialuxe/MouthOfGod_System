---
name: merge-check
description: コンフリクトした PR を merge-doctor で調べ、原因と解消の案を PR に 1 回だけコメントする。GitHub Actions（Conflict check）からルーティンの API で呼ばれる。「PR のコンフリクトを調べてコメントして」と言われたときにも使う。
---

# merge-check

> この仕組みを直す前に、[仕組みの前提](../../../docs/automation-premise.md) を読む（補助線であって決まりではない。人の負荷を増やさない）。

人に聞かず、最後まで進める。**コンフリクトを解消しない。** ファイルの編集・コミット・push・rebase・merge・ラベルの変更をしない。することは、調べることと、PR に 1 回コメントすることだけ。

## 入力
プロンプトは `prs=<番号>:<目印>,<番号>:<目印>,…`（`.github/scripts/check_conflicts.py` が作る。目印は、ぶつかったファイルの組から作った 12 桁）。**5 件を超えていたら、先頭の 5 件だけ**扱う。手で呼ばれて入力が無いときは、`python .github/scripts/check_conflicts.py` と同じ手順で、ぶつかっている open の PR を探す（目印は `conflict_key` で作る）。

## 手順（PR ごと）
1. **まだ必要か確かめる**: `gh pr view N --json state,baseRefName,headRefOid`。`OPEN` でなければ飛ばす。`gh api repos/{owner}/{repo}/issues/N/comments --paginate` に `<!-- merge-doctor:<目印> -->` がすでにあれば、報告済みなので飛ばす
2. **調べる**: `merge-doctor` サブエージェントを呼ぶ（PR 番号だけを渡す）
3. merge-doctor が「コンフリクトなし」と返したら（呼ばれた後に解消された）、コメントせずに飛ばす
4. **コメントする**: `GH_TOKEN="${CLAUDE_BOT_GH_TOKEN:-$GH_TOKEN}" gh api repos/{owner}/{repo}/issues/N/comments -F body=@<一時ファイル>`（`gh pr comment` は GraphQL を使うため、ルーティンの環境で 403 になることがある。`CLAUDE_BOT_GH_TOKEN` は、Claude 専用のアカウントのトークンで、あれば使う）。本文は次の形。**最後の行に目印を必ず入れる**（無いと、次の push でまた呼ばれる）

```
### コンフリクトの調査（Claude）
<merge-doctor の報告の、この PR の部分をそのまま>

解消は人が行ってください。`.unity`・`.prefab` は手でマージせず、どちらかを採用します（CONTRIBUTING 3 章）。
<!-- merge-doctor:<目印> -->
```

merge-doctor の「人が決める点」が「なし」でなければ（例: prefab のどちらを採るか）、**別のコメント**で、`docs/mtg-context.md`「Claude からの質問」の形で聞く（1 点 1 コメント、`<!-- claude-question -->` つき）。急ぐものは PR を開いた人が答え、答えが無ければ水曜の議題に載る。

## 報告
扱った PR ごとに 1 行（番号・コメントした / 飛ばした理由）。公開リポジトリなので、トークンや個人情報を書かない。
