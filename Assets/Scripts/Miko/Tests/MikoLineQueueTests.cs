using NUnit.Framework;

namespace MouthOfGod.Miko.Tests
{
    // しゃべっている間に来たセリフの順番待ち（#337: 古いセリフが先に出て、「Splendid!」が遅れた）。
    public class MikoLineQueueTests
    {
        static MikoLine Line(string id) => new MikoLine { id = id };

        [Test]
        public void 待っているセリフは_新しい1つだけ残る()
        {
            var queue = new MikoLineQueue();

            queue.Enqueue(Line("old-1"));
            queue.Enqueue(Line("old-2"));
            queue.Enqueue(Line("t4-splendid"));

            Assert.AreEqual("t4-splendid", queue.Dequeue().id);
            Assert.IsFalse(queue.HasWaiting);
        }

        [Test]
        public void 空のときは_nullを返す()
        {
            Assert.IsNull(new MikoLineQueue().Dequeue());
        }

        [Test]
        public void Clearで_待ちが消える()
        {
            var queue = new MikoLineQueue();
            queue.Enqueue(Line("a"));

            queue.Clear();

            Assert.IsFalse(queue.HasWaiting);
        }

        [Test]
        public void 出しはじめてすぐなら_最低の秒数まで見せる()
        {
            // 10 秒のセリフを 100 秒に出しはじめ、0.2 秒後に次のセリフが来た。1 秒見せてから切る。
            float cutAt = MikoLineQueue.CutAt(hideAt: 110f, shownAt: 100f, minShowSeconds: 1f, now: 100.2f);

            Assert.AreEqual(101f, cutAt, 0.0001f);
        }

        [Test]
        public void 十分に見せたあとなら_すぐ切る()
        {
            float cutAt = MikoLineQueue.CutAt(hideAt: 110f, shownAt: 100f, minShowSeconds: 1f, now: 105f);

            Assert.AreEqual(105f, cutAt, 0.0001f);
        }

        [Test]
        public void もともとの終わりより_遅くはならない()
        {
            // 0.5 秒のセリフ。最低の秒数（1 秒）より短いので、もともとの終わりのまま。
            float cutAt = MikoLineQueue.CutAt(hideAt: 100.5f, shownAt: 100f, minShowSeconds: 1f, now: 100.1f);

            Assert.AreEqual(100.5f, cutAt, 0.0001f);
        }
    }
}
