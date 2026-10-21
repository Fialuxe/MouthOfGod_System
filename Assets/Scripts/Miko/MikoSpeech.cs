using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女にしゃべらせる。セリフの一覧（MikoLineBook）から id で引いて、字幕を出し、音声を鳴らし、表情と指さしをつける。
    /// 字幕は、巫女の頭の上に、見ている人のほうを向けて出す。
    /// </summary>
    public sealed class MikoSpeech : MonoBehaviour
    {
        [SerializeField] AudioSource voiceSource;
        const float MinHeight = 140f;
        const float TextPaddingY = 10f;

        [SerializeField] Font font; // 空なら、Unity 組み込みのフォント（英語向け）。日本語を出すときは指定する
        [SerializeField] float subtitleDistance = 1.2f; // 見ている人の前、この距離（メートル）に字幕を出す
        [SerializeField] float subtitleDrop = 0.3f; // 目の高さから、この距離だけ下げる
        [SerializeField] float subtitleWidth = 0.9f; // 字幕の幅（メートル）
        [SerializeField] float followSpeed = 3f; // 頭を回したとき、字幕が追いかける速さ

        GameObject _subtitle;
        Text _text;
        float _hideAt;
        Vector3 _flatForward = Vector3.forward; // 字幕を置く向き（水平）。頭に、ゆっくり追わせる
        bool _restoreExpression;
        Coroutine _pending; // 着くのを待っているセリフ
        readonly Queue<MikoLine> _queue = new Queue<MikoLine>(); // しゃべっている間に来たセリフ。順に言う

        // しゃべっている、または着くのを待っている。
        bool Busy => IsSpeaking || _pending != null;

        public bool IsSpeaking => _subtitle != null && _subtitle.activeSelf;

        /// <summary>id のセリフをしゃべる。見つからなければ false。</summary>
        public bool Say(string id)
        {
            // セリフの一覧は、親（Miko）の下の LineBook にある。
            var book = transform.parent != null ? transform.parent.GetComponentInChildren<MikoLineBook>(true) : GetComponent<MikoLineBook>();
            var line = book != null ? book.Find(id) : null;
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
            if (duration <= 0f) duration = Mathf.Max(4f, (line.text?.Length ?? 0) * 0.3f + 2.5f);
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

        // 見ている人の前で、目より少し下に、水平の向きで置く。頭を回すと、ゆっくり追う。
        void PlaceSubtitle(float follow)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                _flatForward = Vector3.Slerp(_flatForward, forward.normalized, Mathf.Clamp01(follow));
            var position = camera.transform.position + _flatForward * subtitleDistance + Vector3.down * subtitleDrop;
            _subtitle.transform.position = position;
            // 字幕の表は、-Z 側から読める。見ている人から字幕へ向かう向きに合わせる。
            _subtitle.transform.rotation = Quaternion.LookRotation(position - camera.transform.position, Vector3.up);
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

            const float PixelsPerMeter = 1000f;
            const float Height = MinHeight;
            var width = subtitleWidth * PixelsPerMeter;

            _subtitle = new GameObject("Subtitle", typeof(Canvas));
            _subtitle.transform.SetParent(transform, false);
            var canvas = _subtitle.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = _subtitle.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, Height);
            rect.localScale = Vector3.one / PixelsPerMeter;

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(_subtitle.transform, false);
            var image = background.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;
            Stretch(background.GetComponent<RectTransform>());

            var label = new GameObject("Text", typeof(Text));
            label.transform.SetParent(_subtitle.transform, false);
            _text = label.GetComponent<Text>();
            _text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 48;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = Color.white;
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = label.GetComponent<RectTransform>();
            Stretch(textRect);
            textRect.offsetMin = new Vector2(20f, TextPaddingY);
            textRect.offsetMax = new Vector2(-20f, -TextPaddingY);

            _subtitle.SetActive(false);
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
