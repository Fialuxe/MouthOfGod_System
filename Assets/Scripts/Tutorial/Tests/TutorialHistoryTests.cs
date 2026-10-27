using System;
using System.Collections;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tutorial.Tests
{
    // 先回りした操作の数え方。待ちを作る前に起きたことも数え、1 回使った操作は次の待ちで数えない。
    public class TutorialHistoryTests
    {
        sealed class FakeFork : IFork
        {
            public bool IsLoaded { get; set; }
            public bool InMouth { get; set; }
            public event Action Pierced;
            public event Action MouthEntered;
            public event Action MouthLeft { add { } remove { } }
            public event Action Emptied { add { } remove { } }

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

        sealed class FakeDevice : IDevice
        {
            public TextureId Current { get; private set; }
            public event Action<TextureId> Confirmed;
            public event Action<TextureId> TextureChanged;
            public event Action Bitten { add { } remove { } }

            public void Change(string texture)
            {
                Current = new TextureId(texture);
                TextureChanged?.Invoke(Current);
            }

            public void Confirm(string texture) => Confirmed?.Invoke(new TextureId(texture));
        }

        sealed class FakeSpawner : IFoodSpawner
        {
            public event Action<Spawned> Spawned;
            public GameObject Preview(TextureId texture) => null;
            public void Spawn(string food) => Spawned?.Invoke(new Spawned(new FoodId(food), Vector3.zero));
        }

        // 待ちを外から作れるようにした人形劇スクリプト。
        sealed class Probe : TutorialScript
        {
            public override PhaseId Phase => new PhaseId("probe");
            public override IEnumerator Play() { yield break; }
            public ForkWait Pierce() => WaitForPierce();
            public ForkWait Mouth() => WaitForMouth();
            public SpawnWait Spawn(string food = "") => WaitForSpawn(food);
            public TextureChangeWait Changes(int count) => WaitForTextureChanges(count);
            public ConfirmWait Confirm() => WaitForConfirm();
            public bool Already(TutorialEvent kind) => AlreadyDone(kind);
        }

        GameObject _host;
        Probe _probe;
        TutorialHistory _history;
        FakeFork _fork;
        FakeDevice _device;
        FakeSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _fork = new FakeFork();
            _device = new FakeDevice();
            _spawner = new FakeSpawner();
            Forks.Register(_fork);
            Devices.Register(_device);
            FoodSpawners.Register(_spawner);

            _host = new GameObject("probe");
            _probe = _host.AddComponent<Probe>();
            _history = new TutorialHistory(); // Runner と同じく、チュートリアルの始まりに記録を始める
            _probe.Attach(new TutorialMemory(), _history);
        }

        [TearDown]
        public void TearDown()
        {
            _probe.CancelWaits();
            _history.Dispose();
            UnityEngine.Object.DestroyImmediate(_host);
            Forks.Unregister(_fork);
            Devices.Unregister(_device);
            FoodSpawners.Unregister(_spawner);
        }

        [Test]
        public void Spawn_BeforeTheWait_CompletesAtOnce_AndIsUsedOnlyOnce()
        {
            _spawner.Spawn("apple");

            SpawnWait first = _probe.Spawn();
            Assert.IsTrue(first.IsDone, "先に生まれていた物で、すぐ終わる");
            Assert.AreEqual(new FoodId("apple"), first.Result.Value.Food);

            SpawnWait second = _probe.Spawn();
            Assert.IsTrue(second.keepWaiting, "使ったリンゴでは、もう進まない");
            _spawner.Spawn("fish");
            Assert.IsFalse(second.keepWaiting);
            Assert.AreEqual(new FoodId("fish"), second.Result.Value.Food);

            Assert.IsTrue(_probe.Spawn().keepWaiting, "待ちの中で使った魚も、次では数えない");
        }

        [Test]
        public void Spawn_WithFilter_SkipsOtherFoods()
        {
            _spawner.Spawn("apple");
            _spawner.Spawn("fish");

            SpawnWait fish = _probe.Spawn("fish");
            Assert.IsTrue(fish.IsDone);
            Assert.AreEqual(new FoodId("fish"), fish.Result.Value.Food);
        }

        [Test]
        public void Pierce_BeforeTheWait_Counts_EvenAfterThePieceIsGone()
        {
            _fork.Pierce();
            _fork.IsLoaded = false; // 刺したあと、もう食べてしまった

            Assert.IsTrue(_probe.Already(TutorialEvent.Pierced));
            Assert.IsTrue(_probe.Pierce().IsDone);
            Assert.IsFalse(_probe.Already(TutorialEvent.Pierced), "使ったら、残っていない");
            Assert.IsTrue(_probe.Pierce().keepWaiting, "同じ刺し方で、2 回は進まない");
        }

        [Test]
        public void Mouth_WithoutAPiece_IsNotCounted()
        {
            _fork.EnterMouth(); // かけらなし
            _fork.InMouth = false;

            Assert.IsTrue(_probe.Mouth().keepWaiting);
        }

        [Test]
        public void TextureChanges_BeforeTheWait_AreCounted()
        {
            _device.Change("a");
            _device.Change("b");

            TextureChangeWait wait = _probe.Changes(3);
            Assert.IsTrue(wait.keepWaiting);
            Assert.AreEqual(2, wait.Changes);

            _device.Change("c");
            Assert.IsFalse(wait.keepWaiting);
            Assert.IsTrue(_probe.Changes(1).keepWaiting, "使った分は、次の待ちで数えない");
        }

        [Test]
        public void Confirm_BeforeTheWait_Counts_ButNotWithoutATexture()
        {
            _device.Confirm(""); // 噛む前の決定は数えない
            Assert.IsTrue(_probe.Confirm().keepWaiting);

            _probe.CancelWaits();
            _device.Confirm("b");
            ConfirmWait wait = _probe.Confirm();
            Assert.IsTrue(wait.IsDone);
            Assert.AreEqual(new TextureId("b"), wait.Result);
        }

        [Test]
        public void EventsBeforeTheTutorialStarted_AreNotCounted()
        {
            _history.Dispose();
            _spawner.Spawn("apple"); // 前のチュートリアルで生まれた物
            _history = new TutorialHistory();
            _probe.Attach(new TutorialMemory(), _history);

            Assert.IsTrue(_probe.Spawn().keepWaiting);
        }
    }
}
