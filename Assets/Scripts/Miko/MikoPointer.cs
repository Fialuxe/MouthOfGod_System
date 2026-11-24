using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 指さしの右腕を、指さす先の方へ向けます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// アニメーションの腕は、前のやや上を指す形に決まっています。
    /// そこで Animator が動かしたあとの LateUpdate で、右腕を指さす先へ回します。
    /// 体の向きは <see cref="MikoFloat"/> が合わせます。
    /// </para>
    /// <para>
    /// 骨は名前（UpperArm.R と Fist.R）で子から探します。
    /// 見つからなければ、腕は回しません。
    /// </para>
    /// </remarks>
    public sealed class MikoPointer : MonoBehaviour
    {
        // 指さしアニメーション（Miko_Point.anim）の時間に合わせた値です。
        // アニメーションの長さを変えたら、ここも合わせて直します。
        const float RaiseSeconds = 0.35f;
        const float HoldUntil = 1.4f;
        const float TotalSeconds = 2f;

        Transform _upperArm;
        Transform _hand;
        Transform _target;
        float _startedAt;

        /// <summary>
        /// 右腕を、指さす先へ向け始めます。Animator に指さしの合図を送るのと同時に呼びます。
        /// </summary>
        /// <param name="target">指さす先です。<see langword="null"/> なら腕を回しません。</param>
        /// <remarks>
        /// 呼んでから <see cref="TotalSeconds"/> 秒かけて、腕を上げて保ち、元に戻します。
        /// 途中で呼ぶと、最初からやり直します。
        /// </remarks>
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
