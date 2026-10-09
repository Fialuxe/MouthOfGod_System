# CONTRIBUTING

MouthOfGod_System の開発手順です。Git ブランチ運用 + Unity の Scene Fusion でシーンを共同編集する前提の最低限のルールをまとめています。
全部読む必要はないです。たぶんそれだと理解が追い付かない可能性があるため。AIのアシストをもらいながら、やりながらチェックしてください。

## 1. 環境セットアップ（初回のみ）

> **はじめに**: GitHub への書き込み権限（Collaborator への招待）、連絡手段、Scene Fusion のアカウントは、公開リポジトリには書かず**チーム内で共有している**。持っていない場合は、チームの誰かに依頼すること。
> 未整備の項目は Issue で管理している: Scene Fusion のセットアップ（#5）、実行用シーンとフォルダ構成（#6）、デバイスなしでの開発（#7）。

### 必要なもの
- Unity **6000.0.73f1**（`ProjectSettings/ProjectVersion.txt` と必ず同じバージョン）
- Git（**Unity を起動する前に**インストールしておく。後から入れた場合は Unity と Unity Hub を再起動。ないとパッケージの取得に失敗する）
- Scene Fusion 2 **クラウド版**（リポジトリに組み込み済み。下記「Scene Fusion のセットアップ」参照）

### Clone
```bash
git clone https://github.com/Fialuxe/MouthOfGod_System.git
cd MouthOfGod_System
```
Unity Hub から `Add project from disk` で開く。初回は `Library/` が生成されるため時間がかかります。

### Scene Fusion のセットアップ
Scene Fusion 2 クラウド版（v2.0.5）は Package Manager 経由でリポジトリに組み込み済みなので、clone して Unity を開けば入る（初回はパッケージ解決に時間がかかる）。**Asset Store 版（Lite / Indie）は LAN 専用なので使わない。**

1. [console.kinematicsoup.com](https://console.kinematicsoup.com/) でアカウントを作る。
   - Subscription で **Scene Fusion** を選び、**Free** で登録する（これをしないと使えない）。
2. 作成したアカウントのメールアドレスを、**チームの連絡手段で管理者に報告する**。管理者がプロジェクトに招待する。
   - 現在は無料枠（**2 席**）のため、招待できる人数に制限がある。席の数え方（登録人数か同時接続数か）は未確認で、Issue #5 で検証中。
3. 招待メールを承認する。
4. Unity で `Window > Scene Fusion` を開き、2 のアカウントでログインする。
5. セッションのメニューで、招待されたプロジェクトを選ぶ（初回だけ）。

- アカウントのメールアドレスは Unity のものと同じでも別でもよい（連携していない）。
- Scene Fusion の設定（`Assets/KinematicSoup/SceneFusion/Editor/SceneFusionConfig.asset`）は、選んだプロジェクトや表示・ログの設定など各自のものなので、Git で共有しない（`.gitignore` 済み）。初めて開いたときに自動で作られる。セッションの設定（マテリアル・Prefab の同期）は、セッションを作った人の値が使われる。
- 使用時のルールは第 3 章を参照。

#### 席が足りない・接続できない場合（想定している対処）
Scene Fusion が使えなくても、開発は止めない。次の順で対処する。

| 状況 | 対処 |
|---|---|
| 3 人目以降を招待できない／参加できない（Free は 2 席） | **Scene Fusion を使う人を、シーン制作の担当 2 人に絞る**。それ以外のメンバーは `feature/` `fix/` とサンドボックスシーンで作業する（第 2 章）。それでも足りない場合は有料にするが、予算申請の都合で月額ではなく**買い切り**を買うことになる見込み。**買う前に先生に相談する** |
| 無料枠の制約（20,000 オブジェクト、テレイン編集不可）に当たる | テレインを使わない・シーンを分割してオブジェクト数を抑える。それでも無理なら有料（買い切りの見込み）を**先生に相談する** |
| 接続できない・同期が不安定・シーンファイルが破損する | **Scene Fusion をやめ、シーンを分割して担当者を 1 人に決める運用**にする。`scene/` ブランチは 1 人だけが編集し、シーン単位で Git に反映する（同じシーンを同時に触らない） |

どの対処にするかは、Issue #5 の検証結果で決める。お金がかかる対処（有料版の購入）は、予算申請が必要なので**必ず先生に相談してから**決める。

### Unity のエディタ設定（確認）
- `Edit > Project Settings > Editor`
  - **Version Control > Mode**: `Visible Meta Files`
  - **Asset Serialization > Mode**: `Force Text`
- `.meta` ファイルは必ずアセットと一緒にコミットする（`.gitignore` で除外しない）。
- Package Manager の画面の状態（`ProjectSettings/PackageManagerSettings.asset`。選んだレジストリや開閉など）は各自のものなので、Git で共有しない（`.gitignore` 済み）。チームで共有するパッケージ（スコープ付きレジストリを含む）は `Packages/manifest.json` に書く。Unity が自動で作り直す。

### Git の設定
```bash
git config pull.rebase true        # pull 時にマージコミットを作らない
git config fetch.prune true        # fetch 時に、リモートで削除済みのブランチの追跡参照を自動で消す
git config alias.cleanup '!git switch main && git pull && git fetch --prune && git branch -vv | grep ": gone]" | awk "{print \$1}" | xargs -r git branch -D'
```

- 改行コードは、リポジトリの `.gitattributes` で LF にそろえている（`core.autocrlf` などの各自の設定は要らない）。
- リモートのブランチは、PR をマージすると GitHub が自動で削除する（リポジトリ設定済み）。
- ローカルのブランチは、マージ後に `git cleanup` を実行すると、リモートで削除済みのものがまとめて消える（Squash マージ後のブランチは `git branch --merged` で検出できないため、この方法を使う）。


## 2. ブランチ運用

| ブランチ | 用途 |
|---|---|
| `main` | 常に動く状態を保つ。直接 push しない |
| `feature/<内容>` | 機能追加（例: `feature/player-move`） |
| `fix/<内容>` | バグ修正 |
| `scene/<シーン名>-<内容>` | シーン編集が主体の作業 |
| `docs/<内容>` | README・CONTRIBUTING などドキュメントのみの変更 |

- 作業は必ず `main` から切ったブランチで行い、**Pull Request 経由**で `main` にマージする。
- ブランチは短命に保つ（1 PR = 1 目的、数日以内にマージ）。
- マージ後はブランチを削除する。
- `main` はブランチ保護済み: PR 経由のみ、force push・削除は禁止、履歴は直線（Squash / Rebase マージのみ）。承認数の要件は 0 で、レビューの扱いは第 5 章に従う。

```bash
git switch main && git pull
git switch -c feature/player-move
# ... 作業 ...
git add <変更したファイル>
git commit -m "Add player movement"
git push -u origin feature/player-move   # → GitHub で PR を作成
```

コミットメッセージは英語/日本語どちらでもよいが、**何をしたかを 1 行目に命令形で**書く。

### ブランチの使い分け

迷ったら「**主に何を変更するか**」で選ぶ。

| 接頭辞 | 使う時 | 主な変更対象 | 例 |
|---|---|---|---|
| `feature/` | 新しい機能・仕組みを作る／既存機能を拡張する | スクリプト、Prefab、ScriptableObject、Input 設定、パッケージ追加 | `feature/player-move`, `feature/dialogue-ui` |
| `fix/` | 既存の挙動の不具合を直す | 該当スクリプト／Prefab の最小限の修正 | `fix/camera-jitter`, `fix/null-ref-on-start` |
| `scene/` | シーン（`.unity`）上の配置・ライティング・レベル制作が中心の作業。**Scene Fusion で複数人編集する時はこれ** | `Assets/Scenes/*.unity` | `scene/Main-stage1-layout` |
| `docs/` | ドキュメントだけを変更する（コード・アセットは触らない） | `README.md`, `CONTRIBUTING.md` など | `docs/update-readme` |

判断のポイント:
- 動作確認にシーンが必要な機能は、**自分専用のサンドボックスシーン**で確認する（下記）。共有シーン（`Main` など）は `feature/` / `fix/` では編集しない。
- 共有シーンへの配置が必要になったら、機能をマージした後に `scene/` ブランチで行う。機能が未マージでも先に進めたい場合は、`scene/` ブランチを `feature/` ブランチから切ってよい（その場合、`feature/` を先にマージしてから `scene/` をマージする）。
- 小さなバグ修正でも、他の作業と混ぜず `fix/` を分ける。
- 命名は `接頭辞/内容`、英小文字とハイフン（シーン名のみ元の大文字小文字可）。

### 動作確認用のサンドボックスシーン

- 場所: `Assets/Scenes/Sandbox/<自分の名前>_<機能名>.unity`（例: `Sandbox/takuma_player-move.unity`）
- 作成者本人だけが編集する。他人のサンドボックスは触らない・Scene Fusion に入れない → 競合が原理的に起きない。
- `feature/` / `fix/` ブランチにそのままコミットしてよい（自分専用なので PR にあっても問題ない）。不要になったら機能のマージ時か後日削除する。
- 共有シーンに入れる前に、サンドボックスで「単体で動く」ことを確認する。
- `Main` や `SampleScene` に直接テスト用オブジェクトを置いてコミットしない。

### 作業の進め方

どの作業でも、始める前に Issue を選ぶ。週に 1 回、ロードマップで今週やるものを決める。

**Assignee は「しばらくこの Issue を持っている」という印**（[README「プロジェクト管理」](README.md#プロジェクト管理)）。厳密に守らなくてよい。
- **付ける**: 何日かかけて進めるとき。ロードマップの「着手中」に出て、ほかの人が同じものを始めなくなる。
- **付けなくてよい**:
  - その日のうちか、すぐに終わる（もう取りかかっていて、すぐ PR を出す）とき
  - 誰がやるか、チームで合意が取れているとき（話し合いや Slack で決まっている）
- 付けないと、ロードマップの「すぐ着手できる」やふりかえりの「次の一手」に、その Issue が出たままになる。気になるなら付ける。
- 手を離すとき（しばらく触らない・ほかの人に渡す）は、Assignee を外す。

**ミーティングで話すことは、ラベルと「議題 Issue」で集める**。

- **ラベル**（MTG の前に、話したい Issue に付ける）:
  - `MTGで共有`: 見てほしい・知っておいてほしいもの（レビューしてほしい案、進み具合、困っていること）
  - `MTGで決める`: その場で決めたいもの。Issue に「決めてほしいこと」を番号付きで書いておく
  - 一覧: [MTGで共有](https://github.com/Fialuxe/MouthOfGod_System/issues?q=is%3Aopen+label%3AMTG%E3%81%A7%E5%85%B1%E6%9C%89)・[MTGで決める](https://github.com/Fialuxe/MouthOfGod_System/issues?q=is%3Aopen+label%3AMTG%E3%81%A7%E6%B1%BA%E3%82%81%E3%82%8B)
- **議題 Issue**（MTG 1 回につき 1 つ。題は「次の MTG の議題と議事録」、ラベルは `MTGで共有`）:
  - 「共有すること」と「決めること（締切の近い順）」を並べる。Issue のないこと（マージした PR、決まりの変更、お知らせ）も、ここに書けば共有できる
- **MTG が終わったら**:
  1. 議題 Issue に**議事録**をコメントする（共有したこと・決めたこと・次にやる人）
  2. 決めたことは、それぞれの Issue にも書き、`MTGで決める` を外す。決まらなかったものは、付けたまま次回へ
  3. 共有した Issue から `MTGで共有` を外す。何を共有したかは議事録に残るので、外してよい
  4. 議題 Issue を閉じ、次の MTG の議題 Issue を作る（閉じた議題 Issue が、MTG の記録になる）

**ほかのラベル**:

| ラベル | 意味 |
|---|---|
| `決めること` | 作業ではなく、決めることが中身の Issue（MTG で決めるとは限らない） |
| `実装`・`ものづくり`・`素材・文言`・`試す・測る`・`応募`・`展示準備` | 作業の種類 |
| `10月末まで` | 10/30 の応募に必要 |
| `任意` | やれたらやる。区切りの完了条件に含めない（ロードマップの数に入らない） |
| `状態` | 作業ではなく、区切りのゴールを分けたもの。子（Blocked by）がすべて閉じると自動で閉じる |
| `Claude提案` | Claude が下書きした Issue |
| `roadmap`・`activity` | ロードマップ・活動記録の Issue（自動で更新。手で付けない） |

#### A. `feature/` / `fix/`（通常の個人作業）
1. 必要なら最新化: `git switch main && git pull`
2. ブランチ作成: `git switch -c feature/xxx`
3. 実装し、**小さな単位でこまめにコミット**（動く状態ごと）。Unity を開いたまま `git switch` しない（切替後に Unity が再インポートを行うため、切替は Unity 上で変更を保存した状態で行う）。
4. `git push -u origin feature/xxx`（初回）／ 以降は `git push`
5. GitHub で PR を作成（`main` 向け）。レビューを受けるなら受けて修正 → マージ。
6. マージ後: `git switch main && git pull && git branch -d feature/xxx`
7. 作業中に `main` が進んだ場合: **自分だけが使っているブランチなら** `git fetch && git rebase origin/main` し、`git push --force-with-lease` で更新する。複数人が使うブランチ（`scene/` など）では rebase せず `git merge origin/main` を使う。シーン／Prefab が競合したら第 3 章の方針で片方を採用する。

#### B. `scene/`（Scene Fusion で共同編集）
1. **事前宣言**: 「どのシーンを、誰と、いつ触るか」を共有する。（チャットで言わなくても作業する人たちがわかっていればOK）同じシーンを別ブランチで同時に触らない。
2. **ホスト 1 人**が `main` から `scene/<シーン名>-<内容>` を作成し push する。
3. 参加者は `git fetch && git switch scene/...` で**同じコミットの状態**にしてから Unity を開く。
4. ホストが Scene Fusion セッションを開始し、参加者が接続する。編集中は互いの変更がリアルタイムに同期される。
5. 区切りごとにホストがシーンを保存 → 他メンバーは Git 操作をしない → ホストがコミット・push。
6. セッション終了後、ホストが PR を作成して `main` にマージ。参加者は `git switch main && git pull` で追従する。
7. シーンに必要な新規スクリプト／Prefab が出てきたら、その場で作らない。Scene Fusion が同期するのはシーン上のオブジェクトで、スクリプトなどのアセットは各自のローカルにある必要があるため。先に `feature/` で作って push し、全員が取り込んでからコンポーネントを付ける。

> **未検証事項**: 「ホストだけがコミットする」運用は、Scene Fusion が各自のローカルのシーンファイルをどう保存するかに依存する。最初のセッションで、(a) 参加者側のシーンファイルに差分が出るか、(b) ホストの保存だけで全員分の変更が含まれるか、を必ず確認し、結果に合わせてこの章を修正すること。

#### 一人でシーンを触る場合
Scene Fusion を使わず 1 人だけでシーンを編集する場合も、`scene/` ブランチを切って上記 B の手順（ホスト＝自分）で進める。

## 3. Scene Fusion を使った作業ルール

Scene Fusion はシーンを**リアルタイム同時編集**する仕組みで、Git のマージとは別物です。衝突を避けるため次を守ってください。

1. **シーン / Prefab ファイルを Git でマージしない。** `.unity` `.prefab` は YAML で競合解消が困難。
2. **同じシーンを触る人は、同じブランチ・同じ Scene Fusion セッションで作業する。**
   - セッションのホスト 1 人が `scene/...` ブランチを用意し、他のメンバーはそれを checkout して参加する。
   - 全員が同一コミットの状態から開始してから Scene Fusion に接続する。
3. **コミット・push はホスト（1 人）が行う。** Scene Fusion 中の変更は全員のシーンに同期されるため、複数人が別々にシーンをコミットすると重複・競合になる。
4. セッション終了前にホストがシーンを保存 → コミット → push。他のメンバーはセッション終了後に `git pull` して最新に追従する。
5. 共有シーンは 1 つ（例: `Main`）にし、分けない。複数人で触るときは Scene Fusion で一緒に編集する（[docs/architecture.md](docs/architecture.md)「シーンを 1 つにするときの約束」）。機能の動作確認はサンドボックスで行う。
6. **Prefab / Script / 設定ファイルは通常どおり feature ブランチで作業**し、Scene Fusion セッションとは分けて PR を出す。
7. 競合が起きたら（同一シーンを別ブランチで編集してしまった場合）、どちらかを採用して捨てる。手作業でのマージはしない。作業前にチームの連絡手段で「今 `Main.unity` を触る」と宣言する。

## 4. コミット前チェックリスト
- [ ] Unity のコンソールにエラーがない
- [ ] 追加したアセットの `.meta` も含まれている
- [ ] `Library/` `Temp/` `Logs/` `UserSettings/` などがステージされていない（`git status` で確認）
- [ ] 不要なシーン変更（無意識に動かした Transform など）が含まれていない（`git diff` で確認）

## 5. Pull Request
- タイトルに目的、本文に変更内容と動作確認の方法を書く。
- 本文に、この PR で終わる Issue を `Closes #番号` と書く（複数なら 1 行に 1 つ）。Issue のない小さな修正は書かなくてよい。
  - **書くと何がいいか**:
    - **閉じ忘れがなくなる**: マージした瞬間に Issue が自動で閉じる。手で閉じる手間も、閉じ忘れもない。
    - **ロードマップが正しく進む**: Issue が閉じると、ロードマップ（Issue と README の図）が作り直され、今の区切りの「完了」の数が増える。閉じ忘れた Issue は、作業が終わっていても「終わっていない」と数えられる。
    - **次の人が着手できる**: その Issue を待っていた Issue（Blocked by）のブロックが外れ、毎日のふりかえり（Slack）の「外れたブロック」「次の一手」に、着手できるようになった Issue として出る。
    - **あとから辿れる**: Issue のページに、閉じた PR が残る。「この決めごとは、どの変更で入ったか」を Issue からも PR からも辿れる。
    - **レビューの基準になる**: レビュワーは、Issue の完了条件を満たしているかを見ればよい。
    - **ふりかえりが正確になる**: ふりかえりは、PR と Issue のつながりから「どの PR で何が終わったか」を書く。`Closes` のないレビュー待ちの PR は「見落とし」として出る。
  - **終わる Issue には、積極的に `Closes` を付ける**。PR を書く前に、Issue の「やること」「できたと言える状態（完了条件）」を読み、この PR で満たせるなら `Closes`。
  - Issue の一部だけを進める PR は `Refs #番号` にし、残っていることを本文に書く。いくつかの PR で 1 つの Issue を終えるときは、**最後の PR に `Closes`** を付ける。
  - Issue に未決のことが少し残るだけなら、それを docs や新しい Issue に移してから `Closes` にする（Issue を開いたままにしない）。
- 原則、最低 1 人のレビュー後に `main` へ **Squash merge** または Rebase merge。
- **レビューなしでマージしてよい場合**: マージして問題がないと判断でき、かつレビュワーがレビューできない状況（レビュワーが自分しかいない、全員が多忙、など）のとき。**必ず動作確認をした上で**マージし、PR に確認内容を書く。シーンや共通設定を変更する PR は、可能な限りレビューを受ける。
- マージ後、ローカルで `git switch main && git pull` して最新化する。

## 6. スクリプト方針

目標は「**どこに何があるか分かる**」ことと「**フォルダの中は自由に分割できる**」ことの両立。

機能どうしのつなぎ方、**別々の作業が同じファイルを編集しない分け方**、避けること（アンチパターン）、レビューのチェック項目は [docs/architecture.md](docs/architecture.md) にまとめている。実装やレビューの前に読む。

### 置き場所
- スクリプトはすべて `Assets/Scripts/` 以下に置く。ルートに直接置かない。
- **機能ごとにフォルダを切る**: `Assets/Scripts/<Feature>/`。フォルダの中の分け方（サブフォルダ、ファイル数）は、その機能の担当者が自由に決めてよい。
- 初期のフォルダ（必要になったら増やしてよい。増やしたら `Assets/Scripts/README.md` の一覧に 1 行足す）:

| フォルダ | 役割 |
|---|---|
| `Core/` | 全体で共有する小さな基盤（イベント、共通インターフェース、ユーティリティ）。**他のフォルダに依存しない** |
| `Device/` | 触感デバイス・Arduino との通信（下記） |
| `Tracking/` | VR・位置取得 |
| `Tutorial/` | チュートリアル 1〜4 の進行 |
| `Game/` | 本編の進行、スコア、判定 |
| `Village/` | ミニチュアの村・フィールド・村人 |
| `UI/` | 画面表示 |

- 各フォルダに短い `README.md`（目的、外に見せる API、担当者）を置くと、他の人が探しやすい。
- **Prefab はすべて `Assets/Resources/Prefabs/` 以下に置く。** スクリプトと同じく機能ごとにフォルダを切り、フォルダ名はスクリプトとそろえる（例: `Village/` のスクリプトで使う Prefab は `Assets/Resources/Prefabs/Village/`）。フォルダの中の分け方は担当者が自由に決めてよい。

### 書き方
- **既定は `private`**。他の機能から呼ばせるものだけを `public` にする。Inspector に出したいだけなら `[SerializeField] private` を使う。
- 名前空間は `MouthOfGod.<Feature>`（例: `MouthOfGod.Device`）。
- **疎結合**: 他の機能とのやり取りは、`event` / `interface` / ScriptableObject 経由にする。他の機能のクラスのフィールドを直接読み書きしない。`FindObjectOfType` や `GameObject.Find` で他機能を探さない。
- 依存の向きは `Core` ← 各機能 ← `Game` / `Tutorial`。`Core` は誰にも依存しない。
- 機能ごとに Assembly Definition（`*.asmdef`）を置くと、依存を**コンパイルで強制**できる。必須ではないが、`Device/` と `Core/` には置くことを推奨する。
- 定数や設定値（ポート名、しきい値など）をコードに埋め込まず、`ScriptableObject` か Inspector で渡す。

### 最終的に Inspector／Editor の操作が要らないシステムにする（LLM エージェント向け）

GDC では、Link で Unity を Play して動かす。**Play したあとに、人が Inspector や Editor を操作しなくても動く**システムにする。実装するときは、次を満たす。

- Play するだけで動く。Play の前後に、手でつなぐ参照や、手で入れる設定値を残さない。
- 調整ややり直し（例: AprilTag の位置の取り直し）は、自動処理か、コントローラーのボタンなどのアプリ内の操作でできるようにする。`[ContextMenu]` や Inspector のボタンは開発中の補助にとどめ、唯一の手段にしない。
- 開発中に Inspector 操作が残るときは、手順を README か PR に書き、コードに置き換える Issue を立てる。

### デバイス連携（`Device/`）の設計
旧プロジェクト MealBeBack のシリアル通信で良くなかった点と、その直し方は [docs/architecture.md「5. デバイス連携」](docs/architecture.md#5-デバイス連携device-旧プロジェクトの反省) にまとめた。`Device/` を触る前に読む。

## 7. 外部素材（サードパーティのアセット）

この repo は公開されているので、**再配布できる素材だけ**を入れる。

- **置き場所**: `Assets/ArtisticResources/ThirdParty/<提供元>/<パック名>/`（例: `Assets/ArtisticResources/ThirdParty/Quaternius/Ultimate Food Pack - Oct 2019/`）。パックのフォルダは、配布されたままの構成・名前で置く。
- **入れてよいもの**: CC0・MIT など、公開 repo で再配布してよいライセンスの素材。パックに付いているライセンスの文書（`License.txt` など）は、消さずに同じフォルダに残す。MIT のように著作権表示が要るライセンスで、配布物に文書が入っていなければ、配布元から取ってきて同じフォルダに置く。
- **`.unitypackage` から入れるとき**: 中の `.meta` の GUID が、このプロジェクトのファイルとぶつかっていないか確かめる。URP のテンプレートから作った設定（`*_RPAsset.asset`・`*_Renderer.asset` など）は、`Assets/Settings/` のものと同じ GUID のことがある（Uber Stylized Water のデモがそうだった）。ぶつかるものは、使われていなければ入れない。確かめ方: `find Assets -name "*.meta" -print0 | xargs -0 grep -h "^guid" | sort | uniq -d`（何も出なければよい）。
- **入れてはいけないもの**: 再配布できない素材（Asset Store の有償アセットなど）。手元で使うだけなら `Assets/Local/` に置く（`.gitignore` 済み。Git に上がらない）。
- **CREDITS.md**: 素材を入れる PR で、[CREDITS.md](CREDITS.md) に「提供元・パック名・ライセンス・URL・置き場所」を 1 行足す。
- **容量**: 使わないファイルは入れない。同じモデルが複数の形式（FBX / OBJ / glTF / Blend）で入っているパックでは、Unity で使う形式だけにする。`.blend`（元データ）とプレビューの動画・GIF は `.gitignore` で除外している（手元に置くのは自由）。
- **Git LFS**: 当面は使わない。素材の合計が大きくなって clone が重くなったら、導入を検討する（導入時は全員に周知）。
- **glTF（`.gltf`）を使うには** Package Manager の `com.unity.cloud.gltfast`（`Packages/manifest.json` に入れてある）が必要。

## 8. 補足
- 大きなバイナリ（音声・動画・巨大テクスチャ）が増えてきたら Git LFS の導入を検討する（導入時は全員に周知）。
- デフォルトブランチは `main`。
