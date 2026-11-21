using MouthOfGod.Miko;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面のスクリプトから巫女を呼ぶ入口です。シーンの巫女（<see cref="MikoController"/>）に頼むだけで、自分では動かしません。
    /// </summary>
    /// <remarks>
    /// 浮く・体験者を向く・口を動かすなど、細かい動きは巫女の部品が自分で行います。
    /// 巫女がシーンにいないときは、警告を出して何もしません。体験は止めません。
    /// </remarks>
    public static class Miko
    {
        static MikoController _controller;

        /// <summary>
        /// 1 行を話し、話し終わるまで待ちます。voice が無ければ、字幕を文字数に合った長さだけ出します。
        /// face を渡すと、話す間だけ表情を変えます。pointAt を渡すと、話し始めに指さします。moveTo を渡すと、着いてから話します。
        /// </summary>
        public static async Awaitable Say(string text, AudioClip voice = null, MikoExpression? face = null, Transform pointAt = null, Transform moveTo = null)
        {
            if (!SayNow(text, voice, face, pointAt, moveTo)) return;
            await Wait.Frame();
            await Wait.Until(() => !Controller.IsBusy);
        }

        /// <summary>待たずに話し始めます。話している途中なら、今のセリフを少し見せてから切り替えます。</summary>
        public static bool SayNow(string text, AudioClip voice = null, MikoExpression? face = null, Transform pointAt = null, Transform moveTo = null)
        {
            if (Controller == null)
            {
                Debug.LogWarning("[Flow.Miko] シーンに巫女（MikoController）がいないので、セリフを出せません: " + text);
                return false;
            }
            Controller.Say(new MikoLine { text = text, voice = voice }, face, pointAt, moveTo);
            return true;
        }

        /// <summary>話すのをやめ、字幕を消します。</summary>
        public static void Hush() => Controller?.Hush();

        public static bool IsSpeaking => Controller != null && Controller.IsBusy;

        /// <summary>spot へ飛んで行き、着くまで待ちます。</summary>
        public static async Awaitable GoTo(Transform spot)
        {
            if (Controller == null || spot == null) return;
            Controller.GoTo(spot);
            await Wait.Until(() => !Controller.IsMoving);
        }

        /// <summary>体験者のそばについてきます。</summary>
        public static void Follow() => Controller?.Follow();

        /// <summary>今の場所にとどまります。</summary>
        public static void Stay() => Controller?.Stay();

        /// <summary>target を指さします。</summary>
        public static void PointAt(Transform target) => Controller?.PointAt(target);

        /// <summary>表情を変えます。話している間に変えたときは、話し終わっても戻りません。</summary>
        public static void SetFace(MikoExpression face) => Controller?.SetFace(face);

        // シーンが変わって巫女が消えていたら、探し直します。
        static MikoController Controller
        {
            get
            {
                if (_controller == null) _controller = Object.FindFirstObjectByType<MikoController>(FindObjectsInactive.Include);
                return _controller;
            }
        }
    }
}
