# DevTools

Quest なしで、エディタの Play だけでプロローグとチュートリアル 1〜4 を試すための道具です。展示では使いません。

## テスト用のシーン

シーンは `Assets/Scenes/Sandbox/tutorial_test.unity` です。開いて Play すると、1 秒後にチュートリアルが始まります。

シーンは、メニュー **MouthOfGod > Dev > Build Tutorial Test Scene** で作り直せます。作り直すと、手で直した所は消えます。部品の作りが変わったときは、作り直してください。

シーンには、次の物を置きます。

| 物 | 中身 |
|---|---|
| Main Camera | 目の高さ 1.6 m の頭です。`DesktopHead` で動きます。子の `MouthAnchor` が口の位置です |
| ForkTip | フォークの先です。手の代わりに、頭の右下前に固定しています |
| CloudRing | 刺す雲です。合図で光る `CloudCues` も付いています |
| FoodSpawner | 食べ物を生む装置と、共通の表です |
| Field_village / Field_sea | 村と海の台です。届く範囲と、欲しい物のステップがあります。子の `Glow`・`Cheer`・`ShakeHead` は、合図で出る仮の印です（下の表） |
| Stand_village / Stand_sea | 立つ場所（`StandArea`）と足あとの印です。村は頭のはじめの位置、海はその右 3 m です |
| Miko | 巫女です。チュートリアルのセリフは、各チュートリアルの Prefab に入っています |
| FoodShadow | 霞の中の影です |
| Indicators | スタッフへの知らせ（赤い箱）と、ボタンの光（黄色い球）です |
| TutorialRunner | プロローグとチュートリアル 1〜4 の Prefab を置いています。Prefab を直せば、シーンを作り直さなくても反映されます。動画は、`Prologue` の Prefab で指定したファイル（今は `Assets/StreamingAssets/Begin_KeltPiano.mp4`）を流します。ファイルが無ければ、プロローグはすぐ終わります |
| DebugHud | Game ビューの左上の表示です |

フィールドの仮の印は、`CueReaction` が合図を受けて出します。本番のフィールドでは、各フィールドの担当者が見せ方を決めます。

| 印 | 出る合図 | 見た目 |
|---|---|---|
| `Glow` | `<field>.glow` で出て、`<field>.glow-off` で消えます | 台の上の黄色い円盤 |
| `Cheer` | `<field>.cheer` で 2 秒出ます | 台の上の緑の球 |
| `ShakeHead` | `<field>.shake-head` で 1.5 秒出ます | 台の上の赤い箱 |

食感は、`MockDevice` の 3 つをそのまま使います。食べ物は次のように割り当てています。

| 食感 | 食べ物 | 噛み始めてから |
|---|---|---|
| `placeholder-a` | リンゴ | すぐ |
| `placeholder-b` | 魚 | 1.5 秒後 |
| `placeholder-c` | ニンジン | 3 秒後 |

## 操作

Game ビューをクリックしてから操作します。

| キー | 操作 |
|---|---|
| W A S D | 歩きます。Shift を押している間は速く歩きます |
| マウスの右ボタンを押しながら動かす | 向きを変えます |
| P | 雲を刺します |
| M（押している間） | フォークの先を口に入れます |
| Space（押している間） | 噛みます。押している間、食感が順に変わります |
| Enter | 決定します。いまの食感の食べ物が生まれます |

左上の表示のボタンでも操作できます。
- **Begin / Restart**: チュートリアルを最初から始めます。
- **Stop**: チュートリアルを止めます。
- **Go Stand_village / Go Stand_sea**: その立つ場所へ移ります。

## 通しで試す手順

1. Play します。プロローグが終わると、チュートリアル 1 が始まります。
2. チュートリアル 1: P で刺し、M を押したまま Space を 4 秒ほど押します。食感が 3 回変わったら、Enter で決定します。
3. チュートリアル 2: P で刺し、M を押したままにします。Space を一瞬押してリンゴの食感にし、Enter で決定します。
4. チュートリアル 3: Space を 2 秒ほど押して魚の食感にし、Enter で決定します。
5. チュートリアル 4: まず村のまま魚を生むと、村が首を振ります。「Go Stand_sea」で海へ移り、もう一度魚を生むと終わります。

左上の表示で、いまの場面、立っている場所、覚えたこと、最近の合図を確かめられます。

Quest がつながっていないと、OpenXR の初期化のエラーが Console に出ます。これは XR の初期化が失敗したという知らせで、チュートリアルの動きには関係しません。

## 部品

| 部品 | 付ける所 | 役割 |
|---|---|---|
| `DesktopHead` | Main Camera | キーボードとマウスで歩き、見回します。高さは変えません。Quest の頭が動いているときは何もしません |
| `TutorialDebugHud` | シーンのどこか | 状態の表示と、始める・止める・移るボタンです。Core の置き場と `TutorialRunner` だけを読みます |
| `TutorialTestSceneBuilder` | （エディタのメニュー） | テスト用のシーンを作ります。バッチモードでは `-executeMethod MouthOfGod.DevTools.Editor.TutorialTestSceneBuilder.Build` で作れます |

チュートリアルの Prefab がまだ無いときは、`TutorialTestSceneBuilder` が先に作ります（`Tutorial/Editor/TutorialPrefabGenerator.cs`）。
