using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Food.Tests
{
    // 食感 → 食べ物の引き方と、表の登録（R1〜R3、R11、R12）。EditMode なので OnEnable は呼ばれない。登録は Register を直接呼ぶ。
    public class FoodTablesTests
    {
        readonly List<GameObject> _created = new List<GameObject>();
        readonly List<string> _warnings = new List<string>();
        System.Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            FoodTables.Reset();
            _created.Clear();
            _warnings.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _savedWarning = FoodTables.ReportWarning;
            FoodTables.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            FoodTables.Reset();
            FoodTables.ReportWarning = _savedWarning;
            foreach (GameObject go in _created)
                if (go != null) Object.DestroyImmediate(go);
        }

        GameObject Prefab(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        FoodTable Table(string fieldId, params FoodRow[] rows)
        {
            var go = new GameObject("Table-" + (fieldId == "" ? "default" : fieldId));
            _created.Add(go);
            FoodTable table = go.AddComponent<FoodTable>();
            table.Configure(fieldId, rows);
            return table;
        }

        // 共通の表: a=apple, b=fish, c=cow。草原の表: b=pig
        void SetUpExample()
        {
            FoodTables.Register(Table("", new FoodRow("a", Prefab("apple")), new FoodRow("b", Prefab("fish")), new FoodRow("c", Prefab("cow"))));
            FoodTables.Register(Table("grassland", new FoodRow("b", Prefab("pig"))));
        }

        static string Name(GameObject go) => go == null ? null : go.name;

        [Test]
        public void Resolve_PrefersFieldTable_ThenDefault_ThenNull()
        {
            SetUpExample();

            Assert.AreEqual("pig", Name(FoodTables.Resolve(new FieldId("grassland"), new TextureId("b"))));
            Assert.AreEqual("apple", Name(FoodTables.Resolve(new FieldId("grassland"), new TextureId("a"))));
            Assert.IsNull(FoodTables.Resolve(new FieldId("sea"), new TextureId("z")));
        }

        [Test]
        public void Resolve_NoneOrFieldWithoutTable_UsesDefaultOnly()
        {
            SetUpExample();

            Assert.AreEqual("fish", Name(FoodTables.Resolve(FieldId.None, new TextureId("b"))));
            Assert.AreEqual("fish", Name(FoodTables.Resolve(new FieldId("sea"), new TextureId("b"))));
        }

        [Test]
        public void Resolve_WithoutDefaultTable_UsesFieldTableOnly_AndNeverFailsWithoutAnyTable()
        {
            Assert.IsNull(FoodTables.Resolve(new FieldId("grassland"), new TextureId("b"))); // 表が 1 つもなくても落ちない

            FoodTables.Register(Table("grassland", new FoodRow("b", Prefab("pig"))));

            Assert.AreEqual("pig", Name(FoodTables.Resolve(new FieldId("grassland"), new TextureId("b"))));
            Assert.IsNull(FoodTables.Resolve(FieldId.None, new TextureId("b")));
            Assert.IsNull(FoodTables.Resolve(new FieldId("grassland"), new TextureId("a")));
            Assert.IsEmpty(FoodTables.Validate()); // 共通の表がないことは、食い違いに入れない
        }

        [Test]
        public void Resolve_EmptyTexture_ReturnsNull()
        {
            SetUpExample();

            Assert.IsNull(FoodTables.Resolve(FieldId.None, TextureId.None));
        }

        [Test]
        public void Textures_AreFreeStrings_CaseSensitive_WithNoLimit()
        {
            FoodTables.Register(Table("",
                new FoodRow("A", Prefab("upper")), new FoodRow("a", Prefab("lower")),
                new FoodRow("c", Prefab("c")), new FoodRow("d", Prefab("d")), new FoodRow("e", Prefab("e"))));

            Assert.AreEqual("upper", Name(FoodTables.Resolve(FieldId.None, new TextureId("A"))));
            Assert.AreEqual("lower", Name(FoodTables.Resolve(FieldId.None, new TextureId("a"))));
            Assert.AreEqual("e", Name(FoodTables.Resolve(FieldId.None, new TextureId("e"))));
        }

        [Test]
        public void DuplicateTextureRows_UseTheFirstRow()
        {
            FoodTables.Register(Table("", new FoodRow("a", Prefab("first")), new FoodRow("a", Prefab("second"))));

            Assert.AreEqual("first", Name(FoodTables.Resolve(FieldId.None, new TextureId("a"))));
        }

        [Test]
        public void Register_SecondDefaultTable_IsRefused_WithWarning()
        {
            FoodTable first = Table("", new FoodRow("a", Prefab("apple")));
            FoodTable second = Table("", new FoodRow("a", Prefab("other")));

            Assert.IsTrue(FoodTables.Register(first));
            Assert.IsTrue(FoodTables.Register(first)); // 同じ表は 2 回でも 1 つ
            Assert.IsFalse(FoodTables.Register(second));
            Assert.AreEqual(1, _warnings.Count);
            Assert.AreEqual("apple", Name(FoodTables.Resolve(FieldId.None, new TextureId("a"))));
        }

        [Test]
        public void Register_SecondTableForSameField_IsRefused_WithWarning()
        {
            FoodTable first = Table("sea", new FoodRow("a", Prefab("fish")));
            FoodTable second = Table("sea", new FoodRow("a", Prefab("crab")));

            Assert.IsTrue(FoodTables.Register(first));
            Assert.IsFalse(FoodTables.Register(second));
            Assert.AreEqual(1, _warnings.Count);
            StringAssert.Contains("sea", _warnings[0]);
            Assert.AreEqual("fish", Name(FoodTables.Resolve(new FieldId("sea"), new TextureId("a"))));
        }

        [Test]
        public void Unregister_RemovesTheTable_AndALaterTableCanTakeItsPlace()
        {
            FoodTable first = Table("sea", new FoodRow("a", Prefab("fish")));
            FoodTable second = Table("sea", new FoodRow("a", Prefab("crab")));
            FoodTables.Register(first);

            FoodTables.Unregister(first);
            Assert.IsNull(FoodTables.Resolve(new FieldId("sea"), new TextureId("a")));
            Assert.IsTrue(FoodTables.Register(second));
            Assert.AreEqual("crab", Name(FoodTables.Resolve(new FieldId("sea"), new TextureId("a"))));
        }

        [Test]
        public void Reset_ClearsEveryRegistration()
        {
            SetUpExample();

            FoodTables.Reset();

            Assert.IsNull(FoodTables.Resolve(FieldId.None, new TextureId("a")));
            Assert.IsNull(FoodTables.Resolve(new FieldId("grassland"), new TextureId("b")));
        }

        [Test]
        public void IdOf_IsThePrefabFileName()
        {
            Assert.AreEqual(new FoodId("pig"), FoodTables.IdOf(Prefab("pig")));
            Assert.AreEqual(FoodId.None, FoodTables.IdOf(null));
        }

        [Test]
        public void Validate_ReportsEmptyTexture_EmptyPrefab_AndDuplicateRows()
        {
            FoodTables.Register(Table("",
                new FoodRow("", Prefab("x")), new FoodRow("a", null), new FoodRow("b", Prefab("fish")), new FoodRow("b", Prefab("fish2"))));

            IReadOnlyList<string> warnings = FoodTables.Validate();

            Assert.AreEqual(3, warnings.Count);
            StringAssert.Contains("1 行目", warnings[0]);
            StringAssert.Contains("2 行目", warnings[1]);
            StringAssert.Contains("4 行目", warnings[2]);
        }

        [Test]
        public void Validate_ReportsFieldTextureMissingFromDefault()
        {
            FoodTables.Register(Table("", new FoodRow("a", Prefab("apple"))));
            FoodTables.Register(Table("sea", new FoodRow("z", Prefab("crab"))));

            IReadOnlyList<string> warnings = FoodTables.Validate();

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("sea", warnings[0]);
            StringAssert.Contains("z", warnings[0]);
        }

        [Test]
        public void Describe_ShowsInheritedCellsWithDefaultMark()
        {
            SetUpExample();

            string text = FoodTables.Describe();

            StringAssert.Contains("(共通) | apple | fish | cow", text);
            StringAssert.Contains("grassland | apple (共通) | pig | cow (共通)", text);
        }

        [Test]
        public void Describe_WithoutAnyTable_SaysSo()
        {
            StringAssert.Contains("表がありません", FoodTables.Describe());
        }
    }
}
