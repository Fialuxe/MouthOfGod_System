using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.UI
{
    /// <summary>
    /// 本編の時間切れのあとに、届けた数を英語の文で正面に出します（#355）。Play で自動で置かれるので、手で置かなくてよいです。
    /// 場面（<see cref="Phases"/>）が main-game の間、<see cref="Fields"/> のフィールドへ届いた物を数えます。
    /// 点の付け方は未定なので、数えるのは欲しがられていた物（<see cref="DeliveryKind.Requested"/>）だけです（docs/decisions.md 2026-10-15）。
    /// game-over になったら、その数をカメラの前に出します。
    /// main-game になったら、数を 0 に戻して隠します。game-over 以外の場面でも隠します。
    /// 時間切れは、場面が game-over になったことで知ります。そのため、Game の型は参照しません。
    /// Prefab もシーンも使わず、文字をコードで作ります。
    /// 文・距離・高さ・文字の大きさと色は、<see cref="GameResultSettings"/> で直します。
    /// 設定は <c>Assets/Resources/GameResultSettings.asset</c> に置きます。無ければ、コードの既定値で動きます。
    /// </summary>
    public sealed class GameResultText : MonoBehaviour
    {
        static readonly PhaseId MainGame = new PhaseId("main-game");
        static readonly PhaseId GameOver = new PhaseId("game-over");

        // 文・距離・高さ・文字の大きさと色は、誰でも直せるように設定のアセットに持つ（GameResultSettings）。
        GameResultSettings _settings;
        GameResultSettings Settings => _settings != null ? _settings : _settings = GameResultSettings.Load();

        static bool _placed;

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
            var go = new GameObject("Game Result Text");
            DontDestroyOnLoad(go);
            go.AddComponent<GameResultText>();
        }

        // フィールドごとの購読。外すときに同じデリゲートが要るので持っておく。
        readonly Dictionary<IFieldRequests, Action<Delivered>> _subscriptions = new Dictionary<IFieldRequests, Action<Delivered>>();
        int _delivered;
        GameObject _label;
        TextMesh _mesh;
        Font _font;
        bool _needsPlacement; // 出したが、カメラが無くてまだ置けていない

        /// <summary>main-game の間に届いた、欲しがられていた物の数。</summary>
        internal int Delivered => _delivered;

        /// <summary>文を出している。</summary>
        internal bool IsShowing => _label != null && _label.activeSelf;

        /// <summary>出している（最後に出した）文。まだ出していなければ空。</summary>
        internal string Text => _mesh != null ? _mesh.text : string.Empty;

        void OnEnable() => Listen();

        void OnDisable() => StopListening();

        // 場面とフィールドの知らせを聞き始める。EditMode のテストでは OnEnable が呼ばれないので、テストから直接呼ぶ。
        internal void Listen()
        {
            foreach (IFieldRequests field in new List<IFieldRequests>(Fields.All)) Attach(field);
            Fields.Added += Attach;
            Fields.Removed += Detach;
            Phases.Changed += OnPhaseChanged;
            // 有効になったときの場面に合わせて、出すか隠すかだけ決める（数は戻さない）。
            if (Phases.Current == GameOver) Show();
            else Hide();
        }

        // 聞くのをやめ、出している文を消す。
        internal void StopListening()
        {
            Fields.Added -= Attach;
            Fields.Removed -= Detach;
            Phases.Changed -= OnPhaseChanged;
            foreach (var pair in _subscriptions) pair.Key.Received -= pair.Value;
            _subscriptions.Clear();
            DestroyLabel();
        }

        void OnDestroy()
        {
            DestroyLabel();
        }

        void Attach(IFieldRequests field)
        {
            if (field == null || _subscriptions.ContainsKey(field)) return;
            Action<Delivered> received = OnReceived;
            _subscriptions.Add(field, received);
            field.Received += received;
        }

        void Detach(IFieldRequests field)
        {
            if (field == null || !_subscriptions.TryGetValue(field, out Action<Delivered> received)) return;
            field.Received -= received;
            _subscriptions.Remove(field);
        }

        void OnReceived(Delivered delivered)
        {
            if (Phases.Current != MainGame || !delivered.WasRequested) return;
            _delivered++;
        }

        void OnPhaseChanged(PhaseId phase)
        {
            if (phase == MainGame)
            {
                _delivered = 0;
                Hide();
            }
            else if (phase == GameOver)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        /// <summary>出す文を作る。書式が壊れていたら（{0} の書き間違いなど）、警告を出して、いつもの書式で作る。</summary>
        internal static string Format(string format, int delivered)
        {
            if (string.IsNullOrEmpty(format)) return string.Format(GameResultSettings.DefaultFormat, delivered);
            try
            {
                return string.Format(format, delivered);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[GameResultText] 文の書式「{format}」が壊れているので、いつもの書式で出します（届けた数は {{0}} と書く）。");
                return string.Format(GameResultSettings.DefaultFormat, delivered);
            }
        }

        /// <summary>
        /// カメラの前の向きを、水平に直す。真上・真下を向いているときは、頭のてっぺんの向きから決める（それも無ければ世界の前）。
        /// </summary>
        internal static Vector3 FlatForward(Vector3 forward, Vector3 up)
        {
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude > 1e-6f) return flat.normalized;
            // 真下を向くと頭のてっぺんが前を、真上を向くと後ろを指す。
            var top = new Vector3(up.x, 0f, up.z) * (forward.y < 0f ? 1f : -1f);
            if (top.sqrMagnitude > 1e-6f) return top.normalized;
            return Vector3.forward;
        }

        /// <summary>文を置く場所。カメラの位置から、水平な前に distance、上に height。</summary>
        internal static Vector3 PlacePosition(Vector3 cameraPosition, Vector3 forward, Vector3 up, float distance, float height) =>
            cameraPosition + FlatForward(forward, up) * distance + Vector3.up * height;

        void Show()
        {
            if (_label == null) CreateLabel();
            GameResultSettings settings = Settings; // 出すたびに読むので、Play 中に直した値は次に出すときに効く
            _mesh.text = Format(settings.Format, _delivered);
            _mesh.characterSize = settings.CharacterSize;
            _mesh.color = settings.Color;
            _label.SetActive(true);
            _needsPlacement = !TryPlace();
        }

        void Hide()
        {
            if (_label != null) _label.SetActive(false);
            _needsPlacement = false;
        }

        void LateUpdate()
        {
            // 出したときにカメラが無かったら、見つかったときに 1 回だけ置く。置いたあとは動かさない（頭に固定しない）。
            if (_needsPlacement && IsShowing) _needsPlacement = !TryPlace();
        }

        // カメラの前に置き、カメラの方へ向ける。カメラが無ければ false。
        bool TryPlace()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Transform eye = cam.transform;
            Vector3 position = PlacePosition(eye.position, eye.forward, eye.up, Settings.Distance, Settings.Height);
            _label.transform.position = position;
            Vector3 away = position - eye.position;
            if (away.sqrMagnitude > 1e-6f) _label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            return true;
        }

        void CreateLabel()
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label = new GameObject("Result Text");
            _label.transform.SetParent(transform, false);
            _mesh = _label.AddComponent<TextMesh>();
            _mesh.fontSize = 64;
            _mesh.anchor = TextAnchor.MiddleCenter;
            _mesh.alignment = TextAlignment.Center;
            if (_font != null)
            {
                _mesh.font = _font;
                _label.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            }
            else
            {
                Debug.LogWarning("[GameResultText] 文字のフォントが見つからないので、文が見えないかもしれません。");
            }
        }

        void DestroyLabel()
        {
            if (_label != null) DestroySafely(_label);
            _label = null;
            _mesh = null;
            _needsPlacement = false;
        }

        // エディタの EditMode のテストでは Destroy が使えないので、DestroyImmediate に切り替える。
        static void DestroySafely(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
