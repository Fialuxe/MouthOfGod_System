# MTG の自動化（録音 → 文字起こし → Issue と議事録の更新）

> 全体の仕組みと図は [docs/system-overview.md](system-overview.md) にまとめてある。ここは、設定と使い方の詳細。

MTG の録音から、議事録・Issue の片づけ・議事録の PR までを、PC の前にいなくても進める仕組みです。使い方と、仕組みの置き場所をまとめます。

```
水曜    ルーティン (mtg-agenda)   金曜 MTG の議題 Issue を作る
MTG 前  余裕があれば各自          議題 Issue に、話したいことをひとことコメント
金曜    MTG                       議題を上から話して、その場で決める。録音する
金曜    録音 → Google ドライブの mtg-inbox に置く（あとはクラウドが進める。PC の run.ps1 は予備）
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

## クラウドで文字起こしする（GitHub Actions、PC が無くても進む）

録音を Google ドライブの `mtg-inbox` に入れるだけで、GitHub Actions（CPU、large-v3）が文字起こしして、非公開リポジトリ `MouthOfGod_transcripts` に保存する。**遅くてよい**（1 ジョブ 6 時間まで。公開リポジトリの標準ランナーは無料）。ワークフローは `.github/workflows/transcribe-cloud.yml`、本体は `tools/mtg/cloud/transcribe_cloud.py`。

**流れ**: 30 分おきに inbox を見る → 録音を `processing/` に移す（取り合いを防ぐ）→ 音量をそろえる → large-v3 で文字起こし → `clean.py` → 非公開リポジトリの `YYYY-MM-DD/` に保存 → `done/` に移す。失敗したら inbox に戻す（次の回でやり直す）。MTG の日付は、ファイル名の日付があればそれ、無ければ Drive の更新日（日本時間）。

**公開リポジトリなので**: ログに、文字起こしの中身・ファイル名・Drive の ID を出さない（日付、件数、時間だけ）。成果物（artifact）はアップロードしない。Secrets は、このリポジトリの Settings → Secrets and variables → Actions に置く。

### 初回の設定（あなた、または管理者が 1 回だけ）
1. **Google Cloud でサービスアカウントを作る**: プロジェクトを作り、「Google Drive API」を有効にし、サービスアカウントを作って、JSON のキーを発行する
2. **Drive のフォルダ `mtg-inbox` を、そのサービスアカウントのメールアドレスと共有する**（権限は「編集者」。録音を `processing/` `done/` に移すため）
3. **フォルダの ID** を控える（フォルダを開いた URL の最後の部分）
4. **GitHub の Secrets を 3 つ置く**:
   - `GDRIVE_SA_JSON`: 手順 1 の JSON の中身
   - `MTG_INBOX_FOLDER_ID`: 手順 3 のフォルダ ID
   - `TRANSCRIPTS_PAT`: 非公開リポジトリ `MouthOfGod_transcripts` だけに、Contents の読み書きを許した Fine-grained personal access token
5. Secrets が無いうちは、ワークフローは何もせずに終わる（エラーにならない）

### 最初にやる: 速さの計測
Actions の「MTG transcribe (cloud)」→ Run workflow → `benchmark_seconds` に `300`（5 分）を入れる。inbox か done にある録音の先頭 5 分を文字起こしして、「録音の長さの何倍の時間がかかったか」だけをログに出す（中身は出ない）。**1 時間の録音が 6 時間に収まるか**（倍率が 5 未満なら安全）をここで判断する。収まらないなら、録音を分割する仕組みを足す。

### 失敗したとき（堅牢にするための決まり）
- **同じ録音が失敗しても、30 分ごとに無限には繰り返さない。** 失敗の回数を、録音のファイル（Drive の `appProperties` の `mtg_attempts`）に数える。**3 回失敗すると、`failed/` に移して、もう試さない**（ジョブも失敗にして、メールで人に知らせる）。原因を直したら、`failed/` から `inbox/` に戻せば、また試す（失敗の回数は、0 から数え直す）。
- **処理できない録音は、すぐ `failed/` に移す**: 4 時間より長い（6 時間のジョブに収まらない）、音声として読めない（0 分）。長い録音は、2 時間くらいに分けて入れ直す。
- **PC が処理中のときは、クラウドは何もしない**: PC の見張りが、フォルダに `.mtg-processing.claim`（3 時間以内）を置いている間は、クラウドは手を出さない。
- 文字起こしを非公開リポジトリに保存するとき、`YYYY-MM-DD/<名前>.minutes.pending` という印も置く（次の節）。

### クラウドで処理した録音の議事録（ルーティンが作る。PC は予備）
GitHub の Secrets `MTG_ROUTINE_URL`・`MTG_ROUTINE_TOKEN`（Claude のルーティンの API トリガー）があると、クラウドの文字起こしが保存を終えたあと、ルーティンを API で呼ぶ（`date=… name=…` を渡す。呼べなくても文字起こしは止まらない）。ルーティンが `mtg-sync` を動かし、議事録の PR を作り、印を `.minutes.done` にする。印には `"route":"cloud"` が付き、**PC の見張りは、作成から 6 時間は手を出さない**。6 時間たっても印が残っていたら、PC が引き継ぐ。議事録がすでに `main` や `claude/mtg-<日付>` ブランチにあれば、PC は作らずに印を `.minutes.done` にする。Secrets が無いときは、従来どおり PC だけが作る（`"route":"pc"`）。

以下は PC が作る場合（予備）の説明。

PC が議事録を作るのは、クラウドの自動処理が動かないときだけです。クラウドが文字起こしを保存すると、`<名前>.minutes.pending` の印が残ります。PC の見張りは、この印がある文字起こしから、議事録だけを作ります。録音と文字起こしは、やり直しません。**見張り（`watch.ps1`）は、10 分おきに非公開リポジトリを更新して、印を探します**。使うコマンドは `run.ps1 -FromTranscript <文字起こしのパス>` です。

- 成功すると、印は `.minutes.done` に変わる（コミットして push する）。失敗すると、`attempts` を増やして残し、次の回にやり直す。**3 回失敗すると `.minutes.failed` になり、もう試さない**（直したら、このファイルを消して `.minutes.pending` を作り直す）
- 試運転（`-Real` なし）では、提案書（`*.proposal.md`）を作るだけで、印は変えない（同じ文字起こしでは、1 回だけ作る）
- 印の無い古い文字起こしは、対象にならない（昔の分を、もう一度処理しない）
- PC が 1 台も動かない間は、印がたまる。PC が戻ったとき、古い順に処理される

### PC の見張りとの関係
Windows の見張り（`watch.ps1`）は、`processing/` に録音がある間は何もしない（クラウドが処理中）。クラウドと PC は、同じ録音を同時に処理しない（上の 2 つの印で譲り合う）。クラウドが止まっても、PC が録音を処理する。

## Mac で使う（Apple Silicon、GPU なし）

`tools/mtg/setup-mac.sh`（zsh）で、環境（Homebrew 経由の git・ffmpeg・gh・Python と、仮想環境 `~/.mtg-venv` の faster-whisper）を確かめて入れ、文字起こしの動作を確かめられる。GPU が無いので、`transcribe.py` は自動で CPU（int8）に切り替わる（`--device auto`）。

- `--check-only`: 何も入れずに確認する / `--test`: 3 秒の音声で動作確認（小さな `tiny` モデルを初回にダウンロード）/ `--transcribe FILE`: 1 本を large-v3 で文字起こしする
- **large-v3 は、CPU では遅い**（録音の長さの数倍かかる見込み。未計測）。**medium・small は、精度が足りないので使わない**（誤認識が増える）。時間がかかっても large-v3 のまま、放置して待つ
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

## チームのカレンダー（IVRC2026）を議題に使う

GitHub Actions「Calendar sync」（`.github/workflows/calendar-sync.yml`）が、毎朝 6 時台に、チームの Google カレンダーの、これから 3 週間の予定を、非公開リポジトリの `calendar/upcoming.md` に書く。水曜の議題が読み、「お知らせ」に日付と予定の名前だけを書く（議題 Issue は公開なので、名前・理由・場所は書かない）。

**初回の設定**（カレンダーの持ち主が 1 回だけ）:
1. Google カレンダーの「設定と共有」→ IVRC2026 →「カレンダーの統合」の「**iCal 形式の非公開 URL**」をコピーする。**この URL を知っている人は誰でも予定を読める。** チャット・Issue・コミットに貼らない
2. このリポジトリの Settings → Secrets and variables → Actions に、`IVRC_CALENDAR_ICS` として入れる
3. claude.ai のルーティン「MTG の議題」に、リポジトリ `Fialuxe/MouthOfGod_transcripts` を足す
4. Actions の「Calendar sync」→ Run workflow で一度動かし、非公開リポジトリに `calendar/upcoming.md` ができることを確かめる

URL が漏れたら、同じ画面の「リセット」で URL を作り直し、Secrets を入れ替える。

## 水曜のルーティン

Claude Code のルーティン（クラウド）が、毎週水曜に `mtg-agenda` スキルを実行して、金曜 MTG の議題 Issue を作る。設定は https://claude.ai/code/routines 。

## モデルと前処理を選んだ理由

同じ 3 分間（声が小さい録音）で比べた結果、**large-v3（faster-whisper）+ 音量の正規化**が一番誤認識が少なかった（turbo、kotoba-whisper v2.0 とも比較）。

- 音量の正規化が一番効く。**短い区間ごとに音量をそろえる**（`dynaudnorm`）。録音全体を一度に正規化するだけだと、小さい声の区間が小さいまま残り、発言が落ちた
- kotoba-whisper は速い（約 7 倍）が、この録音では文がくずれ、VAD を入れると発言が抜けた
- VAD と Whisper の幻覚ガードは切っている（小さい声を捨てるため）。代わりに `clean.py` が幻覚（「ご視聴ありがとうございました」など）を除く
- 速度の目安: RTX 5060 Laptop（8GB）で、43 分の録音が数分
