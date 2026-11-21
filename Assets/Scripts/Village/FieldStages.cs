using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド 1 つの、欲しい物と戻り方を管理します。
    /// フィールドは村・海・草原・山です。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フィールドの Prefab の一番上に付けます。
    /// 戻り方の段は、子にある <see cref="FieldStep"/> を Hierarchy の並び順に使います。
    /// 段に番号はありません。
    /// </para>
    /// <para>
    /// 有効になると Core の <see cref="Fields"/> に登録します。
    /// ほかの部品は、このクラスを <see cref="IFieldRequests"/> として受け取ります。
    /// </para>
    /// <para>
    /// いつ欲しい物を出すかは、呼ぶ側の Flow の場面が決めます。
    /// このクラスは、出す・受け取る・戻すだけを行います。
    /// </para>
    /// </remarks>
    public sealed class FieldStages : MonoBehaviour, IFieldRequests
    {
        public enum RequestOrder { InOrder, Random }

        [Tooltip("フィールドの ID です。village / mountain / sea / grassland のどれかを入れます。空のままだと、ほかの部品から見つけられません（Play の始めに警告を出します）。")]
        [SerializeField] string fieldId = "";

        [Tooltip("体験者に来てほしいときに光らせる物です。Flow の Field.Glow で出し入れします。空なら光りません。")]
        [SerializeField] GameObject glow;

        [Tooltip("欲しい物を出す順番です。InOrder: Steps の並び順に出します。Random: まだのステップからランダムに選びます。")]
        [SerializeField] RequestOrder requestOrder = RequestOrder.InOrder;

        [Tooltip("このフィールドに、同時に出せる欲しい物の数です。別々の村人が、それぞれ別の物を欲しがれます。1 以上にします。")]
        [SerializeField, Min(1)] int maxOpenRequests = 2;

        static readonly System.Random Rng = new System.Random();

        FieldStep[] _steps;
        StepProgress _progress;
        // 見た目を写し直している間は true です。
        // この間に Open されると、村人の吹き出しの出し入れが前後してしまいます。
        bool _closing;

        /// <inheritdoc/>
        /// <remarks>
        /// 欲しがっていた物が届いたときは、<see cref="StepDone"/> より先に出ます。
        /// 村人の吹き出しを閉じたあとです。
        /// </remarks>
        public event Action<Delivered> Received;

        /// <summary>
        /// ステップが済んだときに出ます。
        /// 助かった村人の吹き出しを閉じたあとに出ます。
        /// </summary>
        public event Action<FieldStep> StepDone;

        public FieldId Field => new FieldId(fieldId);
        public bool IsOpen => Progress.IsOpen;
        public bool CanOpen => !_closing && Progress.CanOpen;

        /// <summary>出ている欲しい物のうち、いちばん古い物です。</summary>
        /// <value>食べ物の Prefab です。出ていなければ <see langword="null"/> です。</value>
        public GameObject Current => Progress.Current;

        public int OpenCount => Progress.OpenIndices.Count;

        /// <summary>光らせる物を出すか、消します。</summary>
        /// <param name="on"><see langword="true"/> なら出し、<see langword="false"/> なら消します。光らせる物が空なら何もしません。</param>
        public void SetGlow(bool on)
        {
            if (glow != null) glow.SetActive(on);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 出す順は、Inspector の Request Order で決まります。
        /// 見た目を写し直している間は、出さずに <see langword="false"/> を返します。
        /// </remarks>
        public bool Open()
        {
            if (_closing || !Progress.TryOpen(out var index, requestOrder == RequestOrder.Random ? Rng : null)) return false;
            return Issue(index);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <paramref name="food"/> が <see langword="null"/> のときは、出さずに <see langword="false"/> を返します。
        /// </remarks>
        public bool Open(GameObject food)
        {
            if (_closing || !Progress.TryOpen(food, out var index)) return false;
            return Issue(index);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 欲しがっていた物なら、ステップを済ませて村人の吹き出しを閉じます。
        /// そのあと、<see cref="Received"/>、<see cref="StepDone"/> の順に出します。
        /// </remarks>
        public DeliveryKind Receive(GameObject food)
        {
            if (!Progress.TryResolve(food, out var index))
            {
                var kind = Progress.Wants(food) ? DeliveryKind.Own : DeliveryKind.Foreign;
                Received?.Invoke(new Delivered(food, Field, kind));
                return kind;
            }

            // 見た目を先に変えます。知らせを受けてすぐ Open しても、次のステップが出るようにするためです。
            ApplyThenClose(new[] { index }, resolved: true, celebrate: true);
            Received?.Invoke(new Delivered(food, Field, DeliveryKind.Requested));
            RaiseStepDone(index);
            return DeliveryKind.Requested;
        }

        /// <inheritdoc/>
        public void Expire()
        {
            foreach (var index in Progress.CloseAll()) VillagerOf(index)?.CloseWant(resolved: false);
        }

        /// <inheritdoc/>
        public void Restart()
        {
            var closes = Progress.CloseAll();
            Progress.Reset();
            ApplyThenClose(closes, resolved: false, celebrate: false);
        }

        void OnEnable() => Fields.Register(this);

        void OnDisable()
        {
            Expire(); // 先に終えておくと、吹き出しが残りません
            Fields.Unregister(this);
        }

        void Start()
        {
            if (!TryGetComponent<DeliveryFx>(out _)) Debug.LogWarning($"[FieldStages] {name}: 届いたときの反応（DeliveryFx）がありません。", this);
            WarnIfIncomplete();
            Apply(celebrate: false);
        }

        FieldStep[] StepsInOrder => _steps ??= GetComponentsInChildren<FieldStep>(includeInactive: true);

        // 初めて読まれたときに、ステップの並びから作ります。
        // Start より先に Open などが呼ばれても使えるようにするためです。
        StepProgress Progress
        {
            get
            {
                if (_progress != null) return _progress;
                var foods = new GameObject[StepsInOrder.Length];
                for (var i = 0; i < foods.Length; i++) foods[i] = StepsInOrder[i].Food;
                return _progress = new StepProgress(foods, maxOpenRequests);
            }
        }

        // 欲しい物を出し、そのステップで助かる村人に、吹き出しで見せてもらいます。
        bool Issue(int index)
        {
            GameObject food = Progress.WantOf(index);
            Debug.Log($"[FieldStages] {fieldId} wants {food.name} ({StepsInOrder[index].name})");
            var item = food.GetComponent<IFoodItem>();
            VillagerOf(index)?.ShowWant(item?.Icon, food.name);
            return true;
        }

        // ステップで助かる村人のうち、最初の人を返します。吹き出しはこの人が出します。
        VillagerMood VillagerOf(int index)
        {
            foreach (var mood in StepsInOrder[index].Helped)
                if (mood != null) return mood;
            return null;
        }

        // 見た目を写し直してから、closes のステップの吹き出しを閉じます。
        void ApplyThenClose(IReadOnlyList<int> closes, bool resolved, bool celebrate)
        {
            _closing = closes.Count > 0;
            try { Apply(celebrate); }
            finally { _closing = false; }
            foreach (var index in closes) VillagerOf(index)?.CloseWant(resolved);
        }

        void RaiseStepDone(int index)
        {
            StepsInOrder[index].RaiseDone();
            StepDone?.Invoke(StepsInOrder[index]);
        }

        // 済んだステップの物を出し、消す物を消し、村人の気持ちを合わせます。
        // 1 つの物が、あるステップで出て、別のステップで消えるときは、消すほうを優先します。
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
                if (step.Food == null) Debug.LogWarning($"[FieldStages] {name}/{step.name}: 欲しい物（Wanted Food）が空です（このステップは出ません）。", step);
                if (step.Helped.Count == 0 || step.Helped[0] == null) Debug.LogWarning($"[FieldStages] {name}/{step.name}: 助かる人（Helped）が空です（吹き出しは出ません）。", step);
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
            var closes = Progress.IsOpenAt(index) ? new[] { index } : new int[0];
            Progress.Complete(index);
            ApplyThenClose(closes, resolved: true, celebrate: true);
            RaiseStepDone(index);
        }

        [ContextMenu("Restart")]
        void RestartFromMenu() => Restart();
    }
}
