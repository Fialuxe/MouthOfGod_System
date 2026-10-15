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
        public void IsVisible_AppearsAtAndGoneAt()
        {
            var sprout = new VillageItem { AppearsAt = 1, GoneAt = 2 };

            Assert.IsFalse(VillageLayout.IsVisible(sprout, 0));
            Assert.IsTrue(VillageLayout.IsVisible(sprout, 1));
            Assert.IsFalse(VillageLayout.IsVisible(sprout, 2));
            Assert.IsTrue(VillageLayout.IsVisible(new VillageItem(), 99));
        }

        [Test]
        public void Default_SomethingChangesAtEveryStep()
        {
            var items = VillageLayout.CreateDefault()[0].Items;

            for (var done = 1; done <= VillageLayout.Steps.Count; done++)
            {
                var changed = items.Any(i => VillageLayout.IsVisible(i, done) != VillageLayout.IsVisible(i, done - 1)
                                             || VillageLayout.IsWorried(i, done) != VillageLayout.IsWorried(i, done - 1));
                Assert.IsTrue(changed, $"ステップ {done} で何も変わらない");
            }
        }

        [Test]
        public void Default_EveryStepHasAWant_AndStepNumbersAreInRange()
        {
            Assert.IsTrue(VillageLayout.Steps.All(s => !string.IsNullOrEmpty(s.Food)));
            var count = VillageLayout.Steps.Count;
            foreach (var item in VillageLayout.CreateDefault()[0].Items)
            {
                Assert.That(item.AppearsAt, Is.InRange(0, count), item.Prefab);
                Assert.That(item.GoneAt, Is.InRange(0, count), item.Prefab);
                Assert.That(item.HelpedAt, Is.InRange(-1, count), item.Prefab);
            }
        }

        [Test]
        public void Default_VillagersAreAllWorriedAtFirst_AndAllCalmAtTheEnd()
        {
            var villagers = VillageLayout.CreateDefault()[0].Items.Where(i => i.IsVillager).ToList();

            Assert.IsNotEmpty(villagers);
            Assert.IsTrue(villagers.All(v => VillageLayout.IsWorried(v, 0)));
            Assert.IsTrue(villagers.All(v => !VillageLayout.IsWorried(v, VillageLayout.Steps.Count)));
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
