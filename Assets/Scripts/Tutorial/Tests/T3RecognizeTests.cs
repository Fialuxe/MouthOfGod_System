using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tutorial.Tests
{
    // チュートリアル 3 の流れ。EditMode ではフレームが進まないので、Play() を手で MoveNext して進める。
    // 秒で待つ所（WaitForSeconds）は、そのまま飛ばす。時間切れ（あきらめる）は、Time.time が進まないので確かめていない。
    public class T3RecognizeTests
    {
        sealed class FakeField : IFieldRequests
        {
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool OpenResult = true;
            public readonly List<string> Opened = new List<string>();
            public int Expired;
            public bool IsOpen => Opened.Count > 0;
            public bool CanOpen => OpenResult;
            public bool Open() => false;
            public bool Open(FoodId food)
            {
                if (!OpenResult) return false;
                Opened.Add(food.Value);
                return true;
            }
            public DeliveryKind Receive(FoodId food) => DeliveryKind.Foreign;
            public void Expire() => Expired++;
            public void Expire(int request) => Expired++;
            public void Restart() { }
            public event Action<RequestIssued> Issued { add { } remove { } }
            public event Action<RequestClosed> Closed { add { } remove { } }
            public event Action<Delivered> Received { add { } remove { } }
        }

        sealed class FakeSpawner : IFoodSpawner
        {
            public event Action<Spawned> Spawned;
            public GameObject Preview(TextureId texture) => null;

            public void Spawn(string food) => Spawned?.Invoke(new Spawned(new FoodId(food), Vector3.zero));
        }

        readonly List<string> _cues = new List<string>();
        readonly List<string> _warnings = new List<string>();
        readonly List<GameObject> _created = new List<GameObject>();
        Action<string> _savedWarning;
        FakeField _field;
        FakeSpawner _spawner;
        TutorialMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _cues.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _warnings.Clear();
            _created.Clear();
            _field = null;
            _savedWarning = T3Recognize.ReportWarning;
            T3Recognize.ReportWarning = _warnings.Add;
            Cues.Requested += OnCue;
            _spawner = new FakeSpawner();
            FoodSpawners.Register(_spawner);
            _memory = new TutorialMemory();
        }

        [TearDown]
        public void TearDown()
        {
            Cues.Requested -= OnCue;
            T3Recognize.ReportWarning = _savedWarning;
            FoodSpawners.Unregister(_spawner);
            if (_field != null) Fields.Unregister(_field);
            foreach (GameObject go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        void OnCue(CueRequested request) => _cues.Add(request.Cue.Value);

        FakeField AddField(string id)
        {
            _field = new FakeField(id);
            Fields.Register(_field);
            return _field;
        }

        T3Recognize CreateScript()
        {
            var go = new GameObject("T3");
            _created.Add(go);
            T3Recognize script = go.AddComponent<T3Recognize>();
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

        [Test]
        public void Play_OpensFishOnSea_AndFinishesWhenFishIsSpawned()
        {
            FakeField sea = AddField("sea");
            T3Recognize script = CreateScript();
            IEnumerator play = script.Play();

            Assert.IsTrue(Advance(play)); // 魚を待っている
            CollectionAssert.AreEqual(new[] { "fish" }, sea.Opened);
            CollectionAssert.AreEqual(new[]
            {
                "sea.glow", "miko.say.t3-troubled", "shadow.show", "cloud.glow", "miko.say.t3-which-feel",
            }, _cues);

            _spawner.Spawn("fish");
            Assert.IsFalse(Advance(play));
            CollectionAssert.AreEqual(new[] { "shadow.hide", "miko.say.t3-fish-here" }, _cues.GetRange(5, _cues.Count - 5));
            Assert.IsTrue(_memory.Has("request-seen"));
            Assert.AreEqual(0, sea.Expired); // 海の欲しい物は、T4 のために出したまま
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Play_ShowsFaintShadow_WhenShadowWasSeenBefore()
        {
            AddField("sea");
            _memory.Set("texture-shadow-seen");
            IEnumerator play = CreateScript().Play();

            Advance(play);

            CollectionAssert.Contains(_cues, "shadow.faint");
            CollectionAssert.DoesNotContain(_cues, "shadow.show");
        }

        [Test]
        public void Play_SaysWrongLine_OnOtherFood_AndEmphasizesShadowAfterTwoMisses()
        {
            AddField("sea");
            IEnumerator play = CreateScript().Play();
            Advance(play);
            _cues.Clear();

            _spawner.Spawn("apple");
            Assert.IsTrue(Advance(play)); // まだ魚を待つ
            CollectionAssert.AreEqual(new[] { "miko.say.t3-wrong" }, _cues);

            _spawner.Spawn("apple");
            Assert.IsTrue(Advance(play));
            CollectionAssert.AreEqual(new[] { "miko.say.t3-wrong", "miko.say.t3-wrong", "shadow.emphasize" }, _cues);

            _spawner.Spawn("apple");
            Assert.IsTrue(Advance(play));
            Assert.AreEqual(1, _cues.FindAll(c => c == "shadow.emphasize").Count); // 強めるのは 1 回だけ

            _spawner.Spawn("fish");
            Assert.IsFalse(Advance(play));
            Assert.IsTrue(_memory.Has("request-seen"));
        }

        [Test]
        public void Play_WarnsAndContinues_WhenFieldIsMissing()
        {
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play)); // 止まらずに、魚を待つところまで進む
            Assert.AreEqual(1, _warnings.Count);

            _spawner.Spawn("fish");
            Assert.IsFalse(Advance(play));
        }

        [Test]
        public void Play_WarnsAndContinues_WhenOpenFails()
        {
            FakeField sea = AddField("sea");
            sea.OpenResult = false; // もう出ている、など
            IEnumerator play = CreateScript().Play();

            Assert.IsTrue(Advance(play));
            Assert.AreEqual(1, _warnings.Count);
            CollectionAssert.Contains(_cues, "sea.glow");
        }
    }
}
