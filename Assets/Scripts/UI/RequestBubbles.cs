using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.UI
{
    /// <summary>吹き出しに出す文字を作る純粋な部品（Unity に依存しない）。</summary>
    public static class RequestBubbleText
    {
        /// <summary>
        /// 吹き出しの文字を引く所。差し替えると、名前の引き方が変わる（食べ物のデータ（アイコン付き）へ移るときの入口）。
        /// null を入れると、いつもの <see cref="DisplayName"/> に戻る。Play のたびに戻る。
        /// static なのは、Play で自動で置かれる吹き出し（外から手が届かない）にも効かせるため。
        /// </summary>
        public static Func<FoodId, string> NameLookup { get; set; }

        /// <summary>FoodId から英語の表示名を作る（"fish" → "Fish"、"sea_bass" → "Sea bass"）。空 ID は "?"。</summary>
        public static string DisplayName(FoodId food)
        {
            string raw = food.Value.Trim().Replace('_', ' ').Replace('-', ' ');
            if (raw.Length == 0) return "?";
            return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
        }

        /// <summary>差し替えた引き方（<see cref="NameLookup"/>）で名前を引く。無い・空の文字が返ったときは <see cref="DisplayName"/>。</summary>
        public static string Resolve(FoodId food) => Resolve(food, NameLookup);

        public static string Resolve(FoodId food, Func<FoodId, string> lookup)
        {
            string name = lookup?.Invoke(food);
            return string.IsNullOrEmpty(name) ? DisplayName(food) : name;
        }

        /// <summary>
        /// 食べ物のアイコンの Resources の場所（"Icons/Food/fish"）。アイコンは、Food のメニュー「MouthOfGod/Food/Generate Icons」で作る。
        /// ファイル名が食べ物の ID なのは、食べ物の Prefab と同じ決まり。空 ID は null。
        /// </summary>
        public static string IconPath(FoodId food) => food.IsEmpty ? null : "Icons/Food/" + food.Value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NameLookup = null;
        }
    }

    /// <summary>
    /// 村人の欲しい物を、頭の上の吹き出しで見せる（#59・#270）。食べ物のアイコンがあれば絵を、無ければ英語の名前を出す。Play で自動で置かれるので、手で置かなくてよい。
    /// <see cref="Fields"/> のフィールドの Issued で <see cref="RequestIssued.Anchor"/> の上に出し、Closed で消す（届いたか・待ちきれなかったかで色を変えて、少しだけ見せてから消す）。
    /// Village の型は参照しない。Prefab もシーンも使わず、子の物をコードで作る。
    /// 遠くの村人への気づき方（見えない位置の村人をどう知らせるか）は別 Issue。
    /// </summary>
    public class RequestBubbles : MonoBehaviour
    {
        // 大きさ・高さ・色などは、誰でも直せるように設定のアセットに持つ（RequestBubbleSettings）。
        RequestBubbleSettings _settings;
        RequestBubbleSettings Settings => _settings != null ? _settings : _settings = RequestBubbleSettings.Load();

        // 文字の大きさの目安（TextMesh: fontSize 64 × characterSize 0.1 のとき）。Play で見て直す。
        const float CharWidth = 0.35f;
        const float LineHeight = 0.7f;
        const float Padding = 0.3f;
        const float IconSize = 1.2f; // アイコンの一辺（吹き出しの中の大きさ。文字 1 行の高さの約 2 倍）
        const int CornerSegments = 6; // 角の丸みを何本の線で作るか
        const float TailWidth = 0.4f; // しっぽ（話している人を指す三角）の幅
        const float TailHeight = 0.35f;

        static bool _placed;

        // Domain Reload を切っていても、Play のたびに初期化する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _placed = false;
        }

        // 新しく 1 つ作るだけ（シーンから探さない）。二重に作らないのは static フラグ 1 つ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoPlace()
        {
            if (_placed) return;
            _placed = true;
            var go = new GameObject("Request Bubbles");
            DontDestroyOnLoad(go);
            go.AddComponent<RequestBubbles>();
        }

        sealed class Bubble
        {
            public GameObject Root;
            public Transform Anchor;
            public Renderer Background;
            public Material BackgroundMaterial;
            public Material TextMaterial;
            public Material IconMaterial;
            public Mesh Shape;
            public float DestroyAt;
        }

        // フィールドごとの購読。外すときに同じデリゲートが要るので持っておく。
        sealed class Subscription
        {
            public Action<RequestIssued> Issued;
            public Action<RequestClosed> Closed;
        }

        readonly Dictionary<IFieldRequests, Subscription> _subscriptions = new Dictionary<IFieldRequests, Subscription>();
        readonly Dictionary<FieldId, Bubble> _open = new Dictionary<FieldId, Bubble>();
        readonly List<Bubble> _closing = new List<Bubble>();
        Font _font;
        Camera _camera;

        /// <summary>いま出ている吹き出しの数（終わって消えるのを待っているものは数えない）。</summary>
        internal int OpenCount => _open.Count;

        /// <summary>終わって、色を変えたまま消えるのを待っている吹き出しの数。</summary>
        internal int ClosingCount => _closing.Count;

        internal bool HasBubble(FieldId field) => _open.ContainsKey(field);

        /// <summary>消えるのを待っている吹き出し（0 から数える）の背景の色。背景が無い（シェーダーが無い）ときは透明。</summary>
        internal Color GetClosingColor(int index)
        {
            Material m = _closing[index].BackgroundMaterial;
            return m != null ? m.color : default;
        }

        void OnEnable() => Listen();

        void OnDisable() => StopListening();

        // フィールドの知らせを聞き始める。EditMode のテストでは OnEnable が呼ばれないので、テストから直接呼ぶ。
        internal void Listen()
        {
            foreach (IFieldRequests field in new List<IFieldRequests>(Fields.All)) Attach(field);
            Fields.Added += Attach;
            Fields.Removed += Detach;
        }

        // 聞くのをやめ、出ている吹き出しを消す。
        internal void StopListening()
        {
            Fields.Added -= Attach;
            Fields.Removed -= Detach;
            foreach (var pair in _subscriptions)
            {
                pair.Key.Issued -= pair.Value.Issued;
                pair.Key.Closed -= pair.Value.Closed;
            }
            _subscriptions.Clear();
            DestroyAll();
        }

        void OnDestroy()
        {
            DestroyAll();
        }

        void Attach(IFieldRequests field)
        {
            if (field == null || _subscriptions.ContainsKey(field)) return;
            var sub = new Subscription { Issued = OnIssued, Closed = OnClosed };
            _subscriptions.Add(field, sub);
            field.Issued += sub.Issued;
            field.Closed += sub.Closed;
            // すでに欲しい物が出ているフィールドの吹き出しは、Issued を聞き逃すので出せない（Anchor も分からない）。
            // いまは、次の Issued から出す。
        }

        void Detach(IFieldRequests field)
        {
            if (field == null || !_subscriptions.TryGetValue(field, out Subscription sub)) return;
            field.Issued -= sub.Issued;
            field.Closed -= sub.Closed;
            _subscriptions.Remove(field);
            Remove(field.Field);
        }

        void OnIssued(RequestIssued request)
        {
            Remove(request.Field); // 1 フィールド 1 吹き出し
            if (request.Anchor == null)
            {
                Debug.LogWarning($"[RequestBubbles] フィールド「{request.Field}」の Anchor が無いので、吹き出しを出しません。");
                return;
            }
            _open[request.Field] = Create(request);
        }

        void OnClosed(RequestClosed closed)
        {
            if (!_open.TryGetValue(closed.Field, out Bubble bubble)) return;
            _open.Remove(closed.Field);
            if (bubble.BackgroundMaterial != null) bubble.BackgroundMaterial.color = Faded(closed.Resolved ? Settings.ResolvedColor : Settings.UnresolvedColor);
            bubble.DestroyAt = Time.time + Settings.ClosedHoldSeconds;
            _closing.Add(bubble);
        }

        void Remove(FieldId field)
        {
            if (!_open.TryGetValue(field, out Bubble bubble)) return;
            _open.Remove(field);
            DestroyBubble(bubble);
        }

        void DestroyAll()
        {
            foreach (Bubble bubble in _open.Values) DestroyBubble(bubble);
            foreach (Bubble bubble in _closing) DestroyBubble(bubble);
            _open.Clear();
            _closing.Clear();
        }

        static void DestroyBubble(Bubble bubble)
        {
            if (bubble.BackgroundMaterial != null) DestroySafely(bubble.BackgroundMaterial);
            if (bubble.TextMaterial != null) DestroySafely(bubble.TextMaterial);
            if (bubble.IconMaterial != null) DestroySafely(bubble.IconMaterial);
            if (bubble.Shape != null) DestroySafely(bubble.Shape);
            if (bubble.Root != null) DestroySafely(bubble.Root);
        }

        // エディタの EditMode のテストでは Destroy が使えないので、DestroyImmediate に切り替える。
        static void DestroySafely(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        Color Faded(Color color) => new Color(color.r, color.g, color.b, Settings.BackgroundOpacity);

        /// <summary>
        /// 吹き出しの形（角の丸い四角と、下の辺の真ん中から出るしっぽ）の Mesh を作る。中心が原点。
        /// しっぽを別の物にすると、透けたときに四角との重なりが濃く見えるので、重ならない 1 つの形にする。
        /// </summary>
        internal static Mesh BuildShape(float width, float height, float cornerRadius)
        {
            float radius = Mathf.Min(cornerRadius, width / 2f, height / 2f);
            float halfW = width / 2f - radius;
            float halfH = height / 2f - radius;
            var vertices = new List<Vector3> { Vector3.zero };
            // 角の丸い四角の縁。右上の角から時計回り（カメラ側から見て）。
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

            // しっぽ（下の辺から、村人の方を指す三角）
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

        // アイコンがあれば絵だけ、無ければ文字を出す。
        Bubble Create(RequestIssued request)
        {
            Shader shader = Shader.Find("Sprites/Default");
            // アイコンは Sprites/Default でだけ描ける（代わりの Unlit/Color は絵を貼れない）。
            string iconPath = shader != null ? RequestBubbleText.IconPath(request.Food) : null;
            Texture2D icon = iconPath != null ? Resources.Load<Texture2D>(iconPath) : null;
            if (shader == null) shader = Shader.Find("Unlit/Color");
            string text = RequestBubbleText.Resolve(request.Food);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject($"Bubble {request.Field}");
            root.transform.SetParent(transform, false); // Anchor の子にしない（ミニチュアの拡大縮小を受けないため）
            
            // 背景（角の丸い四角としっぽ）。アイコンなら正方形、文字なら文字の長さに合わせる。
            Vector2 backSize = icon != null
                ? new Vector2(IconSize + Settings.IconPadding * 2f, IconSize + Settings.IconPadding * 2f)
                : new Vector2(text.Length * CharWidth + Padding * 2f, LineHeight + Padding);
            Mesh shape = BuildShape(backSize.x, backSize.y, Settings.CornerRadius);
            var back = new GameObject("Background");
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.01f); // 文字の奥
            back.AddComponent<MeshFilter>().sharedMesh = shape;
            var backRenderer = back.AddComponent<MeshRenderer>();
            Material backMaterial = null;
            if (shader != null)
            {
                backMaterial = new Material(shader) { color = Faded(Settings.OpenColor), renderQueue = 3000 };
                backRenderer.sharedMaterial = backMaterial;
            }
            else
            {
                Debug.LogWarning("[RequestBubbles] 背景のシェーダーが見つからないので、文字だけ出します。");
                backRenderer.enabled = false;
            }

            Material textMaterial = null;
            Material iconMaterial = null;
            if (icon != null)
            {
                // アイコン（背景の手前の Quad）
                var picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
                picture.name = "Icon";
                DestroySafely(picture.GetComponent<Collider>());
                picture.transform.SetParent(root.transform, false);
                picture.transform.localScale = new Vector3(IconSize, IconSize, 1f);
                iconMaterial = new Material(shader) { mainTexture = icon, color = Color.white, renderQueue = 3001 };
                picture.GetComponent<Renderer>().sharedMaterial = iconMaterial;
            }
            else
            {
                // 文字
                var label = new GameObject("Text");
                label.transform.SetParent(root.transform, false);
                var mesh = label.AddComponent<TextMesh>();
                mesh.text = text;
                mesh.font = _font;
                mesh.fontSize = 64;
                mesh.characterSize = 0.1f;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = Color.black;
                textMaterial = new Material(_font.material) { renderQueue = 3001 };
                label.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            }

            var bubble = new Bubble
            {
                Root = root,
                Anchor = request.Anchor,
                Background = backRenderer,
                Shape = shape,
                BackgroundMaterial = backMaterial,
                TextMaterial = textMaterial,
                IconMaterial = iconMaterial,
            };
            Place(bubble, GetCamera());
            return bubble;
        }

        void LateUpdate()
        {
            Camera cam = GetCamera();
            foreach (Bubble bubble in _open.Values) Place(bubble, cam); // Place は _open を変えない

            for (int i = _closing.Count - 1; i >= 0; i--)
            {
                if (Time.time < _closing[i].DestroyAt) { Place(_closing[i], cam); continue; }
                DestroyBubble(_closing[i]);
                _closing.RemoveAt(i);
            }
        }

        Camera GetCamera()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        // Anchor の上に置き、カメラの方を向ける。Anchor が消えていたら、吹き出しも消す。
        void Place(Bubble bubble, Camera cam)
        {
            if (bubble.Anchor == null)
            {
                bubble.Root.SetActive(false);
                return;
            }
            Vector3 position = bubble.Anchor.position + Vector3.up * Settings.Height;
            bubble.Root.transform.position = position;
            bubble.Root.transform.localScale = Vector3.one * Settings.Size; // 毎フレーム読むので、Play 中に設定を直しても効く
            if (cam != null)
            {
                Vector3 away = position - cam.transform.position;
                if (away.sqrMagnitude > 1e-6f) bubble.Root.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }
    }
}
