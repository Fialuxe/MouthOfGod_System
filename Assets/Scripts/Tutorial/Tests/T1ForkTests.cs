using NUnit.Framework;

namespace MouthOfGod.Tutorial.Tests
{
    // T1Fork の、ヒントとあきらめる時間の決まり。流れそのものは、Play して確かめる（フレームと実時間が要るため）。
    public class T1ForkTests
    {
        [Test]
        public void HintComesFirst_BeforeGiveUp()
        {
            Assert.IsTrue(T1Fork.HintComesFirst(10f, 0f, 45f));
            Assert.IsTrue(T1Fork.HintComesFirst(20f, 10f, 45f));
        }

        [Test]
        public void HintComesFirst_NoGiveUp_AlwaysIfAhead()
        {
            Assert.IsTrue(T1Fork.HintComesFirst(10f, 0f, 0f));
        }

        [Test]
        public void HintComesFirst_ZeroHint_IsOff()
        {
            Assert.IsFalse(T1Fork.HintComesFirst(0f, 0f, 45f));
            Assert.IsFalse(T1Fork.HintComesFirst(0f, 0f, 0f));
        }

        [Test]
        public void HintComesFirst_AtOrAfterGiveUp_IsSkipped()
        {
            Assert.IsFalse(T1Fork.HintComesFirst(45f, 0f, 45f));
            Assert.IsFalse(T1Fork.HintComesFirst(60f, 0f, 45f));
        }

        [Test]
        public void HintComesFirst_AlreadyPassed_IsSkipped()
        {
            Assert.IsFalse(T1Fork.HintComesFirst(10f, 10f, 45f));
            Assert.IsFalse(T1Fork.HintComesFirst(5f, 10f, 45f));
        }

        [Test]
        public void RemainingSeconds_IsGiveUpMinusElapsed()
        {
            Assert.AreEqual(35f, T1Fork.RemainingSeconds(45f, 10f), 0.0001f);
            Assert.AreEqual(45f, T1Fork.RemainingSeconds(45f, 0f), 0.0001f);
        }

        [Test]
        public void RemainingSeconds_NoGiveUp_WaitsForever()
        {
            Assert.AreEqual(0f, T1Fork.RemainingSeconds(0f, 10f));
        }

        [Test]
        public void RemainingSeconds_NothingLeft_StillTimesOut()
        {
            Assert.Greater(T1Fork.RemainingSeconds(10f, 10f), 0f); // 0 は「待ち続ける」になるので、0 にはしない
            Assert.Greater(T1Fork.RemainingSeconds(10f, 20f), 0f);
        }
    }
}
