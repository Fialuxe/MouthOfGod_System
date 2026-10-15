using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class FieldsTests
    {
        sealed class FakeField : IFieldRequests
        {
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool IsOpen => false;
            public bool CanOpen => false;
            public bool Open() => false;
            public bool Open(FoodId food) => false;
            public bool Resolve(FoodId food) => false;
            public void Expire() { }
            public void Restart() { }
            public event Action<RequestIssued> Issued { add { } remove { } }
            public event Action<RequestClosed> Closed { add { } remove { } }
        }

        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            Fields.Reset();
            _exceptions.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _warnings.Clear();
            _savedException = Fields.ReportException;
            _savedWarning = Fields.ReportWarning;
            Fields.ReportException = _exceptions.Add;
            Fields.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Fields.Reset();
            Fields.ReportException = _savedException;
            Fields.ReportWarning = _savedWarning;
        }

        [Test]
        public void Register_AddsToAll_AndRaisesAdded_AndFindable()
        {
            var sea = new FakeField("sea");
            var added = new List<IFieldRequests>();
            Fields.Added += added.Add;

            Assert.IsTrue(Fields.Register(sea));

            CollectionAssert.AreEqual(new[] { sea }, Fields.All);
            CollectionAssert.AreEqual(new[] { sea }, added);
            Assert.AreSame(sea, Fields.Find(new FieldId("sea")));
            Assert.IsNull(Fields.Find(new FieldId("village")));
        }

        [Test]
        public void Register_SameIdTwice_WarnsAndKeepsTheFirst()
        {
            var first = new FakeField("village");
            var second = new FakeField("village");
            Fields.Register(first);

            Assert.IsFalse(Fields.Register(second));

            CollectionAssert.AreEqual(new[] { first }, Fields.All);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Register_EmptyId_WarnsAndDoesNotRegister()
        {
            Assert.IsFalse(Fields.Register(new FakeField("")));

            Assert.IsEmpty(Fields.All);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Register_SameObjectTwice_IsOneEntry_WithoutWarning()
        {
            var sea = new FakeField("sea");
            Fields.Register(sea);

            Assert.IsTrue(Fields.Register(sea));

            Assert.AreEqual(1, Fields.All.Count);
            Assert.IsEmpty(_warnings);
        }

        [Test]
        public void Unregister_RemovesAndRaisesRemoved_ThenTheIdCanBeUsedAgain()
        {
            var sea = new FakeField("sea");
            var removed = new List<IFieldRequests>();
            Fields.Removed += removed.Add;
            Fields.Register(sea);

            Fields.Unregister(sea);

            Assert.IsEmpty(Fields.All);
            CollectionAssert.AreEqual(new[] { sea }, removed);
            Assert.IsTrue(Fields.Register(new FakeField("sea")));
        }

        [Test]
        public void AddedHandlerThrows_OthersStillReceive()
        {
            var got = new List<IFieldRequests>();
            Fields.Added += _ => throw new InvalidOperationException("test");
            Fields.Added += got.Add;

            Fields.Register(new FakeField("sea"));

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(1, _exceptions.Count);
        }

        [Test]
        public void Reset_ClearsFieldsAndHandlers()
        {
            var added = 0;
            Fields.Added += _ => added++;
            Fields.Register(new FakeField("sea"));

            Fields.Reset();
            Fields.Register(new FakeField("village"));

            Assert.AreEqual(1, Fields.All.Count);
            Assert.AreEqual(1, added);
        }
    }
}
