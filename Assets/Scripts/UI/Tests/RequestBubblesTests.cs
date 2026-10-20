using System;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MouthOfGod.UI.Tests
{
    public class RequestBubblesTests
    {
        sealed class FakeField : IFieldRequests
        {
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool IsOpen => false;
            public bool CanOpen => false;
            public bool Open() => false;
            public bool Open(FoodId food) => false;
            public DeliveryKind Receive(FoodId food) => DeliveryKind.Foreign;
            public void Expire() { }
            public void Restart() { }
            public event Action<RequestIssued> Issued;
            public event Action<RequestClosed> Closed;
            public event Action<Delivered> Received { add { } remove { } }

            public void RaiseIssued(Transform anchor, string food = "fish") =>
                Issued?.Invoke(new RequestIssued(new FoodId(food), Field, anchor));

            public void RaiseClosed(bool resolved, string food = "fish") =>
                Closed?.Invoke(new RequestClosed(new FoodId(food), Field, resolved));
        }

        GameObject _host;
        RequestBubbles _bubbles;
        GameObject _anchor;
        FakeField _field;

        [SetUp]
        public void SetUp()
        {
            _field = new FakeField("sea");
            _anchor = new GameObject("Anchor");
        }

        [TearDown]
        public void TearDown()
        {
            if (_bubbles != null) _bubbles.StopListening(); // EditMode では OnDisable も呼ばれないので、ここで外す
            _bubbles = null;
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_anchor);
            Fields.Unregister(_field);
            RequestBubbleText.NameLookup = null;
        }

        // EditMode なので OnEnable は呼ばれない。Listen を直接呼ぶ。
        RequestBubbles CreateBubbles()
        {
            _host = new GameObject("Bubbles");
            _bubbles = _host.AddComponent<RequestBubbles>();
            _bubbles.Listen();
            return _bubbles;
        }

        [Test]
        public void DisplayName_Fish_IsCapitalized()
        {
            Assert.AreEqual("Fish", RequestBubbleText.DisplayName(new FoodId("fish")));
            Assert.AreEqual("Sea bass", RequestBubbleText.DisplayName(new FoodId("sea_bass")));
            Assert.AreEqual("A b", RequestBubbleText.DisplayName(new FoodId("a-b")));
        }

        [Test]
        public void DisplayName_EmptyId_DoesNotThrow()
        {
            Assert.AreEqual("?", RequestBubbleText.DisplayName(FoodId.None));
            Assert.AreEqual("?", RequestBubbleText.DisplayName(new FoodId("")));
            Assert.AreEqual("?", RequestBubbleText.DisplayName(new FoodId("  ")));
        }

        [Test]
        public void Resolve_UsesReplacedLookup_AndFallsBackWhenItReturnsEmpty()
        {
            Assert.AreEqual("Fish", RequestBubbleText.Resolve(new FoodId("fish"), null));
            Assert.AreEqual("Sakana", RequestBubbleText.Resolve(new FoodId("fish"), _ => "Sakana"));
            Assert.AreEqual("Fish", RequestBubbleText.Resolve(new FoodId("fish"), _ => null));
            Assert.AreEqual("Fish", RequestBubbleText.Resolve(new FoodId("fish"), _ => ""));

            RequestBubbleText.NameLookup = f => f.Value.ToUpperInvariant();
            Assert.AreEqual("FISH", RequestBubbleText.Resolve(new FoodId("fish")));
        }

        [Test]
        public void IconPath_UsesTheFoodId_AndIsNullForAnEmptyId()
        {
            Assert.AreEqual("Icons/Food/pumpkin", RequestBubbleText.IconPath(new FoodId("pumpkin")));
            Assert.IsNull(RequestBubbleText.IconPath(FoodId.None));
        }

        [Test]
        public void Issued_ShowsBubble_AndClosed_RemovesIt()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();

            _field.RaiseIssued(_anchor.transform);
            Assert.IsTrue(bubbles.HasBubble(_field.Field));
            Assert.AreEqual(1, bubbles.OpenCount);

            _field.RaiseClosed(true);
            Assert.IsFalse(bubbles.HasBubble(_field.Field));
            Assert.AreEqual(0, bubbles.OpenCount);
        }

        [Test]
        public void Closed_KeepsBubbleWhileClosing_EvenWhenOpenCountIsZero()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();
            _field.RaiseIssued(_anchor.transform);

            _field.RaiseClosed(true);

            Assert.AreEqual(0, bubbles.OpenCount);
            Assert.AreEqual(1, bubbles.ClosingCount);
        }

        [Test]
        public void Closed_ColorDiffersBetweenResolvedAndUnresolved()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();

            _field.RaiseIssued(_anchor.transform);
            _field.RaiseClosed(true);
            _field.RaiseIssued(_anchor.transform);
            _field.RaiseClosed(false);

            Assert.AreEqual(2, bubbles.ClosingCount);
            // 背景のシェーダーが無い環境では色が取れない（透明）ので、そのときは比べない。
            Color resolved = bubbles.GetClosingColor(0);
            Color unresolved = bubbles.GetClosingColor(1);
            if (resolved.a > 0f) Assert.AreNotEqual(resolved, unresolved);
        }

        [Test]
        public void Closed_ForUnknownField_DoesNotThrow()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();

            Assert.DoesNotThrow(() => _field.RaiseClosed(true));
            Assert.AreEqual(0, bubbles.ClosingCount);
        }

        [Test]
        public void FieldRemoved_RemovesItsBubble()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();
            _field.RaiseIssued(_anchor.transform);

            Fields.Unregister(_field);

            Assert.IsFalse(bubbles.HasBubble(_field.Field));
            Assert.AreEqual(0, bubbles.OpenCount);
        }

        [Test]
        public void IssuedTwice_KeepsOneBubblePerField()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();

            _field.RaiseIssued(_anchor.transform);
            _field.RaiseIssued(_anchor.transform, "carrot");

            Assert.AreEqual(1, bubbles.OpenCount);
        }

        [Test]
        public void Issued_WithoutAnchor_WarnsAndShowsNothing()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Anchor"));
            _field.RaiseIssued(null);

            Assert.AreEqual(0, bubbles.OpenCount);
        }

        [Test]
        public void FieldRegisteredLater_IsFollowed()
        {
            var bubbles = CreateBubbles();
            Fields.Register(_field);

            _field.RaiseIssued(_anchor.transform);

            Assert.AreEqual(1, bubbles.OpenCount);
        }

        [Test]
        public void Disabled_StopsListening_AndClearsBubbles()
        {
            Fields.Register(_field);
            var bubbles = CreateBubbles();
            _field.RaiseIssued(_anchor.transform);
            Assert.AreEqual(1, bubbles.OpenCount);

            bubbles.StopListening(); // OnDisable が呼ぶもの。EditMode では enabled を外しても OnDisable が呼ばれない
            Assert.AreEqual(0, bubbles.OpenCount);

            _field.RaiseIssued(_anchor.transform);
            Assert.AreEqual(0, bubbles.OpenCount);
        }
    }
}
