using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村人の頭の上に出す吹き出しです。村人の Prefab に付けます。何を出すかは、村人（<see cref="VillagerMood"/>）が決めて呼びます。
    /// </summary>
    /// <remarks>
    /// 絵があれば絵だけを、無ければ名前の文字を出します。吹き出しはシーンの直下に作ります。
    /// ミニチュアの台の拡大縮小を受けないようにするためです。位置は毎フレーム、頭の上に合わせてカメラへ向けます。
    /// </remarks>
    public sealed class VillagerBubble : MonoBehaviour
    {
        const float CharWidth = 0.35f; // TextMesh の fontSize 64 × characterSize 0.1 のときの 1 文字の幅
        const float LineHeight = 0.7f;
        const float Padding = 0.3f;
        const float IconSize = 1.2f; // 吹き出しの中の大きさ。文字 1 行の高さの約 2 倍
        const int CornerSegments = 6;
        const float TailWidth = 0.4f; // しっぽ（話している人を指す三角）の幅
        const float TailHeight = 0.35f;

        [Tooltip("見た目の設定（Assets/Data/RequestBubbleSettings.asset）")]
        [SerializeField] RequestBubbleSettings settings;

        [Tooltip("吹き出しを出す位置。空なら、VillagerMood の頭の上の印、それも無ければこの村人")]
        [SerializeField] Transform anchor;

        GameObject _root;
        Material _background;
        readonly List<Object> _owned = new List<Object>(); // 吹き出しと一緒に消す物
        float _hideAt = -1f;
        Camera _camera;

        /// <summary>欲しい物を出します。icon が無ければ、label の文字を出します。前の吹き出しは消します。</summary>
        public void Show(Texture2D icon, string label)
        {
            Hide();
            if (settings == null)
            {
                Debug.LogError($"[VillagerBubble] {name}: 見た目の設定（Settings）が空なので、吹き出しを出せません。", this);
                return;
            }
            Build(icon, label);
            LateUpdate();
        }

        /// <summary>
        /// 欲しい物が終わったことを見せます。届いたら resolved で緑に、届かなかったら灰色にします。
        /// 少し見せてから消えます。
        /// </summary>
        public void Close(bool resolved)
        {
            if (_root == null) return;
            if (_background != null) _background.color = Faded(resolved ? settings.ResolvedColor : settings.UnresolvedColor);
            _hideAt = Time.time + settings.ClosedHoldSeconds;
        }

        /// <summary>吹き出しを、すぐ消します。</summary>
        public void Hide()
        {
            foreach (Object obj in _owned)
                if (obj != null) Destroy(obj);
            _owned.Clear();
            _root = null;
            _background = null;
            _hideAt = -1f;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            if (_hideAt >= 0f && Time.time >= _hideAt)
            {
                Hide();
                return;
            }
            Transform at = Anchor;
            Vector3 position = at.position + Vector3.up * settings.Height;
            _root.transform.position = position;
            _root.transform.localScale = Vector3.one * settings.Size; // 毎フレーム読むので、Play 中に設定を直しても効きます
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector3 away = position - _camera.transform.position;
            if (away.sqrMagnitude > 1e-6f) _root.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        void OnDisable() => Hide();

        Transform Anchor
        {
            get
            {
                if (anchor != null) return anchor;
                var mood = GetComponent<VillagerMood>();
                return mood != null && mood.WorryMark != null ? mood.WorryMark.transform : transform;
            }
        }

        Color Faded(Color color) => new Color(color.r, color.g, color.b, settings.BackgroundOpacity);

        void Build(Texture2D icon, string label)
        {
            // 絵は Sprites/Default でだけ描けます（代わりの Unlit/Color は絵を貼れません）。
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) icon = null;
            Shader fallback = shader != null ? shader : Shader.Find("Unlit/Color");

            _root = new GameObject($"Bubble ({name})");
            _owned.Add(_root);

            Vector2 size = icon != null
                ? new Vector2(IconSize + settings.IconPadding * 2f, IconSize + settings.IconPadding * 2f)
                : new Vector2(label.Length * CharWidth + Padding * 2f, LineHeight + Padding);
            Mesh shape = BuildShape(size.x, size.y, settings.CornerRadius);
            _owned.Add(shape);

            var back = new GameObject("Background");
            back.transform.SetParent(_root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.01f); // 絵と文字の奥
            back.AddComponent<MeshFilter>().sharedMesh = shape;
            var backRenderer = back.AddComponent<MeshRenderer>();
            if (fallback != null)
            {
                _background = new Material(fallback) { color = Faded(settings.OpenColor), renderQueue = 3000 };
                _owned.Add(_background);
                backRenderer.sharedMaterial = _background;
            }
            else backRenderer.enabled = false;

            if (icon != null)
            {
                var picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
                picture.name = "Icon";
                Destroy(picture.GetComponent<Collider>());
                picture.transform.SetParent(_root.transform, false);
                picture.transform.localScale = new Vector3(IconSize, IconSize, 1f);
                var material = new Material(shader) { mainTexture = icon, color = Color.white, renderQueue = 3001 };
                _owned.Add(material);
                picture.GetComponent<Renderer>().sharedMaterial = material;
                return;
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = new GameObject("Text");
            text.transform.SetParent(_root.transform, false);
            var mesh = text.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.black;
            var textMaterial = new Material(font.material) { renderQueue = 3001 };
            _owned.Add(textMaterial);
            text.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
        }

        // 角の丸い四角と、下の辺の真ん中から出るしっぽを、重ならない 1 つの形にします。
        // 別々の物にすると、半透明のときに重なった所だけ濃く見えるためです。
        static Mesh BuildShape(float width, float height, float cornerRadius)
        {
            float radius = Mathf.Min(cornerRadius, width / 2f, height / 2f);
            float halfW = width / 2f - radius;
            float halfH = height / 2f - radius;
            var vertices = new List<Vector3> { Vector3.zero };
            var corners = new[] { new Vector2(halfW, halfH), new Vector2(halfW, -halfH), new Vector2(-halfW, -halfH), new Vector2(-halfW, halfH) };
            for (int c = 0; c < 4; c++)
            {
                float start = 90f - 90f * c;
                for (int i = 0; i <= CornerSegments; i++)
                {
                    float angle = (start - 90f * i / CornerSegments) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(corners[c].x + Mathf.Cos(angle) * radius, corners[c].y + Mathf.Sin(angle) * radius, 0f));
                }
            }
            var triangles = new List<int>();
            int rim = vertices.Count - 1;
            for (int i = 1; i <= rim; i++) triangles.AddRange(new[] { 0, i, i % rim + 1 });

            int tail = vertices.Count;
            vertices.Add(new Vector3(-TailWidth / 2f, -height / 2f, 0f));
            vertices.Add(new Vector3(TailWidth / 2f, -height / 2f, 0f));
            vertices.Add(new Vector3(0f, -height / 2f - TailHeight, 0f));
            triangles.AddRange(new[] { tail, tail + 1, tail + 2 });

            var mesh = new Mesh { name = "Bubble Shape", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
