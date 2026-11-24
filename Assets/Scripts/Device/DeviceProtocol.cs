namespace MouthOfGod.Device
{
    /// <summary>実機の Arduino が、一定間隔で知らせる状態です。</summary>
    internal readonly struct DeviceStatus
    {
        internal DeviceStatus(bool isDriving, int percentInside)
        {
            IsDriving = isDriving;
            PercentInside = percentInside;
        }

        /// <summary>モーターが動いている間は <see langword="true"/> です。</summary>
        internal bool IsDriving { get; }

        /// <summary>いまの内容量です（0〜100 %）。動いている間は、動き出す前の値のままです。</summary>
        internal int PercentInside { get; }
    }

    /// <summary>実機から届いた 1 行の種類です。</summary>
    internal enum DeviceMessageKind
    {
        /// <summary>状態です。<see cref="DeviceMessage.Status"/> に入っています。</summary>
        Status,

        /// <summary>ハードウェアの確定ボタンが押されました。</summary>
        ConfirmPressed,

        /// <summary>「ここに実機がいる」という合図です。実機を見つけるために、起動時と 1 秒ごとに届きます。</summary>
        Hello,
    }

    /// <summary>実機から届いた 1 行を読んだ結果です。</summary>
    internal readonly struct DeviceMessage
    {
        internal DeviceMessage(DeviceMessageKind kind, DeviceStatus status)
        {
            Kind = kind;
            Status = status;
        }

        internal DeviceMessageKind Kind { get; }

        /// <summary>種類が <see cref="DeviceMessageKind.Status"/> のときだけ、意味があります。</summary>
        internal DeviceStatus Status { get; }
    }

    /// <summary>
    /// Unity と Arduino の、通信の約束です。1 行ずつの短い文字でやり取りします。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 約束は <c>docs/device-protocol.md</c> に書いてあります。Arduino のスケッチも同じ約束です。
    /// ここは、行の組み立てと読み取りだけを持ちます。シリアルの読み書きも、ゲームの意味も知りません。
    /// </para>
    /// <para>
    /// Unity から Arduino へ: <c>(f,80)</c> は「内容量を 80 % まで増やす」、<c>(s,20)</c> は「20 % まで減らす」、
    /// <c>(c,60)</c> は「モーターを回さず、いまの量を 60 % と覚え直す」、<c>(i,0)</c> は「すぐ止める」、
    /// <c>(r,0)</c> は「押し出す側の端まで回して、100 % と決める（原点出し）」、
    /// <c>(t,3)</c> は「動作確認。押す・引くを、それぞれ 50 % ぶん、3 回くり返す」です。
    /// 行の終わりは <c>\n</c> です。
    /// </para>
    /// <para>
    /// Arduino から Unity へ: <c>(0,100)</c> は「止まっていて、内容量は 100 %」です（先頭は、動いていれば 1）。
    /// これを、50 ミリ秒ごとに送ります。<c>(k,1)</c> は「確定ボタンが押された」です。
    /// <c>(h,MOG1)</c> は、実機が自分を知らせる合図です。起動時と 1 秒ごとに送ります。
    /// Unity は、この合図が届いた COM ポートを、実機のポートとして使います。
    /// 読めない行と、知らない種類の行は、読み捨てます。
    /// </para>
    /// </remarks>
    internal static class DeviceProtocol
    {
        internal const int MinPercent = 0;
        internal const int MaxPercent = 100;

        /// <summary>動作確認で、くり返せる回数の上限です。Arduino のスケッチの <c>TEST_MAX_CYCLES</c> と同じです。</summary>
        internal const int MaxSelfTestCycles = 20;

        /// <summary>実機の合図の値です。MOG は Mouth Of God で、1 は約束の版です。約束を変えたら、数字を上げます。</summary>
        internal const string HelloValue = "MOG1";

        /// <summary>内容量を目標まで動かす指令の行を作ります。</summary>
        /// <param name="currentPercent">いまの内容量です。Arduino が最後に知らせた値を渡します。</param>
        /// <param name="targetPercent">動かしたい内容量です。0〜100 の外は、範囲内に丸めます。</param>
        /// <returns>
        /// 指令の行です。<paramref name="currentPercent"/> が丸めた目標と同じなら、動かす必要がないので
        /// <see langword="null"/> を返します。
        /// </returns>
        /// <remarks>
        /// 増やすなら <c>f</c>、減らすなら <c>s</c> を使います。Arduino は、向きが逆の指令を無視するためです。
        /// </remarks>
        internal static string EncodeMoveTo(int currentPercent, int targetPercent)
        {
            int target = Clamp(targetPercent);
            if (target == Clamp(currentPercent))
            {
                return null;
            }

            char command = target > Clamp(currentPercent) ? 'f' : 's';
            return "(" + command + "," + target + ")\n";
        }

        /// <summary>モーターを回さずに、いまの内容量を覚え直させる行を作ります。</summary>
        /// <param name="percent">実際の内容量です。0〜100 の外は、範囲内に丸めます。</param>
        internal static string EncodeSetCurrent(int percent)
        {
            return "(c," + Clamp(percent) + ")\n";
        }

        /// <summary>原点出しの行を作ります。押し出す側（100 %）の端まで回して、そこを 100 % と決めさせます。</summary>
        /// <remarks>端にぶつけたまま回すので、歯車に負担がかかります。起動した直後と、ずれを直したいときだけ使います。</remarks>
        internal static string EncodeHome()
        {
            return "(r,0)\n";
        }

        /// <summary>動作確認の行を作ります。押す・引くを、それぞれ 50 % ぶん、指定の回数くり返させます。</summary>
        /// <param name="cycles">押す・引くを 1 往復として、くり返す回数です。1〜20 の外は、範囲内に丸めます。</param>
        /// <remarks>押すほうから始まります。端にいて動けない向きの動きは、飛ばされます。</remarks>
        internal static string EncodeSelfTest(int cycles)
        {
            int count = cycles < 1 ? 1 : (cycles > MaxSelfTestCycles ? MaxSelfTestCycles : cycles);
            return "(t," + count + ")\n";
        }

        /// <summary>すぐ止める行を作ります。</summary>
        internal static string EncodeStop()
        {
            return "(i,0)\n";
        }

        /// <summary>実機から届いた 1 行を読みます。</summary>
        /// <param name="line">行です。行の終わりの <c>\r</c> や空白は、あってもかまいません。<see langword="null"/> は読めません。</param>
        /// <param name="message">読めたときの結果です。</param>
        /// <returns>読めたら <see langword="true"/> です。知らない種類や、壊れた行は <see langword="false"/> です。</returns>
        internal static bool TryParse(string line, out DeviceMessage message)
        {
            message = default;
            if (line == null)
            {
                return false;
            }

            string text = line.Trim();
            if (text.Length < 5 || text[0] != '(' || text[text.Length - 1] != ')')
            {
                return false;
            }

            string[] parts = text.Substring(1, text.Length - 2).Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            string kind = parts[0].Trim();
            string value = parts[1].Trim();

            if (kind == "h")
            {
                if (value != HelloValue)
                {
                    return false;
                }

                message = new DeviceMessage(DeviceMessageKind.Hello, default);
                return true;
            }

            if (kind == "k")
            {
                message = new DeviceMessage(DeviceMessageKind.ConfirmPressed, default);
                return true;
            }

            if ((kind == "0" || kind == "1")
                && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percent)
                && percent >= MinPercent && percent <= MaxPercent)
            {
                message = new DeviceMessage(DeviceMessageKind.Status, new DeviceStatus(kind == "1", percent));
                return true;
            }

            return false;
        }

        private static int Clamp(int percent)
        {
            if (percent < MinPercent)
            {
                return MinPercent;
            }

            return percent > MaxPercent ? MaxPercent : percent;
        }
    }
}
