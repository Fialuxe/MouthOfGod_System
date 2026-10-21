using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女を、人が宙に浮いているように見せる。ゆっくり上下に揺れ、プレイヤー（メインカメラ）の方を向く。
    /// 動かすのは、このコンポーネントが付いた一番上のオブジェクトだけ。身振りは Animator が担当する。
    /// 居場所（揺れの中心）は <see cref="Anchor"/> で、MikoMover が動かす。
    /// </summary>
    public sealed class MikoFloat : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.01f; // 上下の揺れの幅（m）
        [SerializeField] float period = 2.5f; // 揺れの周期（秒）
        [SerializeField] float turnSpeed = 180f; // 向きを変える速さ（度 / 秒）

        Vector3 _anchor;

        /// <summary>揺れの中心（世界座標）。</summary>
        public Vector3 Anchor
        {
            get => _anchor;
            set => _anchor = value;
        }

        /// <summary>向く相手。空なら、プレイヤー（メインカメラ）の方を向く。</summary>
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
