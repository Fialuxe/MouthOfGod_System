using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.UI.Tests
{
    // 影の、Unity の描画に依存しない部分（合図の読み分け・濃さ・大きさ）。影の形を作る所と、置く所は Play で確かめる。
    public class FoodShadowTests
    {
        static ShadowMode? Mode(string cue) =>
            FoodShadow.ModeForCue(new CueId(cue), "shadow.show", "shadow.faint", "shadow.emphasize", "shadow.hide");

        [Test]
        public void ModeForCue_ReadsTheFourCues()
        {
            Assert.AreEqual(ShadowMode.Normal, Mode("shadow.show"));
            Assert.AreEqual(ShadowMode.Faint, Mode("shadow.faint"));
            Assert.AreEqual(ShadowMode.Emphasize, Mode("shadow.emphasize"));
            Assert.AreEqual(ShadowMode.Hidden, Mode("shadow.hide"));
        }

        [Test]
        public void ModeForCue_IgnoresOtherAndEmptyCues()
        {
            Assert.IsNull(Mode("sea.glow"));
            Assert.IsNull(Mode(""));
        }

        [Test]
        public void ModeForCue_EmptyNameNeverMatches()
        {
            Assert.IsNull(FoodShadow.ModeForCue(new CueId("shadow.show"), "", "", "", ""));
        }

        [Test]
        public void AlphaFor_NormalFaintHidden()
        {
            Assert.AreEqual(0.5f, FoodShadow.AlphaFor(ShadowMode.Normal, 0.5f, 0.2f, 0.9f, 0.5f, 1f, 3f), 1e-5f);
            Assert.AreEqual(0.2f, FoodShadow.AlphaFor(ShadowMode.Faint, 0.5f, 0.2f, 0.9f, 0.5f, 1f, 3f), 1e-5f);
            Assert.AreEqual(0f, FoodShadow.AlphaFor(ShadowMode.Hidden, 0.5f, 0.2f, 0.9f, 0.5f, 1f, 3f));
        }

        [Test]
        public void AlphaFor_EmphasizeStartsStrongest_AndPulsesDownByDepth()
        {
            Assert.AreEqual(0.9f, FoodShadow.AlphaFor(ShadowMode.Emphasize, 0.5f, 0.2f, 0.9f, 0.5f, 1f, 0f), 1e-5f);
            Assert.AreEqual(0.45f, FoodShadow.AlphaFor(ShadowMode.Emphasize, 0.5f, 0.2f, 0.9f, 0.5f, 1f, 0.5f), 1e-5f, "半周期で、いちばん薄い");
            Assert.AreEqual(0.9f, FoodShadow.AlphaFor(ShadowMode.Emphasize, 0.5f, 0.2f, 0.9f, 0f, 1f, 0.5f), 1e-5f, "深さ 0 なら脈打たない");
        }

        [Test]
        public void FitScale_MakesTheLongestSideTheTarget()
        {
            Assert.AreEqual(0.5f, FoodShadow.FitScale(new Vector3(0.1f, 0.24f, 0.2f), 0.12f), 1e-5f);
            Assert.AreEqual(1f, FoodShadow.FitScale(new Vector3(0.1f, 0.2f, 0.2f), 0f), "0 ならそのまま");
            Assert.AreEqual(1f, FoodShadow.FitScale(Vector3.zero, 0.12f), "大きさ 0 なら、そのまま");
        }
    }
}
