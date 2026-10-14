using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

using MouthOfGod.Core;

namespace MouthOfGod.Food.Tests
{
    public class FoodCatalogTests
    {
        static FoodDefinition Make(string food, string texture)
        {
            var def = ScriptableObject.CreateInstance<FoodDefinition>();
            Set(def, "foodId", food);
            Set(def, "textureId", texture);
            return def;
        }

        static void Set(FoodDefinition def, string field, string value)
        {
            typeof(FoodDefinition)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(def, value);
        }

        static FoodCatalog Sample() => FoodCatalog.Build(new[]
        {
            Make("apple", "hard"),
            Make("pudding", "soft"),
            Make("ball", "bouncy"),
        });

        [Test]
        public void Resolve_ReturnsFoodForEachTexture()
        {
            var catalog = Sample();

            Assert.AreEqual(new FoodId("apple"), catalog.Resolve(new TextureId("hard")));
            Assert.AreEqual(new FoodId("pudding"), catalog.Resolve(new TextureId("soft")));
            Assert.AreEqual(new FoodId("ball"), catalog.Resolve(new TextureId("bouncy")));
        }

        [Test]
        public void Resolve_Unregistered_OrNone_ReturnsNone()
        {
            var catalog = Sample();

            Assert.AreEqual(FoodId.None, catalog.Resolve(new TextureId("sticky")));
            Assert.AreEqual(FoodId.None, catalog.Resolve(TextureId.None));
        }

        [Test]
        public void Resolve_IsCaseSensitive()
        {
            Assert.AreEqual(FoodId.None, Sample().Resolve(new TextureId("Hard")));
        }

        [Test]
        public void Build_Empty_ResolvesNone()
        {
            var catalog = FoodCatalog.Build(new FoodDefinition[0]);
            Assert.AreEqual(FoodId.None, catalog.Resolve(new TextureId("hard")));
        }

        [Test]
        public void Build_DuplicateFoodId_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                FoodCatalog.Build(new[] { Make("apple", "hard"), Make("apple", "soft") }));
        }

        [Test]
        public void Build_DuplicateTextureId_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                FoodCatalog.Build(new[] { Make("apple", "hard"), Make("rock", "hard") }));
        }

        [Test]
        public void Build_IdsDifferingOnlyByCase_AreNotDuplicates()
        {
            var catalog = FoodCatalog.Build(new[] { Make("apple", "hard"), Make("Apple", "Hard") });

            Assert.AreEqual(new FoodId("apple"), catalog.Resolve(new TextureId("hard")));
            Assert.AreEqual(new FoodId("Apple"), catalog.Resolve(new TextureId("Hard")));
        }

        [Test]
        public void Build_EmptyFoodId_OrNullEntry_OrNullList_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => FoodCatalog.Build(new[] { Make("", "hard") }));
            Assert.Throws<InvalidOperationException>(() => FoodCatalog.Build(new FoodDefinition[] { null }));
            Assert.Throws<ArgumentNullException>(() => FoodCatalog.Build(null));
        }

        [Test]
        public void Build_EmptyTextureId_IsSkipped_AndNotADuplicate()
        {
            var catalog = FoodCatalog.Build(new[] { Make("a", ""), Make("b", "") });
            Assert.AreEqual(FoodId.None, catalog.Resolve(TextureId.None));
        }
    }
}
