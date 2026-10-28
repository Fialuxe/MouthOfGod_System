<!-- このページは tools/api_doc/build_md.py が作ります。手で直さないでください。 -->
[入口](README.md) ・ [設計の考え方](design.md) ・ [つながりの図](diagrams.md) ・ [やりたいこと別の手順](recipes.md) ・ [ガイド](guide.md)

# MouthOfGod スクリプト リファレンス

`Assets/Scripts` の全スクリプトについて、API・名前空間・Inspector での見え方・機能どうしのつながりをまとめます。表は、いまのコードから作ります。

> `docs/api/` のページは、`tools/api_doc/build_md.py` がコードと説明文から作ります。手で直さないでください。型の説明は、コードの `///` と `[Tooltip]` を直します。章の文は `tools/api_doc/` の `template.md`・`design.md`・`recipes.md` を直します。直したら `python3 tools/api_doc/build_md.py` を実行します。

## 初めて読む人へ

コードの形には、理由があります。数人が並行して作り、シーンと Prefab は Git でマージできない、という前提から来ています。型を読む前に、[設計の考え方（なぜこの形か）](design.md) を読んでください。各型の「なぜこの形か」にも、その型に関わる理由が出ます。

| ページ | 中身 |
|---|---|
| [設計の考え方（なぜこの形か）](design.md) | 決まりと、その理由・あきらめたこと・生まれた型 |
| [つながりの図](diagrams.md) | 機能どうしのつながりと、実行時の流れの図 |
| [やりたいこと別の手順](recipes.md) | 生き物を元気にする、チュートリアルでイベントを待つ、食べ物を足す、など |
| [ガイド](guide.md) | 全体像、機能どうしのつなぎ方（置き場・合図・場面）、Main シーンの姿、メニュー、Resources |

## 機能（名前空間）

| 機能 | 役割 | 型の数 |
|---|---|---|
| [MouthOfGod.Core](Core.md) | 全体で共有する、小さな基盤です。他の asmdef に依存しません。ロジックは置かず、ID・イベントのデータ・interface・置き場だけを置きます。 | 23 |
| [MouthOfGod.Device](Device.md) | 触感デバイスの入力です。`MockDevice` が、デバイスなしで、Quest のコントローラやキーボードで「噛む・決定する」を代わりにします。実機は、`IDevice` を実装して `Devices.Register` するだけで差し替わります。 | 4 |
| [MouthOfGod.Fork](Fork.md) | 体験者が持つフォークです。雲を刺し、口に含み、決定で食べ終えるまでの状態を持ちます。 | 5 |
| [MouthOfGod.Food](Food.md) | 食感から食べ物を引いて、生み、前へ飛ばします。「食感 → 食べ物」の表は、全フィールド共通の表（`Field Id` が空）と、フィールドごとの表に分けます。 | 7 |
| [MouthOfGod.Village](Village.md) | フィールド（村・山・海・草原）の見た目の戻り方と、欲しい物です。物が落ちた場所の判定、届いたときの反応、立つ場所の判定も、ここにあります。 | 17 |
| [MouthOfGod.UI](UI.md) | 表示の部品です。Core のイベントと合図を受けます。 | 7 |
| [MouthOfGod.Miko](Miko.md) | 巫女です。浮いて、体験者の方を向き、しゃべり、表情を変え、指さし、体験者についてきます。身振りは Animator が担当します。 | 13 |
| [MouthOfGod.Tutorial](Tutorial.md) | プロローグとチュートリアル 1〜4 を進めます。1 チュートリアル = 1 つの `TutorialScript`（人形劇スクリプト）を、`TutorialRunner` が順に動かします。 | 24 |
| [MouthOfGod.Game](Game.md) | 本編の進行です。チュートリアルが終わったら本編を始め、制限時間が来たら終わらせます。 | 3 |
| [MouthOfGod.Tracking](Tracking.md) | AprilTag とハンドトラッキングで、位置を合わせます。ほかの機能には、`Transform` と追跡中かどうかだけを渡します。 | 12 |
| [MouthOfGod.CameraWork](CameraWork.md) | 付けたオブジェクト（カメラなど）を、決めた軌道と速さで動かします。直線・ベジェ曲線・円弧・待ちのカットをつなぎ、緩急・向き・ズームを Inspector で決めます。村のカットを置くメニューは、`MouthOfGod/CameraWork/Add Village Tour` と `Add Village Zoom-Out` です。 | 8 |
| [MouthOfGod.DevTools](DevTools.md) | Quest なしで試すための道具です。展示では使いません。 | 9 |

## すべての型

<details><summary>名前順の一覧を開く（132 個）</summary>

| 型 | 機能 | 役割 | 説明 |
|---|---|---|---|
| [`AnyWait`](Tutorial.md#t-anywait) | Tutorial | 待ち（yield return） | 渡した待ちのうち、どれかが終わるのを待つ。 |
| [`AprilTagDetectionService`](Tracking.md#t-apriltagdetectionservice) | Tracking | コンポーネント | パススルーカメラの RGB フレームから AprilTag を検出し、ワールド座標の姿勢を IAprilTagSource として公開する。 |
| [`AprilTagGeometry`](Tracking.md#t-apriltaggeometry) | Tracking | static（置き場・計算） | カメラの内部パラメータの換算と、座標変換。 |
| [`AprilTagObservation`](Tracking.md#t-apriltagobservation) | Tracking | データ | ある 1 フレームで検出された AprilTag 1 枚分の結果。 |
| [`AprilTagPlace`](Tracking.md#t-apriltagplace) | Tracking | コンポーネント | 指定した ID の AprilTag の位置に、対象の Transform（既定は自分自身）を合わせる。 |
| [`AprilTagRecalibrateInput`](Tracking.md#t-apriltagrecalibrateinput) | Tracking | コンポーネント | タグの位置の取り直しを、アプリ内の入力で行う。 |
| [`AprilTagSettings`](Tracking.md#t-apriltagsettings) | Tracking | 設定アセット | AprilTag 検出の設定。 |
| [`CameraCutPlayer`](CameraWork.md#t-cameracutplayer) | CameraWork | コンポーネント | 付けたオブジェクト（カメラなど）を、決めた軌道と速さで動かす。 |
| [`CameraEase`](CameraWork.md#t-cameraease) | CameraWork | 選択肢（enum） | 動きの緩急のつけ方。 |
| [`CameraPathMath`](CameraWork.md#t-camerapathmath) | CameraWork | static（置き場・計算） | カメラの軌道の計算。 |
| [`CameraShot`](CameraWork.md#t-camerashot) | CameraWork | データ | カメラの 1 カット。 |
| [`Cloud`](Fork.md#t-cloud) | Fork | コンポーネント | フォークで刺せる雲。 |
| [`CloudCues`](Fork.md#t-cloudcues) | Fork | コンポーネント | 雲の合図（Cues）を受けて、雲に見せる。 |
| [`CloudPrefabGenerator`](Fork.md#t-cloudprefabgenerator) | Fork | エディタ | 雲の Prefab を、白い球を重ねて Assets/Resources/Prefabs/Fork/Cloud.prefab に作る。 |
| [`CloudRing`](Fork.md#t-cloudring) | Fork | コンポーネント | 体験者の前に、雲（Cloud）を半円状に浮かべる。 |
| [`ConfirmWait`](Tutorial.md#t-confirmwait) | Tutorial | 待ち（yield return） | デバイスで決定する（Confirmed）のを待つ。 |
| [`CueId`](Core.md#t-cueid) | Core | データ | Tutorial から Village や UI への指示の ID。 |
| [`CueReaction`](UI.md#t-cuereaction) | UI | コンポーネント | 合図（Requested）を受けて、物を出す・消す・UnityEvent を呼ぶ。 |
| [`CueRequested`](Core.md#t-cuerequested) | Core | データ | Tutorial から Village や UI への指示（「この場所を光らせる」など）。 |
| [`Cues`](Core.md#t-cues) | Core | static（置き場・計算） | 合図（CueRequested）の置き場。 |
| [`Delivered`](Core.md#t-delivered) | Core | データ | 物がフィールドに届いた。 |
| [`DeliveryFx`](Village.md#t-deliveryfx) | Village | コンポーネント | 物が届いたときの、フィールドの標準の反応。 |
| [`DeliveryKind`](Core.md#t-deliverykind) | Core | 選択肢（enum） | 物がフィールドに届いたとき、そのフィールドにとって何だったか。 |
| [`DeliveryWait`](Tutorial.md#t-deliverywait) | Tutorial | 待ち（yield return） | フィールドに物が届くのを待つ。 |
| [`DeliveryZone`](Village.md#t-deliveryzone) | Village | コンポーネント | 食べ物が落ちる場所。 |
| [`DesktopHead`](DevTools.md#t-desktophead) | DevTools | コンポーネント | Quest なしで試すための頭。 |
| [`Devices`](Core.md#t-devices) | Core | static（置き場・計算） | いま使える IDevice の受け取り口。 |
| [`Entry`](UI.md#t-entry) | UI | データ | 1 つの合図への反応。 |
| [`FieldDiagnostics`](DevTools.md#t-fielddiagnostics) | DevTools | エディタ | Play 中に押すと、場所ごとに食べ物が引けているかを調べる材料を、Console に 1 回で出す。 |
| [`FieldId`](Core.md#t-fieldid) | Core | データ | フィールド（山・海・草原・村）の ID。 |
| [`Fields`](Core.md#t-fields) | Core | static（置き場・計算） | いまシーンにあるフィールド（IFieldRequests）の掲示板。 |
| [`FieldStages`](Village.md#t-fieldstages) | Village | コンポーネント | フィールド（山・海・草原・村）の戻り方と欲しい物。 |
| [`FieldStep`](Village.md#t-fieldstep) | Village | コンポーネント | フィールドの戻り方の 1 段（ステップ）。 |
| [`FoodIconGenerator`](Food.md#t-foodicongenerator) | Food | エディタ | 食べ物の Prefab を、遠近のあるカメラで斜め前から写し、背景が透明な PNG にする。 |
| [`FoodId`](Core.md#t-foodid) | Core | データ | 食べ物の ID。 |
| [`FoodPrefabFitter`](Food.md#t-foodprefabfitter) | Food | エディタ | 手で作った食べ物の Prefab（FoodPrefabGenerator を通っていないもの）の大きさを、その場で合わせる。 |
| [`FoodPrefabGenerator`](Food.md#t-foodprefabgenerator) | Food | エディタ | 食べ物の Prefab を、素材（FBX）から Assets/Resources/Prefabs/Food/ に作る。 |
| [`FoodRow`](Food.md#t-foodrow) | Food | データ | FoodTable の 1 行。 |
| [`FoodShadow`](UI.md#t-foodshadow) | UI | コンポーネント | 口に含んでいる間、「いまの食感なら生まれる物」の影（シルエット）を、霞の中に見せる（シナリオブック S4・S5）。 |
| [`FoodSpawner`](Food.md#t-foodspawner) | Food | コンポーネント | デバイスで決定した食感から、食べ物を引いて生み、前へ飛ばす。 |
| [`FoodSpawners`](Core.md#t-foodspawners) | Core | static（置き場・計算） | いま使える IFoodSpawner の受け取り口。 |
| [`FoodTable`](Food.md#t-foodtable) | Food | コンポーネント | 「食感 → 食べ物の Prefab」の表。 |
| [`FoodTables`](Food.md#t-foodtables) | Food | static（置き場・計算） | いまシーンにある FoodTable の置き場。 |
| [`ForkGoal`](Tutorial.md#t-forkgoal) | Tutorial | 選択肢（enum） | ForkWait が待つもの。 |
| [`Forks`](Core.md#t-forks) | Core | static（置き場・計算） | いま使える IFork の受け取り口。 |
| [`ForkTip`](Fork.md#t-forktip) | Fork | コンポーネント | 体験者が持つフォークの先。 |
| [`ForkWait`](Tutorial.md#t-forkwait) | Tutorial | 待ち（yield return） | フォークで雲を刺すのを待つ（Pierce）。 |
| [`GameClock`](Game.md#t-gameclock) | Game | クラス | 本編の制限時間の時計。 |
| [`GameRunner`](Game.md#t-gamerunner) | Game | コンポーネント | 本編の進行役。 |
| [`HandHeldPose`](Tracking.md#t-handheldpose) | Tracking | コンポーネント | 手（ハンドトラッキング）の位置に、オフセットを足して、手に持ったものの先の位置に自分を動かす。 |
| [`HandHeldTip`](Tracking.md#t-handheldtip) | Tracking | クラス | 手の姿勢から、手に持ったもの（フォークなど）の先の姿勢を出す。 |
| [`IAprilTagSource`](Tracking.md#t-iapriltagsource) | Tracking | interface | AprilTag の検出結果の供給元。 |
| [`IDevice`](Core.md#t-idevice) | Core | interface | 触感デバイスの入力。 |
| [`IFieldRequests`](Core.md#t-ifieldrequests) | Core | interface | 1 つのフィールドの欲しい物。 |
| [`IFoodItem`](Core.md#t-ifooditem) | Core | interface | 生まれた食べ物が持つ。 |
| [`IFoodSpawner`](Core.md#t-ifoodspawner) | Core | interface | 決定した食感から食べ物を生む所。 |
| [`IFork`](Core.md#t-ifork) | Core | interface | 体験者が持つフォーク。 |
| [`ImageIntrinsics`](Tracking.md#t-imageintrinsics) | Tracking | データ | 現在の画像（ピクセル）に換算した内部パラメータ。 |
| [`MainCopySetup`](DevTools.md#t-maincopysetup) | DevTools | エディタ | Main の写し（Main_Copy）を、本編まで通して試せる形にする。 |
| [`MainSeaSwapper`](DevTools.md#t-mainseaswapper) | DevTools | エディタ | 共有シーン（Main）の仮の海（Misc/Sea(Temporal)）を、完成版の海（Sea/Sea_work）に入れ替える（#329・#338）。 |
| [`MainSeaVillagerResizer`](DevTools.md#t-mainseavillagerresizer) | DevTools | エディタ | 共有シーン（Main）の海の村人の背の高さを、村の村人の平均に合わせる。 |
| [`MikoCueReceiver`](Miko.md#t-mikocuereceiver) | Miko | コンポーネント | 合図（Cues）のうち、名前が miko. で始まるものを受けて、巫女に身振りをさせる。 |
| [`MikoExpression`](Miko.md#t-mikoexpression) | Miko | 選択肢（enum） | 表情。 |
| [`MikoEye`](Miko.md#t-mikoeye) | Miko | 選択肢（enum） | 目の形（Miko_Eyes.png の絵の順番）。 |
| [`MikoFace`](Miko.md#t-mikoface) | Miko | コンポーネント | 巫女の表情を切り替える。 |
| [`MikoFloat`](Miko.md#t-mikofloat) | Miko | コンポーネント | 巫女を、人が宙に浮いているように見せる。 |
| [`MikoLine`](Miko.md#t-mikoline) | Miko | データ | 巫女のセリフ 1 つ。 |
| [`MikoLineBook`](Miko.md#t-mikolinebook) | Miko | コンポーネント | 巫女のセリフの一覧。 |
| [`MikoLineQueue`](Miko.md#t-mikolinequeue) | Miko | クラス | しゃべっている間に来たセリフの順番待ち。 |
| [`MikoMode`](Miko.md#t-mikomode) | Miko | 選択肢（enum） | 巫女の居場所の決め方。 |
| [`MikoMover`](Miko.md#t-mikomover) | Miko | コンポーネント | 巫女を動かす。 |
| [`MikoPointer`](Miko.md#t-mikopointer) | Miko | コンポーネント | 指さしの腕を、指さし先の方へ向ける。 |
| [`MikoPrefabGenerator`](Miko.md#t-mikoprefabgenerator) | Miko | エディタ | 巫女の Prefab と、身振りのアニメーション（浮いて待つ・指さす）を、Assets/Resources/Prefabs/Miko/ に作る。 |
| [`MikoSpeech`](Miko.md#t-mikospeech) | Miko | コンポーネント | 巫女にしゃべらせる。 |
| [`MockDevice`](Device.md#t-mockdevice) | Device | コンポーネント | デバイスなしで動かす IDevice。 |
| [`MockDeviceModel`](Device.md#t-mockdevicemodel) | Device | コンポーネント | MockDevice の雑な見た目。 |
| [`MockDevicePrefabGenerator`](Device.md#t-mockdeviceprefabgenerator) | Device | エディタ | MockDevice の Prefab を Assets/Resources/Prefabs/Device/ に作る。 |
| [`MockInputMode`](Device.md#t-mockinputmode) | Device | 選択肢（enum） | Mock を何で操作するか。 |
| [`MountDeliveryZoneAdder`](DevTools.md#t-mountdeliveryzoneadder) | DevTools | エディタ | 山の Main 用 Prefab（Misc/MountVer2 Variant）に、届ける場所（DeliveryZone）と、それを受けるトリガーの箱を足す。 |
| [`OpenAllRequestsAfterTutorial`](Tutorial.md#t-openallrequestsaftertutorial) | Tutorial | 試験用 | チュートリアルが終わったら、すべてのフィールドに、欲しい物をいっぺんに出す仮のスクリプト。 |
| [`OpenAllRequestsMenu`](DevTools.md#t-openallrequestsmenu) | DevTools | エディタ | Play 中に使うメニュー。 |
| [`PhaseId`](Core.md#t-phaseid) | Core | データ | いまの場面（チュートリアルの名前、本編、エンディングなど）の ID。 |
| [`Phases`](Core.md#t-phases) | Core | static（置き場・計算） | いまの場面（チュートリアルの名前、本編、エンディングなど）の置き場。 |
| [`PoseCalibrator`](Tracking.md#t-posecalibrator) | Tracking | クラス | 複数の観測を集め、外れ値を除いて平均した 1 つの姿勢を返す。 |
| [`PoseTracker`](Tracking.md#t-posetracker) | Tracking | クラス | 1 枚のタグの追跡状態。 |
| [`PrologueVideo`](Tutorial.md#t-prologuevideo) | Tutorial | コンポーネント | プロローグ（シナリオブック S1）。 |
| [`RemainingTimeText`](Game.md#t-remainingtimetext) | Game | コンポーネント | GameRunner の残り秒を、指定した TMP_Text に書く最小の表示部品。 |
| [`RequestBubbles`](UI.md#t-requestbubbles) | UI | コンポーネント | 村人の欲しい物を、頭の上の吹き出しで見せる（#59・#270）。 |
| [`RequestBubbleSettings`](UI.md#t-requestbubblesettings) | UI | 設定アセット | 村人の吹き出しの見た目の設定。 |
| [`RequestBubbleText`](UI.md#t-requestbubbletext) | UI | static（置き場・計算） | 吹き出しに出す文字を作る純粋な部品（Unity に依存しない）。 |
| [`RequestClosed`](Core.md#t-requestclosed) | Core | データ | 出ていた欲しい物が終わった（吹き出しを消す合図）。 |
| [`RequestIssued`](Core.md#t-requestissued) | Core | データ | フィールドの困っている人が、欲しい物を出した（吹き出しを出す合図）。 |
| [`RequestOrder`](Village.md#t-requestorder) | Village | 選択肢（enum） |  |
| [`ShadowMode`](UI.md#t-shadowmode) | UI | 選択肢（enum） | 影の見せ方。 |
| [`ShotAim`](CameraWork.md#t-shotaim) | CameraWork | 選択肢（enum） | 動いている間の向きの決め方。 |
| [`ShotKind`](CameraWork.md#t-shotkind) | CameraWork | 選択肢（enum） | 1 カットの軌道の種類。 |
| [`ShotPath`](CameraWork.md#t-shotpath) | CameraWork | クラス | 1 カットの軌道。 |
| [`SkipPopIn`](Village.md#t-skippopin) | Village | コンポーネント | ステップの子の物を、このスクリプトで「ぽんっ」と出さないようにする印。 |
| [`Spawned`](Core.md#t-spawned) | Core | データ | 食べ物が生まれた。 |
| [`SpawnWait`](Tutorial.md#t-spawnwait) | Tutorial | 待ち（yield return） | 食べ物が生まれるのを待つ（Spawned）。 |
| [`StandArea`](Village.md#t-standarea) | Village | コンポーネント | 体験者が、そのフィールドの前に立っているかを判定する場所。 |
| [`Standing`](Core.md#t-standing) | Core | static（置き場・計算） | 体験者がいま前に立っているフィールドの置き場。 |
| [`StandWait`](Tutorial.md#t-standwait) | Tutorial | 待ち（yield return） | 体験者が、あるフィールドの前に立つのを待つ（Standing）。 |
| [`StepProgress`](Village.md#t-stepprogress) | Village | クラス | フィールド 1 つの、ステップ（戻り方の 1 段）の進み具合。 |
| [`SurfaceAnchor`](Village.md#t-surfaceanchor) | Village | コンポーネント | フィールドの Prefab の一番上に付ける。 |
| [`T1Fork`](Tutorial.md#t-t1fork) | Tutorial | コンポーネント | チュートリアル 1「フォークの使い方」（シナリオブック S3）。 |
| [`T2Spawn`](Tutorial.md#t-t2spawn) | Tutorial | コンポーネント | チュートリアル 2「物が生まれる」（シナリオブック S4。 |
| [`T3Recognize`](Tutorial.md#t-t3recognize) | Tutorial | コンポーネント | チュートリアル 3「食感で変わる・欲しい物」（シナリオブック S5。 |
| [`T4Deliver`](Tutorial.md#t-t4deliver) | Tutorial | コンポーネント | チュートリアル 4「届ける」（シナリオブック S6）。 |
| [`TextureChangeWait`](Tutorial.md#t-texturechangewait) | Tutorial | 待ち（yield return） | 噛んでいる間に、食感が count 回切り替わるのを待つ（TextureChanged）。 |
| [`TextureId`](Core.md#t-textureid) | Core | データ | 食感の論理 ID。 |
| [`TutorialAutoStart`](Tutorial.md#t-tutorialautostart) | Tutorial | 試験用 | Play を始めてしばらくしたら、同じ GameObject の TutorialRunner を動かす。 |
| [`TutorialDebugHud`](DevTools.md#t-tutorialdebughud) | DevTools | コンポーネント | Quest なしで試すときの表示。 |
| [`TutorialEvent`](Tutorial.md#t-tutorialevent) | Tutorial | 選択肢（enum） | TutorialHistory が記録すること。 |
| [`TutorialHistory`](Tutorial.md#t-tutorialhistory) | Tutorial | クラス | チュートリアルが始まってから、体験者がしたことの記録。 |
| [`TutorialMemory`](Tutorial.md#t-tutorialmemory) | Tutorial | クラス | チュートリアルのあいだで引き継ぐ「覚えたこと」。 |
| [`TutorialPrefabGenerator`](Tutorial.md#t-tutorialprefabgenerator) | Tutorial | エディタ | プロローグとチュートリアル 1〜4 の Prefab を、Assets/Resources/Prefabs/Tutorial/ に作る。 |
| [`TutorialRunner`](Tutorial.md#t-tutorialrunner) | Tutorial | コンポーネント | 子の TutorialScript（人形劇スクリプト）を、Hierarchy の並び順に 1 つずつ動かす。 |
| [`TutorialScript`](Tutorial.md#t-tutorialscript) | Tutorial | コンポーネント | 人形劇スクリプトの土台。 |
| [`TutorialTestSceneBuilder`](DevTools.md#t-tutorialtestscenebuilder) | DevTools | エディタ | Quest なしで、プロローグとチュートリアル 1〜4 を通して試すシーンを作る。 |
| [`TutorialWait`](Tutorial.md#t-tutorialwait) | Tutorial | 待ち（yield return） | 人形劇スクリプトで yield return して待つもの。 |
| [`VideoScreen`](Tutorial.md#t-videoscreen) | Tutorial | コンポーネント | VR で動画を見せる画面。 |
| [`VillageBaker`](Village.md#t-villagebaker) | Village | エディタ | 下書きの配置（VillageLayout）から、村のフィールドの Prefab（Village.prefab）を作る。 |
| [`VillageField`](Village.md#t-villagefield) | Village | エディタ |  |
| [`VillageFxGenerator`](Village.md#t-villagefxgenerator) | Village | エディタ | 村の演出（煙・喜びの粒）の Prefab を、Assets/Resources/Prefabs/Village/ に作る。 |
| [`VillageItem`](Village.md#t-villageitem) | Village | エディタ | フィールドの 1 つの置き物（下書き）。 |
| [`VillageLayout`](Village.md#t-villagelayout) | Village | エディタ | 村の配置の下書き。 |
| [`VillagePrefabGenerator`](Village.md#t-villageprefabgenerator) | Village | エディタ | 村の置き物の Prefab を、素材（FBX）から Assets/Resources/Prefabs/Village/ に作る。 |
| [`VillagerMood`](Village.md#t-villagermood) | Village | コンポーネント | 村人の気持ち。 |
| [`VillageStep`](Village.md#t-villagestep) | Village | エディタ | 戻り方の 1 段（下書き）。 |
| [`VillageTourBuilder`](CameraWork.md#t-villagetourbuilder) | CameraWork | エディタ | 草原から村へ引いていく「村の一連」のカットを、開いているシーンに組む。 |
| [`VillageTutorialProbe`](Tutorial.md#t-villagetutorialprobe) | Tutorial | 試験用 | 村だけで、チュートリアルを進める仕組みが動くか確かめる仮のスクリプト。 |

</details>

## この文書について

- このページの第 1 章は全体の作りです。第 2 章は、機能どうしがどうつながるかです。第 3 章は、共有シーン Main の中身です。付録に、メニューとファイルの置き場所があります。機能ごとの型の一覧は、機能ごとのページにあります。
- 各型の表は、公開されている `public`・`protected` のメンバーだけを載せます。`internal` はテスト用の入口なので載せません。
- 「Inspector」の表は、Unity が Inspector に出す項目です。項目名は、Unity が変数名から作る表示名です。`[Header]` は太字の行で示します。
- 確かめたこと: 型・メンバー・既定値・説明は、コードから機械的に取り出しています。Main シーンの位置は、`Main.unity` と入れ子の Prefab の YAML から計算しました。
- 確かめていないこと: Unity でコンパイルも実行もしていません。Mesh の見た目の大きさは読んでいないので、第 3 章の位置は、オブジェクトの原点です。手順のコード例も、コンパイルして確かめてはいません。

コードや説明文を直したら、`python3 tools/api_doc/build_md.py` を実行して、このページを作り直します。作り直し忘れは、PR の検査（API docs check）が警告します。検索つきの 1 ファイルの HTML が欲しいときは、`python3 tools/api_doc/build_html.py` で `tools/api_doc/out/index.html` に作れます（リポジトリには入れません）。
