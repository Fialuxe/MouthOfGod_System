using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女が宙に浮いているように見せます。ゆっくり上下に揺れ、体験者の方を向きます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 巫女の一番上のオブジェクトに付けます。動かすのは、このオブジェクトだけです。
    /// 身振りは Animator が受け持ちます。
    /// </para>
    /// <para>
    /// 位置を毎フレーム上書きするので、ほかから transform.position を動かしても効きません。
    /// 居場所を変えるときは、<see cref="Anchor"/> を動かします。<see cref="MikoMover"/> がこれを使います。
    /// </para>
    /// </remarks>
    public sealed class MikoFloat : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.01f;  // 上下の揺れの幅（m）
        [SerializeField] float period = 2.5f;  // 揺れの周期（秒）。0.01 より小さい値は 0.01 として扱います
        [SerializeField] float turnSpeed = 180f;  // 向きを変える速さ（度/秒）

        Vector3 _anchor;

        /// <summary>揺れの中心です。ワールド座標で表します。</summary>
        /// <remarks>有効になったときに、いまの位置で初期化します。</remarks>
        public Vector3 Anchor
        {
            get => _anchor;
            set => _anchor = value;
        }

        /// <summary>向く相手です。<see langword="null"/> なら、メインカメラの方を向きます。</summary>
        /// <remarks>上下には傾かず、水平の向きだけを合わせます。</remarks>
        public Transform LookAt { get; set; }

        void OnEnable() => _anchor = transform.position;

        void Update()
        {
            float y = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(period, 0.01f)) * amplitude;
            transform.position = _anchor + Vector3.up * y;

            Transform target = LookAt;
            if (target == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-6f) return;
            Quaternion look = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }
}
