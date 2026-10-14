using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;

namespace MouthOfGod.Device.Tests
{
    public class MockDeviceTests
    {
        // 起きたことを順に記録する（"bitten" / "confirmed:食感 ID" / 食感 ID）
        static MockDeviceLogic Make(float interval, string[] ids, List<string> log)
        {
            var logic = new MockDeviceLogic(ids, interval);
            logic.Bitten += () => log.Add("bitten");
            logic.Confirmed += id => log.Add("confirmed:" + id.Value);
            logic.TextureChanged += id => log.Add(id.Value);
            return logic;
        }

        [Test]
        public void BeginBite_FiresBitten_ThenFirstTexture()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b" }, log);

            logic.BeginBite();

            CollectionAssert.AreEqual(new[] { "bitten", "a" }, log);
            Assert.IsTrue(logic.IsBiting);
        }

        [Test]
        public void Tick_SwitchesTexture_EveryInterval_AndWraps()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b", "c" }, log);
            logic.BeginBite();
            log.Clear();

            logic.Tick(0.9f);
            Assert.IsEmpty(log);
            logic.Tick(0.2f);
            CollectionAssert.AreEqual(new[] { "b" }, log);
            logic.Tick(1f);
            logic.Tick(1f);
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, log);
        }

        [Test]
        public void Tick_LargeDelta_SwitchesMultipleTimes()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b", "c" }, log);
            logic.BeginBite();
            log.Clear();

            logic.Tick(2.5f);

            CollectionAssert.AreEqual(new[] { "b", "c" }, log);
        }

        [Test]
        public void Tick_WithoutBite_DoesNothing()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b" }, log);

            logic.Tick(10f);

            Assert.IsEmpty(log);
        }

        [Test]
        public void EndBite_StopsSwitching_AndNextBiteRestartsFromFirst()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b" }, log);
            logic.BeginBite();
            logic.Tick(1f);
            logic.EndBite();
            log.Clear();

            logic.Tick(5f);
            Assert.IsEmpty(log);
            Assert.IsFalse(logic.IsBiting);

            logic.BeginBite();
            CollectionAssert.AreEqual(new[] { "bitten", "a" }, log);
        }

        [Test]
        public void BeginBite_WhileBiting_IsIgnored()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a" }, log);
            logic.BeginBite();
            log.Clear();

            logic.BeginBite();

            Assert.IsEmpty(log);
        }

        [Test]
        public void Confirm_FiresConfirmed_WithAndWithoutBite()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a" }, log);

            logic.Confirm();
            logic.BeginBite();
            logic.Confirm();

            CollectionAssert.AreEqual(new[] { "confirmed:", "bitten", "a", "confirmed:a" }, log);
        }

        [Test]
        public void Confirm_AfterRelease_CarriesLastTexture()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b" }, log);

            Assert.IsTrue(logic.Current.IsEmpty);
            logic.BeginBite();
            logic.Tick(1f);
            logic.EndBite();
            Assert.AreEqual(new TextureId("b"), logic.Current);

            log.Clear();
            logic.Confirm();
            CollectionAssert.AreEqual(new[] { "confirmed:b" }, log);
        }

        [Test]
        public void NullOrEmptyIds_FireBittenOnly_AndDoNotThrow()
        {
            var log = new List<string>();
            var logic = Make(1f, null, log);

            logic.BeginBite();
            logic.Tick(5f);

            CollectionAssert.AreEqual(new[] { "bitten" }, log);

            var log2 = new List<string>();
            var logic2 = Make(1f, new string[0], log2);
            logic2.BeginBite();
            logic2.Tick(5f);
            CollectionAssert.AreEqual(new[] { "bitten" }, log2);
        }

        [Test]
        public void BlankIdEntry_IsSkipped()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "", "c" }, log);
            logic.BeginBite();
            logic.Tick(1f);
            logic.Tick(1f);

            CollectionAssert.AreEqual(new[] { "bitten", "a", "c" }, log);
        }

        [Test]
        public void ZeroOrNegativeInterval_IsClampedAndTerminates()
        {
            var log = new List<string>();
            var logic = Make(0f, new[] { "a", "b" }, log);
            logic.BeginBite();
            log.Clear();

            logic.Tick(MockDeviceLogic.MinIntervalSeconds);

            CollectionAssert.AreEqual(new[] { "b" }, log);
        }

        [Test]
        public void NonPositiveDelta_DoesNothing()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "a", "b" }, log);
            logic.BeginBite();
            log.Clear();

            logic.Tick(0f);
            logic.Tick(-3f);

            Assert.IsEmpty(log);
        }

        [Test]
        public void BeginBite_ClearsPreviousTexture_WhenFirstIdIsEmpty()
        {
            var log = new List<string>();
            var logic = Make(1f, new[] { "", "b" }, log);

            logic.BeginBite();
            logic.Tick(1f);
            logic.EndBite();
            Assert.AreEqual(new TextureId("b"), logic.Current);

            logic.BeginBite();
            Assert.IsTrue(logic.Current.IsEmpty);
        }
    }
}
