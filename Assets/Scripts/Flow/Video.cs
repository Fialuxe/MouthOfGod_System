using MouthOfGod.Tutorial;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>動画の画面への入口です。</summary>
    public static class Video
    {
        /// <summary>画面の動画を流し、終わるまで待ちます。</summary>
        /// <param name="screen">流す画面です。<see langword="null"/> か動画が無ければ、警告を出してすぐ戻ります。</param>
        /// <remarks>場面が飛ばされたら、動画も止めます。</remarks>
        public static async Awaitable Play(VideoScreen screen)
        {
            if (screen == null || !screen.HasVideo)
            {
                Debug.LogWarning("[Flow.Video] 動画の画面か動画が無いので、流さずに進みます。");
                return;
            }
            screen.Play();
            try { await Wait.Until(() => !screen.IsBusy); }
            finally { if (screen.IsBusy) screen.Skip(); }
        }
    }
}
