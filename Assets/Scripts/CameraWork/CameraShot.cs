using System;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.CameraWork
{
    /// <summary>1 カットの軌道の種類。</summary>
    public enum ShotKind
    {
        Line, // 直線
        Curve, // 曲線（ベジェ）
        Orbit, // 円弧・一周・らせん
        Hold, // その場で待つ（向きだけ変える）
    }

    /// <summary>動いている間の向きの決め方。</summary>
    public enum ShotAim
    {
        Keep, // 向きは変えない
        AlongPath, // 進む向きを向く
        LookAtTarget, // Aim Target を見続ける
        MatchEnd, // 到着点（End）の向きに、だんだん合わせる
    }

    /// <summary>
    /// カメラの 1 カット。出発点は、前のカットの到着点（最初のカットは、いまの位置）。
    /// 使う項目は種類しだいで、Inspector では使わない項目も見える。種類ごとの使う項目は、各項目の説明にある。
    /// </summary>
    [Serializable]
    public sealed class CameraShot
    {
        [Tooltip("メモ用の名前。動きには効かない")]
        public string name = "Shot";

        public ShotKind kind = ShotKind.Line;

        [Header("時間")]
        [Tooltip("入れると、Speed（m / 秒）で動く。外すと、Seconds で動く。Hold は常に Seconds")]
        public bool useSpeed;

        [Min(0f)] public float seconds = 2f;
        [Min(0f)] public float speed = 1f;

        [Header("緩急（Lerp 加減）")]
        public CameraEase ease = CameraEase.EaseInOut;

        [Tooltip("緩急の強さ。1 で一定の速さ、大きいほど出だしと着き際がゆっくりになる")]
        [Min(1f)] public float easeStrength = 2f;

        [Tooltip("Ease が Custom のときの曲線（横: 進み具合 0〜1、縦: 動いた割合 0〜1）")]
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Line / Curve の到着点")]
        [Tooltip("到着点（位置と向き）。入れると、ここへ向かう。Line で空なら、Direction From の前向きに Distance 進む。Curve では必須")]
        public Transform end;

        [Tooltip("Line で End が空のとき、この Transform の前向きに進む。空なら、カメラ自身の前向き")]
        public Transform directionFrom;

        [Tooltip("Line で End が空のとき、進む距離（m）。負で後ろへ")]
        public float distance = 2f;

        [Header("Curve")]
        [Tooltip("曲がり方を決める点。順に並べる。1 つで二次、2 つで三次のベジェ。曲線はこれらの点に引き寄せられる（通りはしない）")]
        public Transform[] controlPoints = new Transform[0];

        [Header("Orbit")]
        [Tooltip("まわる中心")]
        public Transform center;

        [Tooltip("まわる軸（世界座標）。既定は真上（水平にまわる）")]
        public Vector3 axis = Vector3.up;

        [Tooltip("まわる角度（度）。360 で一周、負で逆向き")]
        public float angle = 90f;

        [Tooltip("到着までに半径を何倍にするか。1 で同じ円、1 より小さいと中心へ寄る")]
        [Min(0f)] public float radiusScale = 1f;

        [Tooltip("まわりながら、軸の向きに動く高さ（m）。0 以外でらせんになる")]
        public float heightChange;

        [Header("向き")]
        public ShotAim aim = ShotAim.AlongPath;

        [Tooltip("Aim が LookAtTarget のとき、見る相手")]
        public Transform aimTarget;

        [Tooltip("Aim が LookAtTarget のとき、入れると、見る点が Aim Target からここへ、カットの進みに合わせて移る（山を見ながら始めて、村を見て終わる、など）")]
        public Transform aimTargetEnd;

        [Tooltip("向きを新しい向きに合わせるのにかける秒数（カットの頭で、向きが急に変わらないように）")]
        [Min(0f)] public float aimBlendSeconds = 0.3f;

        [Header("このカットの終わり")]
        [Tooltip("到着したときに呼ぶもの（音を鳴らす・次の演出を始める、など）")]
        public UnityEvent onArrived = new UnityEvent();
    }
}
