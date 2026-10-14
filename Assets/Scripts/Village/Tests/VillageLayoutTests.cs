using System.Linq;
using MouthOfGod.Village.Editor.Draft;
using NUnit.Framework;

namespace MouthOfGod.Village.Tests
{
    public class VillageLayoutTests
    {
        [Test]
        public void Default_HasOnlyVillageField_WithItems()
        {
            var fields = VillageLayout.CreateDefault();

            Assert.AreEqual(1, fields.Count);
            Assert.AreEqual(VillageLayout.Village, fields[0].Id);
            Assert.IsNotEmpty(fields[0].Items);
        }

        [Test]
        public void Default_EveryItemHasAPrefabName()
        {
            var items = VillageLayout.CreateDefault().SelectMany(f => f.Items);

            Assert.IsTrue(items.All(i => !string.IsNullOrEmpty(i.Prefab)));
        }

        [Test]
        public void Stages_IsVisible_IncludesBothEnds_AndUntilEndMeansToTheLast()
        {
            Assert.IsFalse(Stages.IsVisible(1, 2, 0));
            Assert.IsTrue(Stages.IsVisible(1, 2, 1));
            Assert.IsTrue(Stages.IsVisible(1, 2, 2));
            Assert.IsFalse(Stages.IsVisible(1, 2, 3));
            Assert.IsTrue(Stages.IsVisible(0, Stages.UntilEnd, 0));
            Assert.IsTrue(Stages.IsVisible(0, Stages.UntilEnd, 99));
        }

        [Test]
        public void Stages_IsWorried_UntilCalmStage()
        {
            Assert.IsTrue(Stages.IsWorried(2, 1));
            Assert.IsFalse(Stages.IsWorried(2, 2));
        }

        [Test]
        public void Stages_Clamp_KeepsWithinZeroToMax()
        {
            Assert.AreEqual(0, Stages.Clamp(-1, 4));
            Assert.AreEqual(4, Stages.Clamp(9, 4));
            Assert.AreEqual(2, Stages.Clamp(2, 4));
        }

        [Test]
        public void Default_SomethingChangesAtEveryStage()
        {
            var items = VillageLayout.CreateDefault()[0].Items;

            for (var stage = 1; stage <= VillageLayout.MaxStage; stage++)
            {
                var changed = items.Any(i => VillageLayout.IsVisible(i, stage) != VillageLayout.IsVisible(i, stage - 1)
                                             || VillageLayout.IsWorried(i, stage) != VillageLayout.IsWorried(i, stage - 1));
                Assert.IsTrue(changed, $"段階 {stage} で何も変わらない");
            }
        }

        [Test]
        public void Default_VillagersAreAllWorriedAtFirst_AndAllCalmAtTheEnd()
        {
            var villagers = VillageLayout.CreateDefault()[0].Items.Where(i => i.IsVillager).ToList();

            Assert.IsNotEmpty(villagers);
            Assert.IsTrue(villagers.All(v => VillageLayout.IsWorried(v, 0)));
            Assert.IsTrue(villagers.All(v => !VillageLayout.IsWorried(v, VillageLayout.MaxStage)));
        }

        [Test]
        public void IsWorried_IsFalseForNonVillagers()
        {
            Assert.IsFalse(VillageLayout.IsWorried(new VillageItem(), 0));
        }

        [Test]
        public void Default_EveryItemIsInsideTheVillage()
        {
            const float half = 0.27f; // 村は 0.54 m 四方
            var items = VillageLayout.CreateDefault()[0].Items;

            foreach (var item in items)
            {
                Assert.LessOrEqual(System.Math.Abs(item.X), half, item.Prefab);
                Assert.LessOrEqual(System.Math.Abs(item.Z), half, item.Prefab);
            }
        }
    }
}
