using System.Collections.Generic;
using MouthOfGod.Core;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド 1 つの、ステップ（戻り方の 1 段）の進み具合。Unity に依存しない（テストできる）。
    /// ステップごとに欲しい物が 1 つあり、届くとそのステップが済む。済んだかはステップごとに持つ（番号で数えない）。
    /// 同時に出せる欲しい物は 1 つ。次に出すのは、まだ済んでいない最初のステップ（<see cref="NextIndex"/>）。
    /// </summary>
    public sealed class StepProgress
    {
        readonly FoodId[] _wants;
        readonly bool[] _done;

        public StepProgress(IReadOnlyList<FoodId> wants)
        {
            var count = wants?.Count ?? 0;
            _wants = new FoodId[count];
            _done = new bool[count];
            for (var i = 0; i < count; i++) _wants[i] = wants[i];
        }

        public int Count => _wants.Length;

        /// <summary>出ている欲しい物のステップ。出ていなければ -1。</summary>
        public int OpenIndex { get; private set; } = -1;

        public bool IsOpen => OpenIndex >= 0;

        /// <summary>出ている欲しい物。出ていなければ FoodId.None。</summary>
        public FoodId Current => IsOpen ? _wants[OpenIndex] : FoodId.None;

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

        /// <summary>次に出すステップ（まだ済んでおらず、欲しい物があるもの）。無ければ -1。順番を変えたいときは、ここだけを変える。</summary>
        public int NextIndex
        {
            get
            {
                for (var i = 0; i < _wants.Length; i++)
                    if (!_done[i] && !_wants[i].IsEmpty) return i;
                return -1;
            }
        }

        public bool CanOpen => !IsOpen && NextIndex >= 0;

        /// <summary>次のステップの欲しい物を出す。出せなければ false（index は -1）。</summary>
        public bool TryOpen(out int index)
        {
            index = CanOpen ? NextIndex : -1;
            if (index < 0) return false;
            OpenIndex = index;
            return true;
        }

        /// <summary>届いた物が出ている欲しい物と同じなら、そのステップを済ませて閉じる（true）。違う物・出ていないときは false（index は -1）。</summary>
        public bool TryResolve(FoodId delivered, out int index)
        {
            index = -1;
            if (!IsOpen || delivered != Current) return false;
            index = OpenIndex;
            _done[index] = true;
            OpenIndex = -1;
            return true;
        }

        /// <summary>出ている欲しい物を、届かないまま閉じる。閉じたステップを返す（出ていなければ -1）。</summary>
        public int Close()
        {
            var index = OpenIndex;
            OpenIndex = -1;
            return index;
        }

        /// <summary>欲しい物を通さずに、ステップを済ませる（開発中の確認用）。出ていたら閉じる。</summary>
        public void Complete(int index)
        {
            if (index < 0 || index >= _done.Length) return;
            if (OpenIndex == index) OpenIndex = -1;
            _done[index] = true;
        }

        /// <summary>はじめ（全部まだ）に戻す。出ていた欲しい物も閉じる。</summary>
        public void Reset()
        {
            OpenIndex = -1;
            for (var i = 0; i < _done.Length; i++) _done[i] = false;
        }
    }
}
