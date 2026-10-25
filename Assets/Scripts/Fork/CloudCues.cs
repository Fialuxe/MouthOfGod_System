using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// 雲の合図（Cues）を受けて、雲に見せる。<see cref="CloudRing"/> と同じオブジェクトに付ける（その子の雲を使う）。
    /// </summary>
    /// <remarks>
    /// - <c>cloud.glow</c>: 体験者の正面にいちばん近い雲を 1 つ選び、色と大きさを脈打たせて光らせる。
    /// - <c>cloud.approach</c>: 光らせた雲を、体験者の頭のほうへ寄せる。光らせた雲がなければ、先に 1 つ選ぶ。
    /// - <c>cloud.calm</c>: 光を消し、寄せた雲を元の位置へ戻す。
    /// - 光らせた雲が刺されて隠れたら、光と寄せをやめる（戻ってきたときは、ふつうの雲）。
    /// - 雲の位置は CloudRing が毎フレーム決める。寄せる分は、そのあと（LateUpdate）で足す。
    /// </remarks>
    public sealed class CloudCues : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("雲を探す親。空なら、このオブジェクト（CloudRing）の子から探す")]
        [SerializeField] Transform cloudRoot;

        [Header("合図の名前")]
        [Tooltip("雲を 1 つ光らせる合図")]
        [SerializeField] string glowCue = "cloud.glow";

        [Tooltip("光る雲を体験者の近くへ寄せる合図")]
        [SerializeField] string approachCue = "cloud.approach";

        [Tooltip("光を消し、雲を戻す合図")]
        [SerializeField] string calmCue = "cloud.calm";

        [Header("光らせ方")]
        [Tooltip("光るときの色。元の色とこの色のあいだを行き来する")]
        [SerializeField] Color glowColor = new Color(1f, 0.85f, 0.4f);

        [Tooltip("大きさの脈の幅。0.15 なら、元の 1〜1.15 倍のあいだで脈打つ")]
        [SerializeField, Min(0f)] float pulseScale = 0.15f;

        [Tooltip("1 秒に脈打つ回数")]
        [SerializeField, Min(0f)] float pulsePerSecond = 1.2f;

        [Header("寄せ方")]
        [Tooltip("頭のほうへ寄せる距離（m）")]
        [SerializeField, Min(0f)] float approachMeters = 0.2f;

        [Tooltip("寄せても、頭からこの距離（m）より近くにはしない")]
        [SerializeField, Min(0f)] float minHeadDistance = 0.25f;

        [Tooltip("寄せきるまでの秒数")]
        [SerializeField, Min(0.01f)] float approachSeconds = 1.5f;

        [Tooltip("元の位置へ戻りきるまでの秒数")]
        [SerializeField, Min(0.01f)] float returnSeconds = 1f;

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<Color> _baseColors = new List<Color>();
        MaterialPropertyBlock _block;

        Cloud _target;          // 光らせている（または戻している途中の）雲
        bool _glowing;
        bool _glowPending;      // 雲がまだ無いときに glow が来た。雲ができたら光らせる
        Vector3 _baseScale;
        float _approach;        // 0 = 元の位置、1 = 寄せきった
        float _approachGoal;
        Vector3 _applied;       // いま足している寄せの分
        Vector3 _lastPosition;  // 寄せを足したあとの位置。CloudRing が動かしていないかを見る

        void OnEnable() => Cues.Requested += OnCue;

        void OnDisable()
        {
            Cues.Requested -= OnCue;
            Release();
            _glowPending = false;
        }

        void OnCue(CueRequested request)
        {
            string name = request.Cue.Value;
            if (Is(name, glowCue)) Glow();
            else if (Is(name, approachCue)) Approach();
            else if (Is(name, calmCue)) Calm();
        }

        static bool Is(string name, string cue) => !string.IsNullOrEmpty(cue) && name == cue;

        void Glow()
        {
            Cloud cloud = PickFrontmost();
            if (cloud == null)
            {
                _glowPending = true; // CloudRing の Start より先に来たとき
                return;
            }
            _glowPending = false;
            if (cloud == _target && _glowing) return;
            Release();
            Hold(cloud);
            _glowing = true;
        }

        void Approach()
        {
            if (_target == null || !_glowing) Glow();
            if (_target == null) return;
            _approachGoal = 1f;
        }

        void Calm()
        {
            _glowPending = false;
            if (_target == null) return;
            StopGlow();
            _approachGoal = 0f; // 戻りきったら、LateUpdate で手放す
        }

        void LateUpdate()
        {
            if (_glowPending) Glow();
            if (_target == null) return;

            // 刺されて隠れたら、光も寄せもやめる。
            if (!_target.IsAvailable)
            {
                Release();
                return;
            }

            Transform t = _target.transform;

            // CloudRing が位置を決め直していなければ、前に足した分を引いてから足し直す。
            Vector3 basePosition = t.position == _lastPosition ? t.position - _applied : t.position;

            float seconds = _approachGoal > _approach ? approachSeconds : returnSeconds;
            _approach = Mathf.MoveTowards(_approach, _approachGoal, Time.deltaTime / seconds);

            Transform head = Camera.main != null ? Camera.main.transform : null;
            _applied = head != null
                ? ApproachOffset(basePosition, head.position, approachMeters, minHeadDistance, _approach)
                : Vector3.zero;
            t.position = basePosition + _applied;
            _lastPosition = t.position;

            if (_glowing)
            {
                float pulse = Pulse(Time.time, pulsePerSecond);
                t.localScale = _baseScale * (1f + pulseScale * pulse);
                SetColors(pulse);
            }
            else if (_approach <= 0f && _approachGoal <= 0f)
            {
                Release();
            }
        }

        // 光らせる雲を持つ。元の大きさと色を覚える。
        void Hold(Cloud cloud)
        {
            _target = cloud;
            _baseScale = cloud.transform.localScale;
            _approach = 0f;
            _approachGoal = 0f;
            _applied = Vector3.zero;
            _lastPosition = cloud.transform.position;

            _renderers.Clear();
            _baseColors.Clear();
            foreach (Renderer r in cloud.GetComponentsInChildren<Renderer>())
            {
                _renderers.Add(r);
                _baseColors.Add(BaseColor(r.sharedMaterial));
            }
        }

        // 光をやめ、大きさと色を戻す。位置はそのまま（寄せは LateUpdate で戻す）。
        void StopGlow()
        {
            if (!_glowing || _target == null) return;
            _glowing = false;
            _target.transform.localScale = _baseScale;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            foreach (Renderer r in _renderers)
                if (r != null) r.SetPropertyBlock(_block);
        }

        // 雲を手放す。光をやめ、寄せた分を引く。
        void Release()
        {
            if (_target == null) return;
            StopGlow();
            if (_target.transform.position == _lastPosition) _target.transform.position -= _applied;
            _target = null;
            _applied = Vector3.zero;
            _approach = 0f;
            _approachGoal = 0f;
            _renderers.Clear();
            _baseColors.Clear();
        }

        void SetColors(float pulse)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                Color color = Color.Lerp(_baseColors[i], glowColor, pulse);
                r.GetPropertyBlock(_block);
                if (r.sharedMaterial.HasProperty(BaseColorId)) _block.SetColor(BaseColorId, color);
                if (r.sharedMaterial.HasProperty(ColorId)) _block.SetColor(ColorId, color);
                r.SetPropertyBlock(_block);
            }
        }

        static Color BaseColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        // 刺せる雲のうち、体験者の正面にいちばん近いもの。無ければ null。
        Cloud PickFrontmost()
        {
            Transform root = cloudRoot != null ? cloudRoot : transform;
            var clouds = new List<Cloud>();
            foreach (Cloud cloud in root.GetComponentsInChildren<Cloud>())
                if (cloud.IsAvailable) clouds.Add(cloud);
            if (clouds.Count == 0) return null;

            Transform head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return clouds[clouds.Count / 2]; // 頭が無ければ、並びのまん中（CloudRing では正面）

            var positions = new Vector3[clouds.Count];
            for (int i = 0; i < clouds.Count; i++) positions[i] = clouds[i].transform.position;
            int index = Frontmost(head.position, head.forward, positions);
            return index >= 0 ? clouds[index] : null;
        }

        /// <summary>
        /// 頭から見て、正面（水平の向き）にいちばん近い位置の番号。positions が空なら -1。
        /// 正面の向きが真上・真下で決まらないときは、最初の位置を選ぶ。
        /// </summary>
        internal static int Frontmost(Vector3 head, Vector3 forward, IList<Vector3> positions)
        {
            if (positions == null || positions.Count == 0) return -1;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return 0;
            forward.Normalize();

            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 to = positions[i] - head;
                to.y = 0f;
                float dot = to.sqrMagnitude < 0.0001f ? -1f : Vector3.Dot(forward, to.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 雲を頭のほうへ寄せる分。amount（0〜1）は寄せの進み具合で、なめらかに加速・減速する。
        /// 寄せる距離は meters まで。頭から minDistance より近くにはしない。
        /// </summary>
        internal static Vector3 ApproachOffset(Vector3 cloud, Vector3 head, float meters, float minDistance, float amount)
        {
            Vector3 to = head - cloud;
            float distance = to.magnitude;
            if (distance < 0.0001f) return Vector3.zero;
            float reach = Mathf.Clamp(Mathf.Min(meters, distance - minDistance), 0f, distance);
            return to / distance * reach * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
        }

        /// <summary>脈の強さ（0〜1）。perSecond 回／秒で、0 から 1 へなめらかに行き来する。</summary>
        internal static float Pulse(float time, float perSecond)
            => 0.5f - 0.5f * Mathf.Cos(time * perSecond * 2f * Mathf.PI);
    }
}
