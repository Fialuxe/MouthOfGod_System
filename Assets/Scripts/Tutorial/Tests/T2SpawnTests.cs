using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tutorial.Tests
{
    // チュートリアル 2 の流れ。EditMode では時間が進まないので、時間切れ・あきらめは起きない。Play() を手で MoveNext して進める。
    public class T2SpawnTests
    {
        sealed class FakeFork : IFork
        {
            public bool IsLoaded => true; // もう刺して、口に含んでいる
            public bool InMouth => true;
            public event Action Pierced { add { } remove { } }
            public event Action MouthEntered { add { } remove { } }
            public event Action MouthLeft { add { } remove { } }
            public event Action Emptied { add { } remove { } }
        }

        sealed class FakeSpawner : IFoodSpawner
        {
            public event Action<Spawned> Spawned;
            public GameObject Preview(TextureId texture) => null;
            public void Spawn(string food) => Spawned?.Invoke(new Spawned(new FoodId(food), Vector3.zero));
        }

        readonly List<string> _cues = new List<string>();
        GameObject _host;
        T2Spawn _script;
        TutorialMemory _memory;
        FakeFork _fork;
        FakeSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _cues.Clear();
            Cues.Requested += OnCue;
            _fork = new FakeFork();
            Forks.Register(_fork);
            _spawner = new FakeSpawner();
            FoodSpawners.Register(_spawner);
            _host = new GameObject("T2");
            _script = _host.AddComponent<T2Spawn>();
            _memory = new TutorialMemory();
            _script.Attach(_memory);
        }

        [TearDown]
        public void TearDown()
        {
            Cues.Requested -= OnCue;
            Forks.Unregister(_fork);
            FoodSpawners.Unregister(_spawner);
            UnityEngine.Object.DestroyImmediate(_host);
        }

        void OnCue(CueRequested request) => _cues.Add(request.Cue.Value);

        // 生まれるのを待つところまで進める。
        static void RunUntilSpawnWait(IEnumerator play)
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(play.MoveNext(), "生まれるのを待つ前に終わった");
                if (play.Current is SpawnWait) return;
            }
            Assert.Fail("生まれるのを待たなかった");
        }

        static void RunToEnd(IEnumerator play)
        {
            for (int i = 0; i < 10; i++)
                if (!play.MoveNext()) return;
            Assert.Fail("終わらなかった");
        }

        [Test]
        public void Phase_IsSpawn()
        {
            Assert.AreEqual(new PhaseId("spawn"), _script.Phase);
        }

        [Test]
        public void Start_ShowsOfferingCloudAndShadow_AndSaysRemember()
        {
            IEnumerator play = _script.Play();
            RunUntilSpawnWait(play);

            CollectionAssert.IsSubsetOf(new[] { "offering.show", "cloud.glow", "shadow.show", "miko.say.t2-remember", "miko.say.t2-wait-apple" }, _cues);
            Assert.Less(_cues.IndexOf("miko.say.t2-remember"), _cues.IndexOf("miko.say.t2-wait-apple"));
            CollectionAssert.DoesNotContain(_cues, "miko.say.t2-remember-short");
        }

        [Test]
        public void Start_AfterForkLearned_SaysTheShortLine()
        {
            _memory.Set("fork-learned");
            IEnumerator play = _script.Play();
            RunUntilSpawnWait(play);

            CollectionAssert.Contains(_cues, "miko.say.t2-remember-short");
            CollectionAssert.DoesNotContain(_cues, "miko.say.t2-remember");
        }

        [Test]
        public void Apple_FinishesWithBornLine_HidesThings_AndRemembers()
        {
            IEnumerator play = _script.Play();
            RunUntilSpawnWait(play);

            _spawner.Spawn("apple");
            RunToEnd(play);

            CollectionAssert.IsSubsetOf(new[] { "offering.hide", "shadow.hide", "miko.say.t2-born" }, _cues);
            CollectionAssert.DoesNotContain(_cues, "miko.say.t2-wrong");
            Assert.IsTrue(_memory.Has("food-born"));
            Assert.IsTrue(_memory.Has("texture-shadow-seen"));
        }

        [Test]
        public void OtherFood_SaysWrong_AndKeepsWaiting_ThenEmphasizesOnceAtThirdMiss()
        {
            IEnumerator play = _script.Play();
            RunUntilSpawnWait(play);

            for (int miss = 1; miss <= 4; miss++)
            {
                _spawner.Spawn("fish");
                Assert.IsTrue(play.MoveNext());
                Assert.IsInstanceOf<SpawnWait>(play.Current, "外したら、もう一度待つ");
                Assert.AreEqual(miss >= 3 ? 1 : 0, _cues.FindAll(c => c == "shadow.emphasize").Count, "miss " + miss);
            }
            Assert.AreEqual(4, _cues.FindAll(c => c == "miko.say.t2-wrong").Count);
            CollectionAssert.DoesNotContain(_cues, "miko.say.t2-born");

            _spawner.Spawn("apple");
            RunToEnd(play);
            CollectionAssert.Contains(_cues, "miko.say.t2-born");
        }

        // ---- 純粋な部品

        [Test]
        public void TimeoutFor_UsesTheShorterOfHintAndRemaining()
        {
            Assert.AreEqual(10f, T2Spawn.TimeoutFor(10f, 0f, 100f), "あきらめないなら、ヒントの間隔");
            Assert.AreEqual(0f, T2Spawn.TimeoutFor(0f, 0f, 5f), "どちらも無ければ、時間切れなし");
            Assert.AreEqual(10f, T2Spawn.TimeoutFor(10f, 90f, 5f));
            Assert.AreEqual(5f, T2Spawn.TimeoutFor(10f, 90f, 85f), 1e-4f);
            Assert.AreEqual(60f, T2Spawn.TimeoutFor(0f, 90f, 30f), 1e-4f, "ヒントが無ければ、あきらめるまでの残り");
        }

        [Test]
        public void TimeoutFor_PastGiveUp_IsStillPositive()
        {
            float timeout = T2Spawn.TimeoutFor(10f, 90f, 120f);
            Assert.Greater(timeout, 0f, "0 だと「時間切れなし」になり、ずっと待ってしまう");
        }

        [Test]
        public void IsTarget_MatchesTheFoodId_AndEmptyTargetAcceptsAnything()
        {
            Assert.IsTrue(T2Spawn.IsTarget(new FoodId("apple"), "apple"));
            Assert.IsFalse(T2Spawn.IsTarget(new FoodId("fish"), "apple"));
            Assert.IsTrue(T2Spawn.IsTarget(new FoodId("fish"), ""));
            Assert.IsTrue(T2Spawn.IsTarget(new FoodId("fish"), null));
        }

        [Test]
        public void ShouldEmphasize_OnlyAtTheCount_AndNeverWhenZero()
        {
            Assert.IsFalse(T2Spawn.ShouldEmphasize(2, 3));
            Assert.IsTrue(T2Spawn.ShouldEmphasize(3, 3));
            Assert.IsFalse(T2Spawn.ShouldEmphasize(4, 3));
            Assert.IsFalse(T2Spawn.ShouldEmphasize(0, 0));
            Assert.IsFalse(T2Spawn.ShouldEmphasize(1, 0));
        }
    }
}
