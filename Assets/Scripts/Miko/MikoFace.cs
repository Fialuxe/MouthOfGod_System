using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>目の形（Miko_Eyes.png の絵の順番）。</summary>
    public enum MikoEye
    {
        Open, Line, Happy, Gt, Lt, Heart, Dot, Sleepy,
    }

    /// <summary>表情。左右の目の形の組み合わせ。</summary>
    public enum MikoExpression
    {
        Normal, // ●●
        Happy, // ^^
        Squint, // ＞＜
        Love, // ♡♡
        Sleep, // ーー
        Wink, // ー・
        Tired, // ∪∪
    }

    /// <summary>
    /// 巫女の表情を切り替える。目は、顔に貼った 2 枚の板（Quad）に、絵（8 コマ）の 1 コマを映して変える。
    /// 表情が Normal のときは、ときどきまばたきをする。
    /// </summary>
    public sealed class MikoFace : MonoBehaviour
    {
        const int Columns = 4;
        const int Rows = 2;

        [SerializeField] Renderer viewerLeftEye; // 見ている人から見て左の目
        [SerializeField] Renderer viewerRightEye;
        [SerializeField] MikoExpression expression = MikoExpression.Normal;
        [SerializeField] Vector2 blinkInterval = new Vector2(2.5f, 5f); // まばたきの間隔（秒）
        [SerializeField] float blinkSeconds = 0.12f;

        Material _left;
        Material _right;
        float _nextBlink;
        float _blinkEnd;

        public MikoExpression Expression => expression;

        /// <summary>表情を変える。</summary>
        public void Set(MikoExpression value)
        {
            expression = value;
            Apply(value, blinking: false);
        }

        /// <summary>名前（normal / happy / squint / love / sleep / wink / tired）で変える。分からない名前なら false。</summary>
        public bool Set(string name)
        {
            if (!System.Enum.TryParse(name, true, out MikoExpression value)) return false;
            Set(value);
            return true;
        }

        void Awake()
        {
            // 板ごとに別の材質にして、映すコマを左右で変えられるようにする。
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

        // 絵は 4 列 × 2 行。0 番が左上で、右へ数える。UV は左下が原点なので、行を上下ひっくり返す。
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
        // Inspector で表情を変えたとき、Play 中なら見た目にも反映する。
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
