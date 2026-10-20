using System;
using System.Collections.Generic;
using System.Text;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// いまシーンにある <see cref="FoodTable"/> の置き場。表は有効になると自分で <see cref="Register"/> する。
    /// 食べ物は <see cref="Resolve"/> で引く（そのフィールドの表、なければ共通の表）。
    /// </summary>
    /// <remarks>
    /// - 共通の表（Field Id が空）は 1 つだけ。同じフィールドの表も 1 つだけ。重なったら、警告して登録しない。
    /// - 共通の表はなくてもよい（なければ、専用の表だけで引く）。
    /// - static なので、Play のたびに空にする。
    /// - すべてメインスレッドから呼ぶ。
    /// </remarks>
    public static class FoodTables
    {
        static FoodTable _default;
        static readonly Dictionary<FieldId, FoodTable> _byField = new Dictionary<FieldId, FoodTable>();

        // 警告の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;

        /// <summary>
        /// 表を登録する。空 ID の表がすでにある、または同じフィールドの表がすでにあるなら、登録せずに警告を出して false。
        /// 同じ表を 2 回登録しても 1 つ。
        /// </summary>
        public static bool Register(FoodTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            FieldId id = table.Field;
            if (id.IsEmpty)
            {
                if (_default != null && !ReferenceEquals(_default, table))
                {
                    ReportWarning("[FoodTables] 共通の表（Field Id が空）はもう登録されているので、" + table + " は登録しません。");
                    return false;
                }
                _default = table;
                return true;
            }

            FoodTable existing;
            if (_byField.TryGetValue(id, out existing) && existing != null && !ReferenceEquals(existing, table))
            {
                ReportWarning("[FoodTables] フィールド「" + id + "」の表はもう登録されているので、" + table + " は登録しません。");
                return false;
            }
            _byField[id] = table;
            return true;
        }

        /// <summary>登録を外す。登録されていなければ何もしない。</summary>
        public static void Unregister(FoodTable table)
        {
            if (ReferenceEquals(table, null)) return;
            if (ReferenceEquals(_default, table)) _default = null;

            bool found = false;
            FieldId key = default(FieldId);
            foreach (KeyValuePair<FieldId, FoodTable> pair in _byField)
            {
                if (!ReferenceEquals(pair.Value, table)) continue;
                key = pair.Key;
                found = true;
                break;
            }
            if (found) _byField.Remove(key);
        }

        /// <summary>
        /// 食感から、生まれる食べ物の Prefab を引く。そのフィールドの表 → 共通の表 → null の順。
        /// フィールドが空、または専用の表がないフィールドは、共通の表だけで引く。食感が空なら null。
        /// </summary>
        public static GameObject Resolve(FieldId field, TextureId texture)
        {
            if (texture.IsEmpty) return null;

            FoodTable specific;
            if (!field.IsEmpty && _byField.TryGetValue(field, out specific) && specific != null)
            {
                GameObject fromField = specific.Find(texture);
                if (fromField != null) return fromField;
            }

            if (_default != null)
            {
                GameObject fromDefault = _default.Find(texture);
                if (fromDefault != null) return fromDefault;
            }
            return null;
        }

        /// <summary>食べ物の ID。Prefab のファイル名。</summary>
        public static FoodId IdOf(GameObject prefab) => prefab == null ? FoodId.None : new FoodId(prefab.name);

        /// <summary>
        /// 表の食い違いを集める。無ければ空。共通の表がないことは、食い違いに入れない。
        /// フィールドの欲しい物（FieldStep）との突き合わせは、Food が Village を参照できないため、ここではしない。
        /// </summary>
        public static IReadOnlyList<string> Validate()
        {
            var warnings = new List<string>();
            if (_default != null) _default.CollectWarnings(warnings);

            foreach (FieldId id in SortedFieldIds())
            {
                FoodTable table = _byField[id];
                if (table == null) continue;
                table.CollectWarnings(warnings);
                if (_default == null) continue;
                foreach (FoodRow row in table.Rows)
                {
                    if (string.IsNullOrEmpty(row.texture) || _default.HasTexture(row.texture)) continue;
                    warnings.Add("フィールド「" + id + "」の表の食感「" + row.texture + "」が、共通の表にありません。");
                }
            }
            return warnings;
        }

        /// <summary>
        /// 登録されている表の一覧（フィールド × 食感）。専用の表にない食感は、共通の表の食べ物に「(共通)」を付けて出す。
        /// </summary>
        public static string Describe()
        {
            var tables = new List<FoodTable>();
            if (_default != null) tables.Add(_default);
            foreach (FieldId id in SortedFieldIds())
                if (_byField[id] != null) tables.Add(_byField[id]);
            if (tables.Count == 0) return "[FoodTables] 表がありません。";

            var textures = new List<string>();
            foreach (FoodTable source in tables)
                foreach (FoodRow row in source.Rows)
                    if (!string.IsNullOrEmpty(row.texture) && !textures.Contains(row.texture)) textures.Add(row.texture);

            var sb = new StringBuilder("[FoodTables] 対応一覧（食感 → 食べ物）\nフィールド");
            foreach (string texture in textures) sb.Append(" | ").Append(texture);

            foreach (FoodTable table in tables)
            {
                sb.Append('\n').Append(table.IsDefault ? "(共通)" : table.Field.Value);
                foreach (string texture in textures)
                {
                    var id = new TextureId(texture);
                    GameObject own = table.Find(id);
                    string cell;
                    if (own != null) cell = own.name;
                    else
                    {
                        GameObject inherited = (table.IsDefault || _default == null) ? null : _default.Find(id);
                        cell = inherited != null ? inherited.name + " (共通)" : "(なし)";
                    }
                    sb.Append(" | ").Append(cell);
                }
            }
            return sb.ToString();
        }

        static List<FieldId> SortedFieldIds()
        {
            var ids = new List<FieldId>(_byField.Keys);
            ids.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return ids;
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の登録を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            _default = null;
            _byField.Clear();
        }
    }
}
