using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Miko.Editor
{
    /// <summary>
    /// 目の絵（4 列 × 2 行、1 コマ 256 px）を、コードで描いて PNG にする。順番は MikoEye と同じ。
    /// 0 開く / 1 ー / 2 ^ / 3 ＞ / 4 ＜ / 5 ♡ / 6 ・ / 7 ∪
    /// </summary>
    static class MikoEyeAtlas
    {
        const int Cell = 256;
        const int Columns = 4;
        const int Rows = 2;
        const int Samples = 2; // 1 画素あたり Samples × Samples 点で、縁をなめらかにする

        static readonly Color Dark = new Color(0.16f, 0.09f, 0.12f, 1f);
        static readonly Color Pink = new Color(0.95f, 0.28f, 0.4f, 1f);

        // 既にあれば上書きして、GUID を保つ。
        public static Texture2D Save(string path)
        {
            var texture = new Texture2D(Cell * Columns, Cell * Rows, TextureFormat.RGBA32, false);
            var pixels = new Color32[texture.width * texture.height];
            for (var index = 0; index < Columns * Rows; index++)
            {
                var column = index % Columns;
                var rowFromTop = index / Columns;
                var originX = column * Cell;
                var originY = (Rows - 1 - rowFromTop) * Cell; // 画像は下から並ぶ
                for (var y = 0; y < Cell; y++)
                    for (var x = 0; x < Cell; x++)
                        pixels[(originY + y) * texture.width + originX + x] = Paint(index, x, y);
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // コマの中の 1 画素。中心が (0, 0)、縁が ±1 の座標で、図形に入る点の割合を、色の濃さにする。
        static Color32 Paint(int index, int px, int py)
        {
            var inside = 0;
            var tint = Dark;
            var open = index == 0;
            var whiteHit = 0;
            for (var sy = 0; sy < Samples; sy++)
                for (var sx = 0; sx < Samples; sx++)
                {
                    var u = ((px + (sx + 0.5f) / Samples) / Cell) * 2f - 1f;
                    var v = ((py + (sy + 0.5f) / Samples) / Cell) * 2f - 1f;
                    if (!Hit(index, u, v)) continue;
                    inside++;
                    if (open && Distance(u, v, 0.14f, 0.28f) < 0.13f) whiteHit++; // 開いた目の光
                }
            if (index == 5) tint = Pink;
            var total = Samples * Samples;
            var alpha = (float)inside / total;
            var white = inside > 0 ? (float)whiteHit / inside : 0f;
            var color = Color.Lerp(tint, Color.white, white);
            color.a = alpha;
            return color;
        }

        static float Distance(float u, float v, float cu, float cv) => Mathf.Sqrt((u - cu) * (u - cu) + (v - cv) * (v - cv));

        static bool Hit(int index, float u, float v)
        {
            switch (index)
            {
                case 0: // 開いた目: 縦長のだ円
                    return (u * u) / (0.42f * 0.42f) + (v * v) / (0.66f * 0.66f) <= 1f;
                case 1: // ー
                    return Stroke(u, v, 0.1f, new Vector2(-0.6f, 0f), new Vector2(0.6f, 0f));
                case 2: // ^
                    return Stroke(u, v, 0.1f, Arc(0f, -0.3f, 0.6f, 25f, 155f));
                case 3: // ＞
                    return Stroke(u, v, 0.1f, new Vector2(-0.5f, 0.5f), new Vector2(0.45f, 0f), new Vector2(-0.5f, -0.5f));
                case 4: // ＜
                    return Stroke(u, v, 0.1f, new Vector2(0.5f, 0.5f), new Vector2(-0.45f, 0f), new Vector2(0.5f, -0.5f));
                case 5: // ♡
                    return Heart(u, v);
                case 6: // ・
                    return Distance(u, v, 0f, 0f) <= 0.16f;
                case 7: // ∪
                    return Stroke(u, v, 0.1f, Arc(0f, 0.3f, 0.6f, 205f, 335f));
            }
            return false;
        }

        // 折れ線を太さ（半径）radius で描いたときに、点が入るか。
        static bool Stroke(float u, float v, float radius, params Vector2[] points)
        {
            var p = new Vector2(u, v);
            for (var i = 0; i + 1 < points.Length; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if ((p - (a + ab * t)).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        static Vector2[] Arc(float cu, float cv, float radius, float fromDegrees, float toDegrees)
        {
            const int Segments = 20;
            var points = new Vector2[Segments + 1];
            for (var i = 0; i <= Segments; i++)
            {
                var angle = Mathf.Lerp(fromDegrees, toDegrees, (float)i / Segments) * Mathf.Deg2Rad;
                points[i] = new Vector2(cu + Mathf.Cos(angle) * radius, cv + Mathf.Sin(angle) * radius);
            }
            return points;
        }

        // ハート曲線 (x² + y² − 1)³ − x²y³ ≤ 0。コマの中におさまる大きさに合わせる。
        static bool Heart(float u, float v)
        {
            var x = u / 0.62f * 1.15f;
            var y = (v + 0.05f) / 0.62f * 1.15f;
            var a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }
    }
}
