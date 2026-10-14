using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// この物が、どの段階で見えるか。<see cref="FieldStages"/> の子の物に付ける（手で置いた物にも付けられる）。
    /// 例: 枯れた茎は 0〜0、芽は 1〜1、作物は 2〜最後、羊は 3〜最後。
    /// </summary>
    public sealed class StageVisibility : MonoBehaviour, IStageListener
    {
        [Tooltip("この段階から見える")]
        [SerializeField, Min(0)] int fromStage;

        [Tooltip("この段階まで見える。-1 なら最後まで")]
        [SerializeField, Min(-1)] int untilStage = Stages.UntilEnd;

        public int FromStage { get => fromStage; set => fromStage = value; }
        public int UntilStage { get => untilStage; set => untilStage = value; }

        public void OnStageChanged(int previous, int current)
            => gameObject.SetActive(Stages.IsVisible(fromStage, untilStage, current));
    }
}
