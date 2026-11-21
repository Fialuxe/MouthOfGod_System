using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド 1 つの、ステップ（戻り方の 1 段）の進み具合を持ちます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ステップごとに欲しい物が 1 つあり、その物が届くとステップが済みます。
    /// 同時に出せる欲しい物は <see cref="MaxOpen"/> までです。
    /// </para>
    /// <para>
    /// ステップは、0 から始まる番号で指します。
    /// 範囲の外の番号を渡しても、例外は投げません。
    /// </para>
    /// </remarks>
    public sealed class StepProgress
    {
        readonly GameObject[] _wants;
        readonly bool[] _done;
        readonly List<int> _open = new List<int>(); // 出ているステップを、出した順（古い順）に持ちます

        /// <param name="wants">
        /// ステップごとの欲しい物です。中身をコピーして持ちます。
        /// <see langword="null"/> ならステップ 0 個として扱います。
        /// 要素が <see langword="null"/> のステップは、欲しい物を出しません。
        /// </param>
        /// <param name="maxOpen">同時に出せる欲しい物の数です。1 より小さければ 1 にします。</param>
        public StepProgress(IReadOnlyList<GameObject> wants, int maxOpen = 1)
        {
            var count = wants?.Count ?? 0;
            _wants = new GameObject[count];
            _done = new bool[count];
            for (var i = 0; i < count; i++) _wants[i] = wants[i];
            MaxOpen = maxOpen < 1 ? 1 : maxOpen;
        }

        public int Count => _wants.Length;

        /// <summary>同時に出せる欲しい物の数です。1 以上です。</summary>
        public int MaxOpen { get; }

        /// <summary>出ている欲しい物のステップを、出した順（古い順）に並べたものです。</summary>
        public IReadOnlyList<int> OpenIndices => _open;

        /// <summary>出ている欲しい物のうち、一番古いもののステップです。</summary>
        /// <value>出ていなければ -1 です。</value>
        public int OpenIndex => _open.Count > 0 ? _open[0] : -1;

        public bool IsOpen => _open.Count > 0;

        public bool IsOpenAt(int index) => _open.Contains(index);

        /// <summary>出ている欲しい物のうち、一番古いものです。</summary>
        /// <value>出ていなければ <see langword="null"/> です。</value>
        public GameObject Current => IsOpen ? _wants[_open[0]] : null;

        /// <summary>そのステップの欲しい物を返します。</summary>
        /// <param name="index">ステップの番号です。</param>
        /// <returns>範囲の外か、欲しい物が空なら <see langword="null"/> を返します。</returns>
        public GameObject WantOf(int index) => index >= 0 && index < _wants.Length ? _wants[index] : null;

        /// <summary>そのステップが済んでいるかを返します。</summary>
        /// <param name="index">ステップの番号です。</param>
        /// <returns>済んでいれば <see langword="true"/> です。範囲の外なら <see langword="false"/> を返します。</returns>
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

        // まだ済んでおらず、出てもおらず、欲しい物があるステップだけを出せます。
        bool CanIssue(int index) => !_done[index] && !_open.Contains(index) && _wants[index] != null;

        /// <summary>並び順で次に出すステップです。</summary>
        /// <value>
        /// まだ済んでおらず、出てもおらず、欲しい物があるステップのうち、最初の物です。
        /// 無ければ -1 です。
        /// </value>
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

        /// <summary>次の欲しい物を出します。</summary>
        /// <param name="index">出したステップです。出せなければ -1 です。</param>
        /// <param name="random">
        /// 渡すと、並び順ではなく、出せるステップからランダムに選びます。
        /// <see langword="null"/> なら並び順で選びます。
        /// </param>
        /// <returns>出したときは <see langword="true"/>、出せなければ <see langword="false"/> を返します。</returns>
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

        /// <summary>このフィールドのどれかのステップが、その物を欲しがるかを返します。</summary>
        /// <param name="food">調べる食べ物の Prefab です。</param>
        /// <returns>
        /// 欲しがるステップがあれば <see langword="true"/> を返します。済んだステップも含みます。
        /// <paramref name="food"/> が <see langword="null"/> なら <see langword="false"/> を返します。
        /// </returns>
        public bool Wants(GameObject food)
        {
            if (food == null) return false;
            foreach (var want in _wants)
                if (want == food) return true;
            return false;
        }

        /// <summary>その物を欲しがる、出せる最初のステップを出します。</summary>
        /// <param name="food">欲しがらせる食べ物の Prefab です。</param>
        /// <param name="index">出したステップです。出せなければ -1 です。</param>
        /// <returns>
        /// 出したときは <see langword="true"/> を返します。
        /// 上限まで出ている・出せるステップが無い・<paramref name="food"/> が <see langword="null"/> のときは <see langword="false"/> です。
        /// </returns>
        public bool TryOpen(GameObject food, out int index)
        {
            index = -1;
            if (_open.Count >= MaxOpen || food == null) return false;
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
        /// 届いた物を、出ている欲しい物に当てます。
        /// 当たれば、そのステップを済ませて閉じます。
        /// </summary>
        /// <param name="delivered">届いた食べ物の Prefab です。</param>
        /// <param name="index">当たったステップです。当たらなければ -1 です。</param>
        /// <returns>当たれば <see langword="true"/>、どれにも当たらなければ <see langword="false"/> を返します。</returns>
        /// <remarks>
        /// 同じ物を欲しがるステップが複数出ているときは、一番古いものに当てます。
        /// </remarks>
        public bool TryResolve(GameObject delivered, out int index)
        {
            index = -1;
            if (delivered == null) return false;
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

        /// <summary>出ている欲しい物のうち、一番古いものを、届かないまま閉じます。</summary>
        /// <returns>閉じたステップを返します。出ていなければ -1 です。</returns>
        public int Close()
        {
            var index = OpenIndex;
            if (index >= 0) _open.RemoveAt(0);
            return index;
        }

        /// <summary>そのステップの欲しい物を、届かないまま閉じます。</summary>
        /// <param name="index">閉じるステップです。</param>
        /// <returns>閉じたときは <see langword="true"/>、出ていなければ <see langword="false"/> を返します。</returns>
        public bool Close(int index) => _open.Remove(index);

        /// <summary>出ている欲しい物を、すべて届かないまま閉じます。</summary>
        /// <returns>閉じたステップを、出した順に返します。出ていなければ空の配列です。</returns>
        public int[] CloseAll()
        {
            var closed = _open.ToArray();
            _open.Clear();
            return closed;
        }

        /// <summary>
        /// 欲しい物を届けずに、ステップを済ませます。
        /// 出ていたら閉じます。
        /// </summary>
        /// <param name="index">済ませるステップです。範囲の外なら何もしません。</param>
        public void Complete(int index)
        {
            if (index < 0 || index >= _done.Length) return;
            _open.Remove(index);
            _done[index] = true;
        }

        /// <summary>
        /// すべてのステップを、まだの状態に戻します。
        /// 出ていた欲しい物も閉じます。
        /// </summary>
        public void Reset()
        {
            _open.Clear();
            for (var i = 0; i < _done.Length; i++) _done[i] = false;
        }
    }
}
