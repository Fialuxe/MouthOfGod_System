using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.UI
{
    /// <summary>影の見せ方。<see cref="FoodShadow"/> が合図で切り替える。</summary>
    public enum ShadowMode
    {
        /// <summary>出さない。</summary>
        Hidden,
        /// <summary>ふつうの濃さで出す（チュートリアル 2）。</summary>
        Normal,
        /// <summary>薄く出す（チュートリアル 3）。</summary>
        Faint,
        /// <summary>強く、脈打つように出す（外し続けたとき）。</summary>
        Emphasize,
    }

    /// <summary>
    /// 口に含んでいる間、「いまの食感なら生まれる物」の影（シルエット）を、霞の中に見せる（シナリオブック S4・S5）。合図で出し入れする。
    /// </summary>
    /// <remarks>
    /// - 受ける合図: <c>shadow.show</c>（出す）、<c>shadow.faint</c>（薄く出す）、<c>shadow.emphasize</c>（強く出す）、<c>shadow.hide</c>（消す）。名前は Inspector で変えられる。
    /// - 出している間は、<see cref="IDevice.TextureChanged"/> のたびに <see cref="IFoodSpawner.Preview"/> で Prefab を引き、その形の影を <c>Anchor</c> に出す。
    ///   <c>Anchor</c> が空なら、Camera.main の前（<c>Camera Offset</c>）に出す。
    /// - 影は、Prefab の見た目（MeshRenderer・SkinnedMeshRenderer の形）だけを写して作る。スクリプト・当たり判定・Rigidbody は持たない（生まれた物と同じ動きをしない）。
    ///   Prefab ごとに 1 つ作って、使い回す。
    /// - <c>Only While In Mouth</c> を入れると、フォーク（<see cref="Forks"/>）があるときは、かけらの付いた先を口に含んでいる間だけ出す。フォークが無ければ、この条件は見ない。
    /// - 場面が空（チュートリアルが終わった・止めた）になったら、消す（<c>Hide When Phase Ends</c>）。消し忘れで本編に影が残らないように。
    /// - 参照するのは Core だけ。シーンに 1 つ置く。
    /// </remarks>
    public sealed class FoodShadow : MonoBehaviour
    {
        [Header("出す場所")]
        [Tooltip("影を出す場所（霞の位置など）。空なら、Camera.main の前に出す")]
        [SerializeField] Transform anchor;

        [Tooltip("Anchor が空のとき、カメラから見た位置（右・上・前、メートル）")]
        [SerializeField] Vector3 cameraOffset = new Vector3(0f, -0.05f, 0.35f);

        [Tooltip("影のいちばん長い辺の長さ（メートル）。食べ物の大きさがばらばらでも、同じくらいに見せる。0 なら Prefab の大きさのまま")]
        [SerializeField, Min(0f)] float size = 0.12f;

        [Header("見た目")]
        [Tooltip("影に使うマテリアル（透明にできるもの。URP の Unlit で Surface Type を Transparent にしたものなど）。空なら Sprites/Default で作る")]
        [SerializeField] Material shadowMaterial;

        [Tooltip("影の色。透明度は下の 3 つで決める")]
        [SerializeField] Color color = new Color(0.12f, 0.1f, 0.2f, 1f);

        [Tooltip("shadow.show のときの濃さ（0 = 見えない、1 = 不透明）")]
        [SerializeField, Range(0f, 1f)] float normalAlpha = 0.55f;

        [Tooltip("shadow.faint のときの濃さ")]
        [SerializeField, Range(0f, 1f)] float faintAlpha = 0.2f;

        [Tooltip("shadow.emphasize のときの濃さ（脈打つときの最も濃いところ）")]
        [SerializeField, Range(0f, 1f)] float emphasizeAlpha = 0.9f;

        [Tooltip("shadow.emphasize のとき、1 秒に何回脈打つか")]
        [SerializeField, Min(0f)] float pulsePerSecond = 1.5f;

        [Tooltip("shadow.emphasize のとき、脈打つ深さ（0 = 脈打たない、1 = 消えるところまで薄くなる）")]
        [SerializeField, Range(0f, 1f)] float pulseDepth = 0.5f;

        [Tooltip("shadow.emphasize のときの大きさの倍率")]
        [SerializeField, Min(0f)] float emphasizeScale = 1.2f;

        [Tooltip("ゆっくり回る速さ（度/秒）。0 なら回らない")]
        [SerializeField] float spinDegreesPerSecond = 30f;

        [Tooltip("上下にゆれる幅（メートル）。0 ならゆれない")]
        [SerializeField, Min(0f)] float bobHeight = 0.005f;

        [Tooltip("上下にゆれる速さ（1 秒に何回）")]
        [SerializeField, Min(0f)] float bobPerSecond = 0.5f;

        [Header("出す条件")]
        [Tooltip("入れると、フォークがあるときは、かけらの付いた先を口に含んでいる間だけ出す（口から出したら消す）")]
        [SerializeField] bool onlyWhileInMouth = true;

        [Tooltip("入れると、場面が空になったとき（チュートリアルが終わった・止めた）に消す")]
        [SerializeField] bool hideWhenPhaseEnds = true;

        [Header("受ける合図の名前")]
        [Tooltip("影を出す")]
        [SerializeField] string showCue = "shadow.show";

        [Tooltip("影を薄く出す")]
        [SerializeField] string faintCue = "shadow.faint";

        [Tooltip("影を強く出す")]
        [SerializeField] string emphasizeCue = "shadow.emphasize";

        [Tooltip("影を消す")]
        [SerializeField] string hideCue = "shadow.hide";

        readonly Dictionary<GameObject, GameObject> _silhouettes = new Dictionary<GameObject, GameObject>();
        IDevice _device;
        IFoodSpawner _spawner;
        IFork _fork;
        GameObject _prefab;   // いまの食感で生まれる物。引けなければ null
        GameObject _shown;    // いま出している影
        Material _material;   // 影すべてで使い回す（濃さを 1 か所で変えるため）
        float _modeStartedAt;

        /// <summary>いまの見せ方。</summary>
        public ShadowMode Mode { get; private set; } = ShadowMode.Hidden;

        void OnEnable()
        {
            Cues.Requested += OnCue;
            Phases.Changed += OnPhaseChanged;
            Devices.WhenReady(BindDevice);
            FoodSpawners.WhenReady(BindSpawner);
            Forks.WhenReady(BindFork);
        }

        void OnDisable()
        {
            Cues.Requested -= OnCue;
            Phases.Changed -= OnPhaseChanged;
            Devices.CancelWhenReady(BindDevice);
            FoodSpawners.CancelWhenReady(BindSpawner);
            Forks.CancelWhenReady(BindFork);
            if (_device != null) _device.TextureChanged -= OnTextureChanged;
            if (_fork != null)
            {
                _fork.Pierced -= Refresh;
                _fork.MouthEntered -= Refresh;
                _fork.MouthLeft -= Refresh;
                _fork.Emptied -= Refresh;
            }
            _device = null;
            _spawner = null;
            _fork = null;
            Show(null);
        }

        void OnDestroy()
        {
            foreach (GameObject silhouette in _silhouettes.Values)
                if (silhouette != null) Destroy(silhouette);
            _silhouettes.Clear();
            if (_material != null) Destroy(_material);
        }

        void BindDevice(IDevice device)
        {
            _device = device;
            _device.TextureChanged += OnTextureChanged;
            OnTextureChanged(_device.Current); // もう噛んでいれば、いまの食感で出す
        }

        void BindSpawner(IFoodSpawner spawner)
        {
            _spawner = spawner;
            if (_device != null) OnTextureChanged(_device.Current);
        }

        void BindFork(IFork fork)
        {
            _fork = fork;
            _fork.Pierced += Refresh;
            _fork.MouthEntered += Refresh;
            _fork.MouthLeft += Refresh;
            _fork.Emptied += Refresh;
            Refresh();
        }

        void OnCue(CueRequested request)
        {
            ShadowMode? next = ModeForCue(request.Cue, showCue, faintCue, emphasizeCue, hideCue);
            if (next == null) return;
            if (next.Value != Mode) _modeStartedAt = Time.time;
            Mode = next.Value;
            if (Mode != ShadowMode.Hidden && _device != null) OnTextureChanged(_device.Current); // 出し始めに、いまの食感を引き直す
            else Refresh();
        }

        void OnPhaseChanged(PhaseId phase)
        {
            if (!hideWhenPhaseEnds || !phase.IsEmpty) return;
            Mode = ShadowMode.Hidden;
            Refresh();
        }

        void OnTextureChanged(TextureId texture)
        {
            // 影を出していない間は、Prefab を引かない（本編では何もしない）。
            _prefab = Mode == ShadowMode.Hidden || _spawner == null || texture.IsEmpty ? null : _spawner.Preview(texture);
            Refresh();
        }

        void Refresh()
        {
            bool forkAllows = !onlyWhileInMouth || _fork == null || (_fork.IsLoaded && _fork.InMouth);
            Show(Mode != ShadowMode.Hidden && forkAllows ? _prefab : null);
        }

        void Show(GameObject prefab)
        {
            GameObject next = prefab != null ? SilhouetteOf(prefab) : null;
            if (_shown != null && _shown != next) _shown.SetActive(false);
            _shown = next;
            if (_shown != null && !_shown.activeSelf)
            {
                _shown.SetActive(true);
                Place();
            }
        }

        void Update()
        {
            if (_shown == null) return;
            Place();
            float t = Time.time - _modeStartedAt;
            Color c = color;
            c.a = AlphaFor(Mode, normalAlpha, faintAlpha, emphasizeAlpha, pulseDepth, pulsePerSecond, t);
            SetColor(_material, c);
        }

        // Anchor（無ければカメラの前）に置き、ゆっくり回し、上下にゆらす。
        void Place()
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 position;
            if (anchor != null) position = anchor.position;
            else if (cam != null) position = cam.position + cam.rotation * cameraOffset;
            else position = transform.position;

            float t = Time.time;
            if (bobHeight > 0f) position += Vector3.up * (Mathf.Sin(t * bobPerSecond * 2f * Mathf.PI) * bobHeight);
            Quaternion facing = anchor != null ? anchor.rotation : Quaternion.identity;
            _shown.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(t * spinDegreesPerSecond, Vector3.up) * facing);

            float scale = Mode == ShadowMode.Emphasize ? emphasizeScale : 1f;
            float parentScale = transform.lossyScale.x; // 影はこの物の子。この物の大きさに引きずられないようにする
            _shown.transform.localScale = Vector3.one * (Mathf.Approximately(parentScale, 0f) ? scale : scale / parentScale);
        }

        GameObject SilhouetteOf(GameObject prefab)
        {
            if (_silhouettes.TryGetValue(prefab, out GameObject cached) && cached != null) return cached;
            GameObject built = Build(prefab);
            _silhouettes[prefab] = built;
            return built;
        }

        // Prefab の見た目の形だけを写した影を作る。Prefab は、無効にした入れ物の中で作るので、スクリプトの Awake・OnEnable は動かない。
        GameObject Build(GameObject prefab)
        {
            var holder = new GameObject("Shadow Source");
            holder.SetActive(false);
            GameObject source = Instantiate(prefab, holder.transform, false);
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            source.transform.localScale = Vector3.one;

            var root = new GameObject("Shadow " + prefab.name);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var body = new GameObject("Body"); // 形の中心を root に合わせ、大きさをそろえるための入れ物
            body.transform.SetParent(root.transform, false);

            Material material = SharedMaterial();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null) continue;

                var part = new GameObject(renderer.name);
                part.transform.SetParent(body.transform, false);
                Transform from = renderer.transform;
                part.transform.localPosition = source.transform.InverseTransformPoint(from.position);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * from.rotation;
                part.transform.localScale = from.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = part.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                meshRenderer.sharedMaterials = materials;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                Bounds partBounds = TransformBounds(mesh.bounds, root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix);
                if (hasBounds) bounds.Encapsulate(partBounds);
                else bounds = partBounds;
                hasBounds = true;
            }
            Destroy(holder);

            if (hasBounds)
            {
                // bounds は root から見た値（body はまだ原点・回転なし・大きさ 1）。形の中心を root に合わせ、大きさをそろえる。
                float fit = FitScale(bounds.size, size);
                body.transform.localScale = Vector3.one * fit;
                body.transform.localPosition = -bounds.center * fit;
            }
            return root;
        }

        static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            return null; // パーティクルや線は、影にしない
        }

        static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 c = local.center;
            Vector3 e = local.extents;
            var result = new Bounds(matrix.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -e.x : e.x, (i / 2) % 2 == 0 ? -e.y : e.y, i / 4 == 0 ? -e.z : e.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(c + corner));
            }
            return result;
        }

        Material SharedMaterial()
        {
            if (_material != null) return _material;
            if (shadowMaterial != null) _material = new Material(shadowMaterial);
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                _material = new Material(shader) { renderQueue = 3000 };
            }
            _material.name = "Food Shadow (instance)";
            SetColor(_material, color);
            return _material;
        }

        static void SetColor(Material material, Color c)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c); // URP
            if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        }

        /// <summary>合図の名前から、次の見せ方を引く。関係ない合図なら null。名前が空の行は、合わないものとして扱う。</summary>
        internal static ShadowMode? ModeForCue(CueId cue, string show, string faint, string emphasize, string hide)
        {
            if (cue.IsEmpty) return null;
            if (Matches(cue, hide)) return ShadowMode.Hidden;
            if (Matches(cue, show)) return ShadowMode.Normal;
            if (Matches(cue, faint)) return ShadowMode.Faint;
            if (Matches(cue, emphasize)) return ShadowMode.Emphasize;
            return null;
        }

        static bool Matches(CueId cue, string name) => !string.IsNullOrEmpty(name) && cue == new CueId(name);

        /// <summary>
        /// 見せ方と、その見せ方になってからの秒数から、濃さを決める。Emphasize は、emphasize と emphasize × (1 - depth) の間を脈打つ（始めは最も濃い）。
        /// </summary>
        internal static float AlphaFor(ShadowMode mode, float normal, float faint, float emphasize, float depth, float pulsePerSecond, float seconds)
        {
            switch (mode)
            {
                case ShadowMode.Normal: return Mathf.Clamp01(normal);
                case ShadowMode.Faint: return Mathf.Clamp01(faint);
                case ShadowMode.Emphasize:
                    float wave = 0.5f + 0.5f * Mathf.Cos(seconds * pulsePerSecond * 2f * Mathf.PI); // 1 → 0 → 1
                    return Mathf.Clamp01(emphasize * (1f - Mathf.Clamp01(depth) * (1f - wave)));
                default: return 0f;
            }
        }

        /// <summary>いちばん長い辺を target にする倍率。target が 0 以下、または大きさが 0 なら 1（そのまま）。</summary>
        internal static float FitScale(Vector3 boundsSize, float target)
        {
            float longest = Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z));
            if (target <= 0f || longest <= 0f) return 1f;
            return target / longest;
        }
    }
}
