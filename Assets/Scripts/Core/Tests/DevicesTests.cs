using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class DevicesTests
    {
        sealed class FakeDevice : IDevice
        {
            public TextureId Current => TextureId.None;
            public event Action<TextureId> TextureChanged { add { } remove { } }
            public event Action<TextureId> Confirmed { add { } remove { } }
            public event Action Bitten { add { } remove { } }
        }

        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            Devices.Reset();
            _savedException = Devices.ReportException;
            _savedWarning = Devices.ReportWarning;
            Devices.ReportException = _exceptions.Add;
            Devices.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Devices.Reset();
            Devices.ReportException = _savedException;
            Devices.ReportWarning = _savedWarning;
        }

        [Test]
        public void WhenReady_BeforeRegister_IsCalledOnRegister()
        {
            IDevice got = null;
            Devices.WhenReady(d => got = d);
            Assert.IsNull(got);

            var device = new FakeDevice();
            Assert.IsTrue(Devices.Register(device));
            Assert.AreSame(device, got);
            Assert.AreSame(device, Devices.Current);
        }

        [Test]
        public void WhenReady_AfterRegister_IsCalledImmediately()
        {
            var device = new FakeDevice();
            Devices.Register(device);

            IDevice got = null;
            Devices.WhenReady(d => got = d);
            Assert.AreSame(device, got);
        }

        [Test]
        public void WhenReady_IsCalledOnlyOnce()
        {
            var calls = 0;
            Devices.WhenReady(_ => calls++);
            var device = new FakeDevice();
            Devices.Register(device);
            Devices.Unregister(device);
            Devices.Register(new FakeDevice());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void CancelWhenReady_IsNotCalled()
        {
            var calls = 0;
            Action<IDevice> callback = _ => calls++;
            Devices.WhenReady(callback);
            Devices.CancelWhenReady(callback);
            Devices.Register(new FakeDevice());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Register_Second_IsRejected_AndKeepsFirst()
        {
            var first = new FakeDevice();
            Assert.IsTrue(Devices.Register(first));
            Assert.IsTrue(Devices.Register(first)); // 同じものは受け付ける
            Assert.IsFalse(Devices.Register(new FakeDevice()));
            Assert.AreSame(first, Devices.Current);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Unregister_OnlyRemovesTheRegisteredDevice()
        {
            var device = new FakeDevice();
            Devices.Register(device);
            Devices.Unregister(new FakeDevice());
            Assert.AreSame(device, Devices.Current);
            Devices.Unregister(device);
            Assert.IsNull(Devices.Current);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => Devices.Register(null));
            Assert.Throws<ArgumentNullException>(() => Devices.WhenReady(null));
        }

        [Test]
        public void WhenReady_Several_AreAllCalledInOrder()
        {
            var order = new List<int>();
            Devices.WhenReady(_ => order.Add(1));
            Devices.WhenReady(_ => order.Add(2));
            Devices.WhenReady(_ => order.Add(3));
            Devices.Register(new FakeDevice());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void WhenReady_ThrowingCallback_DoesNotStopTheOthers()
        {
            var called = false;
            Devices.WhenReady(_ => throw new InvalidOperationException("boom"));
            Devices.WhenReady(_ => called = true);
            Assert.IsTrue(Devices.Register(new FakeDevice()));
            Assert.IsTrue(called);
            Assert.AreEqual(1, _exceptions.Count);
        }

        [Test]
        public void WhenReady_InsideCallback_IsCalledImmediately()
        {
            var inner = 0;
            Devices.WhenReady(_ => Devices.WhenReady(__ => inner++));
            Devices.Register(new FakeDevice());
            Assert.AreEqual(1, inner);
        }

        [Test]
        public void WhenReady_AfterUnregister_GetsTheNextDevice()
        {
            var first = new FakeDevice();
            Devices.Register(first);
            Devices.Unregister(first);

            IDevice got = null;
            Devices.WhenReady(d => got = d);
            Assert.IsNull(got);
            var second = new FakeDevice();
            Devices.Register(second);
            Assert.AreSame(second, got);
        }

        [Test]
        public void Reset_DropsPendingCallbacks()
        {
            var calls = 0;
            Devices.WhenReady(_ => calls++);
            Devices.Reset();
            Devices.Register(new FakeDevice());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void CancelWhenReady_AfterCalled_DoesNothing()
        {
            var calls = 0;
            Action<IDevice> callback = _ => calls++;
            Devices.Register(new FakeDevice());
            Devices.WhenReady(callback);
            Devices.CancelWhenReady(callback);
            Assert.AreEqual(1, calls);
        }
    }
}
