using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// 雲を 1 つ選んで光らせ、体験者の頭のほうへ寄せます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CloudRing"/> と同じオブジェクトに付けます。その子の雲を使います。
    /// </para>
    /// <para>
    /// 光らせた雲が刺されて隠れたら、光と寄せをやめます。
    /// 雲が戻ってきたときは、ふつうの雲です。
    /// </para>
    /// <para>
    /// 雲の位置は、<see cref="CloudRing"/> が毎フレーム決めます。
    /// 寄せる分は、そのあとの LateUpdate で足します。
    /// </para>
    /// </remarks>
    public sealed class CloudGlow : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("雲を探す親。空なら、このオブジェクト（CloudRing）の子から探す")]
        [SerializeField] Transform cloudRoot;

        [Header("光らせ方")]
        [Tooltip("光るときの色。元の色とこの色のあいだを行き来する")]
        [SerializeField] Color glowColor = new Color(1f, 0.85f, 0.4f);

        [Tooltip("大きさの脈の幅。0.15 なら、元の 1〜1.15 倍のあいだで脈打つ")]
        [SerializeField, Min(0f)] float pulseScale = 0.15f;

        [Tooltip("1 秒に脈打つ回数。大きいほど、せわしなく光る")]
        [SerializeField, Min(0f)] float pulsePerSecond = 1.2f;

        [Header("寄せ方")]
        [Tooltip("頭のほうへ寄せる距離（m）。頭に近すぎるときは、Min Head Distance で止まる")]
        [SerializeField, Min(0f)] float approachMeters = 0.2f;

        [Tooltip("寄せても、頭からこの距離（m）より近くにはしない")]
        [SerializeField, Min(0f)] float minHeadDistance = 0.25f;

        [Tooltip("寄せきるまでの秒数。途中はなめらかに加速・減速する")]
        [SerializeField, Min(0.01f)] float approachSeconds = 1.5f;

        [Tooltip("元の位置へ戻りきるまでの秒数")]
        [SerializeField, Min(0.01f)] float returnSeconds = 1f;

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<Color> _baseColors = new List<Color>();
        MaterialPropertyBlock _block;

        // 光らせている雲か、元の位置へ戻している途中の雲です。
        Cloud _target;
        bool _glowing;
        // 雲がまだ無いときに Glow が呼ばれたら立てます。雲ができたら光らせます。
        bool _glowPending;
        Vector3 _baseScale;
        // 0 は元の位置、1 は寄せきった位置を表します。
        float _approach;
        float _approachGoal;
        // いま足している寄せの分です。
        Vector3 _applied;
        // 寄せを足したあとの位置です。CloudRing が動かしたかどうかを、これと比べて見ます。
        Vector3 _lastPosition;

        void OnDisable()
        {
            Release();
            _glowPending = false;
        }

        /// <summary>
        /// 体験者の正面にいちばん近い雲を 1 つ選び、光らせます。
        /// </summary>
        /// <remarks>
        /// 色と大きさを脈打たせて光らせます。
        /// 刺せる雲がまだ無いときは、雲ができてから光らせます。
        /// </remarks>
        public void Glow()
        {
            Cloud cloud = PickFrontmost();
            if (cloud == null)
            {
                _glowPending = true;  // CloudRing の Start より先に呼ばれたとき
                return;
            }
            _glowPending = false;
            if (cloud == _target && _glowing) return;
            Release();
            Hold(cloud);
            _glowing = true;
        }

        /// <summary>
        /// 光らせた雲を、体験者の頭のほうへ寄せます。光らせた雲がなければ、先に 1 つ選びます。
        /// </summary>
        public void Approach()
        {
            if (_target == null || !_glowing) Glow();
            if (_target == null) return;
            _approachGoal = 1f;
        }

        /// <summary>
        /// 光を消し、寄せた雲を元の位置へ戻します。
        /// </summary>
        /// <remarks>
        /// 戻りきったら、LateUpdate で雲を手放します。
        /// </remarks>
        public void Calm()
        {
            _glowPending = false;
            if (_target == null) return;
            StopGlow();
            _approachGoal = 0f;
        }

        void LateUpdate()
        {
            if (_glowPending) Glow();
            if (_target == null) return;

            if (!_target.IsAvailable)
            {
                Release();
                return;
            }

            Transform t = _target.transform;

            // CloudRing が位置を決め直していなければ、前に足した分を引いてから足し直します。
            // 引かずに足すと、寄せの分がフレームごとに積み重なるためです。
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

        // 光らせる雲を持ち、元の大きさと色を覚えます。
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

        // 光をやめ、大きさと色を戻します。位置は動かしません。
        // 寄せた分は、LateUpdate で少しずつ戻します。
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

        // 雲を手放します。光をやめ、寄せた分をすぐ引きます。
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

        // 刺せる雲のうち、体験者の正面にいちばん近いものを返します。無ければ null です。
        Cloud PickFrontmost()
        {
            Transform root = cloudRoot != null ? cloudRoot : transform;
            var clouds = new List<Cloud>();
            foreach (Cloud cloud in root.GetComponentsInChildren<Cloud>())
                if (cloud.IsAvailable) clouds.Add(cloud);
            if (clouds.Count == 0) return null;

            Transform head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return clouds[clouds.Count / 2];  // 頭が無ければ並びのまん中。CloudRing では正面です

            var positions = new Vector3[clouds.Count];
            for (int i = 0; i < clouds.Count; i++) positions[i] = clouds[i].transform.position;
            int index = Frontmost(head.position, head.forward, positions);
            return index >= 0 ? clouds[index] : null;
        }

        /// <summary>
        /// 頭から見て、水平の正面にいちばん近い位置の番号を返します。
        /// </summary>
        /// <param name="head">頭の位置です。</param>
        /// <param name="forward">頭の向きです。上下の成分は無視します。</param>
        /// <param name="positions">候補の位置です。<see langword="null"/> でもかまいません。</param>
        /// <returns>
        /// <paramref name="positions"/> の番号です。空か <see langword="null"/> なら -1 です。
        /// 向きが真上・真下で正面が決まらないときは 0 です。
        /// </returns>
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
        /// 雲を頭のほうへ寄せる分を返します。
        /// </summary>
        /// <param name="cloud">寄せる前の雲の位置です。</param>
        /// <param name="head">頭の位置です。</param>
        /// <param name="meters">寄せる距離の上限（m）です。</param>
        /// <param name="minDistance">寄せても、頭からこの距離（m）より近くにはしません。</param>
        /// <param name="amount">
        /// 寄せの進み具合です。0〜1 に丸めます。途中はなめらかに加速・減速します。
        /// </param>
        /// <returns>雲の位置に足す分です。雲と頭が重なるときは 0 です。</returns>
        internal static Vector3 ApproachOffset(Vector3 cloud, Vector3 head, float meters, float minDistance, float amount)
        {
            Vector3 to = head - cloud;
            float distance = to.magnitude;
            if (distance < 0.0001f) return Vector3.zero;
            float reach = Mathf.Clamp(Mathf.Min(meters, distance - minDistance), 0f, distance);
            return to / distance * reach * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
        }

        /// <summary>
        /// 脈の強さを返します。0 から 1 のあいだを、なめらかに行き来します。
        /// </summary>
        /// <param name="time">時刻（秒）です。0 のとき 0 を返します。</param>
        /// <param name="perSecond">1 秒に脈打つ回数です。</param>
        /// <returns>0〜1 の値です。</returns>
        internal static float Pulse(float time, float perSecond)
            => 0.5f - 0.5f * Mathf.Cos(time * perSecond * 2f * Mathf.PI);
    }
}
