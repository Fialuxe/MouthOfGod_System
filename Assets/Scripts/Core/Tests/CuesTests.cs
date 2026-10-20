using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MouthOfGod.Core.Tests
{
    public class CuesTests
    {
        readonly List<Exception> _exceptions = new List<Exception>();
        readonly List<string> _warnings = new List<string>();
        Action<Exception> _savedException;
        Action<string> _savedWarning;

        [SetUp]
        public void SetUp()
        {
            Cues.Reset();
            _exceptions.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _warnings.Clear();
            _savedException = Cues.ReportException;
            _savedWarning = Cues.ReportWarning;
            Cues.ReportException = _exceptions.Add;
            Cues.ReportWarning = _warnings.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Cues.Reset();
            Cues.ReportException = _savedException;
            Cues.ReportWarning = _savedWarning;
        }

        [Test]
        public void Request_DeliversTheCueToEverySubscriber()
        {
            var a = new List<CueId>();
            var b = new List<CueId>();
            Cues.Requested += r => a.Add(r.Cue);
            Cues.Requested += r => b.Add(r.Cue);

            Cues.Request(new CueId("sea.glow"));

            CollectionAssert.AreEqual(new[] { new CueId("sea.glow") }, a);
            CollectionAssert.AreEqual(new[] { new CueId("sea.glow") }, b);
        }

        [Test]
        public void Request_WithNoSubscriber_DoesNothing()
        {
            Cues.Request(new CueId("sea.glow"));

            Assert.IsEmpty(_warnings);
            Assert.IsEmpty(_exceptions);
        }

        [Test]
        public void Request_EmptyCue_WarnsAndRaisesNothing()
        {
            var got = new List<CueId>();
            Cues.Requested += r => got.Add(r.Cue);

            Cues.Request(CueId.None);

            Assert.AreEqual(1, _warnings.Count);
            Assert.IsEmpty(got);
        }

        [Test]
        public void Request_HandlerException_DoesNotStopOtherHandlers()
        {
            var got = new List<CueId>();
            Cues.Requested += r => throw new InvalidOperationException("boom");
            Cues.Requested += r => got.Add(r.Cue);

            Cues.Request(new CueId("village.cheer"));

            Assert.AreEqual(1, _exceptions.Count);
            Assert.AreEqual(1, got.Count);
        }

        [Test]
        public void Reset_ClearsSubscribers()
        {
            var got = new List<CueId>();
            Cues.Requested += r => got.Add(r.Cue);

            Cues.Reset();
            Cues.Request(new CueId("sea.glow"));

            Assert.IsEmpty(got);
        }
    }
}
