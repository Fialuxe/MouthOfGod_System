using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// VR で動画を見せる画面。体験者の正面の空間に、平らな画面を置いて流す（頭には付けない）。
    /// プロローグ（<see cref="PrologueVideo"/>）が使う。エンディングなど、ほかの場面でも使える。
    /// </summary>
    /// <remarks>
    /// VR 酔いを避けるための決まり:
    /// - 画面は空間に固定する。頭を少し回しても動かない。大きく向きを変えたときだけ、ゆっくり正面へ回り込む（Recenter）。
    /// - 出し入れは、フェードで行う。視界を急に暗くしない。
    /// - 画面の大きさは、視野の 40 度くらいに収める（初期値: 3 m 先に幅 2.2 m）。
    /// - 読み込みに失敗したとき・動画が無いときは、警告を出してすぐ終わる。体験を止めない。
    /// シーンは切り替えない。同じシーンの中で流すので、XR リグと AprilTag の位置合わせが残る。
    /// </remarks>
    public sealed class VideoScreen : MonoBehaviour
    {
        // 警告の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;

        [Header("動画")]
        [Tooltip("流す動画。Clip と File のどちらかを入れる。両方あれば Clip を使う")]
        [SerializeField] VideoClip clip;

        [Tooltip("StreamingAssets の中の動画ファイル名（例: prologue.mp4）。大きい動画はこちらに置く。Clip が空のときに使う")]
        [SerializeField] string streamingAssetsFile = "";

        [Tooltip("音量（0〜1）。動画の音は、頭の位置によらず、そのまま鳴る")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;

        [Tooltip("読み込みを待つ最長の秒数。過ぎたら、流さずに終わる")]
        [SerializeField, Min(1f)] float prepareTimeoutSeconds = 10f;

        [Tooltip("流す最長の秒数。0 なら、動画の終わりまで。動画が終わらないときの保険")]
        [SerializeField, Min(0f)] float maxSeconds;

        [Header("置き方")]
        [Tooltip("頭の位置と向き。空なら Main Camera")]
        [SerializeField] Transform head;

        [Tooltip("頭から画面までの距離（m）")]
        [SerializeField, Min(0.5f)] float distance = 3f;

        [Tooltip("画面の幅（m）。高さは動画の縦横比で決まる")]
        [SerializeField, Min(0.1f)] float width = 2.2f;

        [Tooltip("画面の中心を、目の高さからずらす量（m）。負なら下")]
        [SerializeField] float heightOffset = -0.1f;

        [Tooltip("頭の向きがこの角度より大きくずれたら、画面を正面へ回り込ませる（度）。0 なら回り込まない")]
        [SerializeField, Range(0f, 180f)] float recenterAngle = 50f;

        [Tooltip("回り込む速さ（1 秒あたりの割合）")]
        [SerializeField, Min(0.1f)] float recenterSpeed = 1.5f;

        [Header("見え方")]
        [Tooltip("画面のマテリアル。空なら Sprites/Default で作る。色（_Color / _BaseColor）の透明度でフェードする")]
        [SerializeField] Material screenMaterial;

        [Tooltip("出す・消すときのフェードの秒数")]
        [SerializeField, Min(0f)] float fadeSeconds = 1f;

        [Tooltip("動画を流している間だけ隠す物（村など）。終わったら元に戻す")]
        [SerializeField] GameObject[] hideWhilePlaying = new GameObject[0];

        [Tooltip("動画を流している間だけ出す物（暗い背景など）。終わったら隠す")]
        [SerializeField] GameObject[] showWhilePlaying = new GameObject[0];

        VideoPlayer _player;
        RenderTexture _texture;
        GameObject _quad;
        Material _material;
        bool _ended;
        bool _skip;
        bool _shown; // 画面を出したか。出していなければ、隠す物を戻さない
        Vector3 _flatForward = Vector3.forward;

        /// <summary>流している（読み込み・フェードの間も含む）。</summary>
        public bool IsBusy { get; private set; }

        /// <summary>流せる動画が入っているか。</summary>
        public bool HasVideo => clip != null || !string.IsNullOrEmpty(streamingAssetsFile);

        /// <summary>動画を流し始める。終わるまで <see cref="IsBusy"/> が true。もう流していれば何もしない。</summary>
        public void Play()
        {
            if (IsBusy) return;
            IsBusy = true;
            _skip = false;
            StartCoroutine(Run());
        }

        /// <summary>途中でやめる（フェードして消す）。流していなければ何もしない。</summary>
        [ContextMenu("Skip")]
        public void Skip() => _skip = true;

        /// <summary>流し始め、終わるまで待つ。<c>yield return</c> で使う。</summary>
        public CustomYieldInstruction PlayAndWait()
        {
            Play();
            return new WaitWhile(() => IsBusy);
        }

        IEnumerator Run()
        {
            if (!HasVideo)
            {
                ReportWarning("[VideoScreen] 動画が入っていないので、流さずに進みます（Clip か Streaming Assets File を入れる）。");
                IsBusy = false;
                yield break;
            }

            EnsurePlayer();
            _ended = false;
            _player.Prepare();
            float waited = 0f;
            while (!_player.isPrepared && !_skip)
            {
                waited += Time.unscaledDeltaTime;
                if (waited >= prepareTimeoutSeconds)
                {
                    ReportWarning("[VideoScreen] 動画を " + prepareTimeoutSeconds + " 秒で読み込めなかったので、流さずに進みます（" + Describe() + "）。");
                    Cleanup();
                    yield break;
                }
                yield return null;
            }
            if (_skip)
            {
                Cleanup();
                yield break;
            }

            FitToVideo();
            PlaceInFront(true);
            SetVisible(true);
            _player.Play();
            yield return Fade(0f, 1f);

            float played = 0f;
            while (!_ended && !_skip && (maxSeconds <= 0f || played < maxSeconds))
            {
                played += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f);
            Cleanup();
        }

        void LateUpdate()
        {
            if (_quad != null && _quad.activeSelf) PlaceInFront(false);
        }

        // 体験者の正面（水平）に置く。snap なら、いまの向きにすぐ置く。そうでなければ、大きくずれたときだけ回り込む。
        void PlaceInFront(bool snap)
        {
            Transform eye = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
            if (eye == null || _quad == null) return;

            Vector3 forward = eye.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                if (snap) _flatForward = forward;
                else if (recenterAngle > 0f && Vector3.Angle(_flatForward, forward) > recenterAngle)
                    _flatForward = Vector3.Slerp(_flatForward, forward, Mathf.Clamp01(recenterSpeed * Time.unscaledDeltaTime));
            }

            Vector3 position = ScreenPosition(eye.position, _flatForward, distance, heightOffset);
            _quad.transform.position = position;
            // Quad の表は -Z 側から見える。見ている人から画面へ向かう向きに合わせる。
            _quad.transform.rotation = Quaternion.LookRotation(_flatForward, Vector3.up);
        }

        // 画面の中心。目の位置から、水平の正面へ distance、上下に heightOffset。
        internal static Vector3 ScreenPosition(Vector3 eye, Vector3 flatForward, float distance, float heightOffset)
            => eye + flatForward * distance + Vector3.up * heightOffset;

        // 画面の高さ。動画の縦横比に合わせる。大きさが分からなければ 16:9。
        internal static float ScreenHeight(float width, uint videoWidth, uint videoHeight)
            => videoWidth > 0 && videoHeight > 0 ? width * videoHeight / videoWidth : width * 9f / 16f;

        IEnumerator Fade(float from, float to)
        {
            if (fadeSeconds <= 0f)
            {
                SetAlpha(to);
                yield break;
            }
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / fadeSeconds));
                if (_player != null) _player.SetDirectAudioVolume(0, volume * Mathf.Lerp(from, to, t / fadeSeconds));
                yield return null;
            }
            SetAlpha(to);
            if (_player != null) _player.SetDirectAudioVolume(0, volume * to);
        }

        void SetAlpha(float alpha)
        {
            if (_material == null) return;
            if (_material.HasProperty("_BaseColor"))
            {
                Color c = _material.GetColor("_BaseColor");
                c.a = alpha;
                _material.SetColor("_BaseColor", c);
            }
            if (_material.HasProperty("_Color"))
            {
                Color c = _material.GetColor("_Color");
                c.a = alpha;
                _material.SetColor("_Color", c);
            }
        }

        void SetVisible(bool visible)
        {
            _shown = visible;
            if (_quad != null) _quad.SetActive(visible);
            foreach (GameObject go in hideWhilePlaying) if (go != null) go.SetActive(!visible);
            foreach (GameObject go in showWhilePlaying) if (go != null) go.SetActive(visible);
        }

        void EnsurePlayer()
        {
            if (_player == null)
            {
                _player = gameObject.AddComponent<VideoPlayer>();
                _player.playOnAwake = false;
                _player.isLooping = false;
                _player.skipOnDrop = true; // 重いときは、映像を飛ばして音に合わせる（止まって見えるより酔いにくい）
                _player.renderMode = VideoRenderMode.RenderTexture;
                _player.audioOutputMode = VideoAudioOutputMode.Direct;
                _player.loopPointReached += OnEnded;
                _player.errorReceived += OnError;
            }

            if (clip != null)
            {
                _player.source = VideoSource.VideoClip;
                _player.clip = clip;
            }
            else
            {
                _player.source = VideoSource.Url;
                _player.url = Path.Combine(Application.streamingAssetsPath, streamingAssetsFile);
            }
            _player.SetDirectAudioVolume(0, 0f);

            if (_quad == null)
            {
                _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _quad.name = "VideoScreen";
                Collider col = _quad.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _quad.SetActive(false);
                _material = CreateMaterial();
                _quad.GetComponent<Renderer>().sharedMaterial = _material;
                SetAlpha(0f);
            }
        }

        Material CreateMaterial()
        {
            if (screenMaterial != null) return new Material(screenMaterial);
            Shader shader = Shader.Find("Sprites/Default"); // いつもビルドに入るシェーダー。色の透明度でフェードできる
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            return new Material(shader);
        }

        // 動画の大きさが分かってから、RenderTexture と画面の縦横比を合わせる。
        void FitToVideo()
        {
            uint w = _player.width > 0 ? _player.width : 1920;
            uint h = _player.height > 0 ? _player.height : 1080;
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) _texture.Release();
                _texture = new RenderTexture((int)w, (int)h, 0);
                _texture.name = "VideoScreen";
            }
            _player.targetTexture = _texture;
            _material.mainTexture = _texture;
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", _texture);
            _quad.transform.localScale = new Vector3(width, ScreenHeight(width, w, h), 1f);
        }

        void OnEnded(VideoPlayer source) => _ended = true;

        void OnError(VideoPlayer source, string message)
        {
            ReportWarning("[VideoScreen] 動画を流せませんでした（" + Describe() + "）: " + message);
            _ended = true;
            _skip = true;
        }

        string Describe() => clip != null ? clip.name : "StreamingAssets/" + streamingAssetsFile;

        void Cleanup()
        {
            if (_player != null) _player.Stop();
            if (_shown) SetVisible(false);
            IsBusy = false;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (IsBusy) Cleanup();
        }

        void OnDestroy()
        {
            if (_quad != null) Destroy(_quad);
            if (_material != null) Destroy(_material);
            if (_texture != null) _texture.Release();
        }
    }
}
