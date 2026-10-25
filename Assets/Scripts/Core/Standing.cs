using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者がいま前に立っているフィールドの置き場。チュートリアルの「ここに立って」の案内と、そのフィールド専用の食べ物の表を選ぶのに使う。
    /// 物がどのフィールドに届くかは、ここでは決めない。体験者が狙って飛ばした物が、実際に落ちたフィールドに届く（Village の DeliveryZone）。
    /// 立つ位置を判定する側（Village の立つ場所）が <see cref="Set"/> し、使う側（Food・Tutorial・Game）は <see cref="Field"/> で読み、<see cref="Changed"/> で知らせを受ける。
    /// </summary>
    /// <remarks>
    /// - どこにも立っていないときは <see cref="FieldId.None"/>。
    /// - 立つ位置の取り方（頭の位置、床の印など）は、判定する側が決める。使う側は知らない。
    /// - static なので、Play のたびに空にする。すべてメインスレッドから呼ぶ。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public static class Standing
    {
        // 購読先の例外の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;

        /// <summary>いま前に立っているフィールド。どこにも立っていなければ <see cref="FieldId.None"/>。</summary>
        public static FieldId Field { get; private set; }

        /// <summary>立っているフィールドが変わった（新しいフィールドを運ぶ）。<see cref="Field"/> を更新してから出す。同じフィールドを Set しても出ない。</summary>
        public static event Action<FieldId> Changed;

        /// <summary>いま立っているフィールドを決める。</summary>
        public static void Set(FieldId field)
        {
            if (field == Field) return;
            Field = field;

            Action<FieldId> handlers = Changed;
            if (handlers == null) return;
            // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
            foreach (Action<FieldId> handler in handlers.GetInvocationList())
            {
                try { handler(field); }
                catch (Exception e) { ReportException(e); }
            }
        }

        /// <summary>そのフィールドに立っているときだけ、どこにも立っていない状態に戻す。離れた場所が、別の場所の判定を消さないようにするため。</summary>
        public static void Leave(FieldId field)
        {
            if (!field.IsEmpty && field == Field) Set(FieldId.None);
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の値と購読を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Field = FieldId.None;
            Changed = null;
        }
    }
}
