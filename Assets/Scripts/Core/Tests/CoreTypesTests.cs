using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Core.Tests
{
    public class CoreTypesTests
    {
        [Test]
        public void SameValue_IsEqual_AndHashesMatch()
        {
            var a = new TextureId("crunchy");
            var b = new TextureId("crunchy");

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a.Equals((object)b));
        }

        [Test]
        public void DifferentValue_IsNotEqual()
        {
            Assert.IsTrue(new FoodId("apple") != new FoodId("pear"));
            Assert.IsFalse(new FoodId("apple") == new FoodId("pear"));
        }

        [Test]
        public void Case_IsSignificant()
        {
            Assert.AreNotEqual(new FieldId("Mountain"), new FieldId("mountain"));
        }

        [Test]
        public void NullEmptyAndDefault_AreTheSameEmptyId()
        {
            var fromNull = new TextureId(null);
            var fromEmpty = new TextureId("");
            TextureId fromDefault = default;

            Assert.IsTrue(fromNull.IsEmpty);
            Assert.IsTrue(fromEmpty.IsEmpty);
            Assert.IsTrue(fromDefault.IsEmpty);
            Assert.IsTrue(fromNull == fromEmpty);
            Assert.IsTrue(fromEmpty == fromDefault);
            Assert.IsTrue(fromDefault == TextureId.None);
            Assert.AreEqual(fromNull.GetHashCode(), fromDefault.GetHashCode());
            Assert.AreEqual("", fromDefault.Value);
            Assert.AreEqual("", fromDefault.ToString());
        }

        [Test]
        public void NonEmpty_IsNotEmpty_AndToStringReturnsValue()
        {
            var id = new CueId("glow_mountain");

            Assert.IsFalse(id.IsEmpty);
            Assert.AreEqual("glow_mountain", id.ToString());
            Assert.AreNotEqual(CueId.None, id);
        }

        [Test]
        public void DifferentIdTypes_AreNotEqual_EvenWithSameValue()
        {
            object food = new FoodId("x");
            object field = new FieldId("x");

            Assert.IsFalse(food.Equals(field));
            Assert.IsFalse(field.Equals(food));
        }

        [Test]
        public void Ids_WorkAsDictionaryKeys()
        {
            var map = new System.Collections.Generic.Dictionary<FoodId, int>
            {
                [new FoodId("apple")] = 1,
            };

            Assert.AreEqual(1, map[new FoodId("apple")]);
            Assert.IsFalse(map.ContainsKey(new FoodId("Apple")));
        }

        [Test]
        public void Events_KeepTheirValues()
        {
            var food = new FoodId("apple");
            var field = new FieldId("village");

            var spawned = new Spawned(food, new Vector3(1f, 2f, 3f));
            Assert.AreEqual(food, spawned.Food);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), spawned.Position);

            var delivered = new Delivered(food, field, true);
            Assert.AreEqual(food, delivered.Food);
            Assert.AreEqual(field, delivered.Field);
            Assert.IsTrue(delivered.Accepted);
            Assert.IsFalse(new Delivered(food, field, false).Accepted);

            var request = new RequestIssued(food, field);
            Assert.AreEqual(food, request.Food);
            Assert.AreEqual(field, request.Field);

            Assert.AreEqual(new CueId("c"), new CueRequested(new CueId("c")).Cue);
        }
    }
}
