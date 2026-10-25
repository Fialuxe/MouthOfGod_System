using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tutorial.Tests
{
    // チュートリアル 4 の流れ。EditMode ではフレームが進まないので、Play() を手で MoveNext して進める。
    // 秒で待つ所（WaitForSeconds）は、そのまま飛ばす。時間切れ（指示のくり返し・あきらめる）は、Time.time が進まないので確かめていない。
    public class T4DeliverTests
    {
        sealed class FakeField : IFieldRequests
        {
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool OpenResult = true;
            public bool Opened;
            public readonly List<string> OpenCalls = new List<string>();
            public bool IsOpen => Opened;
            public bool CanOpen => OpenResult;
            public bool Open() => false;
            public bool Open(FoodId food)
            {
                OpenCalls.Add(food.Value);
                if (!OpenResult) return false;
                Opened = true;
                return true;
            }
            public DeliveryKind Receive(FoodId food) => DeliveryKind.Foreign;
            public void Expire() { }
            public void Expire(int request) { }
            public void Restart() { }
            public event Action<RequestIssued> Issued { add { } remove { } }
            public event Action<RequestClosed> Closed { add { } remove { } }
            public event Action<Delivered> Received;

            public void Deliver(string food, DeliveryKind kind) => Received?.Invoke(new Delivered(new FoodId(food), Field, kind));
        }

        readonly List<string> _cues = new List<string>();
        readonly List<string> _warnings = new List<string>();
        readonly List<GameObject> _created = new List<GameObject>();
        readonly List<FakeField> _fields = new List<FakeField>();
        Action<string> _savedWarning;
        TutorialMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _cues.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _warnings.Clear();
            _created.Clear();
            _fields.Clear();
            _savedWarning = T4Deliver.ReportWarning;
            T4Deliver.ReportWarning = _warnings.Add;
            Standing.Set(FieldId.None);
            Cues.Requested += OnCue;
            _memory = new TutorialMemory();
        }

        [TearDown]
        public void TearDown()
        {
            Cues.Requested -= OnCue;
            T4Deliver.ReportWarning = _savedWarning;
            Standing.Set(FieldId.None);
            foreach (FakeField field in _fields) Fields.Unregister(field);
            foreach (GameObject go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        void OnCue(CueRequested request) => _cues.Add(request.Cue.Value);

        FakeField AddField(string id, bool open = false)
        {
            var field = new FakeField(id) { Opened = open };
            Fields.Register(field);
            _fields.Add(field);
            return field;
        }

        T4Deliver CreateScript()
        {
            var go = new GameObject("T4");
            _created.Add(go);
            T4Deliver script = go.AddComponent<T4Deliver>();
            script.Attach(_memory);
            return script;
        }

        // 待ちで止まるか、終わるまで進める。終わったら false。秒で待つ所は飛ばす。
        static bool Advance(IEnumerator play)
        {
            int guard = 0;
            while (true)
            {
                if (play.Current is CustomYieldInstruction wait && wait.keepWaiting) return true;
                if (!play.MoveNext()) return false;
                if (++guard > 1000) throw new AssertionException("終わらない");
            }
        }

        List<string> CuesFrom(int index) => _cues.GetRange(index, _cues.Count - index);

        [Test]
        public void Play_FinishesWhenFishIsDeliveredToSea()
        {
            FakeField sea = AddField("sea", open: true);
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play)); // 届くのを待っている
            CollectionAssert.IsEmpty(sea.OpenCalls); // T3 が出したままの欲しい物を使う
            CollectionAssert.AreEqual(new[] { "sea.glow", "sea.footprint", "miko.follow", "miko.say.t4-walk" }, _cues);

            sea.Deliver("fish", DeliveryKind.Requested);
            Assert.IsFalse(Advance(play));
            CollectionAssert.AreEqual(new[]
            {
                "sea.cheer", "miko.say.t4-splendid",
                "sea.footprint-off", "sea.glow-off", "miko.stay", "miko.say.t4-true-god",
            }, CuesFrom(4));
            Assert.IsTrue(_memory.Has("delivered-by-standing"));
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Play_OpensFish_WhenSeaIsNotAskingYet()
        {
            FakeField sea = AddField("sea");
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play));
            CollectionAssert.AreEqual(new[] { "fish" }, sea.OpenCalls);
        }

        [Test]
        public void Play_WarnsOnce_WhenFishCannotBeOpened_AndStillWaits()
        {
            FakeField sea = AddField("sea");
            sea.OpenResult = false;
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play));
            Assert.AreEqual(1, _warnings.Count);

            Standing.Set(new FieldId("sea")); // 待ち直しても、警告はくり返さない
            Assert.IsTrue(Advance(play));
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Play_SaysNotHere_WhenDeliveredToAnotherField_AndKeepsWaiting()
        {
            FakeField sea = AddField("sea", open: true);
            FakeField village = AddField("village");
            IEnumerator play = CreateScript().Play();
            Advance(play);

            village.Deliver("fish", DeliveryKind.Foreign);
            Assert.IsTrue(Advance(play)); // まだ終わらない
            CollectionAssert.AreEqual(new[] { "village.shake-head", "miko.say.t4-not-here", "sea.footprint" }, CuesFrom(4));
            Assert.IsFalse(_memory.Has("delivered-by-standing"));

            sea.Deliver("fish", DeliveryKind.Requested);
            Assert.IsFalse(Advance(play));
            Assert.IsTrue(_memory.Has("delivered-by-standing"));
        }

        [Test]
        public void Play_RepeatsInstruction_WhenWrongFoodIsDeliveredToSea()
        {
            FakeField sea = AddField("sea", open: true);
            IEnumerator play = CreateScript().Play();
            Advance(play);

            sea.Deliver("apple", DeliveryKind.Foreign);
            Assert.IsTrue(Advance(play));
            CollectionAssert.AreEqual(new[] { "sea.shake-head", "miko.say.t4-walk" }, CuesFrom(4));
        }

        [Test]
        public void Play_SaysHere_WhenPlayerArrivesAtSea_OnlyOnce()
        {
            AddField("sea", open: true);
            IEnumerator play = CreateScript().Play();
            Advance(play);

            Standing.Set(new FieldId("village")); // よその前では何も言わない
            Assert.IsTrue(Advance(play));
            Assert.AreEqual(4, _cues.Count);

            Standing.Set(new FieldId("sea"));
            Assert.IsTrue(Advance(play));
            CollectionAssert.AreEqual(new[] { "sea.footprint-off", "miko.say.t4-here" }, CuesFrom(4));

            Standing.Set(new FieldId("village"));
            Standing.Set(new FieldId("sea"));
            Assert.IsTrue(Advance(play));
            Assert.AreEqual(6, _cues.Count); // 2 回目は言わない
        }

        [Test]
        public void Play_SaysHereRightAway_WhenAlreadyStandingAtSea()
        {
            AddField("sea", open: true);
            Standing.Set(new FieldId("sea"));
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play));
            CollectionAssert.AreEqual(new[] { "sea.footprint-off", "miko.say.t4-here" }, CuesFrom(4));
        }

        [Test]
        public void Play_WarnsAndEnds_WhenSeaIsMissing()
        {
            IEnumerator play = CreateScript().Play();

            Assert.IsFalse(Advance(play));
            Assert.AreEqual(1, _warnings.Count);
            CollectionAssert.IsEmpty(_cues);
            Assert.IsFalse(_memory.Has("delivered-by-standing"));
        }

        [Test]
        public void Judge_TellsSuccessWrongFoodAndWrongPlace()
        {
            var sea = new FieldId("sea");
            Assert.AreEqual(T4Deliver.Outcome.Success, T4Deliver.Judge(new Delivered(new FoodId("fish"), sea, DeliveryKind.Requested), sea));
            Assert.AreEqual(T4Deliver.Outcome.WrongFood, T4Deliver.Judge(new Delivered(new FoodId("fish"), sea, DeliveryKind.Own), sea));
            Assert.AreEqual(T4Deliver.Outcome.WrongPlace, T4Deliver.Judge(new Delivered(new FoodId("fish"), new FieldId("village"), DeliveryKind.Requested), sea));
        }

        [Test]
        public void FieldCue_JoinsFieldAndSuffix_AndIsEmptyWhenEitherIsEmpty()
        {
            Assert.AreEqual("sea.glow", T4Deliver.FieldCue("sea", "glow"));
            Assert.AreEqual("", T4Deliver.FieldCue("sea", ""));
            Assert.AreEqual("", T4Deliver.FieldCue("", "glow"));
            Assert.AreEqual("", T4Deliver.FieldCue(null, null));
        }
    }
}
