using System.Collections.Generic;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MouthOfGod.Village.Tests
{
    public class RequestIssuerTests
    {
        static RequestSet CreateSet(params Request[] requests)
        {
            var set = ScriptableObject.CreateInstance<RequestSet>();
            var so = new UnityEditor.SerializedObject(set);
            var prop = so.FindProperty("_requests");
            prop.arraySize = requests.Length;
            for (var i = 0; i < requests.Length; i++)
            {
                var item = prop.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("_food").stringValue = requests[i].Food.Value;
                item.FindPropertyRelative("_field").stringValue = requests[i].Field.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return set;
        }

        [Test]
        public void Issue_RaisesIssuedOnce_WithSameFoodAndField()
        {
            var issuer = new RequestIssuer(null);
            var got = new List<RequestIssued>();
            issuer.Issued += got.Add;

            issuer.Issue(new Request(new FoodId("test-food"), new FieldId("test-field")));

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(new FoodId("test-food"), got[0].Food);
            Assert.AreEqual(new FieldId("test-field"), got[0].Field);
        }

        [Test]
        public void Issue_EmptyId_DoesNotRaise_AndWarns()
        {
            var issuer = new RequestIssuer(null);
            var count = 0;
            issuer.Issued += _ => count++;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RequestIssuer"));
            issuer.Issue(new Request(FoodId.None, new FieldId("test-field")));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RequestIssuer"));
            issuer.Issue(new Request(new FoodId("test-food"), FieldId.None));

            Assert.AreEqual(0, count);
        }

        [Test]
        public void IssueNext_IssuesInOrder_ThenReturnsFalse()
        {
            var set = CreateSet(
                new Request(new FoodId("test-food-1"), new FieldId("test-field-1")),
                new Request(new FoodId("test-food-2"), new FieldId("test-field-2")));
            try
            {
                var issuer = new RequestIssuer(set);
                var got = new List<RequestIssued>();
                issuer.Issued += got.Add;

                Assert.IsTrue(issuer.IssueNext());
                Assert.IsTrue(issuer.IssueNext());
                Assert.IsFalse(issuer.IssueNext());
                Assert.IsFalse(issuer.IssueNext());

                Assert.AreEqual(2, got.Count);
                Assert.AreEqual(new FoodId("test-food-1"), got[0].Food);
                Assert.AreEqual(new FieldId("test-field-2"), got[1].Field);
            }
            finally
            {
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void Issue_SameRequestTwice_RaisesIssuedTwice()
        {
            var issuer = new RequestIssuer(null);
            var got = new List<RequestIssued>();
            issuer.Issued += got.Add;
            var request = new Request(new FoodId("test-food"), new FieldId("test-field"));

            issuer.Issue(request);
            issuer.Issue(request);

            Assert.AreEqual(2, got.Count);
        }

        [Test]
        public void IssueNext_EmptyIdInSet_WarnsAndMovesOn()
        {
            var set = CreateSet(
                new Request(FoodId.None, new FieldId("test-field-1")),
                new Request(new FoodId("test-food-2"), new FieldId("test-field-2")));
            try
            {
                var issuer = new RequestIssuer(set);
                var got = new List<RequestIssued>();
                issuer.Issued += got.Add;

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RequestIssuer"));
                Assert.IsTrue(issuer.IssueNext());
                Assert.AreEqual(0, got.Count);

                Assert.IsTrue(issuer.IssueNext());
                Assert.AreEqual(1, got.Count);
                Assert.AreEqual(new FoodId("test-food-2"), got[0].Food);
                Assert.IsFalse(issuer.IssueNext());
            }
            finally
            {
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void IssueNext_NullOrEmptySet_ReturnsFalse()
        {
            Assert.IsFalse(new RequestIssuer(null).IssueNext());

            var set = CreateSet();
            try
            {
                Assert.IsFalse(new RequestIssuer(set).IssueNext());
            }
            finally
            {
                Object.DestroyImmediate(set);
            }
        }
    }
}
