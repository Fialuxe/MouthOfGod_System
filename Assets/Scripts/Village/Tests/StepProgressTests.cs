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
        public void TryOpenWithFood_OpensTheFirstNotDoneStepWantingIt()
        {
            var progress = new StepProgress(new[] { Carrot, Pumpkin, Pumpkin });
            progress.Complete(1);

            Assert.IsTrue(progress.TryOpen(Pumpkin, out var index));
            Assert.AreEqual(2, index);
            Assert.IsFalse(progress.TryOpen(Carrot, out _), "出ている間は出せない");
            progress.Close();
            Assert.IsFalse(progress.TryOpen(new FoodId("test-fish"), out _));
            Assert.IsFalse(progress.TryOpen(FoodId.None, out _));
        }

        [Test]
        public void Wants_IsTrueForAnyStepsFood_EvenIfDone()
        {
            var progress = CreateProgress();
            progress.Complete(0);

            Assert.IsTrue(progress.Wants(Carrot));
            Assert.IsTrue(progress.Wants(Pumpkin));
            Assert.IsFalse(progress.Wants(new FoodId("test-pig")));
            Assert.IsFalse(progress.Wants(FoodId.None));
        }

        [Test]
        public void TryOpenRandom_PicksOnlyNotDoneSteps_AndCoversAll()
        {
            var sheep = new FoodId("test-sheep");
            var progress = new StepProgress(new[] { Carrot, Pumpkin, sheep, FoodId.None });
            progress.Complete(1);
            var random = new System.Random(1);
            var opened = new System.Collections.Generic.HashSet<int>();

            for (var i = 0; i < 50; i++)
            {
                Assert.IsTrue(progress.TryOpen(out var index, random));
                opened.Add(index);
                progress.Close();
            }

            CollectionAssert.AreEquivalent(new[] { 0, 2 }, opened); // 済んだ 1 と、欲しい物が空の 3 は出ない
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

        // ---- 同時に複数出す（maxOpen が 2 以上）

        [Test]
        public void MaxOpenTwo_OpensTwoDifferentSteps_ThenStops()
        {
            var progress = new StepProgress(new[] { Carrot, Pumpkin, new FoodId("test-sheep") }, maxOpen: 2);

            Assert.IsTrue(progress.TryOpen(out var first));
            Assert.IsTrue(progress.TryOpen(out var second));
            Assert.IsFalse(progress.TryOpen(out _));

            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
            CollectionAssert.AreEqual(new[] { 0, 1 }, progress.OpenIndices);
            Assert.AreEqual(Carrot, progress.Current, "一番古いもの");
        }

        [Test]
        public void MaxOpenTwo_ResolvesTheOldestWithTheSameFood()
        {
            var progress = new StepProgress(new[] { Pumpkin, Carrot, Pumpkin }, maxOpen: 3);
            progress.TryOpen(out _);
            progress.TryOpen(out _);
            progress.TryOpen(out _);

            Assert.IsTrue(progress.TryResolve(Pumpkin, out var index));
            Assert.AreEqual(0, index);
            Assert.IsTrue(progress.TryResolve(Pumpkin, out index));
            Assert.AreEqual(2, index);
            CollectionAssert.AreEqual(new[] { 1 }, progress.OpenIndices);
        }

        [Test]
        public void MaxOpenTwo_CloseOne_AndCloseAll()
        {
            var progress = new StepProgress(new[] { Carrot, Pumpkin }, maxOpen: 2);
            progress.TryOpen(out _);
            progress.TryOpen(out _);

            Assert.IsTrue(progress.Close(1));
            Assert.IsFalse(progress.Close(1));
            CollectionAssert.AreEqual(new[] { 0 }, progress.OpenIndices);

            progress.TryOpen(out _);
            CollectionAssert.AreEqual(new[] { 0, 1 }, progress.CloseAll());
            Assert.IsFalse(progress.IsOpen);
            Assert.AreEqual(0, progress.DoneCount);
        }

        [Test]
        public void MaxOpenTwo_RandomNeverPicksAnOpenStep()
        {
            var progress = new StepProgress(new[] { Carrot, Pumpkin }, maxOpen: 2);
            var random = new System.Random(3);
            for (var i = 0; i < 30; i++)
            {
                Assert.IsTrue(progress.TryOpen(out var a, random));
                Assert.IsTrue(progress.TryOpen(out var b, random));
                Assert.AreNotEqual(a, b);
                progress.CloseAll();
            }
        }

        [Test]
        public void MaxOpenBelowOne_IsOne()
        {
            Assert.AreEqual(1, new StepProgress(new[] { Carrot }, maxOpen: 0).MaxOpen);
        }
    }
}
