using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者がいま前に立っているフィールドの置き場です。
    /// チュートリアルの「ここに立って」の案内と、フィールド専用の食べ物の表を選ぶのに使います。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 物がどのフィールドに届くかは、ここでは決めません。
    /// 体験者が狙って飛ばした物は、実際に落ちたフィールドに届きます。
    /// それを決めるのは Village の DeliveryZone です。
    /// </para>
    /// <para>
    /// 立つ位置を判定する側（Village の立つ場所）が <see cref="Set"/> します。
    /// 使う側（Food・Tutorial・Game）は <see cref="Field"/> で読み、<see cref="Changed"/> で知らせを受けます。
    /// 立つ位置の取り方は、判定する側が決めます。頭の位置や床の印などです。使う側は知りません。
    /// </para>
    /// <para>
    /// static なので、Play のたびに空にします。すべてメインスレッドから呼びます。
    /// この形を変えると使う側すべてに効くので、別の PR にします。
    /// </para>
    /// </remarks>
    public static class Standing
    {
        // 購読先の例外の出し先です。テストで差し替えます。
        internal static Action<Exception> ReportException = Debug.LogException;

        /// <summary>いま前に立っているフィールドです。</summary>
        /// <value>どこにも立っていなければ <see cref="FieldId.None"/> です。</value>
        public static FieldId Field { get; private set; }

        /// <summary>立っているフィールドが変わったときに、新しいフィールドを運んで出ます。</summary>
        /// <remarks>
        /// <see cref="Field"/> を更新してから出します。
        /// 同じフィールドを <see cref="Set"/> しても出ません。
        /// </remarks>
        public static event Action<FieldId> Changed;

        /// <summary>いま立っているフィールドを決めます。</summary>
        /// <param name="field">立っているフィールドです。どこにも立っていないなら <see cref="FieldId.None"/> を渡します。</param>
        public static void Set(FieldId field)
        {
            if (field == Field) return;
            Field = field;

            Action<FieldId> handlers = Changed;
            if (handlers == null) return;
            // 1 つの購読先が例外を投げても、残りには届けます。例外は Console に出します。
            foreach (Action<FieldId> handler in handlers.GetInvocationList())
            {
                try { handler(field); }
                catch (Exception e) { ReportException(e); }
            }
        }

        /// <summary>指定したフィールドに立っているときだけ、どこにも立っていない状態に戻します。</summary>
        /// <param name="field">離れたフィールドです。空 ID なら何もしません。</param>
        /// <remarks>離れた場所が、別の場所の判定を消さないようにするためです。</remarks>
        public static void Leave(FieldId field)
        {
            if (!field.IsEmpty && field == Field) Set(FieldId.None);
        }

        // Play のたびに空にします。ドメインのリロードを切っていても、前回の値と購読を残さないためです。
        // テストからも呼びます。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Field = FieldId.None;
            Changed = null;
        }
    }
}
