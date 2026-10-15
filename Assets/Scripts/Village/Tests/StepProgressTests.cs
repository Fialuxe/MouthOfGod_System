using MouthOfGod.Core;
using NUnit.Framework;

namespace MouthOfGod.Village.Tests
{
    public class StepProgressTests
    {
        static readonly FoodId Carrot = new FoodId("test-carrot");
        static readonly FoodId Pumpkin = new FoodId("test-pumpkin");

        static StepProgress CreateProgress() => new StepProgress(new[] { Carrot, Pumpkin });

        [Test]
        public void TryOpen_OpensTheFirstStepNotDone()
        {
            var progress = CreateProgress();

            Assert.IsTrue(progress.TryOpen(out var index));
            Assert.AreEqual(0, index);
            Assert.AreEqual(Carrot, progress.Current);
        }

        [Test]
        public void TryOpen_WhileOpen_ReturnsFalse_AndKeepsTheOpenWant()
        {
            var progress = CreateProgress();
            progress.TryOpen(out _);

            Assert.IsFalse(progress.CanOpen);
            Assert.IsFalse(progress.TryOpen(out var index));
            Assert.AreEqual(-1, index);
            Assert.AreEqual(Carrot, progress.Current);
        }

        [Test]
        public void TryResolve_SameFood_DoneAndNextOpensTheNextStep()
        {
            var progress = CreateProgress();
            progress.TryOpen(out _);

            Assert.IsTrue(progress.TryResolve(Carrot, out var done));
            Assert.AreEqual(0, done);
            Assert.IsTrue(progress.IsDone(0));
            Assert.IsFalse(progress.IsOpen);
            Assert.IsTrue(progress.TryOpen(out var next));
            Assert.AreEqual(1, next);
            Assert.AreEqual(Pumpkin, progress.Current);
        }

        [Test]
        public void TryResolve_OtherFoodOrNothingOpen_ReturnsFalse()
        {
            var progress = CreateProgress();
            Assert.IsFalse(progress.TryResolve(Carrot, out _));

            progress.TryOpen(out _);
            Assert.IsFalse(progress.TryResolve(Pumpkin, out _));
            Assert.IsFalse(progress.TryResolve(FoodId.None, out _));
            Assert.AreEqual(Carrot, progress.Current);
            Assert.AreEqual(0, progress.DoneCount);
        }

        [Test]
        public void AllDone_IsComplete_AndCannotOpen()
        {
            var progress = CreateProgress();
            progress.TryOpen(out _);
            progress.TryResolve(Carrot, out _);
            progress.TryOpen(out _);
            progress.TryResolve(Pumpkin, out _);

            Assert.IsTrue(progress.IsComplete);
            Assert.IsFalse(progress.CanOpen);
            Assert.IsFalse(progress.TryOpen(out _));
        }

        [Test]
        public void Close_ReturnsTheOpenStep_WithoutDoingIt()
        {
            var progress = CreateProgress();
            progress.TryOpen(out _);

            Assert.AreEqual(0, progress.Close());
            Assert.IsFalse(progress.IsOpen);
            Assert.IsFalse(progress.IsDone(0));
            Assert.AreEqual(-1, progress.Close());
            Assert.IsTrue(progress.CanOpen);
        }

        [Test]
        public void StepsDoneOutOfOrder_NextIsTheFirstNotDone()
        {
            var progress = new StepProgress(new[] { Carrot, Pumpkin, new FoodId("test-sheep") });
            progress.Complete(1);

            Assert.AreEqual(0, progress.NextIndex);
            progress.Complete(0);
            Assert.AreEqual(2, progress.NextIndex);
        }

        [Test]
        public void EmptyWant_IsSkippedWhenOpening()
        {
            var progress = new StepProgress(new[] { FoodId.None, Pumpkin });

            Assert.IsTrue(progress.TryOpen(out var index));
            Assert.AreEqual(1, index);
            Assert.IsFalse(progress.IsComplete);
        }

        [Test]
        public void Reset_UndoesEverything()
        {
            var progress = CreateProgress();
            progress.Complete(0);
            progress.TryOpen(out _);

            progress.Reset();

            Assert.AreEqual(0, progress.DoneCount);
            Assert.IsFalse(progress.IsOpen);
            Assert.AreEqual(0, progress.NextIndex);
        }

        [Test]
        public void NullWants_HasNothingToOpen()
        {
            var progress = new StepProgress(null);

            Assert.AreEqual(0, progress.Count);
            Assert.IsFalse(progress.CanOpen);
            Assert.IsTrue(progress.IsComplete);
        }
    }
}
