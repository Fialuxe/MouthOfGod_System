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

- アカウントのメールアドレスは Unity のものと同じでも別でもよい（連携していない）。
- Scene Fusion のプロジェクト ID は `Assets/KinematicSoup/SceneFusion/Editor/SceneFusionConfig.asset` に入っており、Git で共有している（認証情報ではない）。
- 使用時のルールは第 3 章を参照。

#### 席が足りない・接続できない場合（想定している対処）
Scene Fusion が使えなくても、開発は止めない。次の順で対処する。

| 状況 | 対処 |
|---|---|
| 3 人目以降を招待できない／参加できない（Free は 2 席） | **Scene Fusion を使う人を、シーン制作の担当 2 人に絞る**。それ以外のメンバーは `feature/` `fix/` とサンドボックスシーンで作業する（第 2 章）。シーン制作が増える時期だけ、有料（$25 / 席 / 月、最大 10 席）を必要な席数だけ契約する |
| 無料枠の制約（20,000 オブジェクト、テレイン編集不可）に当たる | 有料プランにするか、テレインを使わない・シーンを分割してオブジェクト数を抑える |
| 接続できない・同期が不安定・シーンファイルが破損する | **Scene Fusion をやめ、シーンを分割して担当者を 1 人に決める運用**にする。`scene/` ブランチは 1 人だけが編集し、シーン単位で Git に反映する（同じシーンを同時に触らない） |
| 有料にしたが期間が終わった | 月額なので解約できる。`Packages/manifest.json` から Scene Fusion を外す PR で、使わない状態に戻せる |

どの対処にするかは、Issue #5 の検証結果で決める。

### Unity のエディタ設定（確認）
- `Edit > Project Settings > Editor`
  - **Version Control > Mode**: `Visible Meta Files`
  - **Asset Serialization > Mode**: `Force Text`
- `.meta` ファイルは必ずアセットと一緒にコミットする（`.gitignore` で除外しない）。

### Git の設定
```bash
git config core.autocrlf false     # 改行コードの差分ノイズを避ける
git config pull.rebase true        # pull 時にマージコミットを作らない
git config fetch.prune true        # fetch 時に、リモートで削除済みのブランチの追跡参照を自動で消す
git config alias.cleanup '!git switch main && git pull && git fetch --prune && git branch -vv | grep ": gone]" | awk "{print \$1}" | xargs -r git branch -D'
```

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
5. 別シーンなら並行作業 OK。シーンを分割して担当を分けるのが最も安全（例: ステージごとに別シーン）。
6. **Prefab / Script / 設定ファイルは通常どおり feature ブランチで作業**し、Scene Fusion セッションとは分けて PR を出す。
7. 競合が起きたら（同一シーンを別ブランチで編集してしまった場合）、どちらかを採用して捨てる。手作業でのマージはしない。作業前にチームの連絡手段で「今 `Main.unity` を触る」と宣言する。

## 4. コミット前チェックリスト
- [ ] Unity のコンソールにエラーがない
- [ ] 追加したアセットの `.meta` も含まれている
- [ ] `Library/` `Temp/` `Logs/` `UserSettings/` などがステージされていない（`git status` で確認）
- [ ] 不要なシーン変更（無意識に動かした Transform など）が含まれていない（`git diff` で確認）

## 5. Pull Request
- タイトルに目的、本文に変更内容と動作確認の方法を書く。
- 原則、最低 1 人のレビュー後に `main` へ **Squash merge** または Rebase merge。
- **レビューなしでマージしてよい場合**: マージして問題がないと判断でき、かつレビュワーがレビューできない状況（レビュワーが自分しかいない、全員が多忙、など）のとき。**必ず動作確認をした上で**マージし、PR に確認内容を書く。シーンや共通設定を変更する PR は、可能な限りレビューを受ける。
- マージ後、ローカルで `git switch main && git pull` して最新化する。

## 6. 補足
- 大きなバイナリ（音声・動画・巨大テクスチャ）が増えてきたら Git LFS の導入を検討する（導入時は全員に周知）。
- デフォルトブランチは `main`。
