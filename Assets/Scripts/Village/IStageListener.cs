namespace MouthOfGod.Village
{
    /// <summary>
    /// <see cref="FieldStages"/> の子に付けると、段階が変わるたびに呼ばれる。見えなくなっている（非アクティブの）物にも届く。
    /// </summary>
    public interface IStageListener
    {
        /// <param name="previous">前の段階。最初に呼ばれるときは -1。</param>
        /// <param name="current">新しい段階。</param>
        void OnStageChanged(int previous, int current);
    }
}
