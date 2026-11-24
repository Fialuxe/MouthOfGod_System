# CONTRIBUTING

MouthOfGod_System の開発の決まりです。全部を一度に読まなくてかまいません。作業しながら、必要な所を見てください。

## 1. 環境の準備

GitHub への書き込み権限、連絡手段、Scene Fusion のアカウントは、チームの中で共有しています。持っていなければ、チームの誰かに頼んでください。

- **Unity 6000.0.73f1**（`ProjectSettings/ProjectVersion.txt` と同じ版）
- **Git**。Unity より先に入れます。あとから入れたら、Unity と Unity Hub を再起動します（パッケージの取得に失敗するためです）。
- **Scene Fusion 2 クラウド版**。準備のしかたは [docs/scene-fusion.md](docs/scene-fusion.md) にあります。

```bash
git clone https://github.com/Fialuxe/MouthOfGod_System.git
git config pull.rebase true
git config fetch.prune true
git config alias.cleanup '!git switch main && git pull && git fetch --prune && git branch -vv | grep ": gone]" | awk "{print \$1}" | xargs -r git branch -D'
```

Unity Hub の `Add project from disk` で開きます。初回は `Library/` を作るので時間がかかります。

- Project Settings の Editor で、Version Control を `Visible Meta Files`、Asset Serialization を `Force Text` にしておきます。
- `.meta` は、必ずアセットと一緒にコミットします。
- 改行コードは `.gitattributes` で LF にそろえています。

## 2. ブランチ

運用は、Git-Flow を簡略化したものです。

| ブランチ | 使うとき |
|---|---|
| `main` | いつも動く状態を保ちます。直接 push しません |
| `develop` | 大きな作り直しを、`main` と分けて試すとき |
| `feature/<内容>` | 機能を作る・広げるとき（スクリプト、Prefab、設定、パッケージ） |
| `fix/<内容>` | 不具合を直すとき |
| `scene/<シーン名>-<内容>` | シーンの配置が中心のとき。Scene Fusion で一緒に編集するときもこれです |
| `docs/<内容>` | 文書だけを変えるとき |

- 作業は、`main`（大きな作り直しは `develop`）から切ったブランチで行い、PR でマージします。1 つの PR は 1 つの目的にし、数日のうちにマージします。
- 作業中に `main` が進んだら、自分だけのブランチなら `git rebase origin/main` して `git push --force-with-lease` します。何人かで使うブランチでは、`git merge origin/main` にします。
- Unity を開いたまま `git switch` しません。切り替えると Unity が読み込み直すので、変更を保存してから切り替えます。
- マージしたら、`git cleanup` で手元の消えたブランチを片付けます。リモートのブランチは、GitHub が自動で消します。
- コミットメッセージは英語でも日本語でもかまいません。1 行目に、何をしたかを書きます。

### シーン

- 体験を動かす共有のシーンは、`Assets/Scenes/Main.unity` の 1 つだけです。`feature/`・`fix/` では `Main` を編集しません。`Main` を触るときは `scene/` を使います。
- 動作の確認は、自分専用のシーン `Assets/Scenes/Sandbox/<名前>_<機能>.unity` で行います。人のサンドボックスは触りません。
- `.unity`・`.prefab` は、Git でマージしません。競合したら、どちらかを採用します。Scene Fusion を使うときの決まりは [docs/scene-fusion.md](docs/scene-fusion.md) にあります。

## 3. Issue と Pull Request

- 作業を始める前に Issue を選びます。Issue の書き方は [`.claude/skills/write-issue/`](.claude/skills/write-issue/SKILL.md) に、ラベルや担当の扱いは [docs/issue-workflow.md](docs/issue-workflow.md) にあります。
- PR の本文には、目的、変えたこと、確かめ方を書きます。
- この PR で終わる Issue は、本文に `Closes #番号` と書きます（1 行に 1 つ）。マージすると Issue が閉じ、ロードマップが進み、その Issue を待っていた作業が始められるようになります。迷ったら、閉じる側に倒します。一部だけ進める PR は `Refs #番号` にして、残りを本文に書きます。
- マージは Squash か Rebase です。できるだけ 1 人に見てもらいます。見てもらえないときは、自分で動かして確かめてからマージし、確かめた内容を PR に書きます。

コミットの前に、次を確かめます。

- Unity の Console にエラーがない
- 追加したアセットの `.meta` が入っている
- `Library/`・`Temp/`・`Logs/`・`UserSettings/` が入っていない
- うっかり動かした Transform など、要らないシーンの変更が入っていない

## 4. スクリプト

設計の考え方は [docs/architecture.md](docs/architecture.md) に、フォルダごとの役割は [Assets/Scripts/README.md](Assets/Scripts/README.md) にあります。

- スクリプトは `Assets/Scripts/<機能>/` に置きます。名前空間は `MouthOfGod.<機能>` です。
- Prefab は `Assets/Prefabs/<機能>/`、ScriptableObject のデータは `Assets/Data/`、音は `Assets/Audio/`、画像は `Assets/Art/` に置きます。使う物は、Inspector でつなぎます。シーンにはつなぎを持たせず、Prefab の中でつなぎます。コードから `Resources.Load` で名前を指定して読むことはしません。`Assets/Resources/` に置くのは、Meta の SDK が決まった場所から読む設定だけです。
- 体験の流れ（プロローグ → チュートリアル 1〜4 → 本編 → エンディング）は、`Flow` の場面のスクリプトに書きます。`Flow` は、各機能の部品を直接呼んでかまいません。各機能どうしは直接参照せず、`Core` の interface かイベントでつなぎます。機能が別の機能を参照すると、PR に警告が出ます（`.github/scripts/check_coupling.py`）。
- 各機能の部品は、自分のことを自分で行います（巫女は浮いて体験者を向く、雲は並んで補充される、食べ物は自分が生まれる条件を持つ）。場面のスクリプトは、何をしてほしいかだけを呼びます。
- 既定は `private` です。Inspector に出したいだけなら `[SerializeField] private` にします。しきい値などの値は、コードに埋め込まず Inspector で渡します。
- 展示の会場では、キーボードも Inspector も使いません。調整ややり直しは、Quest のコントローラの操作でできるようにします。

### コードの書き方

Unity の C# スタイルガイド（Microsoft の C# の決まりが元）に合わせます。コメントは日本語でかまいません。

1. **コメントには、コードから読めない大事なことを書きます。** 理由、前提、気をつけること、呼ぶ順番の約束（例: Closed → Received → StepDone の順に出る）、Inspector で直すときの勘所などです。コードを言い直すだけのコメントは書きません。
2. **Tooltip は、Inspector で値を変える人が迷わないように書きます。** 単位、空のときの動き、ほかの設定との関係まで書いてかまいません。
3. **今はない仕組みや、作ったときの事情は書きません。** Issue の番号や経緯は、Issue と PR に残します。仕組みを消したら、それを指すコメントも直します。
4. **1 つのファイルに、型は 1 つです。** ファイル名は型の名前と同じにします。Unity は、ファイル名と同じ名前の MonoBehaviour しか Inspector に出せないためです。
5. **使われていないコードは残しません。** 呼ばれていない入口や、一度だけ使った Editor の道具は消します。
6. **見えない所で物を作りません。** 部品はシーンか Prefab に置きます。`RuntimeInitializeOnLoadMethod` で自分を作りません（static の値を Play のたびに空に戻すのはかまいません）。

## 5. 外部素材

このリポジトリは公開されているので、再配布できる素材（CC0・MIT など）だけを入れます。

- 置き場所は `Assets/ArtisticResources/ThirdParty/<提供元>/<パック名>/` です。配られたままの構成と名前で置き、ライセンスの文書も残します。
- 素材を入れる PR で、[CREDITS.md](CREDITS.md) に「提供元・パック名・ライセンス・URL・置き場所」を 1 行足します。
- 再配布できない素材（Asset Store の有償アセットなど）は、`Assets/Local/` に置きます（Git に上がりません）。
- `.unitypackage` から入れるときは、`.meta` の GUID がほかのファイルとぶつかっていないかを確かめます: `find Assets -name "*.meta" -print0 | xargs -0 grep -h "^guid" | sort | uniq -d`（何も出なければ大丈夫です）。
- 使わない形式のファイル（同じモデルの FBX・OBJ・glTF・Blend など）は入れません。glTF を使うには `com.unity.cloud.gltfast` が要ります（入れてあります）。
