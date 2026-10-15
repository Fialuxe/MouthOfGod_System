using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド（山・海・草原・村）の戻り方と欲しい物。フィールドの一番上の物に 1 つ付ける。
    /// 戻り方は、下にある <see cref="FieldStep"/> を Hierarchy の並び順に使う（番号は持たない）。
    /// 有効になると Core の <see cref="Fields"/> に自分を登録する。本編（Game）とチュートリアル（Tutorial）は、そこから <see cref="IFieldRequests"/> として受け取り、
    /// 欲しい物を出す（<see cref="Open()"/>）。いつ出すかは、ここでは決めない（docs/requests.md）。
    /// </summary>
    public sealed class FieldStages : MonoBehaviour, IFieldRequests
    {
        [Tooltip("フィールドの ID（docs/fields.md）。village / mountain / sea / grassland のどれか。空のままだと、本編などにつながらない（Play の始めに警告）")]
        [SerializeField] string fieldId = "";

        [Tooltip("欲しい物を出す順番。InOrder: Steps の並び順。Random: まだのステップからランダム")]
        [SerializeField] RequestOrder requestOrder = RequestOrder.InOrder;

        public enum RequestOrder { InOrder, Random }

        static readonly System.Random Rng = new System.Random();

        FieldStep[] _steps;
        StepProgress _progress;
        // 見た目を写し直してから Closed を出すまでの間。ここで Open されると Issued が Closed より先に出るので、出させない。
        bool _closing;

        public event Action<RequestIssued> Issued;
        public event Action<RequestClosed> Closed;
        public event Action<Delivered> Received;

        /// <summary>ステップが済んだとき（済んだステップ）。Closed の後に出る。フィールドごとの独自の演出を、ここから足せる。</summary>
        public event Action<FieldStep> StepDone;

        /// <summary>はじめ（全部まだ）に戻したとき。</summary>
        public event Action Restarted;

        /// <summary>フィールドの ID。有効な間に変えると、Fields に登録し直す（空・重なりの確かめもやり直す）。</summary>
        public string FieldId
        {
            get => fieldId;
            set
            {
                if (fieldId == value) return;
                // 登録は Play 中だけ（OnEnable と同じ）。編集中・EditMode のテストでは掲示板に触らない
                var registered = Application.isPlaying && isActiveAndEnabled;
                if (registered) Fields.Unregister(this);
                fieldId = value;
                if (registered) Fields.Register(this);
            }
        }
        public FieldId Field => new FieldId(fieldId);
        public IReadOnlyList<FieldStep> Steps => StepsInOrder;
        public int DoneCount => Progress.DoneCount;
        public bool IsFullyRecovered => Progress.IsComplete;
        public bool IsOpen => Progress.IsOpen;
        public bool CanOpen => !_closing && Progress.CanOpen;

        /// <summary>出ている欲しい物（出ていなければ FoodId.None）。</summary>
        public FoodId Current => Progress.Current;

        public bool IsDone(FieldStep step) => Progress.IsDone(Array.IndexOf(StepsInOrder, step));

        FieldStep[] StepsInOrder => _steps ??= GetComponentsInChildren<FieldStep>(includeInactive: true);

        StepProgress Progress
        {
            get
            {
                if (_progress != null) return _progress;
                var foods = new FoodId[StepsInOrder.Length];
                for (var i = 0; i < foods.Length; i++) foods[i] = new FoodId(StepsInOrder[i].Food);
                return _progress = new StepProgress(foods);
            }
        }

        // 掲示板への登録。ID が空・重なっているときは、Fields が警告を出す。
        void OnEnable() => Fields.Register(this);

        // 出ていた欲しい物は終えてから外す（使う側が Removed で購読を外しても、吹き出しが残らないように）。
        void OnDisable()
        {
            Expire();
            Fields.Unregister(this);
        }

        // その時点の進み具合を見た目に写す（ふつうは、全部のステップがまだの見た目）。抜けがあれば警告する（止めない）。
        void Start()
        {
            WarnIfIncomplete();
            Apply(celebrate: false);
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

        /// <summary>次の欲しい物を出す（Issued）。出ているものがある・全部済んだなら false。</summary>
        public bool Open()
        {
            if (_closing || !Progress.TryOpen(out var index, requestOrder == RequestOrder.Random ? Rng : null)) return false;
            return Issue(index);
        }

        public RequestOrder Order { get => requestOrder; set => requestOrder = value; }

        /// <summary>その物を欲しがる、まだ済んでいないステップを出す（並び順によらない。チュートリアル用）。出せなければ false。</summary>
        public bool Open(FoodId food)
        {
            if (_closing || !Progress.TryOpen(food, out var index)) return false;
            return Issue(index);
        }

        bool Issue(int index)
        {
            var food = Progress.Current;
            Debug.Log($"[FieldStages] {fieldId} wants {food} ({StepsInOrder[index].name})");
            Issued?.Invoke(new RequestIssued(food, Field, StepsInOrder[index].Anchor));
            return true;
        }

        /// <summary>
        /// 物が届いた。何が届いても受け取り、何だったかを返す。Received を 1 回出す。
        /// 欲しがっていた物（Requested）なら、ステップを済ませ（見た目が変わる）、Closed（Resolved = true）→ Received → StepDone の順に出す。
        /// このフィールドの物（Own）・よその物（Foreign）なら、フィールドは戻らず、Received だけ出す（反応は受けた側が足す）。
        /// 物が落ちた場所を決める所（DeliveryZone）が IFieldRequests 越しに呼ぶ。
        /// </summary>
        public DeliveryKind Receive(FoodId food)
        {
            if (!Progress.TryResolve(food, out var index))
            {
                var kind = Progress.Wants(food) ? DeliveryKind.Own : DeliveryKind.Foreign;
                Received?.Invoke(new Delivered(food, Field, kind));
                return kind;
            }

            // 見た目を先に変えてから知らせる（Closed を受けてすぐ Open しても、次のステップの欲しい物が出るように）
            ApplyThenClose(food, wasOpen: true, resolved: true, celebrate: true);
            Received?.Invoke(new Delivered(food, Field, DeliveryKind.Requested));
            RaiseStepDone(index);
            return DeliveryKind.Requested;
        }

        public void Expire()
        {
            var food = Progress.Current;
            if (Progress.Close() < 0) return;
            Closed?.Invoke(new RequestClosed(food, Field, resolved: false));
        }

        /// <summary>はじめ（全部まだ）に戻す。出ていた欲しい物も終える（体験の終わりのリセット）。</summary>
        public void Restart()
        {
            // 状態を先に変えてから知らせる（Closed を受けてすぐ Open しても、はじめのステップから出るように）
            var food = Progress.Current;
            var wasOpen = Progress.IsOpen;
            Progress.Reset();
            ApplyThenClose(food, wasOpen, resolved: false, celebrate: false);
            Restarted?.Invoke();
        }

        // 見た目を写し直し、出ていた欲しい物があれば Closed を出す。その間（出し入れした物の OnEnable など）に Open されないようにする。
        void ApplyThenClose(FoodId food, bool wasOpen, bool resolved, bool celebrate)
        {
            _closing = wasOpen;
            try { Apply(celebrate); }
            finally { _closing = false; }
            if (wasOpen) Closed?.Invoke(new RequestClosed(food, Field, resolved));
        }

        void RaiseStepDone(int index)
        {
            StepsInOrder[index].RaiseDone();
            StepDone?.Invoke(StepsInOrder[index]);
        }

        // 済んだステップの子を出し、消す物を消し、村人の気持ちを合わせる。「消す」は「出す」より勝つ（芽は、実ったら消える）。
        void Apply(bool celebrate)
        {
            var steps = StepsInOrder;
            foreach (var step in steps)
            foreach (var go in step.HideWhenDone)
                if (go != null) go.SetActive(true);
            for (var i = 0; i < steps.Length; i++) steps[i].ShowChildren(Progress.IsDone(i));
            for (var i = 0; i < steps.Length; i++)
            {
                if (!Progress.IsDone(i)) continue;
                foreach (var go in steps[i].HideWhenDone)
                    if (go != null) go.SetActive(false);
            }

            // 困っている人は、自分の名前があるステップがすべて済むまで悩む（どのステップにも名前が無い人は元気）
            var worried = new Dictionary<VillagerMood, bool>();
            foreach (var mood in GetComponentsInChildren<VillagerMood>(includeInactive: true)) worried[mood] = false;
            for (var i = 0; i < steps.Length; i++)
            foreach (var mood in steps[i].Helped)
                if (mood != null) worried[mood] = (worried.TryGetValue(mood, out var w) && w) || !Progress.IsDone(i);
            foreach (var pair in worried) pair.Key.SetWorried(pair.Value, celebrate);
        }

        // 開発中の確認用（右クリックのメニュー）。展示では Game・Tutorial が呼ぶ。
        [ContextMenu("Open Next Want")]
        void OpenFromMenu()
        {
            if (!Open()) Debug.Log($"[FieldStages] {fieldId}: nothing to open (open: {IsOpen}, done: {DoneCount}/{StepsInOrder.Length}).", this);
        }

        [ContextMenu("Deliver Wanted Food")]
        void ReceiveFromMenu() => Receive(Current);

        // 頼まれていない物を投げたとき（豊作・いびつな世界の反応を作るとき）の確認用。Received だけが出て、見た目は変わらない
        [ContextMenu("Deliver Foreign Food (test)")]
        void ReceiveForeignFromMenu() => Debug.Log($"[FieldStages] {fieldId} received test-foreign: {Receive(new FoodId("test-foreign"))}", this);

        [ContextMenu("Complete Next Step")]
        void CompleteFromMenu()
        {
            var index = Progress.IsOpen ? Progress.OpenIndex : Progress.NextIndex;
            if (index < 0)
                for (var i = 0; i < StepsInOrder.Length && index < 0; i++)
                    if (!Progress.IsDone(i)) index = i; // 欲しい物が空のステップも済ませられる
            if (index < 0) return;
            var food = Progress.Current;
            var wasOpen = Progress.OpenIndex == index;
            Progress.Complete(index);
            ApplyThenClose(food, wasOpen, resolved: true, celebrate: true);
            RaiseStepDone(index);
        }

        [ContextMenu("Restart")]
        void RestartFromMenu() => Restart();
    }
}
