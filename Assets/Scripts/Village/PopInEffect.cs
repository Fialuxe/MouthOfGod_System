using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// ステップの子の物を、このスクリプトで「ぽんっ」と出さないようにする印。付けた物は、出るときに拡大しない。
    /// 自分のスクリプトで出方を作る物（OnEnable で大きさを動かす物など）に付ける。
    /// </summary>
    public sealed class SkipPopIn : MonoBehaviour { }

    /// <summary>
    /// 物が出るとき、小さい所から少し大きくなりすぎて、元の大きさに収まる。<see cref="FieldStep"/> が、出す物に実行時だけ付ける（シーンや Prefab には残らない）。
    /// 出た場所に、星の粒（Resources の Fx_Joy。無ければ出さない）も 1 回出す。
    /// 終わると元の大きさに戻し、自分を消す。途中で消された（リセットなど）ときも、元の大きさに戻す。
    /// </summary>
    sealed class PopInEffect : MonoBehaviour
    {
        const float Duration = 0.7f;
        const float AppearFxScale = 2.5f; // 星の粒の大きさと広がりの倍率。喜びの粒（村人の頭の上）より目立たせる
        const string AppearFx = "Prefabs/Village/Fx_Joy"; // 出た場所で、星の粒を 1 回出す（小さい物でも、出たと分かるように）
        // 1.0 を超えるぶんだけ、一度大きくなりすぎてから収まる
        const float Overshoot = 1.9f;

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
            if (fx != null) Instantiate(fx, transform.position, Quaternion.identity).transform.localScale = Vector3.one * AppearFxScale;
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
