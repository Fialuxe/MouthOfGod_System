using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が出るとき、小さい所から少し大きくなりすぎてから、元の大きさに収まります。出た場所に星の粒も出します。
    /// </summary>
    /// <remarks>
    /// <see cref="FieldStep"/> が、Play 中だけ付けます。終わるか途中で消えると、元の大きさに戻して自分を外します。
    /// </remarks>
    sealed class PopInEffect : MonoBehaviour
    {
        const float Duration = 0.7f;
        const float Overshoot = 1.9f; // 1 を超えた分だけ、一度大きくなりすぎます
        const string AppearFx = "Fx/Fx_Joy";
        const float AppearFxScale = 2.5f; // 村人の頭の上の粒より目立たせます

        Vector3 _scale;
        float _time;
        bool _started;

        void OnEnable()
        {
            if (_started) return;
            _started = true;
            _scale = transform.localScale;
            transform.localScale = Vector3.zero;
            var fx = Resources.Load<GameObject>(AppearFx);
            if (fx != null) Instantiate(fx, transform.position, Quaternion.identity, DeliveryFx.FxParentOf(this)).transform.localScale = Vector3.one * AppearFxScale;
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

        static float EaseOutBack(float t)
        {
            var c3 = Overshoot + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + Overshoot * u * u;
        }
    }
}
