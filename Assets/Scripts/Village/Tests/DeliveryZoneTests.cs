using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Village.Tests
{
    // 食べ物が入ったときの届け方を確かめる。当たり判定そのものは Unity の仕事なので、Deliver を直接呼ぶ。
    public class DeliveryZoneTests
    {
        sealed class FakeFood : MonoBehaviour, IFoodItem
        {
            public FoodId Food { get; set; }
        }

        GameObject _root;
        FieldStages _field;
        DeliveryZone _zone;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestField");
            _field = _root.AddComponent<FieldStages>();
            _field.FieldId = "village";
            var step = new GameObject("Step").AddComponent<FieldStep>();
            step.transform.SetParent(new GameObject("Steps").transform);
            step.transform.parent.SetParent(_root.transform);
            step.Food = "test-carrot";
            _root.AddComponent<BoxCollider>();
            _zone = _root.AddComponent<DeliveryZone>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        static FakeFood NewFood(string id)
        {
            var food = new GameObject(id).AddComponent<FakeFood>();
            food.Food = new FoodId(id);
            return food;
        }

        [Test]
        public void Deliver_NotFood_DoesNothing()
        {
            var thing = new GameObject("Stone");
            Assert.IsFalse(_zone.Deliver(thing.transform));
            Object.DestroyImmediate(thing);
        }

        [Test]
        public void Deliver_RequestedFood_ReceivesAndConsumesIt()
        {
            _field.Open();
            var food = NewFood("test-carrot");
            DeliveryKind? kind = null;
            _field.Received += d => kind = d.Kind;

            Assert.IsTrue(_zone.Deliver(food.transform));

            Assert.AreEqual(DeliveryKind.Requested, kind);
            Assert.IsTrue(food == null, "頼まれた物は消える");
        }

        [Test]
        public void Deliver_ForeignFood_ReceivesAndKeepsItWithoutCountingTwice()
        {
            var food = NewFood("test-pig");
            var count = 0;
            _field.Received += _ => count++;

            Assert.IsTrue(_zone.Deliver(food.transform));
            Assert.IsFalse(_zone.Deliver(food.transform), "同じ食べ物は 2 回届けない");

            Assert.AreEqual(1, count);
            Assert.IsFalse(food == null, "頼まれていない物は残る");
            Object.DestroyImmediate(food.gameObject);
        }
    }
}
