using UnityEngine;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 1 カットの軌道。カットが始まる瞬間に、出発点と目印の位置を写し取って作る（目印がその後に動いても、軌道は変わらない）。
    /// </summary>
    public sealed class ShotPath
    {
        readonly CameraShot _shot;
        readonly Vector3 _start;
        readonly Vector3 _end;
        readonly Vector3[] _bezier;
        readonly Vector3 _center;
        readonly Quaternion _endRotation;

        /// <summary>軌道の長さ（m）。Hold は 0。</summary>
        public float Length { get; }

        /// <summary>動かす秒数。</summary>
        public float Duration { get; }

        public Vector3 StartPosition => _start;
        public Vector3 EndPosition => Position(1f);

        /// <param name="shot">カット。</param>
        /// <param name="start">出発点（世界座標）。</param>
        /// <param name="startRotation">出発したときのカメラの向き。</param>
        /// <param name="selfForward">カメラ自身の前向き。Line で End も Direction From も空のとき使う。</param>
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

        static Vector3[] BuildBezier(CameraShot shot, Vector3 start)
        {
            var points = new System.Collections.Generic.List<Vector3> { start };
            if (shot.controlPoints != null)
                foreach (Transform t in shot.controlPoints)
                    if (t != null) points.Add(t.position);
            points.Add(shot.end != null ? shot.end.position : start);
            return points.ToArray();
        }

        /// <summary>軌道上の位置。u は 0〜1（緩急をかける前の、軌道に沿った割合）。</summary>
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

        /// <summary>軌道上の進む向き。止まっている（Hold・長さ 0）ときは、fallback。</summary>
        public Vector3 Tangent(float u, Vector3 fallback)
        {
            const float Step = 0.01f;
            float a = Mathf.Clamp01(u - Step);
            float b = Mathf.Clamp01(u + Step);
            Vector3 d = Position(b) - Position(a);
            return d.sqrMagnitude < 1e-10f ? fallback : d.normalized;
        }

        /// <summary>到着点の向き。End が無ければ、出発したときの向き。</summary>
        public Quaternion EndRotation => _endRotation;
    }
}
