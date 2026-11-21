using System;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面のスクリプトで await して待つための道具です。
    /// </summary>
    /// <remarks>
    /// 待っている間に場面が飛ばされると、OperationCanceledException で抜けます。
    /// その例外は <see cref="TutorialHandler"/> が受け止めます。
    /// </remarks>
    public static class Wait
    {
        /// <summary>指定した秒数だけ待ちます。0 以下なら待ちません。</summary>
        public static async Awaitable Seconds(float seconds)
        {
            if (seconds > 0f) await Awaitable.WaitForSecondsAsync(seconds, TutorialHandler.Token);
        }

        /// <summary>1 フレーム待ちます。</summary>
        public static Awaitable Frame() => Awaitable.NextFrameAsync(TutorialHandler.Token);

        /// <summary>条件が満たされるまで、毎フレーム調べて待ちます。</summary>
        /// <param name="done">待つ条件です。毎フレーム 1 回呼びます。</param>
        /// <param name="timeout">待つ秒数です。0 以下なら、時間切れにしません。</param>
        /// <returns>条件が満たされたら <see langword="true"/>、時間切れなら <see langword="false"/> です。</returns>
        /// <remarks>呼んだ時点で条件が満たされていれば、待たずに <see langword="true"/> を返します。</remarks>
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
