using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MouthOfGod.Core;
using UnityEngine;

// 立つ場所の判定（Tick など）を、テストから直接呼ぶため。
[assembly: InternalsVisibleTo("MouthOfGod.Village.Tests")]

namespace MouthOfGod.Village
{
    /// <summary>
    /// 体験者が、そのフィールドの前に立っているかを判定する場所。フィールドごとに 1 つ置く。
    /// 頭が箱に入ったら Core の <see cref="Standing.Set"/> を呼び、出たら <see cref="Standing.Leave"/> を呼ぶ。
    /// 立っている場所は、チュートリアルの案内と、そのフィールド専用の食べ物の表を選ぶのに使う。物がどこに届くかは、落ちた所（DeliveryZone）で決まる。
    /// </summary>
    /// <remarks>
    /// - 箱は、このオブジェクトのローカル座標で持つ。台ごと動いても（AprilTag で合わせても）ついてくる。
    /// - 出るときだけ、箱を <c>Margin</c> だけ広げて判定する。境目で、入る・出るをくり返さない。
    /// - 箱が重なっているときは、あとから入った場所が勝つ。そこを出たとき、まだ入っている場所があれば、そちらに戻る。
    /// - フィールドは <c>Field Id</c>。空なら、親にあるフィールド（FieldStages）。
    /// - 床の印（<c>Footprint</c>）は、合図 <c>&lt;field&gt;.footprint</c> で出し、<c>&lt;field&gt;.footprint-off</c> で消す。
    /// - ヘッドセットなしで試すときは、右クリックの「Stand Here」「Leave」を使う。
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

        [Tooltip("Play の始めに、床の印を消しておく（合図で出す）")]
        [SerializeField] bool hideFootprintAtStart = true;

        [Tooltip("立った・離れたを Console に出す")]
        [SerializeField] bool logEvents = true;

        // いま頭が入っている場所（入った順）。最後が、いま立っている場所。
        static readonly List<StandArea> Occupied = new List<StandArea>();

        bool _inside;
        // 右クリックの「Stand Here」で立たせた。頭が外にあっても、出たことにしない。
        bool _pinned;

        /// <summary>この場所のフィールド。Field Id が空なら、親のフィールド。見つからなければ <see cref="FieldId.None"/>。</summary>
        public FieldId Field
        {
            get
            {
                if (!string.IsNullOrEmpty(fieldId)) return new FieldId(fieldId);
                var parent = GetComponentInParent<IFieldRequests>();
                return parent != null ? parent.Field : FieldId.None;
            }
        }

        /// <summary>頭が、いまこの場所に入っているか。</summary>
        public bool IsInside => _inside;

        // テスト用。null の項目は変えない。
        internal void Configure(string fieldId = null, Vector3? center = null, Vector3? size = null, float? margin = null,
                                GameObject footprint = null)
        {
            if (fieldId != null) this.fieldId = fieldId;
            if (center.HasValue) this.center = center.Value;
            if (size.HasValue) this.size = size.Value;
            if (margin.HasValue) this.margin = margin.Value;
            if (footprint != null) this.footprint = footprint;
        }

        /// <summary>
        /// ローカル座標の点が、箱（中心・大きさ）を margin だけ広げた中にあるか。
        /// ignoreHeight なら、高さ（y）は見ない。広げた大きさが負の向きは、何も入らない。
        /// </summary>
        internal static bool Contains(Vector3 localPoint, Vector3 center, Vector3 size, float margin, bool ignoreHeight)
        {
            var half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f + Vector3.one * margin;
            var d = localPoint - center;
            if (half.x < 0f || half.z < 0f) return false;
            if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.z) > half.z) return false;
            if (ignoreHeight) return true;
            return half.y >= 0f && Mathf.Abs(d.y) <= half.y;
        }

        /// <summary>
        /// 次のフレームで、中にいるか。外にいたなら、箱そのもので入ったかを見る。中にいたなら、margin だけ広げた箱で出たかを見る。
        /// </summary>
        internal static bool NextInside(bool wasInside, Vector3 localPoint, Vector3 center, Vector3 size, float margin, bool ignoreHeight)
            => Contains(localPoint, center, size, wasInside ? margin : 0f, ignoreHeight);

        void OnEnable() => Cues.Requested += OnCue;

        void OnDisable()
        {
            Cues.Requested -= OnCue;
            _pinned = false;
            Exit();
        }

        void Start()
        {
            if (hideFootprintAtStart && footprint != null) footprint.SetActive(false);
            if (Field.IsEmpty)
                Debug.LogWarning($"[StandArea] {name}: フィールドの ID が空です（Field Id に入れるか、FieldStages の子に置きます）。", this);
        }

        void Update()
        {
            var target = head;
            if (target == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                target = camera.transform;
            }
            Tick(target.position);
        }

        /// <summary>頭の位置（ワールド座標）から、入った・出たを判定する。毎フレーム呼ぶ。テストからも呼ぶ。</summary>
        internal void Tick(Vector3 headWorld)
        {
            var local = transform.InverseTransformPoint(headWorld);
            var inside = NextInside(_inside, local, center, size, margin, ignoreHeight);
            if (inside && !_inside) Enter();
            else if (!inside && _inside && !_pinned) Exit();
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
            // 重なった場所に、まだ入っていれば、そちらに戻す（最後に入った場所）。
            if (Standing.Field.IsEmpty && Occupied.Count > 0)
            {
                var back = Occupied[Occupied.Count - 1];
                Standing.Set(back.Field);
            }
        }

        // テストからも呼ぶ。
        internal void OnCue(CueRequested request)
        {
            if (footprint == null) return;
            var field = Field;
            if (field.IsEmpty) return;
            var cue = request.Cue.Value;
            if (cue == field.Value + ".footprint") footprint.SetActive(true);
            else if (cue == field.Value + ".footprint-off") footprint.SetActive(false);
        }

        // 開発中の確認用（右クリックのメニュー）。ヘッドセットなしで、ここに立ったことにする。
        [ContextMenu("Stand Here")]
        internal void StandHere()
        {
            _pinned = true;
            if (_inside)
            {
                // もう入っていても、いま立っている場所にする
                Occupied.Remove(this);
                Occupied.Add(this);
                Standing.Set(Field);
                return;
            }
            Enter();
        }

        [ContextMenu("Leave")]
        internal void LeaveHere()
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

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の場所を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAll() => Occupied.Clear();
    }
}
