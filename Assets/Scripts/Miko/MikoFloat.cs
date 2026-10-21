using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女を、人が宙に浮いているように見せる。ゆっくり上下に揺れ、プレイヤー（メインカメラ）の方を向く。
    /// 動かすのは、このコンポーネントが付いた一番上のオブジェクトだけ。身振りは Animator が担当する。
    /// </summary>
    public sealed class MikoFloat : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.01f; // 上下の揺れの幅（m）
        [SerializeField] float period = 2.5f; // 揺れの周期（秒）
        [SerializeField] float turnSpeed = 180f; // 向きを変える速さ（度 / 秒）

        Vector3 _basePosition;

        void OnEnable() => _basePosition = transform.localPosition;

        void Update()
        {
            float y = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(period, 0.01f)) * amplitude;
            transform.localPosition = _basePosition + Vector3.up * y;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-6f) return;
            Quaternion target = Quaternion.LookRotation(toCamera, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
