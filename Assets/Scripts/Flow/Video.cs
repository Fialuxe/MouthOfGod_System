using MouthOfGod.Tutorial;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>動画の画面への入口です。</summary>
    public static class Video
    {
        /// <summary>screen の動画を流し、終わるまで待ちます。場面が飛ばされたら、動画も止めます。</summary>
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
