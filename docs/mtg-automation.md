# MTG の自動化（録音 → 文字起こし → Issue と議事録の更新）

MTG の録音から、議事録・Issue の片づけ・議事録の PR までを、PC の前にいなくても進める仕組みです。使い方と、仕組みの置き場所をまとめます。

```
水曜    ルーティン (mtg-agenda)   金曜 MTG の議題 Issue を作る
金曜    録音 → inbox に置く → run.ps1 を実行して帰る
          ├ transcribe.ps1  音量の正規化 → faster-whisper (large-v3) で文字起こし
          ├ clean.py        幻覚・繰り返しの除去、corrections.tsv の置換
          └ Claude (mtg-sync)  校正 → 議事録コメント → Issue の片づけ → 議事録の PR
翌日    PR を見てマージする。議事録の「アクション」「Issue にしたほうがよさそうなもの」「未解決・次回」を見る
```

## 事前準備（初回のみ）: セットアップのスクリプト

録音を処理する PC（Windows、NVIDIA の GPU）は、**スクリプト 1 本でそろえられる**。足りないものだけを入れる（何度実行してもよい）。**この PC は、誰のものでもよい。2 台以上で用意しておくと、1 台が動かなくても止まらない**（属人化を避ける）。

```powershell
# PowerShell で、リポジトリのフォルダから
.\tools\mtg\setup-pc.ps1 -Inbox "<Google ドライブの mtg-inbox のフォルダ>" -Real
.\tools\mtg\setup-pc.ps1 -CheckOnly      # 何も入れずに、足りないものだけを表で見る
```

入れる・確かめるもの: Git、Python、ffmpeg、GitHub CLI（`gh auth login` まで）、NVIDIA GPU、faster-whisper（と CUDA のライブラリ）、Claude Code、非公開リポジトリのクローン、電源（スリープしない）、Chrome リモート デスクトップのサービス、フォルダを見張るタスク（`register-watch-task.ps1`）。`-Real` を付けると、見張るタスクが本番（Issue・ラベル・PR まで）になる。付けないと試運転。`-Test` で GPU の小さな動作確認、`-SkipPower` `-SkipClaude` `-SkipWatch` で省ける。

あとで手でやること: Google ドライブのデスクトップ版にログインし、`mtg-inbox` を「ミラーリング」にする。`claude` を一度起動してログインする。

以下は、スクリプトが入れるものの内訳（手で入れたいとき用）。

- Windows、NVIDIA の GPU（VRAM 8GB 以上で、large-v3 が動く）
- Python と [faster-whisper](https://github.com/SYSTRAN/faster-whisper)（`pip install faster-whisper`）。GPU 用の CUDA ライブラリが要る（`nvidia-smi` が通れば、多くは追加の設定なしで動く）
- ffmpeg: `winget install Gyan.FFmpeg`
- GitHub CLI: `gh auth login`
- Claude Code: `claude` を一度起動してログインしておく

## 作業フォルダと設定の置き場所

作業フォルダ（`run.ps1` の `-Root`）の既定は、**非公開リポジトリの手元のクローン `~\Github\MouthOfGod_transcripts`**。初回に無ければ、`run.ps1` が自動でクローンする（`gh` のログインと、リポジトリへの招待が要る）。

- `config\`: 人名などの設定（下の 3 ファイル）。**非公開リポジトリに入る**ので、チームで共有され、履歴も残る
- `YYYY-MM-DD\`: 文字起こし。`run.ps1` が自動で push する
- `inbox\`・`transcripts\`・`logs\`・`repo\`・`work\`: 作業用（録音・途中の出力・専用クローン）。`.gitignore` で除外される

## 録音は誰がしてもよい（スマホでよい）

録音する人は、スマホの録音アプリで録り、できたファイルを同期フォルダ（下の `mtg-inbox`）に入れるだけでよい（Google ドライブのアプリで「アップロード」）。処理は、同期フォルダを見ている PC が自動で行う。

- **ファイル名に MTG の日付を入れる**（例: `2026-10-16 MTG.m4a`、`20261016.m4a`）。日付がファイル名にあれば、それが MTG の日付になる。無ければ、ファイルの更新日が使われる（あとからアップロードすると、日付がずれることがある）
- 声が小さいと誤認識が増える。スマホは話す人の近くに置く

## 同期フォルダに録音を入れるだけで処理する（家からの運用）

録音の置き場所（`inbox`）を、Google ドライブなどの同期フォルダにして、**そこへ録音を入れるだけで、自動で処理を始める**ことができる。PC の前や、リモート接続は要らない（PC は起動していて、ネットにつながっている必要がある）。

1. 同期ツール（Google Drive for desktop など）を入れ、「ミラーリング」（ファイルを PC にも保存する）にして、フォルダ（例: `mtg-inbox`）を作る
2. 一度だけ、登録する: `.\tools\mtg\register-watch-task.ps1 -Inbox "<そのフォルダ>"`（10 分おきに見に行く）
3. 録音をそのフォルダに入れる。同期中のファイルは、サイズが変わらなくなってから処理される
4. 結果は、同じフォルダの `results\` に出る（提案書 `*.proposal.md` と実行ログ）。GitHub への反映は、モードによる

**モード**: 既定は**試運転**（GitHub には何も書き込まず、提案書だけ）。`-Real` を付けて登録すると、**本番**（Issue・ラベル・PR まで）を自動で行う。やめるときは `-Remove`。

## Mac で使う（Apple Silicon、GPU なし）

`tools/mtg/setup-mac.sh`（zsh）で、環境（Homebrew 経由の git・ffmpeg・gh・Python と、仮想環境 `~/.mtg-venv` の faster-whisper）を確かめて入れ、文字起こしの動作を確かめられる。GPU が無いので、`transcribe.py` は自動で CPU（int8）に切り替わる（`--device auto`）。

- `--check-only`: 何も入れずに確認する / `--test`: 3 秒の音声で動作確認（小さな `tiny` モデルを初回にダウンロード）/ `--transcribe FILE --model medium`: 1 本を文字起こしする
- **large-v3 は、CPU ではかなり遅い**（録音の長さの数倍かかる見込み。未計測）。急ぐときは `--model medium` か `small` にする（精度は下がる）
- フォルダの見張りと議事録の生成（`watch.ps1`・`run.ps1`）は Windows のみ。Mac は、文字起こしの確認や、予備として手で文字起こしするために使う

## 複数の PC で見張る（1 台が動かなくても止まらない）

同じ同期フォルダを、2 台以上の PC で見張ってよい（どの PC も `setup-pc.ps1` で用意できる）。処理が重ならないように、PC どうしは、フォルダの中の `.mtg-processing.claim` というファイルで譲り合う。

- 処理を始める PC が、自分の PC 名と時刻を書いた claim を置く。ほかの PC は、新しい claim（3 時間以内）が他の PC のものである間は、何もしない
- 同期の遅れで同時に置いてしまったときは、90 秒待って読み直し、最後に残った claim の PC だけが進める
- claim が 3 時間より古いときは、その PC が止まったとみなして、ほかの PC が引き継ぐ（フェイルオーバー）
- 処理が終わると、claim は消える
- 文字起こしの保管先の非公開リポジトリは共通なので、どの PC で処理しても、結果は同じ場所に出る

## GUI で使う

`tools\mtg\mtg-gui.cmd`（ダブルクリック）で、小さなウィンドウが開く（Python だけで動く）。

1. 「録音を追加…」で録音を選ぶ（`inbox` にコピーされる。更新日は保たれる）
2. **試運転**（既定でオン）: GitHub にも、リポジトリにも書き込まず、提案書だけを作る。本番にするときは、チェックを外して「実行」（確認ダイアログが出る）
3. 進み具合はウィンドウに出る。終わったら「提案書（最新）を開く」「文字起こしフォルダ」「ログフォルダ」
4. 「環境チェック」で、ffmpeg・faster-whisper・GPU・git・claude・gh・非公開リポジトリへのアクセスを確かめられる

## 使い方

1. 録音（mp3, m4a, wav など）を `~\Github\MouthOfGod_transcripts\inbox\` に置く。**ファイルの更新日が MTG の日付**として使われる（コピーで更新日が変わるときは、MTG の日に置く）
2. PowerShell で、リポジトリの `tools\mtg\run.ps1` を実行する

   ```powershell
   .\tools\mtg\run.ps1
   ```
3. そのまま帰ってよい（実行中は PC がスリープしない。電源とふたは閉じない設定にしておく）
4. 結果は GitHub に出る（議題 Issue へのコメント、`docs/mtg-YYYYMMDD` の PR）。ログは `~\Github\MouthOfGod_transcripts\logs\`

文字起こしだけ試すときは `-NoClaude`。`run.ps1` は専用のクローンを `~\Github\MouthOfGod_transcripts\repo` に作って使うので、作業中のリポジトリ（Unity）には触れない。

## 文字起こしの保管

文字起こし（`clean.txt`・`clean.srt`）は、**非公開リポジトリ** [MouthOfGod_transcripts](https://github.com/Fialuxe/MouthOfGod_transcripts) に `YYYY-MM-DD/` ごとに保存される（`run.ps1` が `archive.ps1` で push する）。公開リポジトリ（このリポジトリ）には、文字起こしの全文を入れない。見るにはそのリポジトリへのアクセス権が要るので、チームの人は招待する。保存先が非公開でなければ、`archive.ps1` は何もせず止まる。

## Claude が自動でやること・やらないこと

| やる | やらない |
|---|---|
| 議事録の Issue コメント、`docs/minutes/` と `decisions.md` の追記（PR） | PR のマージ |
| 決まったことの Issue へのコメント、`mtg-decide`・`mtg-share` の付け外し | 文字起こしの全文の公開（ローカルの `transcripts/` にだけ残る） |
| 完了と言い切れる Issue のクローズ（理由と時刻つき） | 根拠が発言にない（推測が要る）Issue のクローズ（「未解決・次回」に、何が分かれば閉じられるかを書く） |
| 新規 Issue（`claude-proposal` ラベル付き） | |

閉じられる Issue は、その場で閉じる（「閉じた Issue」に出る）。根拠がなくて閉じなかったものは「未解決・次回」、Issue にしたほうがよさそうな話は「Issue にしたほうがよさそうなもの」に出る。**クローズが間違っていたら、Issue を reopen すればよい。**

## 文字起こしの精度

- `tools/mtg/glossary.txt`: Whisper に渡す用語辞書（短く保つ。約 224 トークンまで）。固有名詞が違って聞き取られたら足す
- `tools/mtg/corrections.tsv`: 「誤 → 正」の置換表。タブ区切り。Claude が、確信のある組を PR に追記する。PR で間違いを直す
- `~\Github\MouthOfGod_transcripts\config\glossary.local.txt`（任意）: メンバーの名前など、**公開リポジトリに入れたくない語**を 1 行 1 語で書く。文字起こしの辞書として使われる（非公開リポジトリには入る。公開リポジトリには入らない）
- `~\Github\MouthOfGod_transcripts\config\members.local.tsv`（任意）: 名前と GitHub ID の対応（`名前<タブ>GitHub ID`、1 行に 1 人）。担当者（Assignee）を付けるのに使う。無ければ付けない。非公開リポジトリには入る（公開リポジトリには入らない）
- `~\Github\MouthOfGod_transcripts\config\corrections.local.tsv`（任意）: 人名の聞き間違いの置換表（`誤<タブ>正`）。`clean.py` が使う。非公開リポジトリには入る（公開リポジトリには入らない）
- 行頭の `?`（`clean.txt`）: Whisper 自身が自信を持てなかった行。Claude の校正で重点的に直される。自信を持った誤認識（例: 「時給」→「地球」）は `?` が付かないので、校正は全体を読む
- `tools/mtg/clean.py`: 「ご視聴ありがとうございました」「音楽」などの幻覚と、同じ行の繰り返しを取り除く（Whisper は無音や雑音の区間で、学習データの字幕を作り出す）

## 水曜のルーティン

Claude Code のルーティン（クラウド）が、毎週水曜に `mtg-agenda` スキルを実行して、金曜 MTG の議題 Issue を作る。設定は https://claude.ai/code/routines 。

## モデルと前処理を選んだ理由

同じ 3 分間（声が小さい録音）で比べた結果、**large-v3（faster-whisper）+ 音量の正規化**が一番誤認識が少なかった（turbo、kotoba-whisper v2.0 とも比較）。

- 音量の正規化が一番効く。**短い区間ごとに音量をそろえる**（`dynaudnorm`）。録音全体を一度に正規化するだけだと、小さい声の区間が小さいまま残り、発言が落ちた
- kotoba-whisper は速い（約 7 倍）が、この録音では文がくずれ、VAD を入れると発言が抜けた
- VAD と Whisper の幻覚ガードは切っている（小さい声を捨てるため）。代わりに `clean.py` が幻覚（「ご視聴ありがとうございました」など）を除く
- 速度の目安: RTX 5060 Laptop（8GB）で、43 分の録音が数分
