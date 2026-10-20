using System.Collections.Generic;
using MouthOfGod.Core;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド 1 つの、ステップ（戻り方の 1 段）の進み具合。Unity に依存しない（テストできる）。
    /// ステップごとに欲しい物が 1 つあり、届くとそのステップが済む。済んだかはステップごとに持つ（番号で数えない）。
    /// 同時に出せる欲しい物は <see cref="MaxOpen"/> まで。次に出すのは、まだ済んでおらず、出てもいない最初のステップ（<see cref="NextIndex"/>）。
    /// </summary>
    public sealed class StepProgress
    {
        readonly FoodId[] _wants;
        readonly bool[] _done;
        readonly List<int> _open = new List<int>(); // 出ているステップ。出した順（古い順）

        /// <param name="maxOpen">同時に出せる欲しい物の数。1 より小さければ 1。</param>
        public StepProgress(IReadOnlyList<FoodId> wants, int maxOpen = 1)
        {
            var count = wants?.Count ?? 0;
            _wants = new FoodId[count];
            _done = new bool[count];
            for (var i = 0; i < count; i++) _wants[i] = wants[i];
            MaxOpen = maxOpen < 1 ? 1 : maxOpen;
        }

        public int Count => _wants.Length;

        /// <summary>同時に出せる欲しい物の数。</summary>
        public int MaxOpen { get; }

        /// <summary>出ている欲しい物のステップ。出した順（古い順）。</summary>
        public IReadOnlyList<int> OpenIndices => _open;

        /// <summary>出ている欲しい物のうち、一番古いもののステップ。出ていなければ -1。</summary>
        public int OpenIndex => _open.Count > 0 ? _open[0] : -1;

        public bool IsOpen => _open.Count > 0;

        public bool IsOpenAt(int index) => _open.Contains(index);

        /// <summary>出ている欲しい物のうち、一番古いもの。出ていなければ FoodId.None。</summary>
        public FoodId Current => IsOpen ? _wants[_open[0]] : FoodId.None;

        /// <summary>そのステップの欲しい物。範囲の外なら FoodId.None。</summary>
        public FoodId WantOf(int index) => index >= 0 && index < _wants.Length ? _wants[index] : FoodId.None;

        public bool IsDone(int index) => index >= 0 && index < _done.Length && _done[index];

        public int DoneCount
        {
            get
            {
                var n = 0;
                foreach (var d in _done) if (d) n++;
                return n;
            }
        }

        public bool IsComplete => DoneCount == Count;

        // 出せるステップ: まだ済んでおらず、出てもおらず、欲しい物があるもの。
        bool CanIssue(int index) => !_done[index] && !_open.Contains(index) && !_wants[index].IsEmpty;

        /// <summary>次に出すステップ（まだ済んでおらず、出てもおらず、欲しい物があるもの）。無ければ -1。順番を変えたいときは、ここだけを変える。</summary>
        public int NextIndex
        {
            get
            {
                for (var i = 0; i < _wants.Length; i++)
                    if (CanIssue(i)) return i;
                return -1;
            }
        }

        public bool CanOpen => _open.Count < MaxOpen && NextIndex >= 0;

        /// <summary>次の欲しい物を出す。出せなければ false（index は -1）。random を渡すと、並び順ではなく、出せるステップからランダムに選ぶ。</summary>
        public bool TryOpen(out int index, System.Random random = null)
        {
            index = -1;
            if (!CanOpen) return false;
            if (random == null)
            {
                index = NextIndex;
            }
            else
            {
                var candidates = 0;
                for (var i = 0; i < _wants.Length; i++)
                    if (CanIssue(i)) candidates++;
                var pick = random.Next(candidates);
                for (var i = 0; i < _wants.Length && index < 0; i++)
                    if (CanIssue(i) && pick-- == 0) index = i;
            }
            _open.Add(index);
            return true;
        }

        /// <summary>このフィールドのどれかのステップが欲しがる物か（済んだステップも含む）。</summary>
        public bool Wants(FoodId food)
        {
            if (food.IsEmpty) return false;
            foreach (var want in _wants)
                if (want == food) return true;
            return false;
        }

        /// <summary>その物を欲しがる、出せる最初のステップを出す。上限まで出ている・出せるステップが無いなら false（index は -1）。</summary>
        public bool TryOpen(FoodId food, out int index)
        {
            index = -1;
            if (_open.Count >= MaxOpen || food.IsEmpty) return false;
            for (var i = 0; i < _wants.Length; i++)
            {
                if (!CanIssue(i) || _wants[i] != food) continue;
                index = i;
                _open.Add(i);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 届いた物を、出ている欲しい物のうち、同じ物を欲しがる一番古いものに当てる。当たれば、そのステップを済ませて閉じる（true）。
        /// どれにも当たらなければ false（index は -1）。
        /// </summary>
        public bool TryResolve(FoodId delivered, out int index)
        {
            index = -1;
            if (delivered.IsEmpty) return false;
            foreach (var open in _open)
            {
                if (_wants[open] != delivered) continue;
                index = open;
                break;
            }
            if (index < 0) return false;
            _done[index] = true;
            _open.Remove(index);
            return true;
        }

        /// <summary>出ている欲しい物のうち、一番古いものを、届かないまま閉じる。閉じたステップを返す（出ていなければ -1）。</summary>
        public int Close()
        {
            var index = OpenIndex;
            if (index >= 0) _open.RemoveAt(0);
            return index;
        }

        /// <summary>そのステップの欲しい物を、届かないまま閉じる。出ていなければ false。</summary>
        public bool Close(int index) => _open.Remove(index);

        /// <summary>出ている欲しい物を、すべて届かないまま閉じる。閉じたステップを、出した順に返す。</summary>
        public int[] CloseAll()
        {
            var closed = _open.ToArray();
            _open.Clear();
            return closed;
        }

        /// <summary>欲しい物を通さずに、ステップを済ませる（開発中の確認用）。出ていたら閉じる。</summary>
        public void Complete(int index)
        {
            if (index < 0 || index >= _done.Length) return;
            _open.Remove(index);
            _done[index] = true;
        }

        /// <summary>はじめ（全部まだ）に戻す。出ていた欲しい物も閉じる。</summary>
        public void Reset()
        {
            _open.Clear();
            for (var i = 0; i < _done.Length; i++) _done[i] = false;
        }
    }
}
