# MTG の自動化（録音 → 文字起こし → Issue と議事録の更新）

MTG の録音から、議事録・Issue の片づけ・議事録の PR までを、PC の前にいなくても進める仕組みです。使い方と、仕組みの置き場所をまとめます。

```
水曜    ルーティン (mtg-agenda)   金曜 MTG の議題 Issue を作る
金曜    録音 → inbox に置く → run.ps1 を実行して帰る
          ├ transcribe.ps1  Whisper (turbo) で文字起こし
          ├ clean.py        幻覚・繰り返しの除去、corrections.tsv の置換
          └ Claude (mtg-sync)  校正 → 議事録コメント → Issue の片づけ → 議事録の PR
翌日    PR を見てマージする。議事録の「確認してほしいこと」を見る
```

## 事前準備（初回のみ）

- Windows、NVIDIA の GPU（VRAM 8GB 以上。`turbo` は約 6GB 使う）
- Python と [openai-whisper](https://github.com/openai/whisper)（`pip install -U openai-whisper`）、PyTorch の CUDA 版
- ffmpeg: `winget install Gyan.FFmpeg`
- GitHub CLI: `gh auth login`
- Claude Code: `claude` を一度起動してログインしておく

## 使い方

1. 録音（mp3, m4a, wav など）を `~\Documents\mtg\inbox\` に置く。**ファイルの更新日が MTG の日付**として使われる（コピーで更新日が変わるときは、MTG の日に置く）
2. PowerShell で、リポジトリの `tools\mtg\run.ps1` を実行する

   ```powershell
   .\tools\mtg\run.ps1
   ```
3. そのまま帰ってよい（実行中は PC がスリープしない。電源とふたは閉じない設定にしておく）
4. 結果は GitHub に出る（議題 Issue へのコメント、`docs/mtg-YYYYMMDD` の PR）。ログは `~\Documents\mtg\logs\`

文字起こしだけ試すときは `-NoClaude`。`run.ps1` は専用のクローンを `~\Documents\mtg\repo` に作って使うので、作業中のリポジトリ（Unity）には触れない。

## Claude が自動でやること・やらないこと

| やる | やらない |
|---|---|
| 議事録の Issue コメント、`docs/minutes/` と `decisions.md` の追記（PR） | PR のマージ |
| 決まったことの Issue へのコメント、`MTGで決める`・`MTGで共有` の付け外し | 文字起こしの全文の公開（ローカルの `transcripts/` にだけ残る） |
| 完了と言い切れる Issue のクローズ（理由と時刻つき） | 確信が持てない Issue のクローズ（「クローズ候補」として議事録に書く） |
| 新規 Issue（`Claude提案` ラベル付き） | |

迷って実行しなかったものは、議事録の「確認してほしいこと」に出る。**クローズが間違っていたら、Issue を reopen すればよい。**

## 文字起こしの精度

- `tools/mtg/glossary.txt`: Whisper に渡す用語辞書（短く保つ。約 224 トークンまで）。固有名詞が違って聞き取られたら足す
- `tools/mtg/corrections.tsv`: 「誤 → 正」の置換表。タブ区切り。Claude が、確信のある組を PR に追記する。PR で間違いを直す
- `tools/mtg/clean.py`: 「ご視聴ありがとうございました」「音楽」などの幻覚と、同じ行の繰り返しを取り除く（Whisper は無音や雑音の区間で、学習データの字幕を作り出す）

## 水曜のルーティン

Claude Code のルーティン（クラウド）が、毎週水曜に `mtg-agenda` スキルを実行して、金曜 MTG の議題 Issue を作る。設定は https://claude.ai/code/routines 。
