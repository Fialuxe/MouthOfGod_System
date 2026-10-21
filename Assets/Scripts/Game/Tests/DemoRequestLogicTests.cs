using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;

namespace MouthOfGod.Game.Tests
{
    // 仮の進行の決まり。時間は引数で渡すので、手で進める。
    public class DemoRequestLogicTests
    {
        sealed class FakeField : IFieldRequests
        {
            int _next;
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool IsOpen => false;
            public bool CanOpen { get; set; } = true;
            public int OpenCalls;
            public readonly List<int> Expired = new List<int>();
            public bool Open() { OpenCalls++; if (!CanOpen) return false; Issue(); return true; }
            public bool Open(FoodId food) => false;
            public DeliveryKind Receive(FoodId food) => DeliveryKind.Foreign;
            public void Expire() { }
            public void Expire(int request) { Expired.Add(request); }
            public void Restart() { }
            public event Action<RequestIssued> Issued;
            public event Action<RequestClosed> Closed;
            public event Action<Delivered> Received { add { } remove { } }

            public void Issue() { Issued?.Invoke(new RequestIssued(new FoodId("carrot"), Field, null, _next++)); }
            public void Close(int request, bool resolved) { Closed?.Invoke(new RequestClosed(new FoodId("carrot"), Field, resolved, request)); }
        }

        // 実際の使い方どおり、Issued / Closed をロジックに渡す。
        static FakeField Make(DemoRequestLogic logic, string id, List<IFieldRequests> fields, Func<float> clock)
        {
            var f = new FakeField(id);
            f.Issued += r => logic.OnIssued(f, r.Request, clock());
            f.Closed += r => logic.OnClosed(f, r.Request);
            fields.Add(f);
            return f;
        }

        float _now;
        List<IFieldRequests> _fields;
        DemoRequestLogic _logic;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            _fields = new List<IFieldRequests>();
            _logic = new DemoRequestLogic(30f, 20f, 30f, 1, 2);
        }

        FakeField Add(string id) => Make(_logic, id, _fields, () => _now);

        void TickAt(float t, bool allowOpen = true)
        {
            _now = t;
            _logic.Tick(_fields, t, allowOpen);
        }

        [Test]
        public void FirstTick_OpensOne()
        {
            var a = Add("village");
            TickAt(0f);
            Assert.AreEqual(1, a.OpenCalls);
            Assert.AreEqual(1, _logic.OpenCount);
        }

        [Test]
        public void OpensAgainOnlyAfterInterval()
        {
            var a = Add("village");
            TickAt(0f);
            a.Close(0, true); // すぐ届いた
            TickAt(29f);
            Assert.AreEqual(1, a.OpenCalls);
            TickAt(30f);
            Assert.AreEqual(2, a.OpenCalls);
        }

        [Test]
        public void EarlyPhase_AllowsOnlyOneAtATime()
        {
            Add("village"); var b = Add("sea");
            TickAt(0f);
            TickAt(1f); TickAt(10f); TickAt(19f); // 間隔の前
            Assert.AreEqual(1, _logic.OpenCount);
            Assert.AreEqual(0, b.OpenCalls);
        }

        [Test]
        public void AfterEarlyPhase_AllowsUpToMax()
        {
            // 期限を長くして、出たままにする。最初の 30 秒は 1 つ、そのあとは 2 つまで。
            var logic = new DemoRequestLogic(10f, 1000f, 30f, 1, 2);
            var fields = new List<IFieldRequests>();
            float now = 0f;
            Make(logic, "village", fields, () => now);
            Make(logic, "sea", fields, () => now);
            Make(logic, "mountain", fields, () => now);
            foreach (float t in new[] { 0f, 10f, 20f, 29f })
            {
                now = t;
                logic.Tick(fields, t);
            }
            Assert.AreEqual(1, logic.OpenCount);
            now = 30f;
            logic.Tick(fields, 30f);
            Assert.AreEqual(2, logic.OpenCount);
            now = 40f;
            logic.Tick(fields, 40f);
            Assert.AreEqual(2, logic.OpenCount);
        }

        [Test]
        public void MaxOpen_BlocksEvenWhenIntervalPassed()
        {
            // 期限を長くして、2 つ出たあとは間隔が来ても出さない
            var logic = new DemoRequestLogic(10f, 1000f, 0f, 1, 2);
            var fields = new List<IFieldRequests>();
            float now = 0f;
            Make(logic, "village", fields, () => now);
            Make(logic, "sea", fields, () => now);
            Make(logic, "mountain", fields, () => now);
            foreach (float t in new[] { 0f, 10f, 20f, 30f })
            {
                now = t;
                logic.Tick(fields, t);
            }
            Assert.AreEqual(2, logic.OpenCount);
        }

        [Test]
        public void ClosedFrees_NextOpensAfterInterval()
        {
            var logic = new DemoRequestLogic(10f, 1000f, 0f, 1, 1);
            var fields = new List<IFieldRequests>();
            float now = 0f;
            var a = Make(logic, "village", fields, () => now);
            logic.Tick(fields, 0f);
            logic.Tick(fields, 10f); // 上限 1 で出ない
            Assert.AreEqual(1, a.OpenCalls);
            a.Close(0, true);
            Assert.AreEqual(0, logic.OpenCount);
            now = 11f;
            logic.Tick(fields, 11f);
            Assert.AreEqual(2, a.OpenCalls);
        }

        [Test]
        public void Expires_AfterExpireSeconds_WithRequestNumber()
        {
            var a = Add("village");
            TickAt(0f);
            TickAt(19.9f);
            Assert.AreEqual(0, a.Expired.Count);
            TickAt(20f);
            Assert.AreEqual(new[] { 0 }, a.Expired.ToArray());
            Assert.AreEqual(0, _logic.OpenCount);
        }

        [Test]
        public void ClosedRequest_IsNotExpired()
        {
            var a = Add("village");
            TickAt(0f);
            a.Close(0, true);
            TickAt(25f);
            Assert.AreEqual(0, a.Expired.Count);
        }

        [Test]
        public void ExpiredOnce_NotExpiredAgain_EvenIfClosedNeverComes()
        {
            var a = Add("village");
            TickAt(0f);
            TickAt(20f);
            TickAt(21f);
            TickAt(40f);
            Assert.AreEqual(1, a.Expired.Count);
        }

        [Test]
        public void CanOpenFalse_IsNotOpened_AndNextFieldIsTried()
        {
            var a = Add("village"); var b = Add("sea");
            a.CanOpen = false;
            TickAt(0f);
            Assert.AreEqual(0, a.OpenCalls);
            Assert.AreEqual(1, b.OpenCalls);
        }

        [Test]
        public void NoFieldCanOpen_RetriesNextTick_WithoutWaitingInterval()
        {
            var a = Add("village");
            a.CanOpen = false;
            TickAt(0f);
            TickAt(1f);
            Assert.AreEqual(0, _logic.OpenCount);
            a.CanOpen = true;
            TickAt(2f);
            Assert.AreEqual(1, a.OpenCalls);
        }

        [Test]
        public void AllowOpenFalse_DoesNotOpen_ButStillExpires()
        {
            var a = Add("village");
            TickAt(0f);
            a.Close(0, true);
            a.Issue(); // 別に出ていた欲しい物（番号 1）
            TickAt(5f, allowOpen: false);
            TickAt(40f, allowOpen: false);
            Assert.AreEqual(1, a.OpenCalls);
            Assert.AreEqual(new[] { 1 }, a.Expired.ToArray());
        }

        [Test]
        public void Forget_DropsFieldRequests()
        {
            var a = Add("village");
            TickAt(0f);
            _logic.Forget(a);
            TickAt(25f);
            Assert.AreEqual(0, a.Expired.Count);
        }

        [Test]
        public void EmptyOrNullFields_DoNothing()
        {
            Assert.DoesNotThrow(() => _logic.Tick(new List<IFieldRequests>(), 0f));
            Assert.DoesNotThrow(() => _logic.Tick(null, 1f));
            Assert.DoesNotThrow(() => _logic.OnIssued(null, 0, 0f));
            Assert.DoesNotThrow(() => _logic.OnClosed(null, 0));
        }

        [Test]
        public void UnknownClosed_IsIgnored()
        {
            var a = Add("village");
            Assert.DoesNotThrow(() => _logic.OnClosed(a, 99));
            Assert.AreEqual(0, _logic.OpenCount);
        }
    }
}
