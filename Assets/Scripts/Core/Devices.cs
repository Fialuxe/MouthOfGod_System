using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いま使える IDevice の受け取り口。デバイスは 1 台なので、全体で 1 つにする。
    /// 実装（MockDevice、実機の Device）が Register し、使う側は WhenReady で受け取る（後から登録されても届く）。
    /// </summary>
    public static class Devices
    {
        static Action<IDevice> _pending;

        /// <summary>登録されている IDevice。まだ無ければ null。</summary>
        public static IDevice Current { get; private set; }

        /// <summary>IDevice が使えるようになったら callback を 1 回呼ぶ。すでに登録されていれば、すぐ呼ぶ。</summary>
        public static void WhenReady(Action<IDevice> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (Current != null) callback(Current);
            else _pending += callback;
        }

        /// <summary>まだ呼ばれていない WhenReady を取り消す（登録前に消える側が、OnDisable で呼ぶ）。</summary>
        public static void CancelWhenReady(Action<IDevice> callback) => _pending -= callback;

        /// <summary>デバイスを登録する。別のデバイスがすでに登録されていれば、登録せずに false を返す。</summary>
        public static bool Register(IDevice device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (Current != null) return ReferenceEquals(Current, device);
            Current = device;
            var pending = _pending;
            _pending = null;
            pending?.Invoke(device);
            return true;
        }

        /// <summary>登録を外す。登録されているデバイスと同じときだけ外す。</summary>
        public static void Unregister(IDevice device)
        {
            if (ReferenceEquals(Current, device)) Current = null;
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
