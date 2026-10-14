using System;
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

        [SetUp]
        public void SetUp() => Devices.Reset();

        [TearDown]
        public void TearDown() => Devices.Reset();

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
    }
}
