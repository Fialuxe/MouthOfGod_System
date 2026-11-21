using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女の身振り（指さし）。指さす先の方を向き、腕を向け、Animator の Point を鳴らす。
    /// </summary>
    public sealed class MikoGesture : MonoBehaviour
    {
        static readonly int PointTrigger = Animator.StringToHash("Point");

        [SerializeField] Animator animator;
        [SerializeField] Transform pointTarget; // 指さす先の既定。PointAt に先を渡さなかったときに使う
        [SerializeField] float pointLookSeconds = 3f; // 指さしたあと、その方を向いている時間

        float _lookUntil;
        Transform _aim;


        void Update()
        {
            if (_lookUntil > 0f && Time.time >= _lookUntil)
            {
                _lookUntil = 0f;
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null) mikoFloat.LookAt = null;
            }
        }

        /// <summary>target の方を向いて、指さしをする。target が空なら、既定の先（Point Target）を使う。</summary>
        public void PointAt(Transform target)
        {
            _aim = target;
            Point();
        }

        // Editor から試す。Play 中に、コンポーネントの右上のメニュー（⋮）から選ぶ。
        [ContextMenu("Point")]
        void PointFromEditor() => Point();

        /// <summary>指さしをする。</summary>
        public void Point()
        {
            var target = _aim != null ? _aim : pointTarget;
            _aim = null;
            if (target != null)
            {
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null)
                {
                    mikoFloat.LookAt = target;
                    _lookUntil = Time.time + pointLookSeconds;
                }
                GetComponent<MikoPointer>()?.Begin(target);
            }
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(PointTrigger);
        }
    }
}
