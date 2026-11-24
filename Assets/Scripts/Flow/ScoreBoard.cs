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
    /// Canvas のオブジェクト（<c>Hud</c>）は、はじめは無効にしておきます。
    /// 本編が始まると、この部品が有効にします。
    /// </para>
    /// <para>
    /// 体験者は、ミニチュアを見下ろして遊びます。そのため、表示は体験者の頭ではなく、ミニチュアの向こう側の縁に立てます。
    /// ミニチュアを見ている視野の中に入り、頭を動かしても、世界の中で動きません。酔いにくく、没入も壊れません。
    /// 置く向きは、体験者が顔を向けている向きです。顔を大きく向け直したときだけ、ゆっくり向こう側へ回り込みます。
    /// 表示は、いつも体験者のほうを向きます。傾けません。
    /// </para>
    /// <para>
    /// 表示の大きさは、距離に合わせて変えて、見かけの大きさ（視野に占める角度）を一定にします。
    /// 遠くても近くても、同じ大きさに見えます。文字は、太く大きくしています。
    /// </para>
    /// <para>
    /// 点が入ると、数字が跳ねて金色に光り、「+300」が浮かび上がり、粒とコインの音が出ます。
    /// 点数は、すぐには変わらず、数えるように増えていきます。
    /// 続けて届けると、音が高くなり、倍率の札が出て、続けるほど札が大きくなります。
    /// 続けて届けるのが途切れると、札は赤くなって縮んで消えます。
    /// 残りが <c>Warning Seconds</c> を切ると、時間と帯が赤くなり、小さく脈打ちます。
    /// 終わると「TIME UP!」を出し、最終の点数と順位（S・A・B・C）を出します。
    /// </para>
    /// <para>
    /// 大きさ・位置は、仮の値です。Quest をかぶって、読みやすさを見て決めます。
    /// </para>
    /// </remarks>
    public sealed class ScoreBoard : MonoBehaviour
    {
        private const float PunchSeconds = 0.4f;
        private const float PunchExtraScale = 0.45f;
        private const float PopupSeconds = 1.1f;
        private const float PopupRisePixels = 110f;
        private const float BannerHoldSeconds = 0.9f;
        private const float BannerFadeSeconds = 0.5f;
        private const float BannerPopSeconds = 0.25f;
        private const float BadgeBreakSeconds = 0.35f;
        private const float PulseExtraScale = 0.06f;
        private const float PulseRadiansPerSecond = 8f;
        private const float BurstLifetimeSeconds = 3f;
        private const float BurstTowardViewerMeters = 0.06f;
        private const float MinimumViewDistanceMeters = 0.3f;
        private const float MinimumFlatForwardLength = 0.2f;
        private const int MaxBadgeGrowSteps = 3;
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
        private static readonly Color BadgeColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color BadgeBreakColor = new Color(0.8f, 0.15f, 0.15f);

        [Header("シーンの UI")]
        [SerializeField]
        [Tooltip("Canvas のオブジェクトです。はじめは無効にしておきます。本編が始まると、この部品が有効にして、ミニチュアの向こう側へ動かします。")]
        private GameObject _hud;

        [SerializeField]
        [Tooltip("残り時間の文字です。終わったあとは、順位を出します。")]
        private TMP_Text _timeText;

        [SerializeField]
        [Tooltip("点数の上の文字です。「SCORE」と出ます。")]
        private TMP_Text _labelText;

        [SerializeField]
        [Tooltip("点数の大きな文字です。")]
        private TMP_Text _scoreText;

        [SerializeField]
        [Tooltip("続けて届けたときに出す札です。Image が付いている必要があります。2 回続けて届けると出ます。")]
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

        [Header("置き場所")]
        [SerializeField]
        [Tooltip("ミニチュアの土台です。表示は、この中心から体験者が向いている向きへ、少し離れた所に立てます。空なら、ワールドの原点を中心にします。")]
        private Transform _miniature;

        [SerializeField]
        [Min(0f)]
        [Tooltip("ミニチュアの中心から、表示までの水平の距離です（メートル）。土台の半分の大きさより少し大きくして、縁の向こう側に置きます。")]
        private float _forwardOffsetMeters = 0.75f;

        [SerializeField]
        [Tooltip("ミニチュアの中心から、表示の中心までの高さです（メートル）。")]
        private float _heightAboveMiniatureMeters = 0.35f;

        [SerializeField]
        [Range(10f, 60f)]
        [Tooltip("表示の横幅が、体験者の視野に占める角度です（度）。距離に合わせて大きさを変えて、この角度に保ちます。大きいほど文字が大きく見えます。")]
        private float _angularWidthDegrees = 28f;

        [SerializeField]
        [Range(5f, 90f)]
        [Tooltip("顔の向きが表示の向きからこの角度より離れたときだけ、表示が回り込みます（度）。")]
        private float _followDeadZoneDegrees = 30f;

        [SerializeField]
        [Min(0.05f)]
        [Tooltip("回り込むときの、なめらかさです（秒）。大きいほどゆっくり動きます。")]
        private float _followSmoothSeconds = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("残りがこの秒数を切ると、時間を赤くして脈打たせます。0 なら、しません。")]
        private float _warningSeconds = 10f;

        private AudioClip[] _coinClips;
        private AudioClip _fanfareClip;
        private Image _badgeImage;
        private Vector2 _popupBasePosition;
        private Vector3 _scoreBaseScale = Vector3.one;
        private Vector3 _badgeBaseScale = Vector3.one;
        private Vector3 _bannerBaseScale = Vector3.one;

        private float _yawDegrees;
        private float _targetYawDegrees;
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
        private float _badgeBreakRemaining;
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

            bool streakJustEnded = _streak >= 2 && streak < 2;
            _isFinalShown = false;
            _targetScore = score;
            _streak = streak;
            if (score == 0)
            {
                _shownScore = 0f;
            }

            if (streakJustEnded)
            {
                _badgeBreakRemaining = BadgeBreakSeconds;
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
            _badgeBreakRemaining = 0f;
            _punch = 1f;

            _labelText.text = "FINAL SCORE";
            _timeBar.SetActive(false);
            _timeText.text = "RANK " + RankOf(score);
            _timeText.color = GoldColor;
            _timeText.transform.localScale = Vector3.one;
            _streakText.text = answered + (answered == 1 ? " WISH" : " WISHES");
            _badgeImage.color = BadgeColor;
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
            _badgeBreakRemaining = 0f;
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
                _badgeImage = _streakBadge.GetComponent<Image>();
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

            PlaceBeyondMiniature(head);
        }

        private bool TryShowHud()
        {
            if (_hud == null || _timeText == null || _labelText == null || _scoreText == null || _streakBadge == null
                || _streakText == null || _popupText == null || _bannerText == null || _timeBar == null
                || _timeBarFill == null || _timeBarFillImage == null || _badgeImage == null)
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
            if (hasStreak)
            {
                _badgeBreakRemaining = 0f;
                _badgeImage.color = BadgeColor;
                _streakText.text = "COMBO x" + _streak;
            }

            _streakBadge.SetActive(hasStreak || _badgeBreakRemaining > 0f);
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

        // 続けるほど札を大きくする。途切れたときは、赤くして縮めて消す。
        private void AnimateBadge(float deltaSeconds)
        {
            _badgePunch = Mathf.Max(0f, _badgePunch - deltaSeconds / PunchSeconds);

            if (_badgeBreakRemaining > 0f)
            {
                _badgeBreakRemaining -= deltaSeconds;
                float shrink = Mathf.Clamp01(_badgeBreakRemaining / BadgeBreakSeconds);
                _badgeImage.color = BadgeBreakColor;
                _streakBadge.transform.localScale = _badgeBaseScale * shrink;
                if (_badgeBreakRemaining <= 0f && _streak < 2)
                {
                    _streakBadge.SetActive(false);
                }

                return;
            }

            float growSteps = Mathf.Clamp(_streak - 2, 0, MaxBadgeGrowSteps);
            float streakScale = 1f + 0.1f * growSteps;
            _streakBadge.transform.localScale = _badgeBaseScale * streakScale * (1f + 0.25f * _badgePunch * _badgePunch);
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

            float goodness = Mathf.Clamp01((remainingRatio - 0.25f) * 4f);
            _timeBarFillImage.color = isWarning ? WarningColor : Color.Lerp(BarCautionColor, BarGoodColor, goodness);
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

        // 顔の水平の向きを、角度で返す。真下を向いて前向きが短いときは、頭の上向きの水平の向きを使う。
        private static float FlatYawDegreesOf(Transform head)
        {
            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.magnitude < MinimumFlatForwardLength)
            {
                flat = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        // ミニチュアの向こう側の縁に立てる。体験者がミニチュアを見ている視野に入り、顔を動かしても世界の中で動かない。
        private void PlaceBeyondMiniature(Transform head)
        {
            float headYaw = FlatYawDegreesOf(head);
            if (!_hasYaw)
            {
                _hasYaw = true;
                _yawDegrees = headYaw;
                _targetYawDegrees = headYaw;
                _yawVelocity = 0f;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(_yawDegrees, headYaw)) > _followDeadZoneDegrees)
            {
                _targetYawDegrees = headYaw;
            }

            _yawDegrees = Mathf.SmoothDampAngle(_yawDegrees, _targetYawDegrees, ref _yawVelocity, _followSmoothSeconds);

            Vector3 center = _miniature != null ? _miniature.position : Vector3.zero;
            Vector3 beyond = Quaternion.Euler(0f, _yawDegrees, 0f) * Vector3.forward;
            Vector3 position = center + beyond * _forwardOffsetMeters + Vector3.up * _heightAboveMiniatureMeters;

            Transform hudTransform = _hud.transform;
            hudTransform.position = position;

            // 体験者は見下ろすので、上下も含めて顔のほうへ向ける。ただし、傾け（ロール）はしない。
            Vector3 towardViewer = head.position - position;
            float distance = Mathf.Max(MinimumViewDistanceMeters, towardViewer.magnitude);
            hudTransform.rotation = Quaternion.LookRotation(-towardViewer, Vector3.up);

            // 距離が変わっても、視野に占める角度を一定にする。
            float canvasWidth = ((RectTransform)hudTransform).rect.width;
            float worldWidth = 2f * distance * Mathf.Tan(_angularWidthDegrees * 0.5f * Mathf.Deg2Rad);
            hudTransform.localScale = Vector3.one * (worldWidth / canvasWidth);
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
