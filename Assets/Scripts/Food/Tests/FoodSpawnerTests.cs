using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Food.Tests
{
    // 食べ物を生む処理（R4〜R10）。EditMode なので OnEnable は呼ばれない。Attach・Detach を直接呼ぶ。
    public class FoodSpawnerTests
    {
        sealed class FakeDevice : IDevice
        {
            public TextureId Current { get; private set; }
            public event System.Action<TextureId> Confirmed;
            public event System.Action<TextureId> TextureChanged { add { } remove { } }
            public event System.Action Bitten { add { } remove { } }

            public void Confirm(string texture)
            {
                Current = new TextureId(texture);
                System.Action<TextureId> handlers = Confirmed;
                if (handlers != null) handlers(Current);
            }
        }

        readonly List<GameObject> _created = new List<GameObject>();
        readonly List<string> _warnings = new List<string>();
        readonly List<Spawned> _spawned = new List<Spawned>();
        System.Action<string> _savedWarning;
        FakeDevice _device;
        FoodSpawner _spawner;
        GameObject _mouth;

        [SetUp]
        public void SetUp()
        {
            FoodTables.Reset();
            Standing.Set(FieldId.None); // ほかのテストで立った場所を残さない
            _created.Clear();
            _warnings.Clear(); // NUnit は同じインスタンスで全テストを走らせるので、前のテストの分を消す
            _spawned.Clear();
            _savedWarning = FoodSpawner.ReportWarning;
            FoodSpawner.ReportWarning = _warnings.Add;

            _mouth = NewObject("Mouth");
            _mouth.transform.position = new Vector3(1f, 2f, 3f);
            _spawner = NewObject("Spawner").AddComponent<FoodSpawner>();
            _spawner.Spawned += _spawned.Add;
            // spawnPoint は Inspector の項目なので、コードからは同じオブジェクトに付けて位置を使う
            _spawner.transform.position = _mouth.transform.position;

            // 共通の表: b=fish。草原の表: b=pig
            FoodTables.Register(NewTable("", new FoodRow("b", NewObject("fish"))));
            FoodTables.Register(NewTable("grassland", new FoodRow("b", NewObject("pig"))));
        }

        [TearDown]
        public void TearDown()
        {
            if (_device != null) Devices.Unregister(_device);
            _device = null;
            foreach (FoodItemTag tag in Object.FindObjectsByType<FoodItemTag>(FindObjectsSortMode.None))
                Object.DestroyImmediate(tag.gameObject);
            FoodTables.Reset();
            Standing.Set(FieldId.None);
            FoodSpawner.ReportWarning = _savedWarning;
            foreach (GameObject go in _created)
                if (go != null) Object.DestroyImmediate(go);
        }

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        FoodTable NewTable(string fieldId, params FoodRow[] rows)
        {
            FoodTable table = NewObject("Table-" + fieldId).AddComponent<FoodTable>();
            table.Configure(fieldId, rows);
            return table;
        }

        [Test]
        public void Spawn_CreatesFoodAtSpawnerPosition_WithItemId_AndRaisesSpawnedOnce()
        {
            GameObject food = _spawner.Spawn(new TextureId("b"));

            Assert.IsNotNull(food);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), food.transform.position);
            IFoodItem item = food.GetComponent<IFoodItem>();
            Assert.IsNotNull(item);
            Assert.AreEqual(new FoodId("fish"), item.Food);
            Assert.AreEqual(1, _spawned.Count);
            Assert.AreEqual(new FoodId("fish"), _spawned[0].Food);
            Assert.AreEqual(food.transform.position, _spawned[0].Position);
            Assert.IsEmpty(_warnings);
        }

        [Test]
        public void Spawn_EmptyTexture_DoesNothing_AndWarnsNothing()
        {
            Assert.IsNull(_spawner.Spawn(TextureId.None));

            Assert.AreEqual(0, _spawned.Count);
            Assert.IsEmpty(_warnings);
            Assert.AreEqual(0, Object.FindObjectsByType<FoodItemTag>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void Spawn_Unresolved_WarnsOnce_WithFieldAndTexture_AndSpawnsNothing()
        {
            _spawner.CurrentField = () => new FieldId("sea");

            Assert.IsNull(_spawner.Spawn(new TextureId("zzz")));

            Assert.AreEqual(1, _warnings.Count);
            StringAssert.Contains("zzz", _warnings[0]);
            StringAssert.Contains("sea", _warnings[0]);
            Assert.AreEqual(0, _spawned.Count);
        }

        [Test]
        public void Spawn_UsesTheFieldFromCurrentField()
        {
            _spawner.CurrentField = () => new FieldId("grassland");

            GameObject food = _spawner.Spawn(new TextureId("b"));

            Assert.AreEqual(new FoodId("pig"), food.GetComponent<IFoodItem>().Food);
        }

        [Test]
        public void Spawn_WithoutCurrentField_UsesTheDefaultTable()
        {
            GameObject food = _spawner.Spawn(new TextureId("b"));

            Assert.AreEqual(new FoodId("fish"), food.GetComponent<IFoodItem>().Food);
        }

        [Test]
        public void Preview_ReturnsTheSamePrefabAsResolve_WithoutSpawning()
        {
            _spawner.CurrentField = () => new FieldId("grassland");

            GameObject prefab = _spawner.Preview(new TextureId("b"));

            Assert.AreEqual("pig", prefab.name);
            Assert.AreEqual(0, _spawned.Count);
            Assert.AreEqual(0, Object.FindObjectsByType<FoodItemTag>(FindObjectsSortMode.None).Length);
            Assert.IsNull(_spawner.Preview(new TextureId("zzz")));
            Assert.IsEmpty(_warnings); // 試し引きは警告しない
        }

        [Test]
        public void Attach_SpawnsOnConfirmed_UntilDetached()
        {
            _device = new FakeDevice();
            Devices.Register(_device);

            _spawner.Attach();
            _device.Confirm("b");
            Assert.AreEqual(1, _spawned.Count);

            _spawner.Detach();
            _device.Confirm("b");
            Assert.AreEqual(1, _spawned.Count);
        }

        [Test]
        public void Attach_Twice_DoesNotSpawnTwicePerConfirm()
        {
            _device = new FakeDevice();
            Devices.Register(_device);

            _spawner.Attach();
            _spawner.Attach();
            _device.Confirm("b");

            Assert.AreEqual(1, _spawned.Count);
        }

        [Test]
        public void Spawned_HandlerException_DoesNotStopOtherHandlers()
        {
            var exceptions = new List<System.Exception>();
            System.Action<System.Exception> saved = FoodSpawner.ReportException;
            FoodSpawner.ReportException = exceptions.Add;
            try
            {
                var second = new List<Spawned>();
                _spawner.Spawned += s => { throw new System.InvalidOperationException("boom"); };
                _spawner.Spawned += second.Add;

                _spawner.Spawn(new TextureId("b"));

                Assert.AreEqual(1, exceptions.Count);
                Assert.AreEqual(1, second.Count);
            }
            finally
            {
                FoodSpawner.ReportException = saved;
            }
        }

        [Test]
        public void LaunchVelocity_IsForwardTimesSpeed()
        {
            _mouth.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Vector3 v = FoodSpawner.LaunchVelocity(_mouth.transform, 3f);

            Assert.AreEqual(3f, v.x, 0.0001f);
            Assert.AreEqual(0f, v.y, 0.0001f);
            Assert.AreEqual(0f, v.z, 0.0001f);
        }

        [Test]
        public void BallisticVelocity_LandsOnTarget_AfterSeconds()
        {
            var origin = new Vector3(0f, 1.5f, 0f);
            var target = new Vector3(0.4f, 0.8f, 1.2f);
            var gravity = new Vector3(0f, -9.81f, 0f);
            const float t = 0.6f;

            Vector3 v = FoodSpawner.BallisticVelocity(origin, target, gravity, t);

            // p(t) = origin + v t + g t^2 / 2
            Vector3 landed = origin + v * t + 0.5f * t * t * gravity;
            Assert.AreEqual(target.x, landed.x, 0.0001f);
            Assert.AreEqual(target.y, landed.y, 0.0001f);
            Assert.AreEqual(target.z, landed.z, 0.0001f);
        }

        [Test]
        public void BallisticVelocity_WithoutGravity_IsStraightLine()
        {
            Vector3 v = FoodSpawner.BallisticVelocity(Vector3.zero, new Vector3(0f, 0f, 2f), Vector3.zero, 0.5f);

            Assert.AreEqual(0f, v.x, 0.0001f);
            Assert.AreEqual(0f, v.y, 0.0001f);
            Assert.AreEqual(4f, v.z, 0.0001f);
        }

        [Test]
        public void BallisticVelocity_ZeroOrNegativeSeconds_IsZero()
        {
            Assert.AreEqual(Vector3.zero, FoodSpawner.BallisticVelocity(Vector3.zero, Vector3.one, Physics.gravity, 0f));
            Assert.AreEqual(Vector3.zero, FoodSpawner.BallisticVelocity(Vector3.zero, Vector3.one, Physics.gravity, -1f));
        }

        [Test]
        public void Spawn_UsesTheFieldFromStanding_WhenCurrentFieldIsEmpty()
        {
            Standing.Set(new FieldId("grassland"));

            GameObject food = _spawner.Spawn(new TextureId("b"));

            Assert.AreEqual(new FoodId("pig"), food.GetComponent<IFoodItem>().Food);
        }
    }
}
