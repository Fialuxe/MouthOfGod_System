using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tutorial.Tests
{
    // 人形劇スクリプトを順に動かす決まり。EditMode ではフレームが進まないので、Run() を手で MoveNext して進める。
    // Begin・Stop（StartCoroutine を使う所）は、ここでは確かめていない。
    public class TutorialRunnerTests
    {
        // 起きたことを順に記録する
        static readonly List<string> Log = new List<string>();

        sealed class StepScript : TutorialScript
        {
            public string Name = "step";
            public int Frames = 1;
            public bool ThrowInside;
            public bool ThrowAtCall;
            public string Remember;
            public string CueName;

            public override PhaseId Phase => new PhaseId(Name);

            public override IEnumerator Play()
            {
                if (ThrowAtCall) throw new InvalidOperationException("at call " + Name);
                return Body();
            }

            IEnumerator Body()
            {
                Log.Add("start:" + Name + ":" + Phases.Current);
                if (!string.IsNullOrEmpty(Remember)) Memory.Set(Remember);
                Log.Add("has-k:" + Name + ":" + Memory.Has("k"));
                if (!string.IsNullOrEmpty(CueName)) Cue(CueName);
                for (int i = 0; i < Frames; i++) yield return null;
                if (ThrowInside) throw new InvalidOperationException("inside " + Name);
                Log.Add("end:" + Name);
            }

            public DeliveryWait StartDeliveryWait() => WaitForDelivery();
        }

        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        readonly List<GameObject> _created = new List<GameObject>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;
        GameObject _root;
        TutorialRunner _runner;

        [SetUp]
        public void SetUp()
        {
            Log.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _exceptions.Clear();
            _warnings.Clear();
            _created.Clear();
            _savedException = TutorialRunner.ReportException;
            _savedWarning = TutorialRunner.ReportWarning;
            TutorialRunner.ReportException = _exceptions.Add;
            TutorialRunner.ReportWarning = _warnings.Add;
            Phases.Set(PhaseId.None);

            _root = new GameObject("Tutorial");
            _created.Add(_root);
            _runner = _root.AddComponent<TutorialRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            Phases.Set(PhaseId.None);
            TutorialRunner.ReportException = _savedException;
            TutorialRunner.ReportWarning = _savedWarning;
            foreach (GameObject go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        StepScript AddScript(string name, Action<StepScript> configure = null)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.SetParent(_root.transform);
            StepScript script = go.AddComponent<StepScript>();
            script.Name = name;
            if (configure != null) configure(script);
            return script;
        }

        static void RunToEnd(IEnumerator run)
        {
            int guard = 0;
            while (run.MoveNext())
                if (++guard > 1000) throw new AssertionException("終わらない");
        }

        [Test]
        public void Skip_PassesThroughLastPhaseThenEmpty_AndRaisesFinished()
        {
            var seen = new List<string>();
            Action<PhaseId> onChanged = p => seen.Add(p.Value);
            Phases.Changed += onChanged;
            bool finished = false;
            _runner.Finished += () => finished = true;
            try { _runner.Skip(); }
            finally { Phases.Changed -= onChanged; }

            CollectionAssert.AreEqual(new[] { "deliver", "" }, seen);
            Assert.IsTrue(finished);
            Assert.IsFalse(_runner.IsRunning);
            Assert.IsTrue(Phases.Current.IsEmpty);
            Assert.IsTrue(_runner.HasFinished);
        }

        [Test]
        public void HasFinished_BecomesTrueOnlyAfterAllScripts()
        {
            AddScript("a");
            Assert.IsFalse(_runner.HasFinished);
            IEnumerator run = _runner.Run();
            run.MoveNext();
            Assert.IsFalse(_runner.HasFinished);
            RunToEnd(run);
            Assert.IsTrue(_runner.HasFinished);
        }

        [Test]
        public void Run_RunsChildScriptsInHierarchyOrder_AndSetsEachPhase()
        {
            AddScript("fork");
            AddScript("spawn");
            AddScript("deliver");

            RunToEnd(_runner.Run());

            CollectionAssert.AreEqual(new[]
            {
                "start:fork:fork", "has-k:fork:False", "end:fork",
                "start:spawn:spawn", "has-k:spawn:False", "end:spawn",
                "start:deliver:deliver", "has-k:deliver:False", "end:deliver",
            }, Log);
            Assert.IsTrue(Phases.Current.IsEmpty); // 全部終わったら、場面を空に戻す
        }

        [Test]
        public void Run_RaisesStartedAndFinishedEvents_InOrder()
        {
            AddScript("a");
            AddScript("b");
            var events = new List<string>();
            _runner.ScriptStarted += p => events.Add("started:" + p);
            _runner.ScriptFinished += p => events.Add("finished:" + p);
            _runner.Finished += () => events.Add("all");

            RunToEnd(_runner.Run());

            CollectionAssert.AreEqual(new[] { "started:a", "finished:a", "started:b", "finished:b", "all" }, events);
        }

        [Test]
        public void Run_SkipsDisabledScripts()
        {
            AddScript("a");
            AddScript("b").enabled = false;
            AddScript("c");

            RunToEnd(_runner.Run());

            CollectionAssert.AreEqual(new[] { "start:a:a", "has-k:a:False", "end:a", "start:c:c", "has-k:c:False", "end:c" }, Log);
        }

        [Test]
        public void Run_ScriptWaitingAFewFrames_HoldsTheNextOne()
        {
            AddScript("a", s => s.Frames = 3);
            AddScript("b");
            IEnumerator run = _runner.Run();

            int frames = 0;
            while (run.MoveNext()) frames++;

            Assert.AreEqual(4, frames); // a の 3 フレーム + b の 1 フレーム
            Assert.AreEqual("end:a", Log[2]);
            Assert.AreEqual("start:b:b", Log[3]);
        }

        [Test]
        public void Run_ExceptionInsideAScript_IsReported_AndTheNextScriptStillRuns()
        {
            AddScript("a", s => s.ThrowInside = true);
            AddScript("b");

            RunToEnd(_runner.Run());

            Assert.AreEqual(1, _exceptions.Count);
            StringAssert.Contains("inside a", _exceptions[0].Message);
            CollectionAssert.Contains(Log, "start:b:b");
            CollectionAssert.Contains(Log, "end:b");
            CollectionAssert.DoesNotContain(Log, "end:a");
        }

        [Test]
        public void Run_ExceptionWhenCallingPlay_IsReported_AndTheNextScriptStillRuns()
        {
            AddScript("a", s => s.ThrowAtCall = true);
            AddScript("b");

            RunToEnd(_runner.Run());

            Assert.AreEqual(1, _exceptions.Count);
            StringAssert.Contains("at call a", _exceptions[0].Message);
            CollectionAssert.Contains(Log, "end:b");
        }

        [Test]
        public void Memory_IsSharedBetweenScripts_ByName()
        {
            AddScript("a", s => s.Remember = "k");
            AddScript("b");

            RunToEnd(_runner.Run());

            CollectionAssert.Contains(Log, "has-k:b:True");
        }

        [Test]
        public void Cue_ReachesCueSubscribers()
        {
            var got = new List<CueId>();
            Action<CueRequested> handler = r => got.Add(r.Cue);
            Cues.Requested += handler;
            try
            {
                AddScript("a", s => s.CueName = "sea.glow");

                RunToEnd(_runner.Run());

                CollectionAssert.AreEqual(new[] { new CueId("sea.glow") }, got);
            }
            finally
            {
                Cues.Requested -= handler;
            }
        }

        [Test]
        public void Waits_LeftOverByAScript_AreCancelledWhenItEnds()
        {
            DeliveryWait leftOver = null;
            AddScript("a", s => leftOver = s.StartDeliveryWait()); // 待たずに終わる

            RunToEnd(_runner.Run());

            Assert.IsTrue(leftOver.IsDone);
        }

        [Test]
        public void Run_WithNoScripts_FinishesAtOnce()
        {
            var events = new List<string>();
            _runner.Finished += () => events.Add("all");

            RunToEnd(_runner.Run());

            CollectionAssert.AreEqual(new[] { "all" }, events);
        }

        [Test]
        public void Run_EmptyPhase_Warns()
        {
            AddScript("", null);

            RunToEnd(_runner.Run());

            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void IsRunning_IsFalse_BeforeBegin()
        {
            Assert.IsFalse(_runner.IsRunning);
        }
    }
}
