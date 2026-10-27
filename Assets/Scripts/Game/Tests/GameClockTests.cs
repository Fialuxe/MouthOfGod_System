using NUnit.Framework;

namespace MouthOfGod.Game.Tests
{
    public class GameClockTests
    {
        [Test]
        public void StartsAtTotal_NotCountdownNotTimeUp()
        {
            var clock = new GameClock(120f);
            Assert.AreEqual(120f, clock.Remaining);
            Assert.AreEqual(120, clock.RemainingWholeSeconds);
            Assert.IsFalse(clock.IsCountdown);
            Assert.IsFalse(clock.IsTimeUp);
        }

        [Test]
        public void TimesUp_After120Seconds()
        {
            var clock = new GameClock(120f);
            int timeUps = 0;
            clock.TimeUp += () => timeUps++;

            clock.Tick(119f);
            Assert.IsFalse(clock.IsTimeUp);
            Assert.AreEqual(0, timeUps);

            clock.Tick(1f);
            Assert.IsTrue(clock.IsTimeUp);
            Assert.AreEqual(0f, clock.Remaining);
            Assert.AreEqual(1, timeUps);
        }

        [Test]
        public void Countdown_BoundaryAt10Seconds()
        {
            var clock = new GameClock(120f);
            int started = 0;
            clock.CountdownStarted += () => started++;

            clock.Tick(109.5f); // 残り 10.5
            Assert.IsFalse(clock.IsCountdown);
            Assert.AreEqual(0, started);

            clock.Tick(0.5f); // 残りちょうど 10
            Assert.IsTrue(clock.IsCountdown);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void Events_DoNotRepeat_AfterTimeUp()
        {
            var clock = new GameClock(120f);
            int started = 0, timeUps = 0, changed = 0;
            clock.CountdownStarted += () => started++;
            clock.TimeUp += () => timeUps++;
            clock.RemainingChanged += _ => changed++;

            clock.Tick(120f);
            int changedAtEnd = changed;
            clock.Tick(5f);
            clock.Tick(100f);

            Assert.AreEqual(1, started);
            Assert.AreEqual(1, timeUps);
            Assert.AreEqual(changedAtEnd, changed);
            Assert.AreEqual(0f, clock.Remaining);
        }

        [Test]
        public void OneBigTick_RaisesCountdownThenTimeUp()
        {
            var clock = new GameClock(120f);
            var order = new System.Collections.Generic.List<string>();
            clock.CountdownStarted += () => order.Add("countdown");
            clock.TimeUp += () => order.Add("timeup");

            clock.Tick(1000f);

            CollectionAssert.AreEqual(new[] { "countdown", "timeup" }, order);
        }

        [Test]
        public void RemainingChanged_RaisedWhenWholeSecondChanges()
        {
            var clock = new GameClock(120f);
            var got = new System.Collections.Generic.List<int>();
            clock.RemainingChanged += got.Add;

            clock.Tick(0.4f); // 119.6 -> 120 のまま
            clock.Tick(0.4f); // 119.2 -> 120 のまま
            clock.Tick(0.3f); // 118.9 -> 119

            CollectionAssert.AreEqual(new[] { 119 }, got);
        }

        [Test]
        public void NonPositiveOrNaNTick_DoesNothing()
        {
            var clock = new GameClock(10f);
            int started = 0;
            clock.CountdownStarted += () => started++;

            clock.Tick(0f);
            clock.Tick(-5f);
            clock.Tick(float.NaN);

            Assert.AreEqual(10f, clock.Remaining);
            Assert.AreEqual(0, started); // 開始時から 10 秒以下でも、Tick するまで出ない
            Assert.IsFalse(clock.IsTimeUp);
        }

        [Test]
        public void ZeroOrNegativeTotal_TimesUpOnFirstTick_Once()
        {
            var clock = new GameClock(-3f);
            int timeUps = 0;
            clock.TimeUp += () => timeUps++;

            clock.Tick(0f);
            clock.Tick(1f);

            Assert.AreEqual(0f, clock.TotalSeconds);
            Assert.AreEqual(1, timeUps);
        }
    }
}
