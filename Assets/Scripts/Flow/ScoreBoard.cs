using MouthOfGod.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// シーンに置いた UI（残り時間・点数・連続の倍率）を、本編の進み具合に合わせて動かします。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 見た目は、シーンの Canvas に置きます。この部品は、文字を作りません。
    /// 数字や文字を入れ替えて、跳ねる・光る・数が増えていく動きと、音を足すだけです。
    /// 見た目を変えたいときは、シーンの Canvas を直接直します。
    /// </para>
    /// <para>
    /// Canvas は、体験者の斜め前の空中に置きます（World Space）。
    /// 頭に貼りつく表示は、VR では見づらく、酔いやすいためです。
    /// 顔を大きく向け直したときだけ、ゆっくり追いかけます。
    /// Canvas のオブジェクト（<c>Hud</c>）は、はじめは無効にしておきます。
    /// 本編が始まると、この部品が有効にします。
    /// </para>
    /// <para>
    /// 点が入ると、数字が跳ねて金色に光り、「+300」が浮かび上がり、粒とコインの音が出ます。
    /// 点数は、すぐには変わらず、数えるように増えていきます。
    /// 続けて届けると、音が高くなり、倍率の札が出ます。
    /// 残りが <c>Warning Seconds</c> を切ると、時間と帯が赤くなり、小さく脈打ちます。
    /// 終わると「TIME UP!」を出し、最終の点数と順位（S・A・B・C）を出します。
    /// </para>
    /// <para>
    /// 大きさ・距離・位置は、仮の値です。Quest をかぶって、読みやすさを見て決めます。
    /// </para>
    /// </remarks>
    public sealed class ScoreBoard : MonoBehaviour
    {
        private const float PunchSeconds = 0.4f;
        private const float PunchExtraScale = 0.45f;
        private const float PopupSeconds = 1.1f;
        private const float PopupRisePixels = 90f;
        private const float BannerHoldSeconds = 0.9f;
        private const float BannerFadeSeconds = 0.5f;
        private const float BannerPopSeconds = 0.25f;
        private const float PulseExtraScale = 0.06f;
        private const float PulseRadiansPerSecond = 8f;
        private const float BurstLifetimeSeconds = 3f;
        private const float BurstTowardViewerMeters = 0.06f;
        private const int RankSScore = 3000;
        private const int RankAScore = 2000;
        private const int RankBScore = 1000;

        private const int SampleRate = 44100;
        private const float VolumeScale = 0.8f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.2f);
        private static readonly Color WarningColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color BarGoodColor = new Color(0.35f, 0.9f, 0.5f);
        private static readonly Color BarCautionColor = new Color(1f, 0.8f, 0.25f);

        [Header("シーンの UI")]
        [SerializeField]
        [Tooltip("Canvas のオブジェクトです。はじめは無効にしておきます。本編が始まると、この部品が有効にして、体験者の斜め前へ動かします。")]
        private GameObject _hud;

        [SerializeField]
        [Tooltip("残り時間の文字です。終わったあとは、順位を出します。")]
        private TMP_Text _timeText;

        [SerializeField]
        [Tooltip("点数の上の小さな文字です。「SCORE」と出ます。")]
        private TMP_Text _labelText;

        [SerializeField]
        [Tooltip("点数の大きな文字です。")]
        private TMP_Text _scoreText;

        [SerializeField]
        [Tooltip("続けて届けたときに出す札です。2 回続けて届けると出ます。")]
        private GameObject _streakBadge;

        [SerializeField]
        [Tooltip("札の中の文字です。")]
        private TMP_Text _streakText;

        [SerializeField]
        [Tooltip("点が入ったときに、上へ浮かんで消える「+300」の文字です。")]
        private TMP_Text _popupText;

        [SerializeField]
        [Tooltip("真ん中に大きく出す文字です。「GO!」と「TIME UP!」を出します。")]
        private TMP_Text _bannerText;

        [SerializeField]
        [Tooltip("残り時間の帯の入れ物です。終わったあとは隠します。")]
        private GameObject _timeBar;

        [SerializeField]
        [Tooltip("残り時間の帯の中身です。右端の位置（Anchor Max の X）を動かして、減っていく様子を見せます。")]
        private RectTransform _timeBarFill;

        [SerializeField]
        [Tooltip("残り時間の帯の中身の色です。緑から黄、赤へ変わります。")]
        private Image _timeBarFillImage;

        [Header("演出")]
        [SerializeField]
        [Tooltip("点が入ったときに、点数の前に出す粒です。一度だけ出て、終わると自分で消える Prefab にします。空なら出しません。")]
        private GameObject _burstPrefab;

        [SerializeField]
        [Min(0f)]
        [Tooltip("粒の大きさの倍率です。")]
        private float _burstScale = 3f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("音の大きさです（0〜1）。0 なら音を出しません。")]
        private float _volume = 0.8f;

        [Header("位置")]
        [SerializeField]
        [Min(0.5f)]
        [Tooltip("体験者から Canvas までの距離です（メートル）。近すぎると目が疲れるので、1.5〜2.5 が目安です。")]
        private float _distanceMeters = 1.8f;

        [SerializeField]
        [Tooltip("体験者の正面から、横にずらす角度です（度）。負の値で左です。正面に置くと本編の邪魔になります。")]
        private float _yawOffsetDegrees = -22f;

        [SerializeField]
        [Tooltip("目の高さからの、上へのずれです（メートル）。")]
        private float _heightOffsetMeters = 0.25f;

        [SerializeField]
        [Range(5f, 90f)]
        [Tooltip("顔の向きが Canvas の向きからこの角度より離れたときだけ、Canvas が追いかけます（度）。")]
        private float _followDeadZoneDegrees = 30f;

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("追いかけるときの、なめらかさです（秒）。大きいほどゆっくり動きます。")]
        private float _followSmoothSeconds = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("残りがこの秒数を切ると、時間を赤くして脈打たせます。0 なら、しません。")]
        private float _warningSeconds = 10f;

        private AudioClip[] _coinClips;
        private AudioClip _fanfareClip;
        private Vector2 _popupBasePosition;
        private Vector3 _scoreBaseScale = Vector3.one;
        private Vector3 _badgeBaseScale = Vector3.one;
        private Vector3 _bannerBaseScale = Vector3.one;

        private float _yaw;
        private float _targetYaw;
        private float _yawVelocity;
        private bool _hasYaw;

        private int _targetScore;
        private float _shownScore;
        private int _shownScoreInteger = -1;
        private int _streak;
        private int _answered;
        private int _secondsLeft = -1;
        private int _totalSeconds = 1;
        private bool _isFinalShown;
        private float _punch;
        private float _badgePunch;
        private float _popupAge = float.PositiveInfinity;
        private float _bannerAge = float.PositiveInfinity;
        private bool _hasWarnedNoHud;
        private bool _hasWarnedNoHead;

        /// <summary>残り時間を更新します。表示が無ければ出します。</summary>
        /// <param name="secondsLeft">残りの秒数です。0 未満は 0 として出します。</param>
        /// <param name="totalSeconds">本編の制限時間です。帯の長さを決めます。1 未満は 1 として扱います。</param>
        public void ShowRemainingSeconds(int secondsLeft, int totalSeconds)
        {
            if (!TryShowHud())
            {
                return;
            }

            _isFinalShown = false;
            _secondsLeft = Mathf.Max(0, secondsLeft);
            _totalSeconds = Mathf.Max(1, totalSeconds);
            RefreshPlayingTexts();
        }

        /// <summary>点数を更新します。表示が無ければ出します。</summary>
        /// <param name="score">いまの点数です。</param>
        /// <param name="streak">続けて届けた数です。2 以上のときだけ、倍率の札を出します。</param>
        /// <param name="gainedPoints">いま入った点です。0 より大きいと、跳ねて光り、「+点」が浮かびます。</param>
        public void ShowScore(int score, int streak, int gainedPoints)
        {
            if (!TryShowHud())
            {
                return;
            }

            _isFinalShown = false;
            _targetScore = score;
            _streak = streak;
            if (score == 0)
            {
                _shownScore = 0f;
            }

            RefreshPlayingTexts();
            if (gainedPoints > 0)
            {
                CelebrateGainedPoints(gainedPoints);
            }
        }

        /// <summary>本編が始まった合図として、真ん中に「GO!」を出します。</summary>
        public void ShowStartBanner()
        {
            if (!TryShowHud())
            {
                return;
            }

            StartBanner("GO!", GoldColor);
        }

        /// <summary>本編が終わったあとの、最終の点数と順位を出します。</summary>
        /// <param name="score">最終の点数です。</param>
        /// <param name="answered">欲しがられていた物を届けた数です。</param>
        public void ShowFinalScore(int score, int answered)
        {
            if (!TryShowHud())
            {
                return;
            }

            _isFinalShown = true;
            _targetScore = score;
            _answered = answered;
            _streak = 0;
            _punch = 1f;

            _labelText.text = "FINAL SCORE";
            _timeBar.SetActive(false);
            _timeText.text = "RANK " + RankOf(score);
            _timeText.color = GoldColor;
            _streakText.text = answered + (answered == 1 ? " WISH ANSWERED" : " WISHES ANSWERED");
            _streakBadge.SetActive(true);
            _badgePunch = 1f;

            StartBanner("TIME UP!", NormalColor);
            PlayClip(_fanfareClip);
            EmitBurst();
        }

        /// <summary>表示を消し、数を 0 に戻します。やり直すときに呼びます。</summary>
        public void HideAndClear()
        {
            _secondsLeft = -1;
            _targetScore = 0;
            _shownScore = 0f;
            _shownScoreInteger = -1;
            _streak = 0;
            _answered = 0;
            _isFinalShown = false;
            _punch = 0f;
            _badgePunch = 0f;
            _popupAge = float.PositiveInfinity;
            _bannerAge = float.PositiveInfinity;
            _hasYaw = false;
            if (_hud != null)
            {
                _hud.SetActive(false);
            }
        }

        private void Awake()
        {
            _coinClips = new[]
            {
                CreateArpeggio("ScoreCoin1", new[] { 1318.5f, 1975.5f }, 0.07f, 0.35f),
                CreateArpeggio("ScoreCoin2", new[] { 1480f, 2217.5f }, 0.07f, 0.35f),
                CreateArpeggio("ScoreCoin3", new[] { 1661f, 2489f, 3322f }, 0.06f, 0.4f),
            };
            _fanfareClip = CreateArpeggio("ScoreFanfare", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f, 0.8f);

            if (_popupText != null)
            {
                _popupBasePosition = _popupText.rectTransform.anchoredPosition;
            }

            if (_scoreText != null)
            {
                _scoreBaseScale = _scoreText.transform.localScale;
            }

            if (_streakBadge != null)
            {
                _badgeBaseScale = _streakBadge.transform.localScale;
            }

            if (_bannerText != null)
            {
                _bannerBaseScale = _bannerText.transform.localScale;
            }
        }

        private void OnDestroy()
        {
            if (_coinClips != null)
            {
                foreach (AudioClip clip in _coinClips)
                {
                    Destroy(clip);
                }
            }

            Destroy(_fanfareClip);
        }

        private void Update()
        {
            if (_hud == null || !_hud.activeSelf)
            {
                return;
            }

            float deltaSeconds = Time.unscaledDeltaTime;
            AnimateScoreCount(deltaSeconds);
            AnimateScorePunch(deltaSeconds);
            AnimateBadge(deltaSeconds);
            AnimatePopup(deltaSeconds);
            AnimateBanner(deltaSeconds);
            AnimateTimeWarning();
        }

        private void LateUpdate()
        {
            if (_hud == null || !_hud.activeSelf)
            {
                return;
            }

            Transform head = FindHead();
            if (head == null)
            {
                if (!_hasWarnedNoHead)
                {
                    _hasWarnedNoHead = true;
                    Debug.LogWarning("[ScoreBoard] 頭（MainCamera タグのカメラか PlayerRig の Head）が見つからないので、位置を決められません。", this);
                }

                return;
            }

            FollowHeadSlowly(head);
        }

        private bool TryShowHud()
        {
            if (_hud == null || _timeText == null || _labelText == null || _scoreText == null || _streakBadge == null
                || _streakText == null || _popupText == null || _bannerText == null || _timeBar == null
                || _timeBarFill == null || _timeBarFillImage == null)
            {
                if (!_hasWarnedNoHud)
                {
                    _hasWarnedNoHud = true;
                    Debug.LogWarning("[ScoreBoard] シーンの UI がつながっていません。Inspector で、Hud と各文字を入れてください。", this);
                }

                return false;
            }

            if (!_hud.activeSelf)
            {
                _hud.SetActive(true);
            }

            return true;
        }

        private void RefreshPlayingTexts()
        {
            _labelText.text = "SCORE";
            _timeBar.SetActive(true);
            if (_secondsLeft >= 0)
            {
                _timeText.text = FormatClock(_secondsLeft);
                float remainingRatio = Mathf.Clamp01(_secondsLeft / (float)_totalSeconds);
                _timeBarFill.anchorMax = new Vector2(remainingRatio, _timeBarFill.anchorMax.y);
            }

            bool hasStreak = _streak >= 2;
            _streakBadge.SetActive(hasStreak);
            if (hasStreak)
            {
                _streakText.text = "COMBO x" + _streak;
            }
        }

        private void CelebrateGainedPoints(int gainedPoints)
        {
            _punch = 1f;
            _badgePunch = 1f;
            _popupText.text = "+" + gainedPoints;
            _popupAge = 0f;
            PlayClip(_coinClips[Mathf.Clamp(_streak - 1, 0, _coinClips.Length - 1)]);
            EmitBurst();
        }

        // 点数は、すぐには変えず、数えるように増やす。増えていく間が、手応えになる。
        private void AnimateScoreCount(float deltaSeconds)
        {
            float difference = _targetScore - _shownScore;
            _shownScore = Mathf.Abs(difference) < 0.5f
                ? _targetScore
                : Mathf.Lerp(_shownScore, _targetScore, 1f - Mathf.Exp(-10f * deltaSeconds));

            int integer = Mathf.RoundToInt(_shownScore);
            if (integer != _shownScoreInteger)
            {
                _shownScoreInteger = integer;
                _scoreText.text = integer.ToString("N0");
            }
        }

        private void AnimateScorePunch(float deltaSeconds)
        {
            _punch = Mathf.Max(0f, _punch - deltaSeconds / PunchSeconds);
            float eased = _punch * _punch;
            _scoreText.transform.localScale = _scoreBaseScale * (1f + PunchExtraScale * eased);
            _scoreText.color = Color.Lerp(NormalColor, GoldColor, eased);
        }

        private void AnimateBadge(float deltaSeconds)
        {
            _badgePunch = Mathf.Max(0f, _badgePunch - deltaSeconds / PunchSeconds);
            _streakBadge.transform.localScale = _badgeBaseScale * (1f + 0.25f * _badgePunch * _badgePunch);
        }

        private void AnimatePopup(float deltaSeconds)
        {
            _popupAge += deltaSeconds;
            float progress = Mathf.Clamp01(_popupAge / PopupSeconds);
            bool isVisible = progress < 1f;
            Color color = GoldColor;
            color.a = isVisible ? 1f - progress * progress : 0f;
            _popupText.color = color;

            float rise = 1f - (1f - progress) * (1f - progress);
            _popupText.rectTransform.anchoredPosition = _popupBasePosition + Vector2.up * (PopupRisePixels * rise);
        }

        private void StartBanner(string message, Color color)
        {
            _bannerText.text = message;
            _bannerText.color = color;
            _bannerAge = 0f;
        }

        private void AnimateBanner(float deltaSeconds)
        {
            _bannerAge += deltaSeconds;
            float popProgress = Mathf.Clamp01(_bannerAge / BannerPopSeconds);
            float scale = 1f + 0.8f * (1f - popProgress) * (1f - popProgress);
            _bannerText.transform.localScale = _bannerBaseScale * scale;

            float fadeProgress = Mathf.Clamp01((_bannerAge - BannerHoldSeconds) / BannerFadeSeconds);
            Color color = _bannerText.color;
            color.a = float.IsInfinity(_bannerAge) ? 0f : 1f - fadeProgress;
            _bannerText.color = color;
        }

        private void AnimateTimeWarning()
        {
            if (_isFinalShown || _secondsLeft < 0)
            {
                return;
            }

            bool isWarning = _warningSeconds > 0f && _secondsLeft <= _warningSeconds;
            float remainingRatio = Mathf.Clamp01(_secondsLeft / (float)_totalSeconds);

            _timeText.color = isWarning ? WarningColor : NormalColor;
            float pulse = isWarning
                ? 1f + PulseExtraScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRadiansPerSecond))
                : 1f;
            _timeText.transform.localScale = Vector3.one * pulse;

            _timeBarFillImage.color = isWarning ? WarningColor : Color.Lerp(BarCautionColor, BarGoodColor, Mathf.Clamp01((remainingRatio - 0.25f) * 4f));
        }

        private void EmitBurst()
        {
            if (_burstPrefab == null)
            {
                return;
            }

            Vector3 position = _scoreText.transform.position - _hud.transform.forward * BurstTowardViewerMeters;
            GameObject burst = Instantiate(_burstPrefab, position, Quaternion.identity);
            burst.transform.localScale *= _burstScale;
            Destroy(burst, BurstLifetimeSeconds);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _volume > 0f)
            {
                AudioSource.PlayClipAtPoint(clip, _hud.transform.position, _volume * VolumeScale);
            }
        }

        private static string RankOf(int score)
        {
            if (score >= RankSScore)
            {
                return "S";
            }

            if (score >= RankAScore)
            {
                return "A";
            }

            return score >= RankBScore ? "B" : "C";
        }

        private static string FormatClock(int totalSeconds)
        {
            return (totalSeconds / 60) + ":" + (totalSeconds % 60).ToString("00");
        }

        // 実際に映しているカメラを先に探す。リグの Head とカメラがずれても、見える所に出すため。
        private static Transform FindHead()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            if (PlayerRig.Current != null && PlayerRig.Current.Head != null)
            {
                return PlayerRig.Current.Head;
            }

            return null;
        }

        // 顔が大きく向きを変えたときだけ追いかける。揺れる表示は、VR では酔いのもとになるため。
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

            Transform hudTransform = _hud.transform;
            hudTransform.position = position;
            // Canvas の面は +Z の向きに背を向けた側から読める。そのため、視線の向きに向ける。
            hudTransform.rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
        }

        // 音を順に鳴らす短い音を、コードで作る。音声ファイルを増やさないため。
        private static AudioClip CreateArpeggio(string clipName, float[] hertzPerNote, float noteSeconds, float tailSeconds)
        {
            float totalSeconds = noteSeconds * (hertzPerNote.Length - 1) + tailSeconds;
            int sampleCount = Mathf.CeilToInt(SampleRate * totalSeconds);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float seconds = i / (float)SampleRate;
                float sum = 0f;
                for (int note = 0; note < hertzPerNote.Length; note++)
                {
                    float sinceStart = seconds - note * noteSeconds;
                    if (sinceStart < 0f)
                    {
                        continue;
                    }

                    float phase = 2f * Mathf.PI * hertzPerNote[note] * sinceStart;
                    float attack = Mathf.Clamp01(sinceStart / 0.004f);
                    sum += attack * Mathf.Exp(-sinceStart * 7f) * (Mathf.Sin(phase) + 0.35f * Mathf.Sin(2f * phase));
                }

                samples[i] = Mathf.Clamp(0.3f * sum, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
