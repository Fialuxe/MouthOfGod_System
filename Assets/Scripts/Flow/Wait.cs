using System;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面のスクリプトで await して待つための道具です。
    /// 待っている間に場面が飛ばされると、OperationCanceledException で抜けます（TutorialHandler が受け止めます）。
    /// </summary>
    public static class Wait
    {
        /// <summary>秒数だけ待ちます。0 以下なら待ちません。</summary>
        public static async Awaitable Seconds(float seconds)
        {
            if (seconds > 0f) await Awaitable.WaitForSecondsAsync(seconds, TutorialHandler.Token);
        }

        /// <summary>1 フレーム待ちます。</summary>
        public static Awaitable Frame() => Awaitable.NextFrameAsync(TutorialHandler.Token);

        /// <summary>done が true になるまで待ちます。timeout 秒（0 なら無制限）で時間切れになったら false を返します。</summary>
        public static async Awaitable<bool> Until(Func<bool> done, float timeout = 0f)
        {
            float end = timeout > 0f ? Time.time + timeout : float.PositiveInfinity;
            while (!done())
            {
                if (Time.time >= end) return false;
                await Frame();
            }
            return true;
        }
    }
}
