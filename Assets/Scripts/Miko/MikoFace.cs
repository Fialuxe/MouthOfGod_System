using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>目の形です。並びは、Miko_Eyes.png の絵の順番と同じにします。</summary>
    /// <remarks>値を絵のコマ番号として使うので、並べ替えるときは絵も合わせて直します。</remarks>
    public enum MikoEye
    {
        Open, Line, Happy, Gt, Lt, Heart, Dot, Sleepy,
    }

    /// <summary>表情です。左右の目の形を組み合わせて表します。</summary>
    public enum MikoExpression
    {
        /// <summary>●● の目です。この表情のときだけ、まばたきをします。</summary>
        Normal,

        /// <summary>^^ の目です。</summary>
        Happy,

        /// <summary>＞＜ の目です。</summary>
        Squint,

        /// <summary>♡♡ の目です。</summary>
        Love,

        /// <summary>ーー の目です。</summary>
        Sleep,

        /// <summary>ー・ の目です。</summary>
        Wink,

        /// <summary>∪∪ の目です。</summary>
        Tired,
    }

    /// <summary>
    /// 巫女の表情を切り替えます。表情が Normal のときは、ときどきまばたきをします。
    /// </summary>
    /// <remarks>
    /// 目は、顔に貼った 2 枚の板（Quad）で表します。
    /// 8 コマの絵から 1 コマを選び、板に映して変えます。
    /// </remarks>
    public sealed class MikoFace : MonoBehaviour
    {
        const int Columns = 4;
        const int Rows = 2;

        [SerializeField] Renderer viewerLeftEye;  // 見ている人から見て左の目。巫女から見た左ではありません
        [SerializeField] Renderer viewerRightEye;
        [SerializeField] MikoExpression expression = MikoExpression.Normal;
        [SerializeField] Vector2 blinkInterval = new Vector2(2.5f, 5f);  // まばたきの間隔（秒）。x〜y のあいだで毎回ランダムに選びます
        [SerializeField] float blinkSeconds = 0.12f;

        Material _left;
        Material _right;
        float _nextBlink;
        float _blinkEnd;

        /// <summary>いまの表情です。まばたき中も、元の表情を返します。</summary>
        public MikoExpression Expression => expression;

        /// <summary>表情を変えます。</summary>
        /// <param name="value">変える表情です。</param>
        public void Set(MikoExpression value)
        {
            expression = value;
            Apply(value, blinking: false);
        }

        /// <summary>名前で表情を変えます。</summary>
        /// <param name="name">
        /// 表情の名前です。大文字と小文字は区別しません。
        /// 例: normal、happy、squint、love、sleep、wink、tired
        /// </param>
        /// <returns>変えたら <see langword="true"/> です。分からない名前なら、何もせず <see langword="false"/> を返します。</returns>
        public bool Set(string name)
        {
            if (!System.Enum.TryParse(name, true, out MikoExpression value)) return false;
            Set(value);
            return true;
        }

        void Awake()
        {
            // 板ごとに材質を複製し、映すコマを左右で変えられるようにします。
            // 複製した材質は自動で消えないので、OnDestroy で消します。
            if (viewerLeftEye != null) _left = viewerLeftEye.material;
            if (viewerRightEye != null) _right = viewerRightEye.material;
            Apply(expression, blinking: false);
            ScheduleBlink();
        }

        void OnDestroy()
        {
            if (_left != null) Destroy(_left);
            if (_right != null) Destroy(_right);
        }

        void Update()
        {
            if (expression != MikoExpression.Normal)
            {
                _blinkEnd = 0f;
                return;
            }
            if (_blinkEnd > 0f)
            {
                if (Time.time >= _blinkEnd)
                {
                    _blinkEnd = 0f;
                    Apply(expression, blinking: false);
                    ScheduleBlink();
                }
            }
            else if (Time.time >= _nextBlink)
            {
                _blinkEnd = Time.time + blinkSeconds;
                Apply(expression, blinking: true);
            }
        }

        void ScheduleBlink() => _nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);

        void Apply(MikoExpression value, bool blinking)
        {
            Eyes(value, out var left, out var right);
            if (blinking) left = right = MikoEye.Line;
            Show(_left, left);
            Show(_right, right);
        }

        static void Eyes(MikoExpression value, out MikoEye left, out MikoEye right)
        {
            switch (value)
            {
                case MikoExpression.Happy: left = right = MikoEye.Happy; break;
                case MikoExpression.Squint: left = MikoEye.Gt; right = MikoEye.Lt; break;
                case MikoExpression.Love: left = right = MikoEye.Heart; break;
                case MikoExpression.Sleep: left = right = MikoEye.Line; break;
                case MikoExpression.Wink: left = MikoEye.Line; right = MikoEye.Dot; break;
                case MikoExpression.Tired: left = right = MikoEye.Sleepy; break;
                default: left = right = MikoEye.Open; break;
            }
        }

        // 絵は 4 列 × 2 行で、0 番が左上です。右へ数えます。
        // UV は左下が原点なので、行を上下ひっくり返します。
        static void Show(Material material, MikoEye eye)
        {
            if (material == null) return;
            int index = (int)eye;
            int column = index % Columns;
            int row = Rows - 1 - index / Columns;
            material.mainTextureScale = new Vector2(1f / Columns, 1f / Rows);
            material.mainTextureOffset = new Vector2((float)column / Columns, (float)row / Rows);
        }

#if UNITY_EDITOR
        // Play 中に Inspector で表情を変えたら、見た目にも反映します。
        void OnValidate()
        {
            if (Application.isPlaying && _left != null) Apply(expression, blinking: false);
        }
#endif

        [ContextMenu("表情: ふつう")] void TestNormal() => Set(MikoExpression.Normal);
        [ContextMenu("表情: にっこり")] void TestHappy() => Set(MikoExpression.Happy);
        [ContextMenu("表情: ＞＜")] void TestSquint() => Set(MikoExpression.Squint);
        [ContextMenu("表情: ♡♡")] void TestLove() => Set(MikoExpression.Love);
        [ContextMenu("表情: ーー")] void TestSleep() => Set(MikoExpression.Sleep);
        [ContextMenu("表情: ー・")] void TestWink() => Set(MikoExpression.Wink);
        [ContextMenu("表情: ∪∪")] void TestTired() => Set(MikoExpression.Tired);
    }
}
