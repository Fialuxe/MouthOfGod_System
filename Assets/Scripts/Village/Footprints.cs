using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 床に、光る足あとを 1 組描きます。つま先はフィールドのほうを向きます。<see cref="StandArea"/> の足あとの物に付けます。
    /// </summary>
    /// <remarks>
    /// 足の裏の形は、Play の始めにテクスチャへ描くので、絵の素材は要りません。
    /// 出し入れは <see cref="StandArea.ShowFootprint"/> が行います。
    /// </remarks>
    public sealed class Footprints : MonoBehaviour
    {
        [Tooltip("つま先を向ける先です。空なら、親のフィールド（FieldStages）に向けます")]
        [SerializeField] Transform faceTowards;

        [Tooltip("足の長さ（m）")]
        [SerializeField, Min(0.05f)] float footLength = 0.26f;

        [Tooltip("左右の足の中心どうしの間（m）")]
        [SerializeField, Min(0f)] float stance = 0.2f;

        [SerializeField] Color color = new Color(1f, 0.85f, 0.45f, 0.9f);

        [Tooltip("明るさが 1 往復する回数（1 秒あたり）。0 なら変えません")]
        [SerializeField, Min(0f)] float pulsePerSecond = 0.8f;

        const int TextureWidth = 64;
        const int TextureHeight = 160;
        const float FloorOffset = 0.004f; // 床とちらつかないように、少しだけ浮かせます

        Material _material;

        void Start()
        {
            FaceField();
            var feet = new GameObject("Feet");
            feet.transform.SetParent(transform, false);
            feet.AddComponent<MeshFilter>().sharedMesh = BuildMesh(footLength, stance);
            _material = BuildMaterial(color);
            feet.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }

        void Update()
        {
            if (_material == null || pulsePerSecond <= 0f) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsePerSecond * Mathf.PI * 2f);
            var c = color;
            c.a *= Mathf.Lerp(0.45f, 1f, t);
            _material.SetColor("_BaseColor", c);
            _material.color = c;
        }

        void OnDestroy()
        {
            if (_material == null) return;
            Destroy(_material.mainTexture);
            Destroy(_material);
        }

        void FaceField()
        {
            Transform target = faceTowards;
            if (target == null)
            {
                var field = GetComponentInParent<FieldStages>();
                if (field != null) target = field.transform;
            }
            if (target == null) return;
            Vector3 toField = target.position - transform.position;
            toField.y = 0f;
            if (toField.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toField.normalized, Vector3.up);
        }

        // ローカル座標に平らな四角を 2 枚作ります。+Z が前で、右足は UV を左右反転します。
        static Mesh BuildMesh(float length, float stance)
        {
            float width = length * 0.4f;
            var mesh = new Mesh { name = "Footprints" };
            var vertices = new Vector3[8];
            var uv = new Vector2[8];
            for (int foot = 0; foot < 2; foot++)
            {
                float x = (foot == 0 ? -0.5f : 0.5f) * stance;
                float z = foot == 0 ? 0.02f : -0.02f; // 少し前後にずらして、立っている感じにします
                int i = foot * 4;
                vertices[i + 0] = new Vector3(x - width / 2, FloorOffset, z - length / 2);
                vertices[i + 1] = new Vector3(x + width / 2, FloorOffset, z - length / 2);
                vertices[i + 2] = new Vector3(x - width / 2, FloorOffset, z + length / 2);
                vertices[i + 3] = new Vector3(x + width / 2, FloorOffset, z + length / 2);
                bool mirror = foot == 1;
                uv[i + 0] = new Vector2(mirror ? 1 : 0, 0);
                uv[i + 1] = new Vector2(mirror ? 0 : 1, 0);
                uv[i + 2] = new Vector2(mirror ? 1 : 0, 1);
                uv[i + 3] = new Vector2(mirror ? 0 : 1, 1);
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material BuildMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "Footprints", mainTexture = PaintLeftSole() };
            material.SetTexture("_BaseMap", material.mainTexture);
            material.SetColor("_BaseColor", color);
            material.color = color;
            // URP の Unlit を、半透明で描く設定にします。
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        // 左足の裏を描きます。v = 0 がかかとで、親指は右（u = 1）です。
        static Texture2D PaintLeftSole()
        {
            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureWidth * TextureHeight];
            for (int y = 0; y < TextureHeight; y++)
            for (int x = 0; x < TextureWidth; x++)
            {
                float u = (x + 0.5f) / TextureWidth;
                float v = (y + 0.5f) / TextureHeight;
                float inside = Mathf.Max(
                    Ellipse(u, v, 0.48f, 0.15f, 0.30f, 0.13f),   // かかと
                    Ellipse(u, v, 0.40f, 0.40f, 0.20f, 0.18f),   // 土踏まずの外側
                    Ellipse(u, v, 0.55f, 0.62f, 0.40f, 0.14f),   // 指の付け根
                    Ellipse(u, v, 0.78f, 0.84f, 0.15f, 0.07f),   // 親指
                    Ellipse(u, v, 0.56f, 0.86f, 0.09f, 0.045f),
                    Ellipse(u, v, 0.40f, 0.84f, 0.08f, 0.04f),
                    Ellipse(u, v, 0.27f, 0.80f, 0.07f, 0.035f),
                    Ellipse(u, v, 0.16f, 0.75f, 0.06f, 0.03f));  // 小指
                pixels[y * TextureWidth + x] = new Color32(255, 255, 255, (byte)(inside * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 楕円の中は 1 で、縁を少し越えると 0 になります。
        static float Ellipse(float u, float v, float cu, float cv, float ru, float rv)
        {
            float du = (u - cu) / ru;
            float dv = (v - cv) / rv;
            float d = Mathf.Sqrt(du * du + dv * dv);
            return 1f - Mathf.SmoothStep(0.85f, 1.05f, d);
        }
    }
}
