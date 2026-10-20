using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いま使える IFork の受け取り口。フォークは 1 本なので、全体で 1 つにする。
    /// 実装（Fork）が Register し、使う側は WhenReady で受け取る（後から登録されても届く）。形は <see cref="Devices"/> と同じ。
    /// </summary>
    /// <remarks>
    /// - すべてメインスレッドから呼ぶ。
    /// - 登録は起動時に 1 回の前提。途中で Unregister して別のフォークを Register しても、すでに受け取った側には知らせない。
    /// </remarks>
    public static class Forks
    {
        static Action<IFork> _pending;

        // コールバックの例外と警告の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;
        internal static Action<string> ReportWarning = Debug.LogWarning;

        /// <summary>登録されている IFork。まだ無ければ null。</summary>
        public static IFork Current { get; private set; }

        /// <summary>
        /// IFork が使えるようになったら callback を 1 回呼ぶ。すでに登録されていれば、すぐ呼ぶ。
        /// 取り消せるように、ラムダではなくメソッドを渡す（CancelWhenReady はメソッドでしか外せない）。
        /// </summary>
        public static void WhenReady(Action<IFork> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (Current != null) callback(Current);
            else _pending += callback;
        }

        /// <summary>まだ呼ばれていない WhenReady を取り消す。WhenReady に渡したのと同じメソッドを渡す。</summary>
        public static void CancelWhenReady(Action<IFork> callback) => _pending -= callback;

        /// <summary>
        /// フォークを登録する。別のフォークがすでに登録されていれば、登録せずに警告を出して false を返す。
        /// 待っている WhenReady を 1 つずつ呼ぶ。1 つが例外を投げても、残りは呼ぶ（例外は Console に出す）。
        /// </summary>
        public static bool Register(IFork fork)
        {
            if (fork == null) throw new ArgumentNullException(nameof(fork));
            if (Current != null)
            {
                if (ReferenceEquals(Current, fork)) return true;
                ReportWarning($"[Forks] 別のフォーク（{Current}）が登録済みなので、{fork} は登録しません。");
                return false;
            }

            Current = fork;
            var pending = _pending;
            _pending = null;
            if (pending == null) return true;
            foreach (Action<IFork> callback in pending.GetInvocationList())
            {
                try { callback(fork); }
                catch (Exception e) { ReportException(e); }
            }
            return true;
        }

        /// <summary>登録を外す。登録されているフォークと同じときだけ外す。</summary>
        public static void Unregister(IFork fork)
        {
            if (ReferenceEquals(Current, fork)) Current = null;
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の登録を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Current = null;
            _pending = null;
        }
    }
}
