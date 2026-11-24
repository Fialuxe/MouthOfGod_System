using MouthOfGod.Core;
using TMPro;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 本編の残り時間と点数を、体験者の斜め前の空中に出します。終わったあとは、最終の点数を出します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 頭に貼りつく表示（HUD）は、VR では見づらく、酔いやすいです。
    /// そのため、この表示は空中の文字として置きます。
    /// 体験者が顔を大きく向け直したときだけ、ゆっくり追いかけて、視界の斜め前に戻ります。
    /// 顔を少し動かしたくらいでは動きません。文字は、いつも体験者のほうを向いて立ちます（傾けません）。
    /// </para>
    /// <para>
    /// 文字は、起動時にコードで作ります。シーンには、この部品を付けるだけです。
    /// 点が増えた瞬間に、数字が大きく跳ねて金色に光ります。続けて届けている間は、倍率も出します。
    /// 残りが <c>Warning Seconds</c> を切ると赤くなり、小さく脈打ちます。
    /// 脈の大きさは、VR で不快にならないよう、小さくしています。
    /// </para>
    /// <para>
    /// 文字の大きさは、仮の値です。Quest をかぶって、読みやすさを見て決めます。
    /// </para>
    /// </remarks>
    public sealed class ScoreBoard : MonoBehaviour
    {
        private const float PunchDurationSeconds = 0.35f;
        private const float PunchExtraScale = 0.5f;
        private const float PulseExtraScale = 0.06f;
        private const float PulseRadiansPerSecond = 8f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color CelebrationColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color WarningColor = new Color(1f, 0.35f, 0.3f);

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("体験者から文字までの距離です（メートル）。近すぎると目が疲れるので、1.5〜2.5 が目安です。")]
        private float _distanceMeters = 1.8f;

        [SerializeField]
        [Tooltip("体験者の正面から、横にずらす角度です（度）。負の値で左です。正面に置くと本編の邪魔になります。")]
        private float _yawOffsetDegrees = -22f;

        [SerializeField]
        [Tooltip("目の高さからの、上へのずれです（メートル）。")]
        private float _heightOffsetMeters = 0.25f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("文字の大きさです。1.5 で、1.8 m 先で 1 行が 15 cm ほどです。")]
        private float _fontSize = 1.5f;

        [SerializeField]
        [Range(5f, 90f)]
        [Tooltip("顔の向きが、文字の向きからこの角度より離れたときだけ、文字が追いかけます（度）。")]
        private float _followDeadZoneDegrees = 30f;

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("追いかけるときの、なめらかさです（秒）。大きいほどゆっくり動きます。")]
        private float _followSmoothSeconds = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("残りがこの秒数を切ると、時間を赤くして脈打たせます。0 なら、しません。")]
        private float _warningSeconds = 10f;

        private TextMeshPro _text;
        private float _targetYaw;
        private float _yaw;
        private float _yawVelocity;
        private bool _hasYaw;
        private float _punch;
        private bool _isFinalShown;

        private int _secondsLeft = -1;
        private int _score;
        private int _streak;
        private int _answered;
        private string _shownContent;

        /// <summary>残り時間を更新します。表示が無ければ出します。</summary>
        /// <param name="secondsLeft">残りの秒数です。0 未満は 0 として出します。</param>
        public void ShowRemainingSeconds(int secondsLeft)
        {
            _isFinalShown = false;
            _secondsLeft = Mathf.Max(0, secondsLeft);
            RefreshText();
        }

        /// <summary>点数を更新します。表示が無ければ出します。</summary>
        /// <param name="score">いまの点数です。</param>
        /// <param name="streak">続けて届けた数です。2 以上のときだけ、倍率を出します。</param>
        /// <param name="celebrate">
        /// <see langword="true"/> なら、数字を跳ねさせて光らせます。
        /// 点が増えたときに渡します。
        /// </param>
        public void ShowScore(int score, int streak, bool celebrate)
        {
            _isFinalShown = false;
            _score = score;
            _streak = streak;
            if (celebrate)
            {
                _punch = 1f;
            }

            RefreshText();
        }

        /// <summary>本編が終わったあとの、最終の点数を出します。</summary>
        /// <param name="score">最終の点数です。</param>
        /// <param name="answered">欲しがられていた物を届けた数です。</param>
        public void ShowFinalScore(int score, int answered)
        {
            _isFinalShown = true;
            _score = score;
            _answered = answered;
            _punch = 1f;
            RefreshText();
        }

        /// <summary>表示を消し、数を 0 に戻します。やり直すときに呼びます。</summary>
        public void HideAndClear()
        {
            _secondsLeft = -1;
            _score = 0;
            _streak = 0;
            _answered = 0;
            _punch = 0f;
            _isFinalShown = false;
            _hasYaw = false;
            _shownContent = null;
            if (_text != null)
            {
                _text.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_text != null)
            {
                Destroy(_text.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.gameObject.activeSelf)
            {
                return;
            }

            Transform head = FindHead();
            if (head == null)
            {
                return;
            }

            FollowHeadSlowly(head);
            AnimateText();
        }

        private Transform FindHead()
        {
            if (PlayerRig.Current != null && PlayerRig.Current.Head != null)
            {
                return PlayerRig.Current.Head;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : null;
        }

        // 顔が大きく向きを変えたときだけ、追いかける。揺れる表示は、VR では酔いのもとになるため。
        private void FollowHeadSlowly(Transform head)
        {
            float headYaw = head.eulerAngles.y;
            if (!_hasYaw)
            {
                _hasYaw = true;
                _yaw = headYaw;
                _targetYaw = headYaw;
                _yawVelocity = 0f;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, headYaw)) > _followDeadZoneDegrees)
            {
                _targetYaw = headYaw;
            }

            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVelocity, _followSmoothSeconds);

            Quaternion around = Quaternion.Euler(0f, _yaw + _yawOffsetDegrees, 0f);
            Vector3 eye = head.position;
            Vector3 position = eye + around * Vector3.forward * _distanceMeters + Vector3.up * _heightOffsetMeters;
            Vector3 towardViewer = position - eye;
            towardViewer.y = 0f;

            Transform textTransform = _text.transform;
            textTransform.position = position;
            // TextMeshPro の文字は、+Z の向きに背を向けた側から読める。そのため、視線の向きに向ける。
            textTransform.rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
        }

        private void AnimateText()
        {
            _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime / PunchDurationSeconds);
            float easedPunch = _punch * _punch;
            float scale = 1f + PunchExtraScale * easedPunch;

            bool isWarning = !_isFinalShown && _warningSeconds > 0f && _secondsLeft >= 0 && _secondsLeft <= _warningSeconds;
            if (isWarning)
            {
                scale += PulseExtraScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRadiansPerSecond));
            }

            _text.transform.localScale = Vector3.one * scale;

            Color baseColor = isWarning ? WarningColor : NormalColor;
            _text.color = Color.Lerp(baseColor, CelebrationColor, easedPunch);
        }

        private void RefreshText()
        {
            EnsureTextCreated();

            string content = _isFinalShown ? BuildFinalContent() : BuildPlayingContent();
            _text.gameObject.SetActive(true);
            if (content != _shownContent)
            {
                _shownContent = content;
                _text.text = content;
            }
        }

        private string BuildPlayingContent()
        {
            string time = _secondsLeft >= 0 ? FormatClock(_secondsLeft) : "";
            string streakLine = _streak >= 2 ? "\n<size=45%><color=#FFD633>STREAK x" + _streak + "</color></size>" : "";
            return "<size=45%>" + time + "</size>\n" + _score + streakLine;
        }

        private string BuildFinalContent()
        {
            return "<size=45%>FINAL SCORE</size>\n" + _score + "\n<size=45%>" + _answered + " WISHES ANSWERED</size>";
        }

        private static string FormatClock(int totalSeconds)
        {
            return (totalSeconds / 60) + ":" + (totalSeconds % 60).ToString("00");
        }

        private void EnsureTextCreated()
        {
            if (_text != null)
            {
                return;
            }

            var holder = new GameObject("ScoreBoardText");
            _text = holder.AddComponent<TextMeshPro>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = _fontSize;
            _text.fontStyle = FontStyles.Bold;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.richText = true;
            _text.color = NormalColor;
        }
    }
}
