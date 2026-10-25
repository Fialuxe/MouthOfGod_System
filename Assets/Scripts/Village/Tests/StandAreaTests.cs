using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Village.Tests
{
    // 立つ場所の判定。EditMode なので Update・OnEnable は呼ばれない。Tick・OnCue を直接呼ぶ。
    public class StandAreaTests
    {
        readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _created.Clear();
            StandArea.ResetAll();
            Standing.Set(FieldId.None);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            StandArea.ResetAll();
            Standing.Set(FieldId.None);
        }

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // 原点に 1 m × 1 m（高さ 2 m）の箱。余白 0.1。
        StandArea NewArea(string field, Vector3 position)
        {
            var area = NewObject("Stand-" + field).AddComponent<StandArea>();
            area.transform.position = position;
            area.Configure(field, center: Vector3.zero, size: new Vector3(1f, 2f, 1f), margin: 0.1f);
            return area;
        }

        // --- 箱の判定（Unity に依存しない） ---

        [Test]
        public void Contains_InsideAndOutside_XZ()
        {
            var size = new Vector3(1f, 2f, 1f);
            Assert.IsTrue(StandArea.Contains(new Vector3(0.4f, 0f, -0.4f), Vector3.zero, size, 0f, true));
            Assert.IsTrue(StandArea.Contains(new Vector3(0.5f, 0f, 0.5f), Vector3.zero, size, 0f, true), "ふちは中");
            Assert.IsFalse(StandArea.Contains(new Vector3(0.6f, 0f, 0f), Vector3.zero, size, 0f, true));
            Assert.IsFalse(StandArea.Contains(new Vector3(0f, 0f, -0.6f), Vector3.zero, size, 0f, true));
        }

        [Test]
        public void Contains_IgnoreHeight_SkipsY_OtherwiseChecksIt()
        {
            var size = new Vector3(1f, 2f, 1f);
            var high = new Vector3(0f, 5f, 0f);
            Assert.IsTrue(StandArea.Contains(high, Vector3.zero, size, 0f, true));
            Assert.IsFalse(StandArea.Contains(high, Vector3.zero, size, 0f, false));
            Assert.IsTrue(StandArea.Contains(new Vector3(0f, 0.9f, 0f), Vector3.zero, size, 0f, false));
        }

        [Test]
        public void Contains_UsesCenter_AndMargin_AndNegativeSize()
        {
            var center = new Vector3(2f, 0f, 0f);
            Assert.IsFalse(StandArea.Contains(Vector3.zero, center, Vector3.one, 0f, true));
            Assert.IsTrue(StandArea.Contains(new Vector3(2.55f, 0f, 0f), center, Vector3.one, 0.1f, true), "余白の分だけ広い");
            Assert.IsFalse(StandArea.Contains(new Vector3(2.55f, 0f, 0f), center, Vector3.one, 0f, true));
            Assert.IsTrue(StandArea.Contains(new Vector3(2.4f, 0f, 0f), center, -Vector3.one, 0f, true), "負の大きさは絶対値で見る");
        }

        [Test]
        public void Contains_ZeroSize_OnlyTheCenter()
        {
            Assert.IsTrue(StandArea.Contains(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true));
            Assert.IsFalse(StandArea.Contains(new Vector3(0.01f, 0f, 0f), Vector3.zero, Vector3.zero, 0f, true));
        }

        [Test]
        public void NextInside_EntersAtTheBox_LeavesOnlyBeyondTheMargin()
        {
            var size = new Vector3(1f, 2f, 1f);
            var nearEdge = new Vector3(0.55f, 0f, 0f); // 箱の外、余白の中
            Assert.IsFalse(StandArea.NextInside(false, nearEdge, Vector3.zero, size, 0.1f, true), "外からは、箱そのもので入る");
            Assert.IsTrue(StandArea.NextInside(true, nearEdge, Vector3.zero, size, 0.1f, true), "中からは、余白を越えるまで出ない");
            Assert.IsFalse(StandArea.NextInside(true, new Vector3(0.65f, 0f, 0f), Vector3.zero, size, 0.1f, true));
        }

        // --- Standing へのつなぎ ---

        [Test]
        public void Tick_Enter_SetsStanding_Leave_ClearsIt()
        {
            var area = NewArea("sea", new Vector3(5f, 0f, 0f));

            area.Tick(new Vector3(5.2f, 1.6f, 0.1f));
            Assert.IsTrue(area.IsInside);
            Assert.AreEqual(new FieldId("sea"), Standing.Field);

            area.Tick(new Vector3(5.55f, 1.6f, 0f)); // 余白の中
            Assert.AreEqual(new FieldId("sea"), Standing.Field);

            area.Tick(Vector3.zero);
            Assert.IsFalse(area.IsInside);
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        [Test]
        public void Tick_UsesLocalSpace_OfAMovedRotatedScaledParent()
        {
            var table = NewObject("Table");
            table.transform.position = new Vector3(10f, 0f, 0f);
            table.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            table.transform.localScale = new Vector3(2f, 2f, 2f);
            var area = NewArea("mountain", Vector3.zero);
            area.transform.SetParent(table.transform, false);
            area.Configure(center: new Vector3(0f, 0f, 1f), size: new Vector3(0.5f, 1f, 0.5f));

            // ローカル (0, 0, 1) は、回して倍にすると、ワールドで (10 + 2, 0, 0)
            area.Tick(new Vector3(12f, 1.7f, 0f));
            Assert.AreEqual(new FieldId("mountain"), Standing.Field);

            area.Tick(new Vector3(10f, 1.7f, 2f)); // 回す前の向き（ずれた場所）
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        [Test]
        public void Overlap_LastEnteredWins_AndFallsBackWhenLeaving()
        {
            var a = NewArea("village", Vector3.zero);
            var b = NewArea("grassland", new Vector3(0.8f, 0f, 0f));
            var both = new Vector3(0.4f, 0f, 0f);

            a.Tick(both);
            b.Tick(both);
            Assert.AreEqual(new FieldId("grassland"), Standing.Field);

            // 同じ場所にいる間は、何度判定しても変わらない（ちらつかない）
            a.Tick(both);
            b.Tick(both);
            Assert.AreEqual(new FieldId("grassland"), Standing.Field);

            // grassland だけを出ると、まだ入っている village に戻る
            var onlyA = new Vector3(-0.3f, 0f, 0f);
            a.Tick(onlyA);
            b.Tick(onlyA);
            Assert.AreEqual(new FieldId("village"), Standing.Field);
        }

        [Test]
        public void Overlap_LeavingTheOlderArea_KeepsTheNewer()
        {
            var a = NewArea("village", Vector3.zero);
            var b = NewArea("grassland", new Vector3(0.8f, 0f, 0f));
            var both = new Vector3(0.4f, 0f, 0f);
            a.Tick(both);
            b.Tick(both);

            var onlyB = new Vector3(1.1f, 0f, 0f);
            a.Tick(onlyB);
            b.Tick(onlyB);

            Assert.AreEqual(new FieldId("grassland"), Standing.Field);
        }

        [Test]
        public void EmptyFieldId_UsesParentField()
        {
            var root = NewObject("Field");
            var field = root.AddComponent<FieldStages>();
            field.FieldId = "sea";
            var area = NewObject("Stand").AddComponent<StandArea>();
            area.transform.SetParent(root.transform, false);
            area.transform.localPosition = new Vector3(0f, 0f, -1f);
            area.Configure(center: Vector3.zero, size: Vector3.one);

            Assert.AreEqual(new FieldId("sea"), area.Field);
            area.Tick(new Vector3(0f, 1.6f, -1f));

            Assert.AreEqual(new FieldId("sea"), Standing.Field);
        }

        [Test]
        public void NoField_DoesNotSetStanding()
        {
            var area = NewArea("", Vector3.zero);

            area.Tick(Vector3.zero);

            Assert.IsFalse(area.IsInside);
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        [Test]
        public void LeavingAnotherField_DoesNotClearTheCurrentOne()
        {
            var sea = NewArea("sea", Vector3.zero);
            var village = NewArea("village", new Vector3(5f, 0f, 0f));
            sea.Tick(Vector3.zero);
            village.Tick(Vector3.zero);
            sea.StandHere(); // sea に立ったまま
            village.LeaveHere(); // 入っていない場所の Leave は何もしない

            Assert.AreEqual(new FieldId("sea"), Standing.Field);
        }

        [Test]
        public void StandHere_StaysWhileHeadIsAway_UntilLeave()
        {
            var area = NewArea("village", Vector3.zero);

            area.StandHere();
            area.Tick(new Vector3(9f, 0f, 9f));
            Assert.AreEqual(new FieldId("village"), Standing.Field);

            area.LeaveHere();
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        // --- 床の印 ---

        [Test]
        public void Footprint_ShownAndHiddenByCues_AndHiddenOnArrive()
        {
            var footprint = NewObject("Footprint");
            footprint.SetActive(false);
            var area = NewArea("sea", Vector3.zero);
            area.Configure(footprint: footprint);

            area.OnCue(new CueRequested(new CueId("village.footprint")));
            Assert.IsFalse(footprint.activeSelf, "よそのフィールドの合図では出ない");

            area.OnCue(new CueRequested(new CueId("sea.footprint")));
            Assert.IsTrue(footprint.activeSelf);

            area.OnCue(new CueRequested(new CueId("sea.footprint-off")));
            Assert.IsFalse(footprint.activeSelf);

            area.OnCue(new CueRequested(new CueId("sea.footprint")));
            area.Tick(Vector3.zero);
            Assert.IsFalse(footprint.activeSelf, "立ったら消える");
        }
    }
}
