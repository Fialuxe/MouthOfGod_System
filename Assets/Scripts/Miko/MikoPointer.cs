using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 指さしの腕を、指さし先の方へ向ける。アニメーションの腕は「前やや上」に決まっているので、
    /// Animator のあとで、右腕を指さし先へ回す。体の向きは MikoFloat が合わせる。
    /// </summary>
    public sealed class MikoPointer : MonoBehaviour
    {
        // 指さしアニメーション（Miko_Point.anim）の長さに合わせる。MikoPrefabGenerator の PointWeight と同じ値。
        const float RaiseSeconds = 0.35f;
        const float HoldUntil = 1.4f;
        const float TotalSeconds = 2f;

        Transform _upperArm;
        Transform _hand;
        Transform _target;
        float _startedAt;

        /// <summary>target へ向けて指さす。アニメーションを始める合図と同時に呼ぶ。</summary>
        public void Begin(Transform target)
        {
            _target = target;
            _startedAt = Time.time;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            var t = Time.time - _startedAt;
            if (t >= TotalSeconds) { _target = null; return; }

            if (_upperArm == null) _upperArm = FindBone("UpperArm.R");
            if (_hand == null) _hand = FindBone("Fist.R");
            if (_upperArm == null || _hand == null) return;

            var up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / RaiseSeconds));
            var down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - HoldUntil) / (TotalSeconds - HoldUntil)));
            var weight = up * (1f - down);

            var current = _hand.position - _upperArm.position;
            var wanted = _target.position - _upperArm.position;
            if (current.sqrMagnitude < 1e-8f || wanted.sqrMagnitude < 1e-8f) return;
            var delta = Quaternion.FromToRotation(current, wanted);
            _upperArm.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * _upperArm.rotation;
        }

        Transform FindBone(string boneName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == boneName) return child;
            return null;
        }
    }
}
