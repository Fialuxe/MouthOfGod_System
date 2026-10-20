using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;

namespace MouthOfGod.Fork.Tests
{
    public class ForkLogicTests
    {
        const float Enter = 0.06f;
        const float Exit = 0.09f;

        readonly List<string> _events = new List<string>();
        ForkLogic _logic;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _logic = new ForkLogic(Enter, Exit);
            _logic.Pierced += () => _events.Add("pierced");
            _logic.MouthEntered += () => _events.Add("entered");
            _logic.MouthLeft += () => _events.Add("left");
            _logic.Emptied += () => _events.Add("emptied");
        }

        [Test]
        public void Pierce_LoadsOnce()
        {
            Assert.IsTrue(_logic.Pierce());
            Assert.IsTrue(_logic.IsLoaded);

            Assert.IsFalse(_logic.Pierce()); // かけらが付いている間は、もう刺さない
            CollectionAssert.AreEqual(new[] { "pierced" }, _events);
        }

        [Test]
        public void UpdateMouth_EntersAtTheEnterRadius()
        {
            _logic.UpdateMouth(0.1f, false);
            Assert.IsFalse(_logic.InMouth);

            _logic.UpdateMouth(Enter, false);
            Assert.IsTrue(_logic.InMouth);
            CollectionAssert.AreEqual(new[] { "entered" }, _events);
        }

        [Test]
        public void UpdateMouth_StaysInsideUntilTheExitRadius()
        {
            _logic.UpdateMouth(0.05f, false);
            _logic.UpdateMouth(0.08f, false); // Enter より遠いが、Exit より近い
            Assert.IsTrue(_logic.InMouth);

            _logic.UpdateMouth(0.1f, false);
            Assert.IsFalse(_logic.InMouth);
            CollectionAssert.AreEqual(new[] { "entered", "left" }, _events);
        }

        [Test]
        public void UpdateMouth_Forced_IsInsideAtAnyDistance()
        {
            _logic.UpdateMouth(float.PositiveInfinity, true);
            Assert.IsTrue(_logic.InMouth);

            _logic.UpdateMouth(float.PositiveInfinity, false);
            Assert.IsFalse(_logic.InMouth);
        }

        [Test]
        public void ExitRadiusSmallerThanEnter_UsesTheEnterRadius()
        {
            var logic = new ForkLogic(0.06f, 0.01f);
            logic.UpdateMouth(0.05f, false);
            logic.UpdateMouth(0.055f, false);
            Assert.IsTrue(logic.InMouth);
        }

        [Test]
        public void Confirm_EmptiesWhenLoaded()
        {
            _logic.Pierce();
            _logic.Confirm(new TextureId("placeholder-a"));

            Assert.IsFalse(_logic.IsLoaded);
            CollectionAssert.AreEqual(new[] { "pierced", "emptied" }, _events);
            Assert.IsTrue(_logic.Pierce()); // また刺せる
        }

        [Test]
        public void Confirm_BeforeBiting_DoesNotEmpty()
        {
            _logic.Pierce();
            _logic.Confirm(TextureId.None);
            Assert.IsTrue(_logic.IsLoaded);
        }

        [Test]
        public void Confirm_WithoutAPiece_DoesNothing()
        {
            _logic.Confirm(new TextureId("placeholder-a"));
            CollectionAssert.IsEmpty(_events);
        }
    }
}
