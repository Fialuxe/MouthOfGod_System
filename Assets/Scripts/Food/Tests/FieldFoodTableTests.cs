using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Food.Tests
{
    // 共有シーン（Main）が使うフィールドの Prefab ごとに、食感 placeholder-a〜c が、別々の食べ物を引くこと。
    // 引けない・同じ食べ物が重なると、フィールドを変えても同じ物が出る、または何も出ない。
    public class FieldFoodTableTests
    {
        static readonly string[] Textures = { "placeholder-a", "placeholder-b", "placeholder-c" };

        // 村は欲しい物が 4 つ（かぶ・トマト・かぼちゃ・にんじん）。表にも d の行が要る。
        const string VillageFourth = "placeholder-d";

        // フィールドの ID と、Resources の Prefab。Main が使うものにそろえる。
        static readonly (string Field, string Prefab)[] Fields =
        {
            ("village", "Prefabs/Misc/Village"),
            ("grassland", "Prefabs/Misc/Grass Variant"),
            ("mountain", "Prefabs/Misc/MountVer2 Variant"),
            ("sea", "Prefabs/Sea/Sea_work"),
        };

        static FoodTable TableOf((string Field, string Prefab) field)
        {
            var prefab = Resources.Load<GameObject>(field.Prefab);
            Assert.IsNotNull(prefab, $"{field.Prefab} が Resources に無い");
            var table = prefab.GetComponentInChildren<FoodTable>(true);
            Assert.IsNotNull(table, $"{field.Prefab} に食べ物の表（FoodTable）が無い");
            return table;
        }

        [Test]
        public void 表のフィールドIDが_フィールドと一致している()
        {
            foreach (var field in Fields)
                Assert.AreEqual(field.Field, TableOf(field).Field.Value, $"{field.Prefab} の表のフィールド ID");
        }

        [Test]
        public void 各フィールドで_placeholder_aからcが_引ける()
        {
            foreach (var field in Fields)
            {
                var table = TableOf(field);
                foreach (var texture in Textures)
                    Assert.IsNotNull(table.Find(new MouthOfGod.Core.TextureId(texture)), $"{field.Field} の {texture} が引けない");
            }
        }

        [Test]
        public void 各フィールドで_placeholder_aからcは_別々の食べ物()
        {
            foreach (var field in Fields)
            {
                var table = TableOf(field);
                var names = new List<string>();
                foreach (var texture in Textures)
                    names.Add(table.Find(new MouthOfGod.Core.TextureId(texture))?.name);
                CollectionAssert.AllItemsAreUnique(names, $"{field.Field} の a〜c が重なっている: {string.Join(", ", names)}");
            }
        }

        [Test]
        public void 村の4つ目の食感dで_トマトが出る()
        {
            var table = TableOf(Fields[0]);

            Assert.AreEqual("village", Fields[0].Field);
            Assert.AreEqual("tomato", table.Find(new MouthOfGod.Core.TextureId(VillageFourth))?.name);
        }

        [Test]
        public void フィールドが違えば_同じ食感で_違う食べ物が出る()
        {
            foreach (var texture in Textures)
            {
                var foods = new List<string>();
                foreach (var field in Fields)
                    foods.Add(TableOf(field).Find(new MouthOfGod.Core.TextureId(texture))?.name);
                CollectionAssert.AllItemsAreUnique(foods, $"{texture} が、フィールドによらず同じ食べ物になっている: {string.Join(", ", foods)}");
            }
        }
    }
}
