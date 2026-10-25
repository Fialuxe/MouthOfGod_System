using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class FoodSpawnersTests
    {
        sealed class FakeSpawner : IFoodSpawner
        {
            public event Action<Spawned> Spawned { add { } remove { } }
            public UnityEngine.GameObject Preview(TextureId texture) => null;
        }

        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            FoodSpawners.Reset();
            _exceptions.Clear();
            _warnings.Clear();
            _savedException = FoodSpawners.ReportException;
            _savedWarning = FoodSpawners.ReportWarning;
            FoodSpawners.ReportException = _exceptions.Add;
            FoodSpawners.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            FoodSpawners.Reset();
            FoodSpawners.ReportException = _savedException;
            FoodSpawners.ReportWarning = _savedWarning;
        }

        [Test]
        public void WhenReady_BeforeRegister_IsCalledOnRegister()
        {
            IFoodSpawner received = null;
            FoodSpawners.WhenReady(f => received = f);
            Assert.IsNull(received);

            var spawner = new FakeSpawner();
            FoodSpawners.Register(spawner);
            Assert.AreSame(spawner, received);
        }

        [Test]
        public void WhenReady_AfterRegister_IsCalledAtOnce()
        {
            var spawner = new FakeSpawner();
            FoodSpawners.Register(spawner);

            IFoodSpawner received = null;
            FoodSpawners.WhenReady(f => received = f);
            Assert.AreSame(spawner, received);
        }

        [Test]
        public void CancelWhenReady_StopsTheCallback()
        {
            var calls = 0;
            Action<IFoodSpawner> callback = _ => calls++;
            FoodSpawners.WhenReady(callback);
            FoodSpawners.CancelWhenReady(callback);

            FoodSpawners.Register(new FakeSpawner());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Register_ASecondSpawner_WarnsAndKeepsTheFirst()
        {
            var first = new FakeSpawner();
            FoodSpawners.Register(first);

            Assert.IsFalse(FoodSpawners.Register(new FakeSpawner()));
            Assert.AreSame(first, FoodSpawners.Current);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Register_ACallbackThrows_StillCallsTheRest()
        {
            var calls = 0;
            FoodSpawners.WhenReady(_ => throw new InvalidOperationException());
            FoodSpawners.WhenReady(_ => calls++);

            FoodSpawners.Register(new FakeSpawner());
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _exceptions.Count);
        }

        [Test]
        public void Unregister_OnlyRemovesTheSameSpawner()
        {
            var spawner = new FakeSpawner();
            FoodSpawners.Register(spawner);

            FoodSpawners.Unregister(new FakeSpawner());
            Assert.AreSame(spawner, FoodSpawners.Current);

            FoodSpawners.Unregister(spawner);
            Assert.IsNull(FoodSpawners.Current);
        }
    }
}
