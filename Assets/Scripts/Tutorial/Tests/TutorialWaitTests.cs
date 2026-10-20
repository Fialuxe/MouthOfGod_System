using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;

namespace MouthOfGod.Tutorial.Tests
{
    // 待ちの決まり。時間は、手で進める時計で確かめる。EditMode なので、フレームは進まない。keepWaiting を直接読む。
    public class TutorialWaitTests
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
            public event Action<RequestIssued> Issued { add { } remove { } }
            public event Action<RequestClosed> Closed { add { } remove { } }
            public event Action<Delivered> Received;

            public int Subscribers => Received == null ? 0 : Received.GetInvocationList().Length;

            public void Deliver(string food, DeliveryKind kind)
            {
                Action<Delivered> handlers = Received;
                if (handlers != null) handlers(new Delivered(new FoodId(food), Field, kind));
            }
        }

        sealed class FakeDevice : IDevice
        {
            public TextureId Current { get; private set; }
            public event Action<TextureId> Confirmed;
            public event Action<TextureId> TextureChanged { add { } remove { } }
            public event Action Bitten { add { } remove { } }

            public int Subscribers => Confirmed == null ? 0 : Confirmed.GetInvocationList().Length;

            public void Confirm(string texture)
            {
                Current = new TextureId(texture);
                Action<TextureId> handlers = Confirmed;
                if (handlers != null) handlers(Current);
            }
        }

        sealed class FakeFork : IFork
        {
            public bool IsLoaded { get; private set; }
            public bool InMouth { get; private set; }
            public event Action Pierced;
            public event Action MouthEntered;
            public event Action MouthLeft { add { } remove { } }
            public event Action Emptied { add { } remove { } }

            public int Subscribers => Pierced == null ? 0 : Pierced.GetInvocationList().Length;

            public void Pierce()
            {
                IsLoaded = true;
                Pierced?.Invoke();
            }

            public void EnterMouth()
            {
                InMouth = true;
                MouthEntered?.Invoke();
            }
        }

        readonly List<FakeField> _fields = new List<FakeField>();
        FakeDevice _device;
        FakeFork _fork;
        float _now;

        [SetUp]
        public void SetUp()
        {
            _fields.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _device = null;
            _fork = null;
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FakeField field in _fields) Fields.Unregister(field);
            if (_device != null) Devices.Unregister(_device);
            if (_fork != null) Forks.Unregister(_fork);
        }

        FakeField AddField(string id)
        {
            var field = new FakeField(id);
            Fields.Register(field);
            _fields.Add(field);
            return field;
        }

        DeliveryWait Delivery(string only = "", float timeout = 0f) => new DeliveryWait(new FieldId(only), timeout, () => _now);

        ConfirmWait Confirm(float timeout = 0f) => new ConfirmWait(timeout, () => _now);
        ForkWait ForkUntil(ForkGoal goal, float timeout = 0f) => new ForkWait(goal, timeout, () => _now);

        FakeFork AddFork()
        {
            _fork = new FakeFork();
            Forks.Register(_fork);
            return _fork;
        }

        // ---- DeliveryWait

        [Test]
        public void DeliveryWait_CompletesOnTheFirstDeliveryToAnyField_AndKeepsTheResult()
        {
            FakeField sea = AddField("sea");
            AddField("village");
            DeliveryWait wait = Delivery();
            Assert.IsTrue(wait.keepWaiting);

            sea.Deliver("fish", DeliveryKind.Requested);

            Assert.IsFalse(wait.keepWaiting);
            Assert.IsTrue(wait.IsDone);
            Assert.IsFalse(wait.TimedOut);
            Assert.AreEqual(new FoodId("fish"), wait.Result.Value.Food);
            Assert.AreEqual(new FieldId("sea"), wait.Result.Value.Field);
            Assert.AreEqual(DeliveryKind.Requested, wait.Result.Value.Kind);
        }

        [Test]
        public void DeliveryWait_WithFieldFilter_IgnoresOtherFields()
        {
            FakeField sea = AddField("sea");
            FakeField village = AddField("village");
            DeliveryWait wait = Delivery("sea");

            village.Deliver("fish", DeliveryKind.Foreign);
            Assert.IsTrue(wait.keepWaiting);

            sea.Deliver("fish", DeliveryKind.Requested);
            Assert.IsFalse(wait.keepWaiting);
            Assert.AreEqual(new FieldId("sea"), wait.Result.Value.Field);
        }

        [Test]
        public void DeliveryWait_AlsoListensToFieldsRegisteredLater()
        {
            DeliveryWait wait = Delivery();

            FakeField late = AddField("mountain");
            late.Deliver("mushroom", DeliveryKind.Requested);

            Assert.IsFalse(wait.keepWaiting);
            Assert.AreEqual(new FieldId("mountain"), wait.Result.Value.Field);
        }

        [Test]
        public void DeliveryWait_StopsListening_WhenDone_OrCancelled()
        {
            FakeField sea = AddField("sea");
            DeliveryWait done = Delivery();
            DeliveryWait cancelled = Delivery();
            Assert.AreEqual(2, sea.Subscribers);

            sea.Deliver("fish", DeliveryKind.Requested); // done は終わる。cancelled も、同じ知らせを受けて終わる
            Assert.IsTrue(done.IsDone);
            Assert.IsTrue(cancelled.IsDone);
            Assert.AreEqual(0, sea.Subscribers);

            DeliveryWait other = Delivery();
            Assert.AreEqual(1, sea.Subscribers);
            other.Cancel();
            Assert.AreEqual(0, sea.Subscribers);
            Assert.IsTrue(other.IsDone);
            Assert.IsNull(other.Result);
        }

        [Test]
        public void DeliveryWait_TimesOut_WithTheClock()
        {
            FakeField sea = AddField("sea");
            DeliveryWait wait = Delivery(timeout: 5f);

            _now = 4.9f;
            Assert.IsTrue(wait.keepWaiting);
            _now = 5f;
            Assert.IsFalse(wait.keepWaiting);

            Assert.IsTrue(wait.TimedOut);
            Assert.IsNull(wait.Result);
            Assert.AreEqual(0, sea.Subscribers);
            sea.Deliver("fish", DeliveryKind.Requested); // 時間切れのあとの届きは、結果を変えない
            Assert.IsNull(wait.Result);
        }

        [Test]
        public void DeliveryWait_WithZeroTimeout_NeverTimesOut()
        {
            AddField("sea");
            DeliveryWait wait = Delivery(timeout: 0f);

            _now = 100000f;

            Assert.IsTrue(wait.keepWaiting);
            Assert.IsFalse(wait.TimedOut);
            wait.Cancel(); // 聞くのをやめて、次のテストに残さない
        }

        // ---- ConfirmWait

        [Test]
        public void ConfirmWait_CompletesOnConfirm_AndIgnoresTheEmptyTexture()
        {
            _device = new FakeDevice();
            Devices.Register(_device);
            ConfirmWait wait = Confirm();

            _device.Confirm(""); // 噛む前の決定
            Assert.IsTrue(wait.keepWaiting);

            _device.Confirm("placeholder-b");
            Assert.IsFalse(wait.keepWaiting);
            Assert.AreEqual(new TextureId("placeholder-b"), wait.Result);
            Assert.AreEqual(0, _device.Subscribers);
        }

        [Test]
        public void ConfirmWait_ListensToADeviceRegisteredLater()
        {
            ConfirmWait wait = Confirm();
            _device = new FakeDevice();

            Devices.Register(_device);
            _device.Confirm("placeholder-a");

            Assert.IsFalse(wait.keepWaiting);
            Assert.AreEqual(new TextureId("placeholder-a"), wait.Result);
        }

        [Test]
        public void ConfirmWait_TimesOut_AndStopsListening()
        {
            _device = new FakeDevice();
            Devices.Register(_device);
            ConfirmWait wait = Confirm(timeout: 3f);

            _now = 3f;

            Assert.IsFalse(wait.keepWaiting);
            Assert.IsTrue(wait.TimedOut);
            Assert.IsTrue(wait.Result.IsEmpty);
            Assert.AreEqual(0, _device.Subscribers);
        }

        // ---- AnyWait

        [Test]
        public void AnyWait_FirstFinishedWins_AndTheOthersAreCancelled()
        {
            FakeField sea = AddField("sea");
            FakeField village = AddField("village");
            DeliveryWait atSea = Delivery("sea");
            DeliveryWait atVillage = Delivery("village");
            var any = new AnyWait(new TutorialWait[] { atSea, atVillage });
            Assert.IsTrue(any.keepWaiting);
            Assert.AreEqual(-1, any.Index);

            village.Deliver("pig", DeliveryKind.Foreign);

            Assert.IsFalse(any.keepWaiting);
            Assert.AreEqual(1, any.Index);
            Assert.AreSame(atVillage, any.Winner);
            Assert.IsTrue(atSea.IsDone); // 負けた待ちは取り消される
            Assert.AreEqual(0, sea.Subscribers);
            Assert.AreEqual(0, village.Subscribers);
        }

        [Test]
        public void AnyWait_OnTimeout_WinnerIsTheTimedOutWait()
        {
            AddField("sea");
            DeliveryWait delivery = Delivery(timeout: 20f);
            ConfirmWait never = Confirm();
            var any = new AnyWait(new TutorialWait[] { delivery, never });

            _now = 20f;

            Assert.IsFalse(any.keepWaiting);
            Assert.AreEqual(0, any.Index);
            Assert.IsTrue(any.Winner.TimedOut);
        }

        [Test]
        public void AnyWait_WhenSeveralAreDone_PicksTheEarlierOne()
        {
            FakeField sea = AddField("sea");
            DeliveryWait first = Delivery();
            DeliveryWait second = Delivery();
            sea.Deliver("fish", DeliveryKind.Requested); // 2 つとも終わる

            var any = new AnyWait(new TutorialWait[] { first, second });

            Assert.IsFalse(any.keepWaiting);
            Assert.AreEqual(0, any.Index);
        }

        [Test]
        public void AnyWait_WithNothingToWaitFor_DoesNotWait()
        {
            var any = new AnyWait(new TutorialWait[0]);

            Assert.IsFalse(any.keepWaiting);
        }

        // ---- ForkWait

        [Test]
        public void ForkWait_Pierce_CompletesWhenPierced()
        {
            FakeFork fork = AddFork();
            ForkWait wait = ForkUntil(ForkGoal.Pierce);
            Assert.IsTrue(wait.keepWaiting);

            fork.Pierce();
            Assert.IsFalse(wait.keepWaiting);
            Assert.IsFalse(wait.TimedOut);
        }

        [Test]
        public void ForkWait_Pierce_AlreadyLoaded_CompletesAtOnce()
        {
            FakeFork fork = AddFork();
            fork.Pierce();

            Assert.IsFalse(ForkUntil(ForkGoal.Pierce).keepWaiting);
        }

        [Test]
        public void ForkWait_Mouth_NeedsAPieceOnTheTip()
        {
            FakeFork fork = AddFork();
            ForkWait wait = ForkUntil(ForkGoal.Mouth);

            fork.EnterMouth(); // かけらなしで口に入れても、数えない
            Assert.IsTrue(wait.keepWaiting);

            fork.Pierce(); // 口の中で刺した状態になった
            Assert.IsFalse(wait.keepWaiting);
        }

        [Test]
        public void ForkWait_ListensToAForkRegisteredLater()
        {
            ForkWait wait = ForkUntil(ForkGoal.Pierce);

            FakeFork fork = AddFork();
            fork.Pierce();
            Assert.IsFalse(wait.keepWaiting);
        }

        [Test]
        public void ForkWait_TimesOut_AndStopsListening()
        {
            FakeFork fork = AddFork();
            ForkWait wait = ForkUntil(ForkGoal.Pierce, timeout: 10f);
            Assert.AreEqual(1, fork.Subscribers);

            _now = 10f;
            Assert.IsFalse(wait.keepWaiting);
            Assert.IsTrue(wait.TimedOut);
            Assert.AreEqual(0, fork.Subscribers);
        }
    }
}
