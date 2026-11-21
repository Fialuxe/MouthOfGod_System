using System;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// しゃべっている間に来たセリフを待たせます。待つのは、いちばん新しい 1 つだけです。
    /// </summary>
    /// <remarks>
    /// 古い待ちは捨てます。
    /// 早く進める体験者でも、いま起きたことへの反応が遅れないようにするためです。
    /// たとえば「Splendid!」が、古いセリフのあとに回らないようにします。
    /// </remarks>
    public sealed class MikoLineQueue
    {
        MikoLine _waiting;

        /// <summary>待っているセリフがあれば <see langword="true"/> です。</summary>
        public bool HasWaiting => _waiting != null;

        /// <summary>セリフを待たせます。すでに待っているセリフがあれば、捨てて置き換えます。</summary>
        /// <param name="line">待たせるセリフです。<see langword="null"/> を渡すと、待ちを空にします。</param>
        public void Enqueue(MikoLine line) => _waiting = line;

        /// <summary>待っているセリフを取り出し、待ちを空にします。</summary>
        /// <returns>待っていたセリフです。無ければ <see langword="null"/> です。</returns>
        public MikoLine Dequeue()
        {
            MikoLine line = _waiting;
            _waiting = null;
            return line;
        }

        /// <summary>待っているセリフを捨てます。</summary>
        public void Clear() => _waiting = null;

        /// <summary>
        /// 次のセリフが来たとき、いま出ているセリフをいつ切り上げるかを返します。
        /// </summary>
        /// <param name="hideAt">いまのセリフの、もともとの終わりの時刻（秒）です。</param>
        /// <param name="shownAt">いまのセリフを出しはじめた時刻（秒）です。</param>
        /// <param name="minShowSeconds">出しはじめから、最低これだけは見せる秒数です。</param>
        /// <param name="now">いまの時刻（秒）です。</param>
        /// <returns>
        /// 切り上げる時刻（秒）です。<paramref name="now"/> より早くはしません。
        /// <paramref name="hideAt"/> より遅くもしません。
        /// </returns>
        public static float CutAt(float hideAt, float shownAt, float minShowSeconds, float now)
            => Math.Min(hideAt, Math.Max(now, shownAt + minShowSeconds));
    }
}
