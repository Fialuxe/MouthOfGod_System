using System;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// しゃべっている間に来たセリフの順番待ち。新しいセリフだけを 1 つ残す（古い待ちは捨てる）。
    /// 早く進める体験者でも、いま起きたことへの反応（「Splendid!」など）が、古いセリフに遅れないようにするため。
    /// </summary>
    public sealed class MikoLineQueue
    {
        MikoLine _waiting;

        public bool HasWaiting => _waiting != null;

        /// <summary>待たせる。すでに待っているセリフがあれば、捨てて置き換える。</summary>
        public void Enqueue(MikoLine line) => _waiting = line;

        /// <summary>待っているセリフを返して、空にする。無ければ null。</summary>
        public MikoLine Dequeue()
        {
            MikoLine line = _waiting;
            _waiting = null;
            return line;
        }

        public void Clear() => _waiting = null;

        /// <summary>
        /// いま出ているセリフを、いつ切り上げるか。出しはじめから <paramref name="minShowSeconds"/> 秒は見せる。
        /// もともとの終わりの時刻（<paramref name="hideAt"/>）より遅くはしない。
        /// </summary>
        public static float CutAt(float hideAt, float shownAt, float minShowSeconds, float now)
            => Math.Min(hideAt, Math.Max(now, shownAt + minShowSeconds));
    }
}
