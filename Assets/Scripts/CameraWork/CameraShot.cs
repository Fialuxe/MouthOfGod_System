using System;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 1 カットの軌道の種類を表します。
    /// </summary>
    public enum ShotKind
    {
        /// <summary>直線で進みます。</summary>
        Line,

        /// <summary>ベジェ曲線で進みます。</summary>
        Curve,

        /// <summary>円弧・一周・らせんでまわります。</summary>
        Orbit,

        /// <summary>その場で待ちます。向きだけ変えられます。</summary>
        Hold,
    }

    /// <summary>
    /// 動いている間の向きの決め方を表します。
    /// </summary>
    public enum ShotAim
    {
        /// <summary>向きを変えません。</summary>
        Keep,

        /// <summary>進む向きを向きます。</summary>
        AlongPath,

        /// <summary>Aim Target を見続けます。</summary>
        LookAtTarget,

        /// <summary>到着点（End）の向きに、だんだん合わせます。</summary>
        MatchEnd,
    }

    /// <summary>
    /// カメラの 1 カットの設定を保持します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 出発点は、前のカットの到着点です。
    /// 最初のカットは、再生を始めたときの位置から出ます。
    /// </para>
    /// <para>
    /// 使う項目は、種類によって違います。
    /// Inspector では、使わない項目も見えます。
    /// 種類ごとに使う項目は、各項目の Tooltip に書いてあります。
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class CameraShot
    {
        [Tooltip("メモ用の名前。動きには効かない")]
        public string name = "Shot";

        [Tooltip("軌道の種類。Line: 直線、Curve: ベジェ曲線、Orbit: 円弧・一周・らせん、Hold: その場で待つ（向きだけ変える）")]
        public ShotKind kind = ShotKind.Line;

        [Header("時間")]
        [Tooltip("入れると、Speed（m / 秒）で動く。外すと、Seconds で動く。Hold は常に Seconds")]
        public bool useSpeed;

        [Tooltip("このカットにかける秒数。Use Speed が外れているとき、または Hold のときに使う")]
        [Min(0f)] public float seconds = 2f;
        [Tooltip("動く速さ（m / 秒）。Use Speed が入っているときに使う。軌道の長さ ÷ 速さが、かかる秒数になる")]
        [Min(0f)] public float speed = 1f;

        [Header("緩急（Lerp 加減）")]
        [Tooltip("緩急のつけ方。Linear: 一定の速さ、EaseIn: ゆっくり出て速く着く、EaseOut: 速く出てゆっくり着く、EaseInOut: ゆっくり出てゆっくり着く、Custom: Ease Curve で決める")]
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
        [Tooltip("動いている間の向き。Keep: 変えない、AlongPath: 進む向き、LookAtTarget: Aim Target を見続ける、MatchEnd: End の向きにだんだん合わせる")]
        public ShotAim aim = ShotAim.AlongPath;

        [Tooltip("Aim が LookAtTarget のとき、見る相手")]
        public Transform aimTarget;

        [Tooltip("Aim が LookAtTarget のとき、入れると、見る点が Aim Target からここへ、カットの進みに合わせて移る（山を見ながら始めて、村を見て終わる、など）")]
        public Transform aimTargetEnd;

        [Tooltip("向きを新しい向きに合わせるのにかける秒数（カットの頭で、向きが急に変わらないように）。Aim が AlongPath と LookAtTarget のときに使う。0 ですぐに合わせる")]
        [Min(0f)] public float aimBlendSeconds = 0.3f;

        [Header("ズーム")]
        [Tooltip("0 より大きいと、このカットの間に、画角（度）をここまで変える。小さいほど寄り、大きいほど引く。0 なら変えない。同じオブジェクトに Camera が必要")]
        [Min(0f)] public float fieldOfViewEnd;

        [Header("このカットの終わり")]
        [Tooltip("到着したときに呼ぶもの（音を鳴らす・次の演出を始める、など）")]
        public UnityEvent onArrived = new UnityEvent();
    }
}
