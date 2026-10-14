using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>食べ物 1 件のデータ。1 件 = 1 アセット（Assets/Resources/Data/Foods/）。食べ物と生物は同じ型で扱う。</summary>
    [CreateAssetMenu(fileName = "Food_", menuName = "MouthOfGod/Food Definition")]
    public class FoodDefinition : ScriptableObject
    {
        [SerializeField] private string foodId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [Tooltip("分類（例: 果物、魚、生き物）。自由な文字列")]
        [SerializeField] private string category;
        [Tooltip("この食べ物に対応する食感の ID")]
        [SerializeField] private string textureId;

        public FoodId FoodId => new FoodId(foodId);
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public string Category => category;
        public TextureId TextureId => new TextureId(textureId);
    }

    /// <summary>食感 → 食べ物 の対応表。FoodDefinition を集めて起動時に作る。手で編集しない。</summary>
    public class FoodCatalog
    {
        public const string ResourcesPath = "Data/Foods";

        readonly Dictionary<TextureId, FoodId> _byTexture;

        FoodCatalog(Dictionary<TextureId, FoodId> byTexture)
        {
            _byTexture = byTexture;
        }

        /// <summary>FoodId または TextureId が重複していたら InvalidOperationException。null の要素・空の FoodId も例外。空の TextureId は対応なしとして飛ばす。</summary>
        public static FoodCatalog Build(IEnumerable<FoodDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var foodIds = new HashSet<FoodId>();
            var byTexture = new Dictionary<TextureId, FoodId>();
            foreach (var def in definitions)
            {
                if (def == null)
                    throw new InvalidOperationException("FoodDefinition に null が含まれています。");
                if (def.FoodId.IsEmpty)
                    throw new InvalidOperationException($"FoodId が空の FoodDefinition があります: {def.name}");
                if (!foodIds.Add(def.FoodId))
                    throw new InvalidOperationException($"FoodId が重複しています: '{def.FoodId}' ({def.name})");
                if (def.TextureId.IsEmpty) continue;
                if (byTexture.ContainsKey(def.TextureId))
                    throw new InvalidOperationException(
                        $"TextureId が重複しています: '{def.TextureId}' ({def.name}, 先に登録: '{byTexture[def.TextureId]}')");
                byTexture.Add(def.TextureId, def.FoodId);
            }
            return new FoodCatalog(byTexture);
        }

        /// <summary>Resources/Data/Foods の全 FoodDefinition から作る。</summary>
        public static FoodCatalog LoadFromResources()
        {
            return Build(Resources.LoadAll<FoodDefinition>(ResourcesPath));
        }

        /// <summary>食感に対応する食べ物。無ければ FoodId.None。</summary>
        public FoodId Resolve(TextureId texture)
        {
            return _byTexture.TryGetValue(texture, out var food) ? food : FoodId.None;
        }
    }
}
