using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class PhasesTests
    {
        readonly List<Exception> _exceptions = new List<Exception>();
        Action<Exception> _savedException;

        [SetUp]
        public void SetUp()
        {
            Phases.Reset();
            _exceptions.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _savedException = Phases.ReportException;
            Phases.ReportException = _exceptions.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Phases.Reset();
            Phases.ReportException = _savedException;
        }

        [Test]
        public void Current_StartsEmpty()
        {
            Assert.IsTrue(Phases.Current.IsEmpty);
            Assert.AreEqual(PhaseId.None, Phases.Current);
        }

        [Test]
        public void Set_UpdatesCurrent_BeforeRaisingChanged()
        {
            PhaseId seenInHandler = PhaseId.None;
            Phases.Changed += p => seenInHandler = Phases.Current;

            Phases.Set(new PhaseId("fork"));

            Assert.AreEqual(new PhaseId("fork"), Phases.Current);
            Assert.AreEqual(new PhaseId("fork"), seenInHandler);
        }

        [Test]
        public void Set_SamePhase_DoesNotRaiseAgain()
        {
            var got = new List<PhaseId>();
            Phases.Changed += got.Add;

            Phases.Set(new PhaseId("fork"));
            Phases.Set(new PhaseId("fork"));
            Phases.Set(new PhaseId("spawn"));
            Phases.Set(PhaseId.None);

            CollectionAssert.AreEqual(new[] { new PhaseId("fork"), new PhaseId("spawn"), PhaseId.None }, got);
        }

        [Test]
        public void Set_HandlerException_DoesNotStopOtherHandlers()
        {
            var got = new List<PhaseId>();
            Phases.Changed += p => throw new InvalidOperationException("boom");
            Phases.Changed += got.Add;

            Phases.Set(new PhaseId("deliver"));

            Assert.AreEqual(1, _exceptions.Count);
            Assert.AreEqual(1, got.Count);
        }

        [Test]
        public void Reset_ClearsCurrentAndSubscribers()
        {
            var got = new List<PhaseId>();
            Phases.Changed += got.Add;
            Phases.Set(new PhaseId("fork"));

            Phases.Reset();
            Phases.Set(new PhaseId("spawn"));

            Assert.AreEqual(new PhaseId("spawn"), Phases.Current);
            Assert.AreEqual(1, got.Count); // 最初の Set の分だけ。Reset のあとの購読は消えている
        }
    }
}
