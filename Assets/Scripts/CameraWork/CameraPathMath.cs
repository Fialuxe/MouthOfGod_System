using UnityEngine;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 動きの緩急のつけ方を表します。
    /// </summary>
    public enum CameraEase
    {
        /// <summary>一定の速さで動きます。</summary>
        Linear,

        /// <summary>ゆっくり出て、速く着きます。</summary>
        EaseIn,

        /// <summary>速く出て、ゆっくり着きます。</summary>
        EaseOut,

        /// <summary>ゆっくり出て、ゆっくり着きます。</summary>
        EaseInOut,

        /// <summary>Ease Curve の曲線で決めます。</summary>
        Custom,
    }

    /// <summary>
    /// カメラの軌道を計算します。
    /// </summary>
    /// <remarks>
    /// Unity の Transform を知らない、位置と数だけの計算です。
    /// </remarks>
    public static class CameraPathMath
    {
        // 軌道の長さを測るときの区切りの数です。
        const int LengthSamples = 48;

        /// <summary>
        /// 進み具合に緩急をかけます。
        /// </summary>
        /// <param name="ease">緩急のつけ方です。</param>
        /// <param name="t">進み具合です。0〜1 の外は、0〜1 に丸めます。</param>
        /// <param name="strength">緩急の強さです。大きいほど強くなります。1 以下は 1 として扱い、Linear と同じになります。</param>
        /// <param name="curve">
        /// <see cref="CameraEase.Custom"/> のときの曲線です。
        /// <see langword="null"/> か空なら、Linear と同じになります。
        /// </param>
        /// <returns>
        /// 緩急をかけた進み具合を返します。
        /// Custom 以外は 0〜1 です。Custom は曲線の値をそのまま返します。
        /// </returns>
        public static float Ease(CameraEase ease, float t, float strength, AnimationCurve curve = null)
        {
            t = Mathf.Clamp01(t);
            float k = Mathf.Max(strength, 1f);
            switch (ease)
            {
                case CameraEase.EaseIn:
                    return Mathf.Pow(t, k);
                case CameraEase.EaseOut:
                    return 1f - Mathf.Pow(1f - t, k);
                case CameraEase.EaseInOut:
                    return t < 0.5f
                        ? 0.5f * Mathf.Pow(2f * t, k)
                        : 1f - 0.5f * Mathf.Pow(2f * (1f - t), k);
                case CameraEase.Custom:
                    return curve != null && curve.length > 0 ? curve.Evaluate(t) : t;
                default:
                    return t;
            }
        }

        /// <summary>
        /// 直線上の位置を返します。
        /// </summary>
        /// <param name="from">出発点です。</param>
        /// <param name="to">到着点です。</param>
        /// <param name="t">進み具合です。0 で出発点、1 で到着点です。範囲の外は延長します。</param>
        /// <returns>直線上の位置を返します。</returns>
        public static Vector3 Line(Vector3 from, Vector3 to, float t) => Vector3.LerpUnclamped(from, to, t);

        /// <summary>
        /// ベジェ曲線上の位置を返します。
        /// </summary>
        /// <remarks>
        /// 3 点なら二次、4 点なら三次になります。それより多くても動きます。
        /// 呼ぶたびに作業用の配列を作ります。
        /// </remarks>
        /// <param name="points">
        /// 最初が出発点、最後が到着点です。
        /// 間の点は、曲がり方を決める引っぱる点です。
        /// </param>
        /// <param name="t">進み具合です。0 で出発点、1 で到着点です。</param>
        /// <returns>
        /// 曲線上の位置を返します。
        /// 点が 1 つならその点を返します。
        /// <paramref name="points"/> が <see langword="null"/> か空なら <see cref="Vector3.zero"/> を返します。
        /// </returns>
        public static Vector3 Bezier(Vector3[] points, float t)
        {
            if (points == null || points.Length == 0) return Vector3.zero;
            if (points.Length == 1) return points[0];
            var work = new Vector3[points.Length];
            System.Array.Copy(points, work, points.Length);
            for (int n = work.Length - 1; n > 0; n--)
                for (int i = 0; i < n; i++)
                    work[i] = Vector3.LerpUnclamped(work[i], work[i + 1], t);
            return work[0];
        }

        /// <summary>
        /// 円弧やらせんの上の位置を返します。
        /// </summary>
        /// <param name="start">出発点です。</param>
        /// <param name="center">まわる中心です。</param>
        /// <param name="axis">まわる軸です。長さが 0 なら真上として扱います。</param>
        /// <param name="angle">まわる角度（度）です。360 で一周です。負なら逆向きです。</param>
        /// <param name="radiusScale">到着までに半径を何倍にするかです。1 なら同じ円です。</param>
        /// <param name="heightChange">到着までに、軸の向きに動く高さ（m）です。</param>
        /// <param name="t">進み具合です。0 で出発点、1 で到着点です。</param>
        /// <returns>円弧やらせんの上の位置を返します。</returns>
        public static Vector3 Orbit(Vector3 start, Vector3 center, Vector3 axis, float angle, float radiusScale, float heightChange, float t)
        {
            Vector3 up = axis.sqrMagnitude < 1e-8f ? Vector3.up : axis.normalized;
            Vector3 offset = start - center;
            Vector3 turned = Quaternion.AngleAxis(angle * t, up) * offset;
            return center + turned * Mathf.Lerp(1f, radiusScale, t) + up * (heightChange * t);
        }

        /// <summary>
        /// 軌道を細かく区切って、長さを測ります。
        /// </summary>
        /// <param name="position">進み具合 0〜1 から位置を返す関数です。<see langword="null"/> は不可です。</param>
        /// <returns>軌道のおよその長さ（m）を返します。</returns>
        public static float Length(System.Func<float, Vector3> position)
        {
            float length = 0f;
            Vector3 previous = position(0f);
            for (int i = 1; i <= LengthSamples; i++)
            {
                Vector3 current = position(i / (float)LengthSamples);
                length += Vector3.Distance(previous, current);
                previous = current;
            }
            return length;
        }

        /// <summary>
        /// 動かす時間を求めます。
        /// </summary>
        /// <param name="useSpeed"><see langword="true"/> なら、長さと速さから求めます。</param>
        /// <param name="seconds"><paramref name="useSpeed"/> が <see langword="false"/> のときの時間（秒）です。</param>
        /// <param name="speed">速さ（m / 秒）です。0 以下でも 0 除算にはなりません。</param>
        /// <param name="length">軌道の長さ（m）です。</param>
        /// <returns>
        /// 動かす時間（秒）を返します。
        /// 進み具合を求めるときの割り算に使うので、0 にはしません。
        /// </returns>
        public static float Duration(bool useSpeed, float seconds, float speed, float length)
        {
            float result = useSpeed ? length / Mathf.Max(speed, 1e-4f) : seconds;
            return Mathf.Max(result, 0.001f);
        }
    }
}
