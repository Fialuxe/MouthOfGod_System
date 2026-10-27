using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MouthOfGod.Tutorial.Tests
{
    // プロローグの流れと、動画の画面の置き方の計算。EditMode ではフレームが進まないので、Play() を手で MoveNext して進める。
    public class PrologueVideoTests
    {
        readonly List<string> _cues = new List<string>();
        GameObject _go;

        void OnCue(CueRequested request) => _cues.Add(request.Cue.Value);

        [SetUp]
        public void SetUp()
        {
            _cues.Clear();
            Cues.Requested += OnCue;
            _go = new GameObject("prologue");
        }

        [TearDown]
        public void TearDown()
        {
            Cues.Requested -= OnCue;
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Play_WithoutScreen_SendsCuesAndFinishes()
        {
            var prologue = _go.AddComponent<PrologueVideo>();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VideoScreen が無い"));

            IEnumerator play = prologue.Play();
            int steps = 0;
            while (play.MoveNext())
            {
                if (++steps > 10) Assert.Fail("終わらない");
            }

            CollectionAssert.AreEqual(new[] { "prologue.start", "prologue.end", "miko.say.prologue-stand-up" }, _cues);
            Assert.AreEqual(new PhaseId("prologue"), prologue.Phase);
        }

        [Test]
        public void ScreenHeight_FollowsTheVideoAspect()
        {
            Assert.AreEqual(1.125f, VideoScreen.ScreenHeight(2f, 1920, 1080), 0.0001f);
            Assert.AreEqual(2f, VideoScreen.ScreenHeight(2f, 1080, 1080), 0.0001f);
        }

        [Test]
        public void ScreenWidth_CoversTheViewAngle()
        {
            Assert.AreEqual(6f, VideoScreen.ScreenWidth(3f, 90f), 0.0001f);
            Assert.AreEqual(2f * 3f * Mathf.Tan(50f * Mathf.Deg2Rad), VideoScreen.ScreenWidth(3f, 100f), 0.0001f);
        }

        [Test]
        public void FitWidth_WideVideo_IsLimitedByTheHorizontalAngle()
        {
            // 16:9 は、水平 80 度の幅（約 5.03 m）のほうが、縦 55 度から決まる幅（約 5.55 m）より小さい。
            float width = VideoScreen.FitWidth(3f, 80f, 55f, 1920, 1080);

            Assert.AreEqual(VideoScreen.ScreenWidth(3f, 80f), width, 0.0001f);
        }

        [Test]
        public void FitWidth_SquareVideo_IsLimitedByTheVerticalAngle()
        {
            // 正方形は、縦 55 度で決まる高さ（約 3.12 m）が、そのまま幅になる。水平の幅（約 5.03 m）まで広げると、上下にはみ出す。
            float width = VideoScreen.FitWidth(3f, 80f, 55f, 1080, 1080);

            Assert.AreEqual(VideoScreen.ScreenWidth(3f, 55f), width, 0.0001f);
            Assert.Less(width, VideoScreen.ScreenWidth(3f, 80f));
        }

        [Test]
        public void FitWidth_UnknownSize_Uses16By9()
        {
            Assert.AreEqual(VideoScreen.FitWidth(3f, 80f, 55f, 1920, 1080), VideoScreen.FitWidth(3f, 80f, 55f, 0, 0), 0.0001f);
        }

        [Test]
        public void ScreenHeight_UnknownSize_Uses16By9()
        {
            Assert.AreEqual(2f * 9f / 16f, VideoScreen.ScreenHeight(2f, 0, 0), 0.0001f);
        }

        [Test]
        public void ScreenPosition_IsInFrontAtEyeHeightPlusOffset()
        {
            Vector3 p = VideoScreen.ScreenPosition(new Vector3(1f, 1.6f, 0f), Vector3.forward, 3f, -0.1f);
            Assert.AreEqual(1f, p.x, 0.0001f);
            Assert.AreEqual(1.5f, p.y, 0.0001f);
            Assert.AreEqual(3f, p.z, 0.0001f);
        }
    }
}
