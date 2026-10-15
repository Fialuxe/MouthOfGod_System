using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Village.Tests
{
    // FieldStages と FieldStep のつながりを確かめる（決まりそのものは StepProgressTests）。EditMode なので Start は呼ばれない。
    public class FieldStagesTests
    {
        GameObject _root;
        FieldStages _field;
        FieldStep _sprout, _crops;
        GameObject _withered, _sproutChild, _cropsChild;
        VillagerMood _farmer, _elder;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestField");
            _field = _root.AddComponent<FieldStages>();
            _farmer = NewChild("Farmer", _root.transform).AddComponent<VillagerMood>();
            _elder = NewChild("Elder", _root.transform).AddComponent<VillagerMood>();
            _withered = NewChild("Withered", _root.transform);

            var steps = NewChild("Steps", _root.transform).transform;
            _sprout = NewChild("Sprout", steps).AddComponent<FieldStep>();
            _crops = NewChild("Crops", steps).AddComponent<FieldStep>();
            _sproutChild = NewChild("SproutChild", _sprout.transform);
            _cropsChild = NewChild("CropsChild", _crops.transform);

            _sprout.Food = "test-carrot";
            _sprout.SetHelped(_farmer);
            _sprout.SetHideWhenDone(_withered);
            _crops.Food = "test-pumpkin";
            _crops.SetHelped(_elder, _farmer);
            _crops.SetHideWhenDone(_sproutChild);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go;
        }

        [Test]
        public void Open_RaisesIssued_WithFirstStepWantAndHelpedPerson()
        {
            var got = new List<RequestIssued>();
            _field.Issued += got.Add;

            Assert.IsTrue(_field.Open());

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(new FoodId("test-carrot"), got[0].Food);
            Assert.AreEqual(new FieldId("village"), got[0].Field);
            Assert.AreEqual(_farmer.transform, got[0].Anchor);
            Assert.IsFalse(_field.Open());
        }

        [Test]
        public void Resolve_WantedFood_ShowsChildrenHidesListed_AndCalmsOnlyFullyHelped()
        {
            var closed = new List<RequestClosed>();
            _field.Closed += closed.Add;
            _field.Open();

            Assert.IsFalse(_field.Resolve(new FoodId("test-pumpkin")));
            Assert.IsTrue(_field.Resolve(new FoodId("test-carrot")));

            Assert.AreEqual(1, closed.Count);
            Assert.IsTrue(closed[0].Resolved);
            Assert.IsTrue(_field.IsDone(_sprout));
            Assert.IsTrue(_sproutChild.activeSelf);
            Assert.IsFalse(_cropsChild.activeSelf);
            Assert.IsFalse(_withered.activeSelf);
            Assert.IsTrue(_farmer.IsWorried, "名前があるステップ（Crops）がまだ");
            Assert.IsTrue(_elder.IsWorried);
        }

        [Test]
        public void LaterStepHides_ObjectShownByEarlierStep()
        {
            _field.Open();
            _field.Resolve(new FoodId("test-carrot"));
            _field.Open();
            _field.Resolve(new FoodId("test-pumpkin"));

            Assert.IsTrue(_field.IsFullyRecovered);
            Assert.IsFalse(_sproutChild.activeSelf);
            Assert.IsTrue(_cropsChild.activeSelf);
            Assert.IsFalse(_farmer.IsWorried);
            Assert.IsFalse(_elder.IsWorried);
            Assert.IsFalse(_field.CanOpen);
        }

        [Test]
        public void OpenFromClosed_OpensTheNextStepWant()
        {
            var issued = new List<RequestIssued>();
            _field.Issued += issued.Add;
            _field.Closed += _ => _field.Open();
            _field.Open();

            _field.Resolve(new FoodId("test-carrot"));

            Assert.AreEqual(2, issued.Count);
            Assert.AreEqual(new FoodId("test-pumpkin"), issued[1].Food);
            Assert.AreEqual(_elder.transform, issued[1].Anchor);
        }

        [Test]
        public void OpenFromStepDone_IssuedAndClosedStillAlternate()
        {
            var events = new List<string>();
            _field.Issued += r => events.Add("Issued " + r.Food);
            _field.Closed += r => events.Add("Closed " + r.Food);
            _field.StepDone += _ => events.Add("StepDone");
            _field.StepDone += _ => _field.Open();
            _field.Open();

            _field.Resolve(new FoodId("test-carrot"));

            CollectionAssert.AreEqual(
                new[] { "Issued test-carrot", "Closed test-carrot", "StepDone", "Issued test-pumpkin" }, events);
        }

        [Test]
        public void Restart_RaisesClosedThenRestarted()
        {
            var events = new List<string>();
            _field.Open();
            _field.Closed += _ => events.Add("Closed");
            _field.Restarted += () => events.Add("Restarted");

            _field.Restart();

            CollectionAssert.AreEqual(new[] { "Closed", "Restarted" }, events);
        }

        [Test]
        public void InsertingAStep_InHierarchy_ChangesTheOrder_WithoutNumbers()
        {
            var between = NewChild("Between", _sprout.transform.parent).AddComponent<FieldStep>();
            between.transform.SetSiblingIndex(1);
            between.Food = "test-turnip";
            var field = new GameObject("Other").AddComponent<FieldStages>();
            _sprout.transform.parent.SetParent(field.transform); // 新しいフィールドは、いまの並びを読む
            var issued = new List<RequestIssued>();
            field.Issued += issued.Add;

            field.Open();
            field.Resolve(new FoodId("test-carrot"));
            field.Open();

            Assert.AreEqual(new FoodId("test-turnip"), issued[1].Food);
            Object.DestroyImmediate(field.gameObject);
        }

        [Test]
        public void Restart_OpenFromClosed_OpensTheFirstStepAgain()
        {
            _field.Open();
            _field.Resolve(new FoodId("test-carrot"));
            _field.Open();
            var issued = new List<RequestIssued>();
            _field.Issued += issued.Add;
            _field.Closed += _ => _field.Open();

            _field.Restart();

            Assert.AreEqual(1, issued.Count);
            Assert.AreEqual(new FoodId("test-carrot"), issued[0].Food);
            Assert.IsTrue(_field.IsOpen);
        }

        [Test]
        public void Expire_ClosesWithoutDoing()
        {
            var closed = new List<RequestClosed>();
            _field.Closed += closed.Add;
            _field.Open();

            _field.Expire();

            Assert.IsFalse(_field.IsOpen);
            Assert.AreEqual(0, _field.DoneCount);
            Assert.AreEqual(1, closed.Count);
            Assert.IsFalse(closed[0].Resolved);
            _field.Expire();
            Assert.AreEqual(1, closed.Count);
        }

        [Test]
        public void Restart_UndoesStepsAndClosesTheOpenWant()
        {
            var closed = new List<RequestClosed>();
            _field.Open();
            _field.Resolve(new FoodId("test-carrot"));
            _field.Open();
            _field.Closed += closed.Add;

            _field.Restart();

            Assert.AreEqual(0, _field.DoneCount);
            Assert.AreEqual(1, closed.Count);
            Assert.IsFalse(closed[0].Resolved);
            Assert.IsFalse(_sproutChild.activeSelf);
            Assert.IsTrue(_withered.activeSelf);
            Assert.IsTrue(_farmer.IsWorried);
        }
    }
}
