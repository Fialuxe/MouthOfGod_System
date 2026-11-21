---
name: create-pr
description: git diff とコミット履歴から Google eng-practices (Change Author's Guide / CL descriptions / Small CLs) 準拠の PR を作り、gh pr create で提出する。「PR作って」「プルリク」「pull request」「PR説明文」「PRを分けて」と言われた時、またはブランチの作業を提出する時に使う。Unity プロジェクト向けの注意を含む。
---

# create-pr: 小さく、説明が正確な PR を作る

PR の説明文は変更の永久記録である。
レビュアーと未来の開発者が読む。
未来の開発者は説明文で PR を検索する。
重要な情報がコードにしかない PR は見つからない。

この手順は Google の次の文書に基づく。

- Writing good CL descriptions
- Small CLs
- The CL author's guide to getting through code review

Google の「CL」は GitHub の「PR」と同じ意味で扱う。

---

## 手順の全体

1. 状態を確認する
2. 差分を集める
3. サイズと焦点を判定する
4. 必要なら分割する
5. テストを確認する
6. タイトルを書く
7. 本文を書く
8. 提出前に見直す
9. gh pr create で作成する

---

## 1. 状態を確認する

次を実行する。

```bash
git status --short
git branch --show-current
gh repo view --json defaultBranchRef --jq .defaultBranchRef.name
```

次の場合は止まってユーザーに聞く。

- 現在のブランチがデフォルトブランチ (main / master / develop) である
- 未コミットの変更がある
- `gh auth status` が失敗する

未コミット変更を勝手にコミットしない。
デフォルトブランチ上なら、新しいブランチを切るか聞く。

ベースブランチを `BASE` とする。
指定がなければデフォルトブランチを使う。

```bash
BASE=$(gh repo view --json defaultBranchRef --jq .defaultBranchRef.name)
git fetch origin "$BASE"
```

---

## 2. 差分を集める

次をすべて読む。

```bash
git log --oneline "origin/$BASE"..HEAD
git log --format='%h %s%n%b' "origin/$BASE"..HEAD
git diff --stat "origin/$BASE"...HEAD
git diff "origin/$BASE"...HEAD -- '*.cs' '*.asmdef' '*.shader' '*.hlsl' '*.uss' '*.uxml'
```

Unity の YAML アセットは別に一覧だけ取る。

```bash
git diff --stat "origin/$BASE"...HEAD -- '*.unity' '*.prefab' '*.asset' '*.mat' '*.anim' '*.controller' '*.meta'
```

関連 Issue を探す。

- ブランチ名の番号 (例: `feature/42-door-event`)
- コミットメッセージの `#42`
- ユーザーの発言

見つかった Issue は `gh issue view 42` で内容を読む。
Issue の内容を本文の背景に使う。

既存 PR の書き方を確認する。

```bash
gh pr list --state merged --limit 5 --json title,body
ls .github/pull_request_template.md .github/PULL_REQUEST_TEMPLATE* 2>/dev/null
```

テンプレートがあれば、テンプレートの見出しを優先する。
既存 PR の言語 (日本語 / 英語) に合わせる。
判断できない場合は日本語で書く。

---

## 3. サイズと焦点を判定する

### 3.1 焦点の判定

PR は「1つのこと」だけを扱う。
次の質問に1文で答えられるか確認する。

> この PR は何を変えるか。

「A と B をする」になる場合は分割候補である。

次の混在は分割する。

| 混在の種類 | 対応 |
|---|---|
| リファクタリング + 機能追加 | リファクタリングを先の PR に分ける |
| リファクタリング + バグ修正 | リファクタリングを先の PR に分ける |
| 無関係な2つの機能 | 機能ごとに分ける |
| コード + 設定値の調整 | 設定を別 PR にする (ロールバックが簡単になる) |
| 新規テスト基盤 + 機能 | テスト基盤を先の PR に分ける |

例外として、小さな整理は機能 PR に含めてよい。
例: その関数内のローカル変数名の変更。

### 3.2 行数の判定

行数は `.cs` などのコードファイルで数える。
`.unity` `.prefab` `.meta` などの自動生成 YAML は数えない。

```bash
git diff --shortstat "origin/$BASE"...HEAD -- '*.cs'
```

| 変更行数 (追加+削除) | 判定 |
|---|---|
| 〜100 行 | 適正 |
| 101〜400 行 | 分割を検討する |
| 401〜1000 行 | 分割を提案する |
| 1000 行超 | 原則として分割する |

ファイル数も見る。
1ファイル 200 行は許容できる。
50 ファイルにまたがる 200 行は大きすぎる。

迷ったら小さくする。
小さすぎる PR に文句を言うレビュアーはまれである。

### 3.3 大きくてよい例外

- ファイルの丸ごと削除 (実質1行の変更として扱う)
- 信頼できるツールによる機械的変更 (一括リネーム、フォーマッタ適用)
- Unity が生成したアセットの差分 (シーンやプレハブの YAML)

例外の場合は本文にその旨を書く。
例: 「`Assets/Scenes/Main.unity` の差分は Unity エディタによる自動保存です」

### 3.4 理解可能性の判定

PR 単体で意味がわかる必要がある。
新しい API を追加するなら、その API の使用箇所も同じ PR に入れる。
使われないメソッドだけの PR は作らない。

PR マージ後もプロジェクトが動く必要がある。
コンパイルエラーが出る中間状態の PR は作らない。

---

## 4. 必要なら分割する

分割が必要と判定したら、PR を作る前にユーザーに提案する。
勝手にコミットを並べ替えたり、force push したりしない。

提案には次を含める。

- 分割後の PR の一覧と順番
- 各 PR に入るファイル
- 各 PR のタイトル案

分割の戦略は次から選ぶ。

| 戦略 | 内容 | Unity での例 |
|---|---|---|
| スタッキング | PR1 を出し、PR1 を土台に PR2 を作る | イベント定義 PR → 購読側 PR |
| ファイル単位 | レビュアーが違うファイル群で分ける | C# スクリプト PR と シェーダー PR |
| 水平分割 | 層ごとに分ける (インターフェースやスタブで切る) | `IDamageable` 定義 PR → 実装 PR → UI PR |
| 垂直分割 | 機能の縦切りで分ける | 「ドアを開ける」PR と「鍵を拾う」PR |

スタッキングでは、2本目以降の PR のベースを前の PR のブランチにする。

```bash
gh pr create --base feature/door-event-definition ...
```

---

## 5. テストを確認する

ロジックを追加・変更する PR はテストを含める。
純粋なリファクタリングは、既存テストで挙動が守られている必要がある。
テストがなければ、先にテストを追加する PR を出すことを提案する。

Unity プロジェクトでは次を確認する。

```bash
# テストアセンブリの有無
find Assets -name '*.asmdef' -path '*Tests*'
```

Unity CLI でテストを実行できる環境なら実行する。

```bash
"$UNITY_PATH" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults-EditMode.xml -logFile -
"$UNITY_PATH" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults ./TestResults-PlayMode.xml -logFile -
```

実行できない環境では、テストを実行したと書かない。
未実行の項目はチェックを外して書く。
ユーザーに手動確認の手順を渡す。

### 厳守事項

- 実行していないテストを「確認済み」と書かない
- 見ていない画面を「動作確認済み」と書かない
- テスト結果を推測で書かない

---

## 6. タイトルを書く

タイトルは説明文の1行目にあたる。
履歴の一覧で単独で読まれる。
PR を開かなくても何をしたかわかる必要がある。

### ルール

- 命令形で書く (英語: `Add` `Fix` `Remove`。日本語: 「追加する」「修正する」)
- 何を変えたかを具体的に書く
- 対象 (クラス名、機能名) を入れる
- 50〜70 文字を目安にする
- 末尾にピリオドや句点を付けない
- 短いタグは先頭に付けてよい (例: `[Player]`)
- タグは1〜2個までにする

### Bad

```
Fix bug
バグ修正
Fix build
Add patch
Phase 1
Add convenience functions
WIP
Update PlayerController.cs
いろいろ修正
```

理由: 何のバグか、何をしたかがわからない。

### Good

```
Fix Player falling through floor at high speed by using continuous collision detection
DoorController の開閉イベントを System.Action で通知する
[UI] Remove unused HealthBar prefab and its references
Rename PlayerMover.Execute to MovePlayerTowardTarget
```

英語の動詞は命令形にする。

| Bad | Good |
|---|---|
| `Adding door event` | `Add door event` |
| `Fixed null reference` | `Fix null reference` |
| `Removes old UI` | `Remove old UI` |

---

## 7. 本文を書く

本文はタイトルに入らない情報を補う。
外部リンクだけに頼らない。
リンク先は未来の読者が見られない場合がある。
必要な文脈は本文に直接書く。

### テンプレート

リポジトリにテンプレートがない場合は次を使う。

```markdown
## 概要
<タイトルを1〜3文で補足する。何をする PR か>

## 背景 (Why)
<どんな問題があったか。なぜ今この変更が必要か>
<なぜこの方法を選んだか。検討して捨てた案があれば書く>

Closes #<Issue番号>

## 変更内容
- <変更1: ファイル名やクラス名を含めて具体的に>
- <変更2>

## テスト確認
- [x] <実際に実行して通ったもの>
- [ ] <未実行のもの。実行手順を書く>

## 欠点・既知の制約
<この方法の弱点、残した課題、将来の方向性。なければ「なし」>

## レビューの観点
<特に見てほしい箇所。読む順番。不要なら省略>
```

### 各セクションの書き方

**概要**

- 何をするかを書く
- 「本 PR では」などの前置きを書かない

**背景 (Why)**

- 最も重要なセクションである
- 解決する問題を書く
- この方法が最善である理由を書く
- バグなら再現条件を書く
- 性能改善なら計測値を書く (例: 「GC Alloc が 1 フレームあたり 2.1KB から 0B に減った」)
- Issue 番号を書く

**変更内容**

- 差分を読めばわかる行単位の説明は書かない
- 設計上の変更点を書く
- 公開 API の変更 (public メソッド名、引数) は必ず書く
- Unity のシリアライズされたフィールドの名前変更は必ず書く
- `[FormerlySerializedAs]` を付けたかを書く
- シーンやプレハブの変更は、何を変えたかを書く

**テスト確認**

- 実行したテストだけにチェックを付ける
- 手動確認は操作手順と期待結果を書く
- 見た目の変更はスクリーンショットか GIF の添付を求める
- 添付はユーザーに依頼する (Claude は添付しない)

**欠点・既知の制約**

- 欠点を隠さない
- レビュアーが気づく前に書く

### PR の種類別の書き方

**機能追加**

```markdown
## 概要
ドアの開閉を GameEvents 経由で通知し、効果音と UI が購読できるようにする。

## 背景 (Why)
DoorController が AudioSource と DoorUI を直接参照しており、ドアを増やすたびに参照の設定漏れが起きていた (#42)。
イベント経由にすると、DoorController は購読者を知らずに済む。
UnityEvent も検討したが、Inspector での設定漏れが同じく起きるため System.Action を選んだ。

Closes #42

## 変更内容
- `GameEvents` に `DoorOpened` / `DoorClosed` (System.Action) を追加
- `DoorController` から AudioSource と DoorUI への直接参照を削除
- `DoorSound` と `DoorUI` が OnEnable で購読し OnDisable で解除するよう変更

## テスト確認
- [x] EditMode テスト `GameEventsTests` が通過
- [ ] Play モードで Door_01 に近づき E キーで開閉し、音と UI 表示が出ることを確認

## 欠点・既知の制約
イベントの発火順序は購読順に依存する。順序が必要になった場合は別途対応する。
```

**リファクタリング**

```markdown
## 概要
PlayerMover.Execute を MovePlayerTowardTarget に改名する。挙動は変えない。

## 背景 (Why)
Execute という名前では何を実行するかわからず、呼び出し側を読む必要があった。
後続の PR (#58 ダッシュ機能) で同クラスにメソッドを追加する前に名前を整える。

## 変更内容
- `PlayerMover.Execute()` を `MovePlayerTowardTarget()` に改名
- 呼び出し箇所 3 ファイルを更新

## テスト確認
- [x] 既存の PlayMode テスト `PlayerMoverTests` が通過 (挙動の変化なし)

## 欠点・既知の制約
なし
```

**文脈が必要な小さい変更**

```markdown
## 概要
Rigidbody の Collision Detection を Continuous に変更する。

## 背景 (Why)
移動速度が 20 m/s を超えると、プレイヤーが厚さ 0.1m の床をすり抜ける (#61)。
Discrete では1フレームの移動量が床の厚さを超えると衝突を検出できない。
床を厚くする案は、既存レベル 12 個の修正が必要なため採用しなかった。

Closes #61

## 変更内容
- `Player.prefab` の Rigidbody.collisionDetectionMode を Continuous に変更

## テスト確認
- [ ] Play モードで速度 30 m/s で落下し、床で止まることを確認

## 欠点・既知の制約
Continuous は Discrete より物理演算のコストが高い。プレイヤー1体のみなので影響は小さいと判断した。
```

---

## 8. 提出前に見直す

PR 作成前に次のチェックリストをすべて確認する。

### 説明文

- [ ] タイトルだけで何をしたかわかる
- [ ] タイトルが命令形である
- [ ] タイトルが「Fix bug」型の曖昧な表現でない
- [ ] 背景 (Why) に問題と理由が書かれている
- [ ] 外部リンクがなくても理解できる
- [ ] 欠点や制約を書いた (なければ「なし」)
- [ ] 説明文が最新の差分と一致している (途中で方針が変わっていない)

### サイズと焦点

- [ ] PR が1つのことだけを扱う
- [ ] リファクタリングと機能変更が混ざっていない
- [ ] コードの変更行数が 400 行以下、または分割できない理由を書いた
- [ ] 新しい API に使用箇所がある

### テスト

- [ ] ロジック変更にテストがある、またはない理由を書いた
- [ ] 実行していないテストにチェックを付けていない

### Unity 固有

- [ ] `.meta` ファイルが対応するアセットと一緒にコミットされている
- [ ] 不要なシーンの差分 (誤って保存したもの) が含まれていない
- [ ] シリアライズフィールドの改名に `[FormerlySerializedAs]` を付けた
- [ ] `Library/` `Temp/` `Logs/` `UserSettings/` がコミットされていない
- [ ] コンパイルエラーと警告が増えていない

`.meta` の不足は次で確認する。

```bash
git diff --name-only --diff-filter=A "origin/$BASE"...HEAD -- 'Assets/*' \
  | grep -v '\.meta$' \
  | while read f; do git diff --name-only "origin/$BASE"...HEAD | grep -qx "$f.meta" || echo "missing meta: $f"; done
```

問題があれば PR を作らず、ユーザーに報告する。

---

## 9. gh pr create で作成する

ブランチを push する。

```bash
git push -u origin HEAD
```

本文はファイルに書いてから渡す。
コマンドライン引数に直接書くと、改行やバッククォートが壊れる。

```bash
cat > /tmp/pr-body.md <<'EOF'
## 概要
...

## 背景 (Why)
...
EOF

gh pr create \
  --base "$BASE" \
  --title "DoorController の開閉イベントを System.Action で通知する" \
  --body-file /tmp/pr-body.md
```

必要に応じて次を付ける。

| オプション | 使う場面 |
|---|---|
| `--draft` | テスト未完了、またはユーザーが下書きを望んだ |
| `--reviewer <user>` | ユーザーがレビュアーを指定した |
| `--assignee @me` | ユーザーが自分を担当にしたい |
| `--label <name>` | リポジトリに既存のラベルがある |

テスト確認に未チェック項目がある場合は `--draft` を提案する。

作成後、PR の URL をユーザーに返す。

```bash
gh pr view --json url --jq .url
```

---

## やってはいけないこと

- ユーザーの確認なしに force push する
- ユーザーの確認なしにコミットを並べ替える、まとめる
- 実行していないテストを「通過」と書く
- 差分にない変更を本文に書く
- 差分にある重要な変更を本文から省く
- 「Fix bug」「Update」「WIP」だけのタイトルを付ける
- リンクだけで背景を済ませる
