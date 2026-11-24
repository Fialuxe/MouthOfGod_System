using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 体験者がフィールドの前に立っているかを判定します。フィールドごとに 1 つ置きます。
    /// 頭が箱に入ると <see cref="Standing.Set"/> を、出ると <see cref="Standing.Leave"/> を呼びます。
    /// </summary>
    /// <remarks>
    /// 箱はこの物のローカル座標で持つので、台ごと動いてもついてきます。
    /// 出るときだけ箱を <c>margin</c> だけ広げて判定し、境目で入る・出るをくり返さないようにします。
    /// 箱が重なっているときは、あとから入った場所が勝ちます。そこを出たとき、まだ入っている場所があれば、そちらに戻ります。
    /// ヘッドセットなしで試すときは、右クリックの「Stand Here」「Leave」で立った・離れたにできます。
    /// </remarks>
    public sealed class StandArea : MonoBehaviour
    {
        [Tooltip("このフィールドの ID（village など）。空なら、親にあるフィールド（FieldStages）の ID")]
        [SerializeField] string fieldId = "";

        [Tooltip("立つ場所の箱の中心（このオブジェクトのローカル座標）")]
        [SerializeField] Vector3 center = new Vector3(0f, 1f, 0f);

        [Tooltip("立つ場所の箱の大きさ（このオブジェクトのローカル座標）。台の 1 辺の前に、体験者が立つ広さにする")]
        [SerializeField] Vector3 size = new Vector3(0.8f, 2f, 0.6f);

        [Tooltip("高さを見ない（床の上の広さだけで判定する）。しゃがんでも、背が高くても、同じに扱う")]
        [SerializeField] bool ignoreHeight = true;

        [Tooltip("出たと判定するまでの余白（ローカル座標の長さ）。境目で、入る・出るをくり返さないようにする")]
        [SerializeField, Min(0f)] float margin = 0.1f;

        [Tooltip("体験者の頭。空なら、Main Camera")]
        [SerializeField] Transform head;

        [Tooltip("床の印（足あと）。合図 <field>.footprint で出し、<field>.footprint-off で消す。空でもよい")]
        [SerializeField] GameObject footprint;

        [Tooltip("体験者が立ったら、床の印を消す")]
        [SerializeField] bool hideFootprintOnArrive = true;

        [Tooltip("Play の始めに、床の印を消しておく（必要なときに ShowFootprint で出す）")]
        [SerializeField] bool hideFootprintAtStart = true;

        [Tooltip("立った・離れたを Console に出す")]
        [SerializeField] bool logEvents = true;

        static readonly List<StandArea> Occupied = new List<StandArea>(); // 頭が入っている場所。最後が、いま立っている場所です

        bool _inside;
        bool _pinned; // 「Stand Here」で立たせたときは、頭が外にあっても出たことにしません

        /// <summary>この場所のフィールドです。見つからなければ <see cref="FieldId.None"/> です。</summary>
        public FieldId Field
        {
            get
            {
                if (!string.IsNullOrEmpty(fieldId)) return new FieldId(fieldId);
                var parent = GetComponentInParent<IFieldRequests>();
                return parent != null ? parent.Field : FieldId.None;
            }
        }

        /// <summary>頭が、いまこの場所に入っています。</summary>
        public bool IsInside => _inside;

        /// <summary>足あとを出すか、消します。</summary>
        public void ShowFootprint(bool on)
        {
            if (footprint != null) footprint.SetActive(on);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Occupied.Clear();

        void Start()
        {
            if (hideFootprintAtStart && footprint != null) footprint.SetActive(false);
            if (Field.IsEmpty)
                Debug.LogWarning($"[StandArea] {name}: フィールドの ID が空です（Field Id に入れるか、FieldStages の子に置きます）。", this);
        }

        void Update()
        {
            Transform target = head != null ? head : Camera.main != null ? Camera.main.transform : null;
            if (target == null) return;

            var local = transform.InverseTransformPoint(target.position);
            var inside = Contains(local, _inside ? margin : 0f);
            if (inside && !_inside) Enter();
            else if (!inside && _inside && !_pinned) Exit();
        }

        void OnDisable()
        {
            _pinned = false;
            Exit();
        }

        bool Contains(Vector3 localPoint, float grow)
        {
            var half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f + Vector3.one * grow;
            var d = localPoint - center;
            if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.z) > half.z) return false;
            return ignoreHeight || Mathf.Abs(d.y) <= half.y;
        }

        void Enter()
        {
            var field = Field;
            if (field.IsEmpty) return;
            _inside = true;
            Occupied.Remove(this);
            Occupied.Add(this);
            Standing.Set(field);
            if (logEvents) Debug.Log($"[StandArea] {field} に立った（{name}）", this);
            if (hideFootprintOnArrive && footprint != null) footprint.SetActive(false);
        }

        void Exit()
        {
            if (!_inside) return;
            _inside = false;
            Occupied.Remove(this);
            Standing.Leave(Field);
            if (logEvents) Debug.Log($"[StandArea] {Field} から離れた（{name}）", this);
            if (Standing.Field.IsEmpty && Occupied.Count > 0) Standing.Set(Occupied[Occupied.Count - 1].Field);
        }

        [ContextMenu("Stand Here")]
        void StandHere()
        {
            _pinned = true;
            if (!_inside)
            {
                Enter();
                return;
            }
            Occupied.Remove(this);
            Occupied.Add(this);
            Standing.Set(Field);
        }

        [ContextMenu("Leave")]
        void LeaveHere()
        {
            _pinned = false;
            Exit();
        }

        void OnDrawGizmos() => DrawBox(selected: false);

        void OnDrawGizmosSelected() => DrawBox(selected: true);

        void DrawBox(bool selected)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            var color = _inside ? new Color(0.2f, 1f, 0.3f, 1f) : new Color(0.2f, 0.7f, 1f, 1f);
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            if (!selected) return;
            color.a = 0.25f;
            Gizmos.color = color;
            Gizmos.DrawCube(center, size);
        }
    }
}
