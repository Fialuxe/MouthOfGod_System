using System;
using System.Collections.Generic;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// AprilTag の検出結果を配る側の窓口です。
    /// 実機のカメラや擬似実装など、検出の実装を差し替えられるようにします。
    /// </summary>
    public interface IAprilTagSource
    {
        /// <summary>
        /// 1 回検出するたびに発火します。
        /// 検出したタグが 0 枚のときは発火しません。
        /// </summary>
        /// <remarks>
        /// 引数のリストは、次の検出で使い回します。
        /// 購読側は、リストへの参照を保持しないでください。
        /// </remarks>
        event Action<IReadOnlyList<AprilTagObservation>> ObservationsUpdated;

        /// <summary>
        /// 指定した ID のタグを、最後に検出した結果を返します。
        /// </summary>
        /// <param name="tagId">タグの ID です。</param>
        /// <param name="observation">最後の検出結果です。まだ検出していなければ <see langword="default"/> です。</param>
        /// <returns>一度でも検出していれば <see langword="true"/> を返します。</returns>
        bool TryGetLatest(int tagId, out AprilTagObservation observation);
    }
}
