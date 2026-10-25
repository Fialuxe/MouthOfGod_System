using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Core.Tests
{
    public class StandingTests
    {
        readonly List<Exception> _exceptions = new List<Exception>();
        Action<Exception> _savedException;

        [SetUp]
        public void SetUp()
        {
            Standing.Reset();
            _exceptions.Clear();
            _savedException = Standing.ReportException;
            Standing.ReportException = _exceptions.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Standing.Reset();
            Standing.ReportException = _savedException;
        }

        [Test]
        public void Field_StartsEmpty()
        {
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        [Test]
        public void Set_UpdatesField_BeforeRaisingChanged()
        {
            FieldId seen = FieldId.None;
            Standing.Changed += f => seen = Standing.Field;
            Standing.Set(new FieldId("sea"));
            Assert.AreEqual(new FieldId("sea"), seen);
        }

        [Test]
        public void Set_SameField_DoesNotRaiseAgain()
        {
            int count = 0;
            Standing.Changed += f => count++;
            Standing.Set(new FieldId("sea"));
            Standing.Set(new FieldId("sea"));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Leave_OnlyClearsWhenStandingThere()
        {
            Standing.Set(new FieldId("sea"));
            Standing.Leave(new FieldId("village"));
            Assert.AreEqual(new FieldId("sea"), Standing.Field);
            Standing.Leave(new FieldId("sea"));
            Assert.IsTrue(Standing.Field.IsEmpty);
        }

        [Test]
        public void Set_HandlerThrows_OthersStillReceive()
        {
            bool reached = false;
            Standing.Changed += f => throw new InvalidOperationException();
            Standing.Changed += f => reached = true;
            Standing.Set(new FieldId("sea"));
            Assert.IsTrue(reached);
            Assert.AreEqual(1, _exceptions.Count);
        }
    }
}
