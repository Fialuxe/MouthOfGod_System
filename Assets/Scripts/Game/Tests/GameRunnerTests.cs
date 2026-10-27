using System.Collections.Generic;
using System.Reflection;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Game.Tests
{
    public class GameRunnerTests
    {
        static readonly PhaseId Deliver = new PhaseId("deliver");
        static readonly PhaseId MainGame = new PhaseId("main-game");
        static readonly PhaseId GameOver = new PhaseId("game-over");

        GameObject _go;
        GameRunner _runner;

        [SetUp]
        public void SetUp()
        {
            Phases.Reset();
            _go = new GameObject("GameRunnerTest");
            _runner = _go.AddComponent<GameRunner>();
            _runner.Attach(); // EditMode では OnEnable が呼ばれないので、自分で Phases を見はじめる
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Unattach();
            Object.DestroyImmediate(_go);
            Phases.Reset();
        }

        static void SetField(GameRunner r, string name, object value) =>
            typeof(GameRunner).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(r, value);

        [Test]
        public void TutorialEnds_StartsMainGame()
        {
            Phases.Set(Deliver);
            Assert.AreNotEqual(MainGame, Phases.Current);

            Phases.Set(PhaseId.None);

            Assert.AreEqual(MainGame, Phases.Current);
            Assert.IsTrue(_runner.IsRunning);
        }

        [Test]
        public void OtherPhaseEnding_DoesNotStart()
        {
            Phases.Set(new PhaseId("fork"));
            Phases.Set(PhaseId.None);

            Assert.AreEqual(PhaseId.None, Phases.Current);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void EmptyAfterPhase_DoesNotStartAutomatically()
        {
            SetField(_runner, "afterPhase", "");

            Phases.Set(Deliver);
            Phases.Set(PhaseId.None);

            Assert.AreEqual(PhaseId.None, Phases.Current);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void TimeUp_SetsGameOver_BeforeRaisingTimeUp()
        {
            SetField(_runner, "timeLimitSeconds", 5f);
            var seen = new List<PhaseId>();
            _runner.TimeUp += () => seen.Add(Phases.Current);
            _runner.Begin();

            _runner.Tick(5f);
            _runner.Tick(5f); // 時間切れのあとは何も起きない

            Assert.AreEqual(GameOver, Phases.Current);
            CollectionAssert.AreEqual(new[] { GameOver }, seen);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void Begin_Again_RestartsFromTheStart()
        {
            SetField(_runner, "timeLimitSeconds", 30f);
            _runner.Begin();
            _runner.Tick(12f);
            Assert.AreEqual(18f, _runner.Remaining, 0.001f);

            _runner.Begin();

            Assert.AreEqual(30f, _runner.Remaining, 0.001f);
            Assert.AreEqual(MainGame, Phases.Current);
        }

        [Test]
        public void Begin_AfterGameOver_RunsAgain_AndCountdownFiresAgain()
        {
            SetField(_runner, "timeLimitSeconds", 12f);
            int countdowns = 0;
            _runner.CountdownStarted += () => countdowns++;
            _runner.Begin();
            _runner.Tick(12f);

            _runner.Begin();
            _runner.Tick(3f);

            Assert.AreEqual(MainGame, Phases.Current);
            Assert.IsTrue(_runner.IsRunning);
            Assert.AreEqual(2, countdowns);
        }
    }
}
