# CameraWork

カメラ（や任意のオブジェクト）を、決めた軌道と速さで動かします。アニメのカット割りのように、複数のカットをつないで撮れます。

## 使い方

1. 動かしたいオブジェクトに `CameraCutPlayer` を付けます。
2. Shots にカットを足します。位置の目印は、空の GameObject を置いて End・Control Points・Center に入れます。
3. `Play()` を呼びます。Play On Start を入れると、自動で始まります。選んでいるとき、軌道がシーンビューに線で出ます。

## カットの種類

| 種類 | 動き | 使う項目 |
|---|---|---|
| Line | 直線 | End、または Direction From（空ならカメラ自身）の前向きに Distance |
| Curve | ベジェ曲線 | End、Control Points（1 つで二次、2 つで三次） |
| Orbit | 円弧・一周・らせん | Center、Axis、Angle、Radius Scale、Height Change |
| Hold | その場で待つ | Seconds |

速さは Use Speed で m / 秒、外すと Seconds で決めます。緩急は Ease と Ease Strength、細かく決めたいときは Custom の曲線です。向きは Aim（Keep・AlongPath・LookAtTarget・MatchEnd）で決めます。

出発点は、前のカットの到着点です。目印の位置は、そのカットが始まる瞬間に読みます。
