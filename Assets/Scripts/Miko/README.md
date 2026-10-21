# Miko

空に浮かんで、プレイヤーに指示を出す巫女です。Core にだけ依存します。Tutorial は、合図（`Cues`）を出すだけで、巫女を直接は触りません。

## 合図

名前は、すべて `miko.` から始めます。

| 合図 | 起きること |
|---|---|
| `miko.point` | 指さしをします。`MikoCueReceiver` の `Point Target` があれば、その方も向きます。 |
| `miko.follow` | プレイヤーのそば（右前）についてきます。 |
| `miko.stay` | いまの場所にとどまります。 |
| `miko.say.<id>` | `LineBook` の `MikoLineBook` の `Lines` から `<id>` を引いて、字幕と音声を出します。表情と指さしも、セリフに付けられます。 |
| `miko.hush` | しゃべるのをやめます。 |
| `miko.face.<name>` | 表情を変えます。`normal` `happy` `squint` `love` `sleep` `wink` `tired` です。 |

## 居場所のモード

`MikoMover` が、3 つのモードを切り替えます。

| モード | 動き |
|---|---|
| Stay | いまの場所にいます。 |
| Go | セリフの `Move To` に物を入れると、しゃべりはじめにその場所へ飛んで行き、着いたら Stay になります。コードからは `GoTo(Transform)` でも呼べます。 |
| Follow | プレイヤーのそばについてきます。近いときは動きません。 |

## セリフの足し方

セリフは、Miko の子の `LineBook` にある `MikoLineBook` の `Lines` に並べます。見本の 3 つ（`welcome` `give` `thanks`）は、Prefab を最初に作るときに入ります。

1. `Lines` に行を足し、`Id` と `Text` を入れます。
2. 音声があれば `Voice` に入れます。無ければ字幕だけ出ます。
3. しゃべりながら指さすなら、`Point` を入れ、`Point Target` にシーンの物をドラッグします。シーンの物は、Prefab には入れられません。シーンに置いた Miko で入れます。
4. 移動するなら、`Move To` に行き先の物をドラッグします。`Speak After Move` を入れると、着いてからしゃべります。外すと、動きながらしゃべります。
5. 字幕の長さは、`Seconds` が 0 なら、音声の長さか文字数から決まります。字幕の高さは、文字の量に合わせて広がります。

`Point Target` が空のセリフは、`MikoCueReceiver` の `Point Target`（既定の先）を使います。それも空なら、向きを変えません。

## Prefab の作り直し

メニューの「MouthOfGod/Miko/Generate Prefab」で作ります。Prefab は、Miko（空）の下に `Body`（モデルと動きの部品）と `LineBook`（セリフ）を置く形です。2 回目からは `Body` だけ作り直し、`LineBook` は触りません。セリフと、シーンの Miko に入れた指さす物は、残ります。目の位置は `MikoPrefabGenerator.cs` の定数に固定してあります。

## 字幕の言葉

字幕は、英語を想定しています。`MikoSpeech` の `Font` が空なら、Unity 組み込みのフォントを使います。このフォントは日本語を表示できません。日本語を出すときは、日本語を含むフォントを `Font` に入れます。
