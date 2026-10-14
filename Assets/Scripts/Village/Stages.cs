namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの戻り具合（段階）の決まり。0 が飢饉（始まり）、最大の段階ですっかり戻る。Unity に依存しない（テストできる）。
    /// 置き物は「何段目から何段目まで見えるか」、村人は「何段目で元気になるか」を持つ。
    /// </summary>
    public static class Stages
    {
        /// <summary>「最後の段階まで」を表す値（<see cref="IsVisible"/> の until に使う）。</summary>
        public const int UntilEnd = -1;

        /// <summary>from〜until（両端を含む）の段階で見えるか。until が <see cref="UntilEnd"/> なら最後まで見える。</summary>
        public static bool IsVisible(int from, int until, int stage)
            => from <= stage && (until == UntilEnd || stage <= until);

        /// <summary>calmFrom の段階になるまでは悩んでいる。</summary>
        public static bool IsWorried(int calmFrom, int stage) => stage < calmFrom;

        /// <summary>0〜max に収める。</summary>
        public static int Clamp(int stage, int max) => stage < 0 ? 0 : stage > max ? max : stage;
    }
}
