using System;
using System.Text.RegularExpressions;
using MouthOfGod.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MouthOfGod.UI.Tests
{
    public class GameResultTextTests
    {
        static readonly PhaseId MainGame = new PhaseId("main-game");
        static readonly PhaseId GameOver = new PhaseId("game-over");

        // Received を出せるフィールド（RequestBubblesTests の FakeField を元にした）。
        sealed class FakeField : IFieldRequests
        {
            public FakeField(string id) => Field = new FieldId(id);
            public FieldId Field { get; }
            public bool IsOpen => false;
            public bool CanOpen => false;
            public bool Open() => false;
            public bool Open(FoodId food) => false;
            public DeliveryKind Receive(FoodId food) => DeliveryKind.Foreign;
            public void Expire() { }
            public void Expire(int request) { }
            public void Restart() { }
            public event Action<RequestIssued> Issued { add { } remove { } }
            public event Action<RequestClosed> Closed { add { } remove { } }
            public event Action<Delivered> Received;

            public bool HasReceivedListeners => Received != null;

            public void RaiseReceived(DeliveryKind kind, string food = "fish") =>
                Received?.Invoke(new Delivered(new FoodId(food), Field, kind));
        }

        GameObject _host;
        GameResultText _result;
        FakeField _field;
        FakeField _later;

        [SetUp]
        public void SetUp()
        {
            // UI.Tests からは Phases.Reset を呼べないので、場面を空に戻す。
            Phases.Set(PhaseId.None);
            _field = new FakeField("sea");
            _later = new FakeField("mountain");
            Fields.Register(_field);
        }

        [TearDown]
        public void TearDown()
        {
            if (_result != null) _result.StopListening(); // EditMode では OnDisable も呼ばれないので、ここで外す
            _result = null;
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            Fields.Unregister(_field);
            Fields.Unregister(_later);
            Phases.Set(PhaseId.None);
        }

        // EditMode なので OnEnable は呼ばれない。Listen を直接呼ぶ。
        GameResultText CreateResult()
        {
            _host = new GameObject("Game Result");
            _result = _host.AddComponent<GameResultText>();
            _result.Listen();
            return _result;
        }

        [Test]
        public void DuringMainGame_CountsOnlyRequested()
        {
            CreateResult();
            Phases.Set(MainGame);

            _field.RaiseReceived(DeliveryKind.Requested);
            _field.RaiseReceived(DeliveryKind.Own);
            _field.RaiseReceived(DeliveryKind.Foreign);
            _field.RaiseReceived(DeliveryKind.Requested);

            Assert.AreEqual(2, _result.Delivered);
        }

        [Test]
        public void BeforeMainGame_DoesNotCount()
        {
            CreateResult();
            Phases.Set(new PhaseId("deliver")); // チュートリアルの間
            _field.RaiseReceived(DeliveryKind.Requested);
            Assert.AreEqual(0, _result.Delivered);

            Phases.Set(MainGame);
            Assert.AreEqual(0, _result.Delivered);
        }

        [Test]
        public void AfterGameOver_DoesNotCount()
        {
            CreateResult();
            Phases.Set(MainGame);
            _field.RaiseReceived(DeliveryKind.Requested);
            Phases.Set(GameOver);
            _field.RaiseReceived(DeliveryKind.Requested);

            Assert.AreEqual(1, _result.Delivered);
        }

        [Test]
        public void IsHidden_UntilGameOver()
        {
            CreateResult();
            Assert.IsFalse(_result.IsShowing);
            Phases.Set(MainGame);
            Assert.IsFalse(_result.IsShowing);
        }

        [Test]
        public void GameOver_ShowsTheCount()
        {
            CreateResult();
            Phases.Set(MainGame);
            _field.RaiseReceived(DeliveryKind.Requested);
            _field.RaiseReceived(DeliveryKind.Requested);
            _field.RaiseReceived(DeliveryKind.Requested);

            Phases.Set(GameOver); // カメラが無くても例外を出さない

            Assert.IsTrue(_result.IsShowing);
            Assert.AreEqual("Time's up!\nDelivered: 3", _result.Text);
        }

        [Test]
        public void GameOver_WithNothingDelivered_ShowsZero()
        {
            CreateResult();
            Phases.Set(MainGame);
            Phases.Set(GameOver);

            Assert.IsTrue(_result.IsShowing);
            StringAssert.Contains("Delivered: 0", _result.Text);
        }

        [Test]
        public void NextMainGame_ResetsTheCount_AndHides()
        {
            CreateResult();
            Phases.Set(MainGame);
            _field.RaiseReceived(DeliveryKind.Requested);
            Phases.Set(GameOver);
            Assert.IsTrue(_result.IsShowing);

            Phases.Set(MainGame);

            Assert.AreEqual(0, _result.Delivered);
            Assert.IsFalse(_result.IsShowing);
        }

        [Test]
        public void OtherPhaseAfterGameOver_Hides()
        {
            CreateResult();
            Phases.Set(MainGame);
            Phases.Set(GameOver);
            Phases.Set(PhaseId.None);

            Assert.IsFalse(_result.IsShowing);
        }

        [Test]
        public void FieldRegisteredLater_IsCounted()
        {
            CreateResult();
            Phases.Set(MainGame);
            Fields.Register(_later);

            _later.RaiseReceived(DeliveryKind.Requested);

            Assert.AreEqual(1, _result.Delivered);
        }

        [Test]
        public void RemovedField_IsNoLongerListened()
        {
            CreateResult();
            Assert.IsTrue(_field.HasReceivedListeners);

            Fields.Unregister(_field);

            Assert.IsFalse(_field.HasReceivedListeners);
        }

        [Test]
        public void StopListening_StopsCountingAndHides()
        {
            CreateResult();
            Phases.Set(MainGame);
            Phases.Set(GameOver);
            Phases.Set(MainGame);
            _result.StopListening();

            _field.RaiseReceived(DeliveryKind.Requested);
            Phases.Set(GameOver);

            Assert.AreEqual(0, _result.Delivered);
            Assert.IsFalse(_result.IsShowing);
            Assert.IsFalse(_field.HasReceivedListeners);
        }

        [Test]
        public void Listen_DuringGameOver_ShowsRightAway()
        {
            Phases.Set(GameOver);
            CreateResult();

            Assert.IsTrue(_result.IsShowing);
        }

        [Test]
        public void Format_PutsTheCount()
        {
            Assert.AreEqual("Time's up!\nDelivered: 5", GameResultText.Format(GameResultText.DefaultFormat, 5));
            Assert.AreEqual("7 delivered", GameResultText.Format("{0} delivered", 7));
        }

        [Test]
        public void Format_EmptyOrNull_UsesTheDefault()
        {
            Assert.AreEqual("Time's up!\nDelivered: 1", GameResultText.Format(null, 1));
            Assert.AreEqual("Time's up!\nDelivered: 1", GameResultText.Format("", 1));
        }

        [Test]
        public void Format_Broken_WarnsAndUsesTheDefault()
        {
            LogAssert.Expect(LogType.Warning, new Regex("書式"));
            Assert.AreEqual("Time's up!\nDelivered: 2", GameResultText.Format("Delivered: {N}", 2));
        }

        [Test]
        public void FlatForward_DropsTheTilt()
        {
            Vector3 forward = new Vector3(0f, -1f, 1f).normalized; // 下を向いている
            Vector3 flat = GameResultText.FlatForward(forward, Vector3.up);
            Assert.AreEqual(0f, flat.y, 0.0001f);
            Assert.AreEqual(1f, flat.z, 0.0001f);
        }

        [Test]
        public void FlatForward_StraightDownOrUp_UsesTheTopOfTheHead()
        {
            // 真下を向くと、頭のてっぺんが前（+z）を指す
            Vector3 down = GameResultText.FlatForward(Vector3.down, Vector3.forward);
            Assert.AreEqual(1f, down.z, 0.0001f);
            // 真上を向くと、頭のてっぺんは後ろ（-z）を指すので、その逆が前
            Vector3 up = GameResultText.FlatForward(Vector3.up, Vector3.back);
            Assert.AreEqual(1f, up.z, 0.0001f);
            // どちらも決まらなければ、世界の前
            Assert.AreEqual(Vector3.forward, GameResultText.FlatForward(Vector3.down, Vector3.down));
        }

        [Test]
        public void PlacePosition_IsInFrontAtTheGivenHeight()
        {
            Vector3 position = GameResultText.PlacePosition(new Vector3(1f, 1.6f, 0f), new Vector3(1f, 0.5f, 0f), Vector3.up, 2f, 0.1f);
            Assert.AreEqual(3f, position.x, 0.0001f);
            Assert.AreEqual(1.7f, position.y, 0.0001f);
            Assert.AreEqual(0f, position.z, 0.0001f);
        }
    }
}
