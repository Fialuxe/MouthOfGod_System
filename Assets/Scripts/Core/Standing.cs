using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者がいま前に立っているフィールドの置き場。口から生まれた物は、このフィールドに落ちる（シナリオブック S6）。
    /// 立つ位置を判定する側（Village の立つ場所）が <see cref="Set"/> し、使う側（Food・Tutorial・Game）は <see cref="Field"/> で読み、<see cref="Changed"/> で知らせを受ける。
    /// </summary>
    /// <remarks>
    /// - どこにも立っていないときは <see cref="FieldId.None"/>。
    /// - <see cref="DropPoint"/> は、そのフィールドで物を落としたい場所（無ければ null）。食べ物を生む側が、狙う先に使う。
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

        /// <summary>そのフィールドで物を落としたい場所。無ければ null。</summary>
        public static Transform DropPoint { get; private set; }

        /// <summary>立っているフィールドが変わった（新しいフィールドを運ぶ）。<see cref="Field"/> を更新してから出す。同じフィールドを Set しても出ない。</summary>
        public static event Action<FieldId> Changed;

        /// <summary>いま立っているフィールドを決める。dropPoint は、そのフィールドで物を落としたい場所（無ければ null）。</summary>
        public static void Set(FieldId field, Transform dropPoint = null)
        {
            DropPoint = field.IsEmpty ? null : dropPoint;
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
            DropPoint = null;
            Changed = null;
        }
    }
}
