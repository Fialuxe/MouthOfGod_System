using UnityEngine;

namespace MouthOfGod.CameraWork
{
    /// <summary>動きの緩急のつけ方。</summary>
    public enum CameraEase
    {
        Linear, // 一定の速さ
        EaseIn, // ゆっくり出て、速く着く
        EaseOut, // 速く出て、ゆっくり着く
        EaseInOut, // ゆっくり出て、ゆっくり着く
        Custom, // Ease Curve で決める
    }

    /// <summary>カメラの軌道の計算。Unity の Transform を知らない、位置と数だけの計算。</summary>
    public static class CameraPathMath
    {
        const int LengthSamples = 48;

        /// <summary>緩急をかける。t は 0〜1 で、結果も 0〜1。strength が大きいほど、緩急が強い（1 で Linear と同じ）。</summary>
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

        /// <summary>直線。from から to へ、t（0〜1）の位置。</summary>
        public static Vector3 Line(Vector3 from, Vector3 to, float t) => Vector3.LerpUnclamped(from, to, t);

        /// <summary>
        /// ベジェ曲線。points の最初が出発点、最後が到着点で、間は引っぱる点（曲がり方を決める）。
        /// 3 点なら二次、4 点なら三次、それ以上でも動く。点が 1 つなら、その点。
        /// </summary>
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
        /// 円弧・らせん。center のまわりを、axis を軸に angle 度まわる。
        /// start は出発点。radiusScale は到着までに半径を何倍にするか（1 で同じ円）、heightChange は軸の向きに動く高さ（m）。
        /// 360 度で一周する。負の角度で逆向き。
        /// </summary>
        public static Vector3 Orbit(Vector3 start, Vector3 center, Vector3 axis, float angle, float radiusScale, float heightChange, float t)
        {
            Vector3 up = axis.sqrMagnitude < 1e-8f ? Vector3.up : axis.normalized;
            Vector3 offset = start - center;
            Vector3 turned = Quaternion.AngleAxis(angle * t, up) * offset;
            return center + turned * Mathf.Lerp(1f, radiusScale, t) + up * (heightChange * t);
        }

        /// <summary>軌道の長さを、細かく区切って測る。</summary>
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
        /// 動かす長さ（秒）。useSpeed なら、軌道の長さ ÷ 速さ（m / 秒）。そうでなければ seconds。0 にはしない。
        /// </summary>
        public static float Duration(bool useSpeed, float seconds, float speed, float length)
        {
            float result = useSpeed ? length / Mathf.Max(speed, 1e-4f) : seconds;
            return Mathf.Max(result, 0.001f);
        }
    }
}
