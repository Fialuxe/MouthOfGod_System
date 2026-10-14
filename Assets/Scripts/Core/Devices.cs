using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いま使える IDevice の受け取り口。デバイスは 1 台なので、全体で 1 つにする。
    /// 実装（MockDevice、実機の Device）が Register し、使う側は WhenReady で受け取る（後から登録されても届く）。
    /// </summary>
    /// <remarks>
    /// - すべてメインスレッドから呼ぶ（シリアル通信の受信スレッドから直接呼ばない）。
    /// - 登録は起動時に 1 回の前提。途中で Unregister して別のデバイスを Register しても、すでに受け取った側には知らせない。
    /// </remarks>
    public static class Devices
    {
        static Action<IDevice> _pending;

        // コールバックの例外と警告の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;
        internal static Action<string> ReportWarning = Debug.LogWarning;

        /// <summary>登録されている IDevice。まだ無ければ null。</summary>
        public static IDevice Current { get; private set; }

        /// <summary>
        /// IDevice が使えるようになったら callback を 1 回呼ぶ。すでに登録されていれば、すぐ呼ぶ。
        /// 取り消せるように、ラムダではなくメソッドを渡す（CancelWhenReady はメソッドでしか外せない）。
        /// </summary>
        public static void WhenReady(Action<IDevice> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (Current != null) callback(Current);
            else _pending += callback;
        }

        /// <summary>まだ呼ばれていない WhenReady を取り消す（登録前に消える側が、OnDisable で呼ぶ）。WhenReady に渡したのと同じメソッドを渡す。</summary>
        public static void CancelWhenReady(Action<IDevice> callback) => _pending -= callback;

        /// <summary>
        /// デバイスを登録する。別のデバイスがすでに登録されていれば、登録せずに警告を出して false を返す。
        /// 待っている WhenReady を 1 つずつ呼ぶ。1 つが例外を投げても、残りは呼ぶ（例外は Console に出す）。
        /// </summary>
        public static bool Register(IDevice device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (Current != null)
            {
                if (ReferenceEquals(Current, device)) return true;
                ReportWarning($"[Devices] 別のデバイス（{Current}）が登録済みなので、{device} は登録しません。");
                return false;
            }

            Current = device;
            var pending = _pending;
            _pending = null;
            if (pending == null) return true;
            foreach (Action<IDevice> callback in pending.GetInvocationList())
            {
                try { callback(device); }
                catch (Exception e) { ReportException(e); }
            }
            return true;
        }

        /// <summary>登録を外す。登録されているデバイスと同じときだけ外す。</summary>
        public static void Unregister(IDevice device)
        {
            if (ReferenceEquals(Current, device)) Current = null;
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の登録を残さない）。テストからも呼ぶ。
        // ドメインのリロードを切っていると、Play を終えてから次の Play までは、Current が破棄済みのデバイスを指す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Current = null;
            _pending = null;
        }
    }
}
