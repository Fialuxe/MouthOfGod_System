using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Food.Tests
{
    // 食べ物の Prefab が、飛ばして見やすい大きさに収まっていること（#335: 村以外で、食べ物が世界を覆うほど大きかった）。
    public class FoodPrefabSizeTests
    {
        const float MinLongest = 0.03f; // m。これより小さいと、見えない
        const float MaxLongest = 0.3f; // m。これより大きいと、手に余る

        static GameObject[] Prefabs() => Resources.LoadAll<GameObject>("Prefabs/Food");

        static float LongestSide(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return 0f;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void 食べ物のPrefabがある()
        {
            Assert.That(Prefabs().Length, Is.GreaterThan(0), "Resources/Prefabs/Food に Prefab が無い");
        }

        [Test]
        public void すべての食べ物が_飛ばして見やすい大きさに収まっている()
        {
            foreach (var prefab in Prefabs())
            {
                var longest = LongestSide(prefab);
                Assert.That(longest, Is.InRange(MinLongest, MaxLongest),
                    $"{prefab.name} の一番長い辺が {longest:F3} m。{MinLongest}〜{MaxLongest} m に収まっていない");
            }
        }

        [Test]
        public void すべての食べ物が_重さと当たり判定を持つ()
        {
            foreach (var prefab in Prefabs())
            {
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null, $"{prefab.name} に Rigidbody が無い");
                Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null, $"{prefab.name} に Collider が無い");
            }
        }
    }
}
