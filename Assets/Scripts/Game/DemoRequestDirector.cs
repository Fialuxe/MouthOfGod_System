using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Game
{
    /// <summary>
    /// デモシーン用の、仮の進行（#287）。Play するだけで、村人の欲しい物が順に出る。届くと次が出て、届かなければ一定時間で消える。
    /// <see cref="Fields"/> のフィールドに Open() / Expire(番号) を呼ぶだけ。決まりは <see cref="DemoRequestLogic"/>、ここは Unity とつなぐ部分。
    /// Play で自動で置かれるので、手で置かなくてよい。チュートリアル中（<see cref="Phases.Current"/> が空でない間）は新しく出さない。
    /// 本編の進行（スコア・届け先の判定）ができたら置き換える。
    /// </summary>
    public class DemoRequestDirector : MonoBehaviour
    {
        [Tooltip("欲しい物を出す間隔（秒）。仮。")]
        [SerializeField] private float interval = 30f;

        [Tooltip("届かないとき、欲しい物を消すまでの時間（秒）。")]
        [SerializeField] private float expireSeconds = 20f;

        [Tooltip("最初のこの秒数のあいだは、同時に出す数を Early Max Open にする。")]
        [SerializeField] private float earlySeconds = 30f;

        [SerializeField] private int earlyMaxOpen = 1;

        [Tooltip("最初の時間のあとの、同時に出す数の上限。")]
        [SerializeField] private int maxOpen = 2;

        static bool _placed;
        DemoRequestLogic _logic;

        // Domain Reload を切っていても、Play のたびに初期化する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _placed = false;
        }

        // 新しく 1 つ作るだけ（シーンから探さない）。二重に作らないのは static フラグ 1 つ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoPlace()
        {
            if (_placed) return;
            _placed = true;
            var go = new GameObject("Demo Request Director");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoRequestDirector>();
        }

        void OnEnable()
        {
            _logic = new DemoRequestLogic(interval, expireSeconds, earlySeconds, earlyMaxOpen, maxOpen);
            foreach (var field in Fields.All) Watch(field); // もうある場所
            Fields.Added += Watch;                           // あとから来た場所
            Fields.Removed += Unwatch;
        }

        void OnDisable()
        {
            Fields.Added -= Watch;
            Fields.Removed -= Unwatch;
            foreach (var field in Fields.All) Unwatch(field);
            _logic?.Clear();
        }

        void Update()
        {
            // チュートリアルの場面が決まっている間は、新しく出さない（待ちきれたものの Expire は続ける）。
            _logic.Tick(Fields.All, Time.time, Phases.Current.IsEmpty);
        }

        void Watch(IFieldRequests field)
        {
            field.Issued += OnIssued;
            field.Closed += OnClosed;
        }

        void Unwatch(IFieldRequests field)
        {
            field.Issued -= OnIssued;
            field.Closed -= OnClosed;
            _logic?.Forget(field);
        }

        void OnIssued(RequestIssued r)
        {
            _logic.OnIssued(Fields.Find(r.Field), r.Request, Time.time);
        }

        void OnClosed(RequestClosed r)
        {
            _logic.OnClosed(Fields.Find(r.Field), r.Request);
        }
    }
}
