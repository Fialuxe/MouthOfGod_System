using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.UI.Tests
{
    // 合図への反応。EditMode なので OnEnable は呼ばれない。Handle を直接呼ぶ。
    public class CueReactionTests
    {
        readonly List<GameObject> _created = new List<GameObject>();
        CueReaction _reaction;

        [SetUp]
        public void SetUp()
        {
            _created.Clear();
            _reaction = NewObject("Reaction").AddComponent<CueReaction>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        GameObject NewObject(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            _created.Add(go);
            return go;
        }

        [Test]
        public void Handle_MatchingCue_ShowsAndHides_AndInvokesEvent()
        {
            var glow = NewObject("Glow", active: false);
            var dim = NewObject("Dim");
            var entry = new CueReaction.Entry("sea.glow", new[] { glow }, new[] { dim });
            var called = 0;
            entry.OnCue.AddListener(() => called++);
            _reaction.Configure(entry);

            Assert.AreEqual(1, _reaction.Handle(new CueId("sea.glow")));

            Assert.IsTrue(glow.activeSelf);
            Assert.IsFalse(dim.activeSelf);
            Assert.AreEqual(1, called);
        }

        [Test]
        public void Handle_OtherCue_OrEmpty_DoesNothing()
        {
            var glow = NewObject("Glow", active: false);
            _reaction.Configure(new CueReaction.Entry("sea.glow", new[] { glow }, null));

            Assert.AreEqual(0, _reaction.Handle(new CueId("village.glow")));
            Assert.AreEqual(0, _reaction.Handle(new CueId("Sea.Glow")), "大文字小文字は区別する");
            Assert.AreEqual(0, _reaction.Handle(CueId.None));

            Assert.IsFalse(glow.activeSelf);
        }

        [Test]
        public void Handle_RunsEveryMatchingEntry_AndSkipsNullObjects()
        {
            var a = NewObject("A", active: false);
            var b = NewObject("B", active: false);
            _reaction.Configure(
                new CueReaction.Entry("village.cheer", new[] { a, null }, new GameObject[] { null }),
                null,
                new CueReaction.Entry("village.cheer", new[] { b }, null));

            Assert.AreEqual(2, _reaction.Handle(new CueId("village.cheer")));

            Assert.IsTrue(a.activeSelf);
            Assert.IsTrue(b.activeSelf);
        }

        [Test]
        public void Handle_EventException_IsReported_AndOtherEntriesStillRun()
        {
            var exceptions = new List<Exception>();
            Action<Exception> saved = CueReaction.ReportException;
            CueReaction.ReportException = exceptions.Add;
            try
            {
                var b = NewObject("B", active: false);
                var first = new CueReaction.Entry("staff.alert", null, null);
                first.OnCue.AddListener(() => throw new InvalidOperationException("boom"));
                _reaction.Configure(first, new CueReaction.Entry("staff.alert", new[] { b }, null));

                Assert.AreEqual(2, _reaction.Handle(new CueId("staff.alert")));

                Assert.AreEqual(1, exceptions.Count);
                Assert.IsTrue(b.activeSelf);
            }
            finally
            {
                CueReaction.ReportException = saved;
            }
        }

        [Test]
        public void Handle_WithSeconds_AppliesNow_AndDoesNotRevertInEditMode()
        {
            var glow = NewObject("Glow", active: false);
            _reaction.Configure(new CueReaction.Entry("device.button.glow", new[] { glow }, null, seconds: 1f));

            _reaction.Handle(new CueId("device.button.glow"));

            // 戻すのは Play 中のコルーチン。ここでは、出したままになることだけ確かめる
            Assert.IsTrue(glow.activeSelf);
        }
    }
}
