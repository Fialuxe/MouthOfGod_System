using System.Linq;
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
        public void IsVisible_FollowsShowWhen()
        {
            var always = new VillageItem { ShowWhen = ShowWhen.Always };
            var lacking = new VillageItem { ShowWhen = ShowWhen.Lacking };
            var delivered = new VillageItem { ShowWhen = ShowWhen.Delivered };

            Assert.IsTrue(VillageLayout.IsVisible(always, false));
            Assert.IsTrue(VillageLayout.IsVisible(always, true));
            Assert.IsTrue(VillageLayout.IsVisible(lacking, false));
            Assert.IsFalse(VillageLayout.IsVisible(lacking, true));
            Assert.IsFalse(VillageLayout.IsVisible(delivered, false));
            Assert.IsTrue(VillageLayout.IsVisible(delivered, true));
        }

        [Test]
        public void Default_HasDifferentItemsBeforeAndAfterDelivery()
        {
            var items = VillageLayout.CreateDefault()[0].Items;

            var before = items.Count(i => VillageLayout.IsVisible(i, false));
            var after = items.Count(i => VillageLayout.IsVisible(i, true));

            Assert.Greater(after, before);
        }
    }
}
