# 録音の置き方（議題 Issue に毎回入れる文）

`mtg-agenda` が、議題 Issue の冒頭に、下のコードブロックの中身をそのまま入れる。中身を変えるときは、事実（`docs/mtg-automation.md`、`.github/workflows/transcribe-cloud.yml`）と合っているか確かめる。

```markdown
> 🎙 **MTG の録音をお願いします**（スマホでよい。終わったら [mtg-inbox](https://drive.google.com/drive/folders/1PBN9Ckx2SGr4Tx_-3p87E8MYO3VkB2Xi?usp=drive_link) に置く）

<details><summary>録音の置き方と、そのあと起こること</summary>

- **置く場所**: Google ドライブの [mtg-inbox](https://drive.google.com/drive/folders/1PBN9Ckx2SGr4Tx_-3p87E8MYO3VkB2Xi?usp=drive_link) の**いちばん上**。中の `processing`・`done`・`failed` は自動で使うので、触らない

- **ファイル名**: MTG の日付を入れる（例: `2026-10-16 MTG.m4a`）。無いと、置いた日が MTG の日になる

- **形式**: m4a・mp3・wav など。4 時間まで（長いときは 2 時間くらいに分けて置く）

- **そのあと**（人は何もしなくてよい）:

  1. 数時間以内に、録音が `processing` に移る。文字起こしが始まった印（GitHub が 30 分おきに見に行くが、混んでいると遅れる）

  2. 文字起こしが終わると、`done` に移る（録音の長さの数倍かかる）

  3. 続けて Claude が、この Issue に議事録をコメントし、各 Issue を更新し、議事録の PR を作る。PR を見てマージする

  4. うまくいかないと、録音は `mtg-inbox` に戻り、やり直す。3 回失敗すると `failed` に移り、代表者に知らせが届く

</details>
```
