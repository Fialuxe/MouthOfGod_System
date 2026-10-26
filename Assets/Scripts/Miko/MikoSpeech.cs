using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女にしゃべらせる。セリフの一覧（MikoLineBook）から id で引いて、字幕を出し、音声を鳴らし、表情と指さしをつける。
    /// 字幕は、見ている人の視線の少し下に出す。頭を回す・見下ろすと、ゆっくり追う。台のミニチュアより手前に置き、ほかの物に隠れないように描く。
    /// </summary>
    public sealed class MikoSpeech : MonoBehaviour
    {
        [SerializeField] AudioSource voiceSource;
        const float MinHeight = 140f;
        const float TextPaddingY = 10f;
        const float CanvasWidth = 900f; // 字幕の Canvas の幅（px）。文字の大きさ（48px）は、この幅に対して決まる

        [SerializeField] Font font; // 空なら、Unity 組み込みのフォント（英語向け）。日本語を出すときは指定する
        [Tooltip("見ている人から字幕までの距離（m）。台のミニチュア（およそ 0.9 m 先）より手前にする")]
        [SerializeField] float subtitleDistanceMeters = 0.7f;
        [Tooltip("字幕の長さ（秒）= 文字数 × この値 + 余白。Seconds が 0 で、音声もないときに使う。英語を読む速さの目安")]
        [SerializeField, Min(0f)] float secondsPerCharacter = 0.06f;
        [Tooltip("字幕の長さの余白（秒）。読み始めるまでの間")]
        [SerializeField, Min(0f)] float subtitlePaddingSeconds = 1.5f;
        [Tooltip("字幕の長さの下限（秒）。短いセリフでも、これだけは出す")]
        [SerializeField, Min(0f)] float minSubtitleSeconds = 2f;
        [Tooltip("視線より、この角度だけ下に出す（度）。見ている物を字幕でふさがないため")]
        [SerializeField] float subtitleBelowGaze = 12f;
        [Tooltip("見下ろす・見上げるのに合わせて、字幕の上下を追う範囲（度。下が負）。台を見下ろしても字幕が視界に入るようにする")]
        [SerializeField] Vector2 pitchRange = new Vector2(-55f, 10f);
        [Tooltip("字幕の幅（m）。文字の大きさも、幅に合わせて変わる")]
        [SerializeField] float subtitleWidthMeters = 0.55f;
        [Tooltip("頭を回したとき、字幕が追いかける速さ")]
        [SerializeField] float followSpeed = 3f;
        [Tooltip("入れると、字幕をほかの物より前に描く（村や雲に埋もれない）")]
        [SerializeField] bool alwaysOnTop = true;

        GameObject _subtitle;
        Text _text;
        float _hideAt;
        Vector3 _flatForward = Vector3.forward; // 字幕を置く向き（水平）。頭に、ゆっくり追わせる
        float _pitch; // 字幕を置く上下の角度（度。下が負）。頭に、ゆっくり追わせる
        Material _onTopMaterial;
        bool _restoreExpression;
        Coroutine _pending; // 着くのを待っているセリフ
        readonly Queue<MikoLine> _queue = new Queue<MikoLine>(); // しゃべっている間に来たセリフ。順に言う

        // しゃべっている、または着くのを待っている。
        bool Busy => IsSpeaking || _pending != null;

        public bool IsSpeaking => _subtitle != null && _subtitle.activeSelf;

        /// <summary>id のセリフをしゃべる。見つからなければ false。</summary>
        public bool Say(string id)
        {
            // まず親（Miko）の下の LineBook を見る。無ければ、シーンにある場面ごとの LineBook から探す。
            var book = transform.parent != null ? transform.parent.GetComponentInChildren<MikoLineBook>(true) : GetComponent<MikoLineBook>();
            var line = (book != null ? book.Find(id) : null) ?? MikoLineBook.FindAny(id);
            if (line == null) return false;
            // 言っている最中に来たセリフは、上書きせず、言い終わるのを待つ。
            if (Busy) _queue.Enqueue(line);
            else Speak(line);
            return true;
        }

        /// <summary>しゃべるのをやめて、字幕を消す。順番待ちのセリフも捨てる。</summary>
        public void Hush()
        {
            _queue.Clear();
            Stop();
        }

        void Stop()
        {
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
            if (voiceSource != null) voiceSource.Stop();
            if (_subtitle != null) _subtitle.SetActive(false);
            RestoreExpression();
        }

        void Speak(MikoLine line)
        {
            Stop();
            var mover = GetComponent<MikoMover>();
            if (line.moveTo != null && mover != null)
            {
                mover.GoTo(line.moveTo);
                if (line.speakAfterMove)
                {
                    _pending = StartCoroutine(SpeakAfterMove(line, mover));
                    return;
                }
            }
            Begin(line);
        }

        // 着いてからしゃべる。着くまで、字幕も声も出さない。
        IEnumerator SpeakAfterMove(MikoLine line, MikoMover mover)
        {
            while (mover.Mode == MikoMode.Go) yield return null;
            _pending = null;
            Begin(line);
        }

        void Begin(MikoLine line)
        {
            EnsureSubtitle();
            _text.text = line.text;
            _subtitle.SetActive(true);
            FitSubtitle();

            var face = GetComponent<MikoFace>();
            if (face != null && line.changeExpression)
            {
                face.Set(line.expression);
                _restoreExpression = true;
            }
            if (line.point)
            {
                var receiver = GetComponent<MikoCueReceiver>();
                if (receiver != null) receiver.PointAt(line.pointTarget);
            }

            var duration = line.seconds;
            if (line.voice != null && voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = line.voice;
                voiceSource.Play();
                if (duration <= 0f) duration = line.voice.length + 0.5f;
            }
            if (duration <= 0f) duration = Mathf.Max(minSubtitleSeconds, (line.text?.Length ?? 0) * secondsPerCharacter + subtitlePaddingSeconds);
            _hideAt = Time.time + duration;
            SnapSubtitle();
        }

        void Update()
        {
            if (_subtitle == null || !_subtitle.activeSelf) return;
            if (Time.time < _hideAt) return;
            Stop();
            if (_queue.Count > 0) Speak(_queue.Dequeue());
        }

        void LateUpdate()
        {
            if (_subtitle == null || !_subtitle.activeSelf) return;
            PlaceSubtitle(followSpeed * Time.deltaTime);
        }

        // 出しはじめは、いま見ている向きにすぐ置く。
        void SnapSubtitle() => PlaceSubtitle(1f);

        // 見ている人の視線の少し下に置く。頭を回す・見下ろすと、ゆっくり追う。
        void PlaceSubtitle(float follow)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var view = camera.transform.forward;
            var forward = view;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                _flatForward = Vector3.Slerp(_flatForward, forward.normalized, Mathf.Clamp01(follow));
            var pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(view.y, -1f, 1f)) * Mathf.Rad2Deg, pitchRange.x, pitchRange.y);
            _pitch = Mathf.Lerp(_pitch, pitch, Mathf.Clamp01(follow));
            var position = camera.transform.position + SubtitleDirection(_flatForward, _pitch - subtitleBelowGaze) * subtitleDistanceMeters;
            _subtitle.transform.position = position;
            // 字幕の表は、-Z 側から読める。見ている人から字幕へ向かう向きに合わせる。
            _subtitle.transform.rotation = Quaternion.LookRotation(position - camera.transform.position, Vector3.up);
        }

        // 水平の向き flatForward から、pitch 度（下が負）だけ上下に傾けた向き。
        internal static Vector3 SubtitleDirection(Vector3 flatForward, float pitch)
        {
            var radians = pitch * Mathf.Deg2Rad;
            return (flatForward.normalized * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)).normalized;
        }

        // 字幕の高さを、文字がすべて入る高さにそろえる。
        void FitSubtitle()
        {
            Canvas.ForceUpdateCanvases();
            var rect = _subtitle.GetComponent<RectTransform>();
            var height = Mathf.Max(MinHeight, _text.preferredHeight + 2f * TextPaddingY + 20f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        void RestoreExpression()
        {
            if (!_restoreExpression) return;
            _restoreExpression = false;
            var face = GetComponent<MikoFace>();
            if (face != null) face.Set(MikoExpression.Normal);
        }

        void EnsureSubtitle()
        {
            if (_subtitle != null) return;

            const float Height = MinHeight;
            var width = CanvasWidth;

            // 巫女の子にはしない。巫女の大きさを変えても、字幕の大きさが変わらないようにする。
            _subtitle = new GameObject("MikoSubtitle", typeof(Canvas));
            var canvas = _subtitle.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = _subtitle.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, Height);
            rect.localScale = Vector3.one * (subtitleWidthMeters / CanvasWidth);
            canvas.sortingOrder = 100;
            if (alwaysOnTop)
            {
                // UI のマテリアルの深度テストを切り、ほかの物の後ろでも見えるようにする。
                _onTopMaterial = new Material(Canvas.GetDefaultCanvasMaterial());
                _onTopMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                _onTopMaterial.renderQueue = (int)RenderQueue.Overlay;
            }

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(_subtitle.transform, false);
            var image = background.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;
            if (_onTopMaterial != null) image.material = _onTopMaterial;
            Stretch(background.GetComponent<RectTransform>());

            var label = new GameObject("Text", typeof(Text));
            label.transform.SetParent(_subtitle.transform, false);
            _text = label.GetComponent<Text>();
            _text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 48;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = Color.white;
            _text.raycastTarget = false;
            if (_onTopMaterial != null) _text.material = _onTopMaterial;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = label.GetComponent<RectTransform>();
            Stretch(textRect);
            textRect.offsetMin = new Vector2(20f, TextPaddingY);
            textRect.offsetMax = new Vector2(-20f, -TextPaddingY);

            _subtitle.SetActive(false);
        }

        void OnDestroy()
        {
            if (_subtitle != null) Destroy(_subtitle);
            if (_onTopMaterial != null) Destroy(_onTopMaterial);
        }

        void OnDisable()
        {
            if (_subtitle != null) _subtitle.SetActive(false);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        [ContextMenu("テスト: 最初のセリフ")]
        void TestFirst()
        {
            if (!Say("welcome")) Debug.LogWarning("[Miko] MikoLineBook が無いか、welcome のセリフがありません。");
        }
    }
}
