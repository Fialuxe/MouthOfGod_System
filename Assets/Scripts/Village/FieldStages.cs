using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド（村・海・草原・山）1 つの、欲しい物と戻り方を管理します。フィールドの Prefab の一番上に付けます。
    /// </summary>
    /// <remarks>
    /// 戻り方の段は、子にある <see cref="FieldStep"/> を Hierarchy の並び順に使います。段に番号はありません。
    /// 有効になると Core の <see cref="Fields"/> に登録し、ほかの部品は <see cref="IFieldRequests"/> として受け取ります。
    /// いつ欲しい物を出すかは、呼ぶ側（Flow の場面）が決めます。このクラスは、出す・受け取る・戻すだけを行います。
    /// </remarks>
    public sealed class FieldStages : MonoBehaviour, IFieldRequests
    {
        public enum RequestOrder { InOrder, Random }

        [Tooltip("フィールドの ID。village / mountain / sea / grassland のどれか。空のままだと、ほかの部品から見つけられない（Play の始めに警告）")]
        [SerializeField] string fieldId = "";

        [Tooltip("体験者に来てほしいときに光らせる物。Flow の Field.Glow で出し入れする。空なら光らない")]
        [SerializeField] GameObject glow;

        [Tooltip("欲しい物を出す順番。InOrder: Steps の並び順。Random: まだのステップからランダム")]
        [SerializeField] RequestOrder requestOrder = RequestOrder.InOrder;

        [Tooltip("このフィールドに、同時に出せる欲しい物の数。別々の村人が、それぞれ別の物を欲しがれる")]
        [SerializeField, Min(1)] int maxOpenRequests = 2;

        static readonly System.Random Rng = new System.Random();

        FieldStep[] _steps;
        StepProgress _progress;
        readonly Dictionary<int, int> _requests = new Dictionary<int, int>(); // ステップの番号 → 欲しい物の番号
        int _lastRequest;
        bool _closing; // 見た目を写し直している間。ここで Open されると、Issued が Closed より先に出てしまいます

        public event Action<RequestIssued> Issued;
        public event Action<RequestClosed> Closed;
        public event Action<Delivered> Received;

        /// <summary>ステップが済んだときに出ます。Closed のあとです。</summary>
        public event Action<FieldStep> StepDone;

        public FieldId Field => new FieldId(fieldId);
        public bool IsOpen => Progress.IsOpen;
        public bool CanOpen => !_closing && Progress.CanOpen;

        /// <summary>出ている欲しい物のうち、いちばん古い物です。出ていなければ <see cref="FoodId.None"/> です。</summary>
        public FoodId Current => Progress.Current;

        /// <summary>出ている欲しい物の数です。</summary>
        public int OpenCount => Progress.OpenIndices.Count;

        /// <summary>光らせる物を出すか、消します。</summary>
        public void SetGlow(bool on)
        {
            if (glow != null) glow.SetActive(on);
        }

        /// <summary>次の欲しい物を出します。上限まで出ているか、出せる物が残っていなければ false です。</summary>
        public bool Open()
        {
            if (_closing || !Progress.TryOpen(out var index, requestOrder == RequestOrder.Random ? Rng : null)) return false;
            return Issue(index);
        }

        /// <summary>food を欲しがる、まだ済んでいないステップを出します。並び順は見ません。出せなければ false です。</summary>
        public bool Open(FoodId food)
        {
            if (_closing || !Progress.TryOpen(food, out var index)) return false;
            return Issue(index);
        }

        /// <summary>
        /// 届いた物を受け取り、何だったかを返します。<see cref="Received"/> は必ず 1 回出ます。
        /// 欲しがっていた物ならステップを済ませ、Closed → Received → StepDone の順に出します。
        /// </summary>
        public DeliveryKind Receive(FoodId food)
        {
            if (!Progress.TryResolve(food, out var index))
            {
                var kind = Progress.Wants(food) ? DeliveryKind.Own : DeliveryKind.Foreign;
                Received?.Invoke(new Delivered(food, Field, kind));
                return kind;
            }

            // 見た目を先に変えます。Closed を受けてすぐ Open しても、次のステップが出るようにするためです。
            ApplyThenClose(new[] { Take(index) }, resolved: true, celebrate: true);
            Received?.Invoke(new Delivered(food, Field, DeliveryKind.Requested));
            RaiseStepDone(index);
            return DeliveryKind.Requested;
        }

        /// <summary>出ている欲しい物を、すべて届かないまま終えます。</summary>
        public void Expire()
        {
            foreach (var index in Progress.CloseAll())
            {
                var (food, request) = Take(index);
                Closed?.Invoke(new RequestClosed(food, Field, resolved: false, request));
            }
        }

        /// <summary>request の番号の欲しい物だけを、届かないまま終えます。</summary>
        public void Expire(int request)
        {
            var index = -1;
            foreach (var pair in _requests)
                if (pair.Value == request) index = pair.Key;
            if (index < 0 || !Progress.Close(index)) return;
            var (food, _) = Take(index);
            Closed?.Invoke(new RequestClosed(food, Field, resolved: false, request));
        }

        /// <summary>すべてのステップを、まだの状態に戻します。出ていた欲しい物も終えます。</summary>
        public void Restart()
        {
            var closes = new List<(FoodId, int)>();
            foreach (var index in Progress.CloseAll()) closes.Add(Take(index));
            Progress.Reset();
            ApplyThenClose(closes, resolved: false, celebrate: false);
        }

        void Awake()
        {
            // 届いたときの反応が無いフィールドでも、標準の反応を出します。
            if (!TryGetComponent<DeliveryFx>(out _)) gameObject.AddComponent<DeliveryFx>();
        }

        void OnEnable() => Fields.Register(this);

        void OnDisable()
        {
            Expire(); // 先に終えておくと、吹き出しが残りません
            Fields.Unregister(this);
        }

        void Start()
        {
            WarnIfIncomplete();
            Apply(celebrate: false);
        }

        FieldStep[] StepsInOrder => _steps ??= GetComponentsInChildren<FieldStep>(includeInactive: true);

        StepProgress Progress
        {
            get
            {
                if (_progress != null) return _progress;
                var foods = new FoodId[StepsInOrder.Length];
                for (var i = 0; i < foods.Length; i++) foods[i] = new FoodId(StepsInOrder[i].Food);
                return _progress = new StepProgress(foods, maxOpenRequests);
            }
        }

        bool Issue(int index)
        {
            var food = Progress.WantOf(index);
            var request = ++_lastRequest;
            _requests[index] = request;
            Debug.Log($"[FieldStages] {fieldId} wants {food} ({StepsInOrder[index].name}, #{request})");
            Issued?.Invoke(new RequestIssued(food, Field, StepsInOrder[index].Anchor, request));
            return true;
        }

        (FoodId Food, int Request) Take(int index)
        {
            _requests.TryGetValue(index, out var request);
            _requests.Remove(index);
            return (Progress.WantOf(index), request);
        }

        void ApplyThenClose(IReadOnlyList<(FoodId Food, int Request)> closes, bool resolved, bool celebrate)
        {
            _closing = closes.Count > 0;
            try { Apply(celebrate); }
            finally { _closing = false; }
            foreach (var (food, request) in closes) Closed?.Invoke(new RequestClosed(food, Field, resolved, request));
        }

        void RaiseStepDone(int index)
        {
            StepsInOrder[index].RaiseDone();
            StepDone?.Invoke(StepsInOrder[index]);
        }

        // 済んだステップの物を出し、消す物を消し、村人の気持ちを合わせます。消すほうが、出すより優先です。
        void Apply(bool celebrate)
        {
            var steps = StepsInOrder;
            foreach (var step in steps)
            foreach (var go in step.HideWhenDone)
                if (go != null) go.SetActive(true);
            for (var i = 0; i < steps.Length; i++) steps[i].ShowChildren(Progress.IsDone(i), celebrate);
            for (var i = 0; i < steps.Length; i++)
            {
                if (!Progress.IsDone(i)) continue;
                foreach (var go in steps[i].HideWhenDone)
                    if (go != null) go.SetActive(false);
            }

            // 村人は、自分の名前があるステップがすべて済むまで悩みます。
            var worried = new Dictionary<VillagerMood, bool>();
            foreach (var mood in GetComponentsInChildren<VillagerMood>(includeInactive: true)) worried[mood] = false;
            for (var i = 0; i < steps.Length; i++)
            foreach (var mood in steps[i].Helped)
                if (mood != null) worried[mood] = (worried.TryGetValue(mood, out var w) && w) || !Progress.IsDone(i);
            foreach (var pair in worried) pair.Key.SetWorried(pair.Value, celebrate);
        }

        void WarnIfIncomplete()
        {
            if (StepsInOrder.Length == 0) Debug.LogWarning($"[FieldStages] {name}: ステップ（FieldStep）が 1 つもありません。", this);
            foreach (var step in StepsInOrder)
            {
                if (string.IsNullOrEmpty(step.Food)) Debug.LogWarning($"[FieldStages] {name}/{step.name}: 欲しい物（Food）が空です（このステップは出ません）。", step);
                if (step.Helped.Count == 0 || step.Helped[0] == null) Debug.LogWarning($"[FieldStages] {name}/{step.name}: 助かる人（Helped）が空です（吹き出しはステップの場所に出ます）。", step);
            }
        }

        [ContextMenu("Open Next Want")]
        void OpenFromMenu()
        {
            if (!Open()) Debug.Log($"[FieldStages] {fieldId}: nothing to open (open: {OpenCount}/{maxOpenRequests}).", this);
        }

        [ContextMenu("Deliver Wanted Food")]
        void ReceiveFromMenu() => Receive(Current);

        [ContextMenu("Complete Next Step")]
        void CompleteFromMenu()
        {
            var index = Progress.IsOpen ? Progress.OpenIndex : Progress.NextIndex;
            if (index < 0)
                for (var i = 0; i < StepsInOrder.Length && index < 0; i++)
                    if (!Progress.IsDone(i)) index = i; // 欲しい物が空のステップも済ませられます
            if (index < 0) return;
            var closes = Progress.IsOpenAt(index) ? new[] { Take(index) } : new (FoodId, int)[0];
            Progress.Complete(index);
            ApplyThenClose(closes, resolved: true, celebrate: true);
            RaiseStepDone(index);
        }

        [ContextMenu("Restart")]
        void RestartFromMenu() => Restart();
    }
}
