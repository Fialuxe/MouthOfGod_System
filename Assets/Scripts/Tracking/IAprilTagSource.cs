using System;
using System.Collections.Generic;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// AprilTag の検出結果の供給元。検出の実装（実機のカメラ、擬似実装など）を差し替えられるようにする。
    /// </summary>
    public interface IAprilTagSource
    {
        /// <summary>
        /// 検出のたびに呼ばれる。引数のリストは次回の検出で再利用されるため、保持しないこと。
        /// 検出したタグが 0 枚のときは呼ばれない。
        /// </summary>
        event Action<IReadOnlyList<AprilTagObservation>> ObservationsUpdated;

        /// <summary>指定 ID の、最後に検出した結果を返す。まだ検出していなければ false。</summary>
        bool TryGetLatest(int tagId, out AprilTagObservation observation);
    }
}
