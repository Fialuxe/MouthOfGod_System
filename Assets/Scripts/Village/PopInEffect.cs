using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が出るとき、小さい所から少し大きくなりすぎてから、元の大きさに収めます。
    /// 出た場所に星の粒も出します。
    /// </summary>
    /// <remarks>
    /// <see cref="FieldStep"/> が、Play 中だけ付けます。
    /// 終わるか途中で消えると、元の大きさに戻して自分を外します。
    /// </remarks>
    sealed class PopInEffect : MonoBehaviour
    {
        const float Duration = 0.7f;
        const float Overshoot = 1.9f; // 大きいほど、一度大きくなりすぎる量が増えます

        Vector3 _scale;
        float _time;
        bool _started;

        void OnEnable()
        {
            if (_started) return;
            _started = true;
            _scale = transform.localScale;
            transform.localScale = Vector3.zero;
            var fx = DeliveryFx.Of(this);
            var reactions = fx != null ? fx.Reactions : null;
            if (reactions != null && reactions.StepItemSparkles != null)
                Instantiate(reactions.StepItemSparkles, transform.position, Quaternion.identity, fx.FxParent).transform.localScale = Vector3.one * reactions.StepItemSparklesScale;
        }

        void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / Duration);
            transform.localScale = _scale * EaseOutBack(t);
            if (t >= 1f) Finish();
        }

        void OnDisable()
        {
            if (_started) Finish();
        }

        void Finish()
        {
            _started = false;
            transform.localScale = _scale;
            Destroy(this);
        }

        // t が 0 で 0、1 で 1 を返し、途中で一度 1 を超えます。
        static float EaseOutBack(float t)
        {
            var c3 = Overshoot + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + Overshoot * u * u;
        }
    }
}
