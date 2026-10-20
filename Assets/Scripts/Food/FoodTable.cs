using System;
using System.Collections.Generic;
using System.Text;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// 「食感 → 食べ物の Prefab」の表。Field Id が空なら全フィールド共通の表、フィールドの ID（village / mountain / sea / grassland）なら、そのフィールドの専用の表。
    /// 専用の表は、そのフィールドの Prefab に付ける。有効になると、自分で <see cref="FoodTables"/> に登録する。
    /// </summary>
    /// <remarks>
    /// - 共通の表は、なくてもよい。なければ、専用の表にない食感は何も生まれない。
    /// - 同じ食感の行が重なったら、先の行を使う。
    /// - 引くのは <see cref="FoodTables.Resolve"/>。この表だけを引くときは <see cref="Find"/>。
    /// </remarks>
    public sealed class FoodTable : MonoBehaviour
    {
        [Tooltip("空なら全フィールド共通の表。専用の表は、フィールドの ID（village / mountain / sea / grassland）。そのフィールドの FieldStages の Field Id と同じ文字にする")]
        [SerializeField] string fieldId = "";

        [Tooltip("食感の ID と、生まれる食べ物の Prefab。食感の段階の順に並べる。足す・消すだけで、食感の数を変えられる")]
        [SerializeField] FoodRow[] rows = new FoodRow[0];

        bool _registered;

        /// <summary>この表のフィールド。空なら共通の表。</summary>
        public FieldId Field => new FieldId(fieldId);

        /// <summary>全フィールド共通の表か。</summary>
        public bool IsDefault => Field.IsEmpty;

        public IReadOnlyList<FoodRow> Rows => rows;

        /// <summary>コードから表を作る（テストや補助ツール用）。有効なら、登録し直す。</summary>
        public void Configure(string fieldId, params FoodRow[] rows)
        {
            bool wasRegistered = _registered;
            if (wasRegistered) FoodTables.Unregister(this);
            this.fieldId = fieldId ?? "";
            this.rows = rows ?? new FoodRow[0];
            if (wasRegistered) _registered = FoodTables.Register(this);
        }

        /// <summary>
        /// この表だけを引く。行がない、食感が空、または Prefab が空なら null。
        /// 同じ食感の行が重なっているときは、先の行を使う。
        /// </summary>
        public GameObject Find(TextureId texture)
        {
            if (texture.IsEmpty || rows == null) return null;
            foreach (FoodRow row in rows)
            {
                if (string.IsNullOrEmpty(row.texture)) continue;
                if (!string.Equals(row.texture, texture.Value, StringComparison.Ordinal)) continue;
                return row.food;
            }
            return null;
        }

        // その食感の行があるか（Prefab が空でもよい）。
        internal bool HasTexture(string texture)
        {
            if (string.IsNullOrEmpty(texture) || rows == null) return false;
            foreach (FoodRow row in rows)
                if (string.Equals(row.texture, texture, StringComparison.Ordinal)) return true;
            return false;
        }

        // 表の中の食い違いを集める。
        internal void CollectWarnings(List<string> into)
        {
            string label = IsDefault ? "共通の表" : "フィールド「" + fieldId + "」の表";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Length; i++)
            {
                FoodRow row = rows[i];
                int line = i + 1;
                if (string.IsNullOrEmpty(row.texture))
                {
                    into.Add(label + "の " + line + " 行目: 食感が空です（この行は使いません）。");
                    continue;
                }
                if (row.food == null)
                    into.Add(label + "の " + line + " 行目: 食感「" + row.texture + "」の Prefab が空です。");
                if (!seen.Add(row.texture))
                    into.Add(label + "の " + line + " 行目: 食感「" + row.texture + "」の行が重なっています（先の行を使います）。");
            }
        }

        // 再生していないときの、この表だけの一覧。
        string DescribeSelf()
        {
            var sb = new StringBuilder();
            sb.Append("[FoodTable] ").Append(IsDefault ? "共通の表" : "フィールド「" + fieldId + "」の表").Append("（食感 → 食べ物）");
            foreach (FoodRow row in rows)
                sb.Append('\n').Append("  ").Append(string.IsNullOrEmpty(row.texture) ? "(食感が空)" : row.texture)
                  .Append(" -> ").Append(row.food != null ? row.food.name : "(Prefab が空)");
            return sb.ToString();
        }

        [ContextMenu("対応一覧を出す")]
        void LogTable() => Debug.Log(Application.isPlaying ? FoodTables.Describe() : DescribeSelf(), this);

        void OnEnable() => _registered = FoodTables.Register(this);

        void OnDisable()
        {
            FoodTables.Unregister(this);
            _registered = false;
        }
    }
}
