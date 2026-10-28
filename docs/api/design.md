<!-- このページは tools/api_doc/build_md.py が作ります。手で直さないでください。 -->
[入口](README.md) ・ [設計の考え方](design.md) ・ [つながりの図](diagrams.md) ・ [やりたいこと別の手順](recipes.md) ・ [ガイド](guide.md)

このページは、コードが「なぜこの形なのか」をまとめます。型のページの「なぜこの形か」は、ここから引いています。

前提が 3 つあります。どの決まりも、この 3 つから出ています。

- **数人が、同じ時期に、別々の物を作ります。** 山・海・草原・村は別々の担当者が作ります。チュートリアルも、担当者が並行して作ります。
- **Unity の `.unity`（シーン）・`.prefab`・`.asset` は、Git でマージできません。** 2 人が同じファイルを直すと、どちらかの作業を捨てることになります。そのため、同じファイルを直す作業は、順番待ちになります。
- **締切があります。** GDC の応募は 10/30 です。あとから直すと全体の作り直しになる所だけは、最初から守ります。

決めた日と経緯の全文は、[docs/decisions.md](https://github.com/Fialuxe/MouthOfGod_System/blob/main/docs/decisions.md) と [docs/architecture.md](https://github.com/Fialuxe/MouthOfGod_System/blob/main/docs/architecture.md) にあります。

## 決まりの一覧

1. [作業の単位と、ファイルの単位をそろえる](#d-files)
2. [中央の進行役を作らない](#d-nocenter)
3. [置き場に自分で登録する（組み立て役を置かない）](#d-registry)
4. [機能どうしは Core だけを通してつながる](#d-core)
5. [ID は文字列にする（enum にしない）](#d-ids)
6. [チュートリアルは合図を出し、見せ方は受け手が決める](#d-cues)
7. [チュートリアルは、コルーチンの人形劇スクリプトで書く](#d-puppet)
8. [チュートリアルは 1 本ずつ Prefab にし、セリフもその中に持つ](#d-tutprefab)
9. [チュートリアルの間の引き継ぎは、名前だけで行う](#d-memory)
10. [フィールドの戻り方は Hierarchy の並びで持ち、いつ出すかは Game・Tutorial が決める](#d-steps)
11. [何が届いても受け取り、何が届いたかを知らせる](#d-receive)
12. [1 つのフィールドに、欲しい物を同時に複数出す](#d-multi)
13. [食感 → 食べ物の対応は、Prefab に付ける表で持つ](#d-foodtable)
14. [生まれた物は、体験者が狙って飛ばす](#d-aim)
15. [デバイスがなくても、全部が動く](#d-mock)
16. [Play の前に、手作業を要らなくする](#d-noprep)
17. [計算は Unity から切り離して、テストできるようにする](#d-pure)
18. [共有シーンは 1 つにし、動画も同じシーンで流す](#d-scene)
19. [カメラワークは、どの物にも付けられる独立した部品にする](#d-camera)

---

<a id="d-files"></a>

## 作業の単位と、ファイルの単位をそろえる

**決めたこと**

1 人が直す物は、1 つのファイルに収めます。チュートリアルは 1 本につき、スクリプト 1 つと Prefab 1 つです。フィールドは、担当者ごとに Prefab 1 つです。食べ物の表は、フィールドの Prefab ごとに付けます。

**なぜ**

シーンや Prefab は、Git でマージできないからです。2 人が同じファイルを直すと、片方の作業が消えます。ファイルが分かれていれば、担当者は自分のファイルだけを直せます。ほかの人を待たずに、並行して作れます。

**あきらめたこと**

全体を 1 つのファイルで見渡すことは、あきらめました。たとえば、セリフの英文を 1 か所でまとめて見直すことはできません。

**読む人へ**

自分の担当のファイル以外を直す変更は、分け方を見直す合図です。PR が同じファイルでぶつかったら、分け方の問題として扱います。

**この決まりから生まれた型**: [`TutorialScript`](Tutorial.md#t-tutorialscript)、[`PrologueVideo`](Tutorial.md#t-prologuevideo)、[`T1Fork`](Tutorial.md#t-t1fork)、[`T2Spawn`](Tutorial.md#t-t2spawn)、[`T3Recognize`](Tutorial.md#t-t3recognize)、[`T4Deliver`](Tutorial.md#t-t4deliver)、[`MikoLineBook`](Miko.md#t-mikolinebook)、[`FoodTable`](Food.md#t-foodtable)、[`FieldStep`](Village.md#t-fieldstep)、[`FieldStages`](Village.md#t-fieldstages)

出典: docs/architecture.md「1. 同じファイルを取り合わない」

---

<a id="d-nocenter"></a>

## 中央の進行役を作らない

**決めたこと**

`GameManager` や `GameFlow` のような、全部を知っている進行役は作りません。チュートリアルを進める [`TutorialRunner`](Tutorial.md#t-tutorialrunner) は、どのチュートリアルでも同じコードです。中身の流れは、チュートリアルごとのスクリプトに書きます。本編の [`GameRunner`](Game.md#t-gamerunner) は、チュートリアルの型を知りません。場面（[`Phases`](Core.md#t-phases)）が `deliver` から空に変わったのを見て、本編を始めます。

**なぜ**

進行役が全部を持つと、何かを足すたびに、全員がその 1 つのファイルを直します。作業が順番待ちになります。1 か所の変更が、ほかの全部を壊すこともあります。

**あきらめたこと**

「いま何がどう動いているか」を 1 ファイルで読めることは、あきらめました。流れを追うときは、このリファレンスの「機能どうしのつなぎ方」を読みます。

**この決まりから生まれた型**: [`TutorialRunner`](Tutorial.md#t-tutorialrunner)、[`GameRunner`](Game.md#t-gamerunner)、[`Phases`](Core.md#t-phases)

出典: docs/decisions.md 2026-10-13・2026-10-15、docs/architecture.md「6. 避けること」

---

<a id="d-registry"></a>

## 置き場に自分で登録する（組み立て役を置かない）

**決めたこと**

機能をまたいで使う「もの」（デバイス・フォーク・フィールドなど）は、有効になったときに、Core の置き場へ自分で登録します。使う側は、置き場から受け取ります。置き場は [`Devices`](Core.md#t-devices)・[`Forks`](Core.md#t-forks)・[`FoodSpawners`](Core.md#t-foodspawners)・[`Fields`](Core.md#t-fields)・[`Standing`](Core.md#t-standing)・[`Phases`](Core.md#t-phases)・[`Cues`](Core.md#t-cues) だけです。全部の機能を参照して組み立てる役（Bootstrap）は置きません。

**なぜ**

4 つのフィールドは、4 人が別々に作り、最後に 1 つのシーンへまとめます。登録する形なら、作ったフィールドは、シーンに置くだけで本編・チュートリアル・吹き出しにつながります。担当者は、自分のサンドボックスのシーンで、1 人で試せます。組み立て役がいると、機能を足すたびに全員がそのファイルを直します。組み立て役ができるまで、誰もつないで動かせません。

**あきらめたこと**

- 組み立て役（#34 の案）: つながりを 1 ファイルで見渡せます。しかし、`GameManager` と同じ問題が起きます。
- イベントバスや、ScriptableObject のイベントチャンネル: 結合はいちばん弱くなります。しかし、誰が出して誰が受けるかを追えません。`Open()` の戻り値のような返事や、「`Issued` と `Closed` が交互に来る」といった順番の約束を守らせにくいです。チャンネルを `.asset` にすると、Git でぶつかります。

**読む人へ**

使う側は `WhenReady` で受け取ります。後から登録されても届きます。消える側は `OnDisable` で `CancelWhenReady` を呼びます。置き場は static なので、Play のたびに空にします。置き場を足すのは、機能をまたぐ「もの」が増えたときだけです。

**この決まりから生まれた型**: [`Devices`](Core.md#t-devices)、[`Forks`](Core.md#t-forks)、[`FoodSpawners`](Core.md#t-foodspawners)、[`Fields`](Core.md#t-fields)、[`Standing`](Core.md#t-standing)、[`Phases`](Core.md#t-phases)、[`Cues`](Core.md#t-cues)、[`MockDevice`](Device.md#t-mockdevice)、[`ForkTip`](Fork.md#t-forktip)、[`FoodSpawner`](Food.md#t-foodspawner)、[`FieldStages`](Village.md#t-fieldstages)、[`StandArea`](Village.md#t-standarea)

出典: docs/decisions.md 2026-10-15「全部をつなぐ組み立て役（Bootstrap）は置かず、Core の置き場に自分で登録する」

---

<a id="d-core"></a>

## 機能どうしは Core だけを通してつながる

**決めたこと**

Device・Fork・Food・Village・UI・Miko・Tutorial・Game は、互いのクラスを参照しません。参照してよいのは Core だけです。Core には、ID・イベントのデータ・interface・置き場だけを置き、ロジックは置きません。asmdef の `references` で向きを決め、`check_coupling.py` が直接参照を PR に警告します。

**なぜ**

機能どうしが絡み合うと、1 か所の変更が全体を壊し、作業が止まります。Core の interface を通せば、相手の中身を差し替えても、使う側は直さずに済みます。たとえば、フィールドを [`FieldStages`](Village.md#t-fieldstages) とは違う作りにしても、[`IFieldRequests`](Core.md#t-ifieldrequests) を実装すれば、本編にそのままつながります。

**読む人へ**

別の機能の型が欲しくなったら、その型を直接使わずに、Core の interface か合図で受け取れないかを考えます。

**この決まりから生まれた型**: [`IDevice`](Core.md#t-idevice)、[`IFork`](Core.md#t-ifork)、[`IFoodSpawner`](Core.md#t-ifoodspawner)、[`IFoodItem`](Core.md#t-ifooditem)、[`IFieldRequests`](Core.md#t-ifieldrequests)、[`Spawned`](Core.md#t-spawned)、[`Delivered`](Core.md#t-delivered)、[`RequestIssued`](Core.md#t-requestissued)、[`RequestClosed`](Core.md#t-requestclosed)、[`CueRequested`](Core.md#t-cuerequested)

出典: docs/architecture.md「2. 機能と依存の向き」、docs/decisions.md 2026-10-13

---

<a id="d-ids"></a>

## ID は文字列にする（enum にしない）

**決めたこと**

食感・食べ物・フィールド・合図・場面の ID は、文字列を包んだ型にします。`enum` にはしません。

**なぜ**

`enum` にすると、食感や食べ物を 1 つ足すたびに、Core を直すことになります。全機能が再コンパイルされ、素材の担当者は自分で足せません。文字列なら、Prefab のファイル名を変えるだけで食べ物を足せます。ただの `string` にせず型で包むのは、食べ物の ID を食感の ID として渡す、といった取り違えをコンパイルで止めるためです。

**あきらめたこと**

打ち間違いを、コンパイルでは見つけられません。代わりに、起動時の確かめ（`FoodTables.Validate` など）で警告を出します。

**この決まりから生まれた型**: [`TextureId`](Core.md#t-textureid)、[`FoodId`](Core.md#t-foodid)、[`FieldId`](Core.md#t-fieldid)、[`CueId`](Core.md#t-cueid)、[`PhaseId`](Core.md#t-phaseid)

出典: docs/decisions.md 2026-10-13、CONTRIBUTING「型の名前とデータの置き場所」

---

<a id="d-cues"></a>

## チュートリアルは合図を出し、見せ方は受け手が決める

**決めたこと**

チュートリアルは「ここを光らせる」「巫女にこれを言わせる」を、合図（`Cues.Request`）で頼むだけです。光らせ方・しゃべり方は、合図を受けた側（フィールド・巫女・UI）が決めます。合図の名前は、受け手の名前から始めます（`sea.glow`、`miko.say.<id>`）。

**なぜ**

チュートリアルの担当者は流れだけを、フィールドの担当者は見せ方だけを直せるようにするためです。チュートリアルが Village や Miko のクラスを直接呼ぶと、依存の向きが崩れ、片方だけを直せなくなります。

**あきらめたこと**

合図を受けた側がいなくても、エラーにはなりません。合図の名前を打ち間違えると、何も起きないだけです。[`TutorialDebugHud`](DevTools.md#t-tutorialdebughud) に、最近の合図が出るので、そこで確かめます。

**この決まりから生まれた型**: [`Cues`](Core.md#t-cues)、[`CueId`](Core.md#t-cueid)、[`CueRequested`](Core.md#t-cuerequested)、[`CueReaction`](UI.md#t-cuereaction)、[`CloudCues`](Fork.md#t-cloudcues)、[`MikoCueReceiver`](Miko.md#t-mikocuereceiver)、[`FoodShadow`](UI.md#t-foodshadow)、[`StandArea`](Village.md#t-standarea)、[`TutorialScript`](Tutorial.md#t-tutorialscript)

出典: docs/decisions.md 2026-10-19

---

<a id="d-puppet"></a>

## チュートリアルは、コルーチンの人形劇スクリプトで書く

**決めたこと**

チュートリアル 1 本につき、[`TutorialScript`](Tutorial.md#t-tutorialscript) を継いだクラスを 1 つ書きます。流れはコルーチンで書き、合図を出し、待ち（`WaitForPierce` など）で待ち、分岐します。[`TutorialRunner`](Tutorial.md#t-tutorialrunner) が、Hierarchy の並び順に 1 本ずつ動かします。

**なぜ**

チュートリアルは約 8 分の一本道です。シナリオブックと同じ順に、1 か所で流れを読めるほうが直しやすいからです。待ちは、待ち始める前に済んだ操作（先回り）も数えます。早く進める体験者でも、止まらずに進みます。

**あきらめたこと**

- 手順をデータ（`TutorialStep`）で並べる案: Inspector で並べられます。しかし、待てる条件が決まった種類に限られます。
- チュートリアルとフィールドの組み合わせごとにスクリプトを書く案（4 × 4 で 16 個）: 空の組み合わせが多く、流れを直すたびに複数の担当のファイルを直すことになります。

**この決まりから生まれた型**: [`TutorialScript`](Tutorial.md#t-tutorialscript)、[`TutorialRunner`](Tutorial.md#t-tutorialrunner)、[`TutorialWait`](Tutorial.md#t-tutorialwait)、[`DeliveryWait`](Tutorial.md#t-deliverywait)、[`ConfirmWait`](Tutorial.md#t-confirmwait)、[`ForkWait`](Tutorial.md#t-forkwait)、[`SpawnWait`](Tutorial.md#t-spawnwait)、[`TextureChangeWait`](Tutorial.md#t-texturechangewait)、[`StandWait`](Tutorial.md#t-standwait)、[`AnyWait`](Tutorial.md#t-anywait)、[`T1Fork`](Tutorial.md#t-t1fork)、[`T2Spawn`](Tutorial.md#t-t2spawn)、[`T3Recognize`](Tutorial.md#t-t3recognize)、[`T4Deliver`](Tutorial.md#t-t4deliver)、[`PrologueVideo`](Tutorial.md#t-prologuevideo)

出典: docs/decisions.md 2026-10-19

---

<a id="d-tutprefab"></a>

## チュートリアルは 1 本ずつ Prefab にし、セリフもその中に持つ

**決めたこと**

プロローグとチュートリアル 1〜4 は、1 本ずつ Prefab（`Resources/Prefabs/Tutorial/`）にします。共有シーンには、その Prefab を [`TutorialRunner`](Tutorial.md#t-tutorialrunner) の子として置くだけです。そのチュートリアルのセリフは、その Prefab に付けた [`MikoLineBook`](Miko.md#t-mikolinebook) に書きます。

**なぜ**

チュートリアルは、担当者が並行して作ります。セリフが巫女の 1 つの一覧にあると、全員が同じ一覧を直すことになり、順番待ちになります。チュートリアルが共有シーンに直接あると、直すたびに Scene Fusion のセッションが要ります。Prefab に分ければ、担当者は自分の Prefab だけを直せます。

**あきらめたこと**

- シーンの物（指さす先や行き先）は、Prefab に入れられません。これだけは、シーンに置いた Prefab の上で入れます。
- セリフの id は、シーン全体で重ならないようにする必要があります。重なると、Play の始めに警告が出ます。

**この決まりから生まれた型**: [`MikoLineBook`](Miko.md#t-mikolinebook)、[`MikoLine`](Miko.md#t-mikoline)、[`T1Fork`](Tutorial.md#t-t1fork)、[`T2Spawn`](Tutorial.md#t-t2spawn)、[`T3Recognize`](Tutorial.md#t-t3recognize)、[`T4Deliver`](Tutorial.md#t-t4deliver)、[`PrologueVideo`](Tutorial.md#t-prologuevideo)、[`TutorialRunner`](Tutorial.md#t-tutorialrunner)

出典: docs/decisions.md 2026-10-25

---

<a id="d-memory"></a>

## チュートリアルの間の引き継ぎは、名前だけで行う

**決めたこと**

前のチュートリアルで覚えたことは、名前（`fork-learned` など）だけを [`TutorialMemory`](Tutorial.md#t-tutorialmemory) に書きます。次のチュートリアルは、その名前があるかだけを見ます。名前は `docs/progress-keys.md` の 1 か所に書きます。

**なぜ**

T3 のコードが「T2 では」と書くと、T2 を直したときに T3 が壊れます。名前だけを見れば、T2 の中身を変えても、名前を変えなければ T3 は壊れません。名前は「事実」にします（`t2-done` ではなく `food-born`）。チュートリアルの順番を変えても、意味が変わらないようにするためです。

**この決まりから生まれた型**: [`TutorialMemory`](Tutorial.md#t-tutorialmemory)、[`TutorialRunner`](Tutorial.md#t-tutorialrunner)、[`TutorialScript`](Tutorial.md#t-tutorialscript)

出典: docs/decisions.md 2026-10-19、docs/progress-keys.md

---

<a id="d-steps"></a>

## フィールドの戻り方は Hierarchy の並びで持ち、いつ出すかは Game・Tutorial が決める

**決めたこと**

フィールドの戻り方は、`Steps` の下に並べた [`FieldStep`](Village.md#t-fieldstep) で持ちます。順番は Hierarchy の並び順で、番号は持ちません。いつ・どのフィールドに欲しい物を出すかは、フィールドではなく、本編（Game）とチュートリアル（Tutorial）が [`IFieldRequests`](Core.md#t-ifieldrequests) 越しに決めます。

**なぜ**

4 つのフィールドを別々の人が作り、それでも全体が調和して動くようにするためです。フィールドの担当者は、自分の Prefab だけを触り、時間や他のフィールドのことを考えずに済みます。出す順番と間隔は 1 か所で決めるので、4 つがばらばらに動きません。段階を途中に挟む・並べ替えるのは、Hierarchy の操作だけで済みます。

**あきらめたこと**

段階を番号で持つ案（#237）は採りませんでした。途中に段階を挟むと、後ろの番号を全部ずらす必要があり、ずらし忘れても気づけないからです。

**この決まりから生まれた型**: [`FieldStages`](Village.md#t-fieldstages)、[`FieldStep`](Village.md#t-fieldstep)、[`StepProgress`](Village.md#t-stepprogress)、[`IFieldRequests`](Core.md#t-ifieldrequests)、[`VillagerMood`](Village.md#t-villagermood)

出典: docs/decisions.md 2026-10-15

---

<a id="d-receive"></a>

## 何が届いても受け取り、何が届いたかを知らせる

**決めたこと**

フィールドは、何が届いても受け取ります（`Receive`）。戻るのは、頼まれた物が届いたときだけです。届くたびに、届いた物の種類（`Requested`・`Own`・`Foreign`）を `Received` で知らせます。

**なぜ**

体験者は、頼まれていない物も投げてきます。にんじんを 2 本目に投げると「やたらと豊作になる」、村に豚を投げると「いびつな世界になる」、のような反応を、あとから足せるようにするためです。反応や採点は、知らせを受けた側に足します。フィールドの仕組みは直しません。

**この決まりから生まれた型**: [`DeliveryKind`](Core.md#t-deliverykind)、[`Delivered`](Core.md#t-delivered)、[`FieldStages`](Village.md#t-fieldstages)、[`DeliveryFx`](Village.md#t-deliveryfx)、[`DeliveryZone`](Village.md#t-deliveryzone)、[`IFieldRequests`](Core.md#t-ifieldrequests)

出典: docs/decisions.md 2026-10-15

---

<a id="d-multi"></a>

## 1 つのフィールドに、欲しい物を同時に複数出す

**決めたこと**

1 つのフィールドに、欲しい物を同時に複数出せます。上限は [`FieldStages`](Village.md#t-fieldstages) の Max Open Requests（仮に 2）です。欲しい物には番号（`Request`）を付けて見分けます。

**なぜ**

1 つの場所にいる何人かが、それぞれ別の物を欲しがるほうが、ゲームとして面白いからです（10/20 の判断）。

**この決まりから生まれた型**: [`RequestIssued`](Core.md#t-requestissued)、[`RequestClosed`](Core.md#t-requestclosed)、[`FieldStages`](Village.md#t-fieldstages)、[`StepProgress`](Village.md#t-stepprogress)、[`RequestBubbles`](UI.md#t-requestbubbles)

出典: docs/decisions.md 2026-10-20

---

<a id="d-foodtable"></a>

## 食感 → 食べ物の対応は、Prefab に付ける表で持つ

**決めたこと**

どの食感でどの食べ物が生まれるかは、[`FoodTable`](Food.md#t-foodtable)（表）で持ちます。全フィールド共通の表は 1 つまでです。フィールドごとに変えたいときだけ、そのフィールドの Prefab に専用の表を付けます。引く順は、立っているフィールドの表、共通の表、なし、です。

**なぜ**

食感の数は、これからのテストで変わります。フィールドごとに別の食べ物を生む可能性もあります。行を足す・消すだけで、どちらも直せる形にしました。各フィールドの担当者は、自分の Prefab の表だけを触ります。

**あきらめたこと**

- ScriptableObject の `FoodDefinition`・`FoodCatalog`: 食感 3 つに対して 13 ファイル・330 行と大きく、フィールドごとの違いを表せませんでした。
- CSV の表: 全体を 1 ファイルで見られます。しかし、Prefab を名前の文字で指すので打ち間違いが起き、読み込みのコードも要ります。

**この決まりから生まれた型**: [`FoodTable`](Food.md#t-foodtable)、[`FoodTables`](Food.md#t-foodtables)、[`FoodRow`](Food.md#t-foodrow)、[`FoodSpawner`](Food.md#t-foodspawner)

出典: docs/decisions.md 2026-10-19・2026-10-25

---

<a id="d-aim"></a>

## 生まれた物は、体験者が狙って飛ばす

**決めたこと**

口から生まれた物は、顔を向けた方へ飛びます。どのフィールドに届いたかは、物が落ちた所の [`DeliveryZone`](Village.md#t-deliveryzone) で決まります。立っている場所（[`Standing`](Core.md#t-standing)）は、チュートリアルの「ここに立って」の案内と、食べ物の表を選ぶのに使います。

**なぜ**

狙いを定めて飛ばすことが、この体験の遊びだからです。立っている場所へ自動で飛ばすと、狙う操作がなくなります。

**この決まりから生まれた型**: [`FoodSpawner`](Food.md#t-foodspawner)、[`DeliveryZone`](Village.md#t-deliveryzone)、[`StandArea`](Village.md#t-standarea)、[`Standing`](Core.md#t-standing)

出典: docs/decisions.md 2026-10-25

---

<a id="d-mock"></a>

## デバイスがなくても、全部が動く

**決めたこと**

ゲームは、触感デバイスを [`IDevice`](Core.md#t-idevice) 越しにしか見ません。実機が無いときは、[`MockDevice`](Device.md#t-mockdevice) が自動で用意され、Quest のコントローラかキーボードで「噛む・決定する」を代わりにします。Quest が無いときは、[`DesktopHead`](DevTools.md#t-desktophead) と [`TutorialDebugHud`](DevTools.md#t-tutorialdebughud) で試せます。

**なぜ**

実機のデバイスは、ゲームと並行して作っています。デバイスができるまで誰も試せない、という状態を避けるためです。前のプロジェクトでは、通信のクラスがゲームの状態を持っていて、代わりの物に差し替えられませんでした。その反省から、デバイスは入力を ID とイベントにするだけにしました。

**この決まりから生まれた型**: [`IDevice`](Core.md#t-idevice)、[`Devices`](Core.md#t-devices)、[`MockDevice`](Device.md#t-mockdevice)、[`MockDeviceModel`](Device.md#t-mockdevicemodel)、[`DesktopHead`](DevTools.md#t-desktophead)、[`TutorialDebugHud`](DevTools.md#t-tutorialdebughud)

出典: docs/architecture.md「5. デバイス連携」

---

<a id="d-noprep"></a>

## Play の前に、手作業を要らなくする

**決めたこと**

機能をまたぐつなぎは、Inspector で手で入れません（`[SerializeField]` の手配線をしません）。足りない部品は、自分で用意します。たとえば、[`MockDevice`](Device.md#t-mockdevice)・[`RequestBubbles`](UI.md#t-requestbubbles)・[`AprilTagDetectionService`](Tracking.md#t-apriltagdetectionservice) は Play で自動で現れます。[`FieldStages`](Village.md#t-fieldstages) は、[`DeliveryFx`](Village.md#t-deliveryfx) が無ければ自分で付けます。設定のアセットが無いときは、コードの既定値で動きます。

**なぜ**

手で入れるつなぎは、付け忘れると動きません。しかも、どこを付け忘れたかが分かりにくいです。シーンに置くだけで動けば、担当者は自分の部分だけに集中できます。

**この決まりから生まれた型**: [`MockDevice`](Device.md#t-mockdevice)、[`RequestBubbles`](UI.md#t-requestbubbles)、[`RequestBubbleSettings`](UI.md#t-requestbubblesettings)、[`AprilTagDetectionService`](Tracking.md#t-apriltagdetectionservice)、[`AprilTagSettings`](Tracking.md#t-apriltagsettings)、[`DeliveryFx`](Village.md#t-deliveryfx)、[`FieldStages`](Village.md#t-fieldstages)

出典: CONTRIBUTING「最終的に Inspector／Editor の操作が要らないシステムにする」、docs/architecture.md「6. 避けること」

---

<a id="d-pure"></a>

## 計算は Unity から切り離して、テストできるようにする

**決めたこと**

判定や計算の中身は、MonoBehaviour から分けて、Unity のシーンに依存しないクラスにします（[`StepProgress`](Village.md#t-stepprogress)、[`GameClock`](Game.md#t-gameclock) など）。MonoBehaviour は、Unity とのつなぎだけを受け持ちます。

**なぜ**

Quest をかぶらずに、Play もせずに、EditMode のテストで正しさを確かめられるからです。並行して作る人数が多いほど、壊したことに早く気づける仕組みが要ります。

**この決まりから生まれた型**: [`StepProgress`](Village.md#t-stepprogress)、[`GameClock`](Game.md#t-gameclock)、[`HandHeldTip`](Tracking.md#t-handheldtip)、[`PoseTracker`](Tracking.md#t-posetracker)、[`PoseCalibrator`](Tracking.md#t-posecalibrator)、[`AprilTagGeometry`](Tracking.md#t-apriltaggeometry)、[`CameraPathMath`](CameraWork.md#t-camerapathmath)、[`MikoLineQueue`](Miko.md#t-mikolinequeue)

出典: 各機能の README

---

<a id="d-scene"></a>

## 共有シーンは 1 つにし、動画も同じシーンで流す

**決めたこと**

村と全フィールドは、1 つの共有シーン（Main）に置きます。複数人で編集するときは、Scene Fusion を使います。プロローグの動画も、シーンを切り替えずに、同じシーンの中の画面（[`VideoScreen`](Tutorial.md#t-videoscreen)）で流します。

**なぜ**

シーンを分けると、重ねて読み込む仕組み、シーンの間の位置合わせ、他の人のシーンと合わせる手間が要ります。このチームには重すぎます。動画のためにシーンを読み込み直すと、XR リグが作り直されて画面が一瞬黒くなり、AprilTag の位置合わせも消えます。VR では、この暗転が酔いの元になります。

**あきらめたこと**

フィールドごとにシーンを分ける案は、採りませんでした。別々の時間に作業してもぶつからない利点はあります。Scene Fusion が使えなくなったら、見直します。

**この決まりから生まれた型**: [`VideoScreen`](Tutorial.md#t-videoscreen)、[`PrologueVideo`](Tutorial.md#t-prologuevideo)、[`AprilTagPlace`](Tracking.md#t-apriltagplace)、[`SurfaceAnchor`](Village.md#t-surfaceanchor)

出典: docs/decisions.md 2026-10-09・2026-10-25

---

<a id="d-camera"></a>

## カメラワークは、どの物にも付けられる独立した部品にする

**決めたこと**

カメラの動きは、[`CameraCutPlayer`](CameraWork.md#t-cameracutplayer) を付けた物を、カット（直線・曲線・円弧・待ち）の並びで動かします。CameraWork は Core にも依存しません。位置の目印は、シーンに置いた空の GameObject で指定します。

**なぜ**

アニメのカット割りのように、カメラを自由に動かせるようにするためです。Core に依存しないので、ゲームのどの仕組みとも関係なく、どのカメラにも付けられます。目印を Transform にしたので、シーンビューで目印を動かせば、軌道の線もその場で変わります。数字を打ち込むより、直感的に直せます。

**この決まりから生まれた型**: [`CameraCutPlayer`](CameraWork.md#t-cameracutplayer)、[`CameraShot`](CameraWork.md#t-camerashot)、[`ShotPath`](CameraWork.md#t-shotpath)、[`CameraPathMath`](CameraWork.md#t-camerapathmath)、[`CameraEase`](CameraWork.md#t-cameraease)、[`ShotKind`](CameraWork.md#t-shotkind)、[`ShotAim`](CameraWork.md#t-shotaim)、[`VillageTourBuilder`](CameraWork.md#t-villagetourbuilder)

出典: PR #347
