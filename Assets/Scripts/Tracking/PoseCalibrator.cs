using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 複数の観測を集め、外れ値を除いて平均した 1 つの姿勢を返す。タグが動かない前提で、位置を「測って固定する」ために使う。
    /// Unity のオブジェクトに依存しない。
    /// </summary>
    public sealed class PoseCalibrator
    {
        readonly int _requiredSamples;
        readonly float _maxPositionOutlierMeters;
        readonly float _maxAngleOutlierDegrees;
        readonly List<Pose> _samples = new List<Pose>();

        /// <param name="requiredSamples">集めるサンプル数。</param>
        /// <param name="maxPositionOutlierMeters">位置の中央値からこの距離（メートル）より離れたサンプルは、外れ値として捨てる。</param>
        /// <param name="maxAngleOutlierDegrees">平均の向きからこの角度（度）より離れたサンプルは、外れ値として捨てる。</param>
        public PoseCalibrator(int requiredSamples, float maxPositionOutlierMeters, float maxAngleOutlierDegrees)
        {
            _requiredSamples = Mathf.Max(1, requiredSamples);
            _maxPositionOutlierMeters = maxPositionOutlierMeters;
            _maxAngleOutlierDegrees = maxAngleOutlierDegrees;
        }

        public int Count => _samples.Count;
        public int RequiredSamples => _requiredSamples;
        public bool IsComplete => _samples.Count >= _requiredSamples;

        public void Reset()
        {
            _samples.Clear();
        }

        /// <summary>サンプルを足す。必要な数が集まったあとは無視する。</summary>
        public void Add(Pose sample)
        {
            if (IsComplete) return;
            _samples.Add(sample);
        }

        /// <summary>集めたサンプルの平均を返す。サンプルが 1 つもなければ false。</summary>
        public bool TryGetResult(out Pose result)
        {
            result = default;
            if (_samples.Count == 0) return false;

            var medianPosition = MedianPosition(_samples);
            var positionInliers = new List<Pose>(_samples.Count);
            foreach (var sample in _samples)
            {
                if (Vector3.Distance(sample.position, medianPosition) <= _maxPositionOutlierMeters)
                {
                    positionInliers.Add(sample);
                }
            }
            if (positionInliers.Count == 0) positionInliers.AddRange(_samples);

            var meanPosition = Vector3.zero;
            foreach (var sample in positionInliers) meanPosition += sample.position;
            meanPosition /= positionInliers.Count;

            var firstPassRotation = MeanRotation(positionInliers);
            var rotationInliers = new List<Pose>(positionInliers.Count);
            foreach (var sample in positionInliers)
            {
                if (Quaternion.Angle(sample.rotation, firstPassRotation) <= _maxAngleOutlierDegrees)
                {
                    rotationInliers.Add(sample);
                }
            }
            var rotation = rotationInliers.Count > 0 ? MeanRotation(rotationInliers) : firstPassRotation;

            result = new Pose(meanPosition, rotation);
            return true;
        }

        static Vector3 MedianPosition(List<Pose> samples)
        {
            return new Vector3(
                Median(samples, p => p.position.x),
                Median(samples, p => p.position.y),
                Median(samples, p => p.position.z));
        }

        static float Median(List<Pose> samples, Func<Pose, float> selector)
        {
            var values = new float[samples.Count];
            for (var i = 0; i < values.Length; i++) values[i] = selector(samples[i]);
            Array.Sort(values);
            var middle = values.Length / 2;
            return values.Length % 2 == 1 ? values[middle] : 0.5f * (values[middle - 1] + values[middle]);
        }

        // 向きの平均。q と -q は同じ回転なので、符号をそろえてから足して正規化する（小さなばらつきに対して有効）。
        static Quaternion MeanRotation(List<Pose> samples)
        {
            var reference = samples[0].rotation;
            var sum = new Vector4(0f, 0f, 0f, 0f);
            foreach (var sample in samples)
            {
                var q = sample.rotation;
                var dot = q.x * reference.x + q.y * reference.y + q.z * reference.z + q.w * reference.w;
                var sign = dot < 0f ? -1f : 1f;
                sum += new Vector4(q.x, q.y, q.z, q.w) * sign;
            }
            var length = sum.magnitude;
            if (length < 1e-6f) return reference;
            sum /= length;
            return new Quaternion(sum.x, sum.y, sum.z, sum.w);
        }
    }
}
