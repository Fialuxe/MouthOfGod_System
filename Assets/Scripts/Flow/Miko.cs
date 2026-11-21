using MouthOfGod.Miko;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面のスクリプトから巫女を呼ぶ入口です。
    /// シーンの巫女（<see cref="MikoController"/>）に頼むだけで、自分では動かしません。
    /// </summary>
    /// <remarks>
    /// 浮く・体験者を向く・口を動かすなど、細かい動きは巫女の部品が自分で行います。
    /// 巫女がシーンにいないときは、何もしません。体験は止めません。
    /// </remarks>
    public static class Miko
    {
        static MikoController _controller;

        /// <summary>1 行を話し、話し終わるまで待ちます。</summary>
        /// <param name="text">字幕に出すセリフです。</param>
        /// <param name="voice">声です。<see langword="null"/> なら、字幕を文字数に合った長さだけ出します。</param>
        /// <param name="face">話す間だけ変える表情です。<see langword="null"/> なら変えません。</param>
        /// <param name="pointAt">話し始めに指さす物です。<see langword="null"/> なら指さしません。</param>
        /// <param name="moveTo">先に飛んで行く場所です。着いてから話します。<see langword="null"/> ならその場で話します。</param>
        /// <remarks>巫女がいなければ、警告を出してすぐ戻ります。</remarks>
        public static async Awaitable Say(string text, AudioClip voice = null, MikoExpression? face = null, Transform pointAt = null, Transform moveTo = null)
        {
            if (!SayNow(text, voice, face, pointAt, moveTo)) return;
            await Wait.Frame();
            await Wait.Until(() => !Controller.IsBusy);
        }

        /// <summary>待たずに話し始めます。</summary>
        /// <remarks>
        /// 話している途中なら、今のセリフを少し見せてから切り替えます。
        /// 引数の意味は <see cref="Say"/> と同じです。
        /// </remarks>
        /// <returns>話し始められたら <see langword="true"/> です。巫女がいなければ、警告を出して <see langword="false"/> を返します。</returns>
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

        /// <summary>巫女が話している間 <see langword="true"/> です。</summary>
        public static bool IsSpeaking => Controller != null && Controller.IsBusy;

        /// <summary>指定した場所へ飛んで行き、着くまで待ちます。</summary>
        /// <param name="spot">行き先です。<see langword="null"/> なら何もしません。</param>
        public static async Awaitable GoTo(Transform spot)
        {
            if (Controller == null || spot == null) return;
            Controller.GoTo(spot);
            await Wait.Until(() => !Controller.IsMoving);
        }

        /// <summary>体験者のそばについてくるようにします。</summary>
        public static void Follow() => Controller?.Follow();

        /// <summary>今の場所にとどまるようにします。</summary>
        public static void Stay() => Controller?.Stay();

        /// <summary>指定した物を指さします。</summary>
        public static void PointAt(Transform target) => Controller?.PointAt(target);

        /// <summary>表情を変えます。</summary>
        /// <remarks>話している間に変えたときは、話し終わっても戻りません。</remarks>
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
