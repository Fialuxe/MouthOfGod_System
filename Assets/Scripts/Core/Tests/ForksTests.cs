using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class ForksTests
    {
        sealed class FakeFork : IFork
        {
            public bool IsLoaded => false;
            public bool InMouth => false;
            public event Action Pierced { add { } remove { } }
            public event Action MouthEntered { add { } remove { } }
            public event Action MouthLeft { add { } remove { } }
            public event Action Emptied { add { } remove { } }
        }

        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            Forks.Reset();
            _exceptions.Clear();
            _warnings.Clear();
            _savedException = Forks.ReportException;
            _savedWarning = Forks.ReportWarning;
            Forks.ReportException = _exceptions.Add;
            Forks.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Forks.Reset();
            Forks.ReportException = _savedException;
            Forks.ReportWarning = _savedWarning;
        }

        [Test]
        public void WhenReady_BeforeRegister_IsCalledOnRegister()
        {
            IFork received = null;
            Forks.WhenReady(f => received = f);
            Assert.IsNull(received);

            var fork = new FakeFork();
            Forks.Register(fork);
            Assert.AreSame(fork, received);
        }

        [Test]
        public void WhenReady_AfterRegister_IsCalledAtOnce()
        {
            var fork = new FakeFork();
            Forks.Register(fork);

            IFork received = null;
            Forks.WhenReady(f => received = f);
            Assert.AreSame(fork, received);
        }

        [Test]
        public void CancelWhenReady_StopsTheCallback()
        {
            var calls = 0;
            Action<IFork> callback = _ => calls++;
            Forks.WhenReady(callback);
            Forks.CancelWhenReady(callback);

            Forks.Register(new FakeFork());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Register_ASecondFork_WarnsAndKeepsTheFirst()
        {
            var first = new FakeFork();
            Forks.Register(first);

            Assert.IsFalse(Forks.Register(new FakeFork()));
            Assert.AreSame(first, Forks.Current);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Register_ACallbackThrows_StillCallsTheRest()
        {
            var calls = 0;
            Forks.WhenReady(_ => throw new InvalidOperationException());
            Forks.WhenReady(_ => calls++);

            Forks.Register(new FakeFork());
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _exceptions.Count);
        }

        [Test]
        public void Unregister_OnlyRemovesTheSameFork()
        {
            var fork = new FakeFork();
            Forks.Register(fork);

            Forks.Unregister(new FakeFork());
            Assert.AreSame(fork, Forks.Current);

            Forks.Unregister(fork);
            Assert.IsNull(Forks.Current);
        }
    }
}
