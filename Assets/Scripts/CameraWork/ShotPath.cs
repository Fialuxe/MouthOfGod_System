using UnityEngine;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 1 カットの軌道を表します。
    /// </summary>
    /// <remarks>
    /// カットが始まる瞬間に、出発点と目印の位置を写し取って作ります。
    /// その後に目印が動いても、軌道は変わりません。
    /// <see cref="CameraShot"/> への参照は保持し、種類や Orbit の設定はそこから読みます。
    /// </remarks>
    public sealed class ShotPath
    {
        readonly CameraShot _shot;
        readonly Vector3 _start;
        readonly Vector3 _end;
        // Curve のときだけ使います。ほかの種類では null です。
        readonly Vector3[] _bezier;
        // Orbit のときだけ使います。
        readonly Vector3 _center;
        readonly Quaternion _endRotation;

        /// <summary>
        /// 軌道の長さ（m）です。Hold は 0 です。
        /// </summary>
        public float Length { get; }

        /// <summary>
        /// 動かす秒数です。0 にはなりません。
        /// </summary>
        public float Duration { get; }

        public Vector3 StartPosition => _start;
        public Vector3 EndPosition => Position(1f);

        /// <param name="shot">カットの設定です。<see langword="null"/> は不可です。参照を保持します。</param>
        /// <param name="start">出発点のワールド座標です。</param>
        /// <param name="startRotation">出発したときのカメラの向きです。</param>
        /// <param name="selfForward">カメラ自身の前向きです。Line で End も Direction From も空のときに使います。</param>
        public ShotPath(CameraShot shot, Vector3 start, Quaternion startRotation, Vector3 selfForward)
        {
            _shot = shot;
            _start = start;
            _endRotation = startRotation;

            switch (shot.kind)
            {
                case ShotKind.Line:
                    if (shot.end != null)
                    {
                        _end = shot.end.position;
                        _endRotation = shot.end.rotation;
                    }
                    else
                    {
                        Vector3 forward = shot.directionFrom != null ? shot.directionFrom.forward : selfForward;
                        _end = start + forward.normalized * shot.distance;
                    }
                    break;
                case ShotKind.Curve:
                    _bezier = BuildBezier(shot, start);
                    _end = _bezier[_bezier.Length - 1];
                    if (shot.end != null) _endRotation = shot.end.rotation;
                    break;
                case ShotKind.Orbit:
                    _center = shot.center != null ? shot.center.position : start;
                    _end = Position(1f);
                    break;
                default:
                    _end = start;
                    break;
            }

            Length = shot.kind == ShotKind.Hold ? 0f : CameraPathMath.Length(Position);
            Duration = CameraPathMath.Duration(shot.useSpeed && shot.kind != ShotKind.Hold, shot.seconds, shot.speed, Length);
        }

        // 出発点、空でない Control Points、到着点の順に並べます。
        // End が空なら、到着点は出発点と同じにします。
        static Vector3[] BuildBezier(CameraShot shot, Vector3 start)
        {
            var points = new System.Collections.Generic.List<Vector3> { start };
            if (shot.controlPoints != null)
                foreach (Transform t in shot.controlPoints)
                    if (t != null) points.Add(t.position);
            points.Add(shot.end != null ? shot.end.position : start);
            return points.ToArray();
        }

        /// <summary>
        /// 軌道上の位置を返します。
        /// </summary>
        /// <param name="u">
        /// 軌道に沿った進み具合です。0 で出発点、1 で到着点です。
        /// ここでは緩急をかけません。再生では、緩急をかけた後の値を渡します。
        /// </param>
        /// <returns>軌道上のワールド座標を返します。Hold では出発点を返します。</returns>
        public Vector3 Position(float u)
        {
            switch (_shot.kind)
            {
                case ShotKind.Line:
                    return CameraPathMath.Line(_start, _end, u);
                case ShotKind.Curve:
                    return CameraPathMath.Bezier(_bezier, u);
                case ShotKind.Orbit:
                    return CameraPathMath.Orbit(_start, _center, _shot.axis, _shot.angle, _shot.radiusScale, _shot.heightChange, u);
                default:
                    return _start;
            }
        }

        /// <summary>
        /// 軌道上の進む向きを返します。
        /// </summary>
        /// <param name="u">軌道に沿った進み具合です。0〜1 で渡します。</param>
        /// <param name="fallback">進む向きが決まらないときに返す向きです。</param>
        /// <returns>
        /// 長さ 1 の進む向きを返します。
        /// Hold や長さ 0 の軌道で止まっているときは <paramref name="fallback"/> を返します。
        /// </returns>
        public Vector3 Tangent(float u, Vector3 fallback)
        {
            const float Step = 0.01f;
            float a = Mathf.Clamp01(u - Step);
            float b = Mathf.Clamp01(u + Step);
            Vector3 d = Position(b) - Position(a);
            return d.sqrMagnitude < 1e-10f ? fallback : d.normalized;
        }

        /// <summary>
        /// 到着点の向きです。
        /// </summary>
        /// <value>
        /// Line と Curve で End があれば、End の向きです。
        /// それ以外は、出発したときの向きです。
        /// </value>
        public Quaternion EndRotation => _endRotation;
    }
}
