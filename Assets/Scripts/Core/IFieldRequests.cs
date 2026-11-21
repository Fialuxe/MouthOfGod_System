using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 1 つのフィールドの欲しい物です。実装は Village の FieldStages で、フィールドは自分で <see cref="Fields"/> に登録します。
    /// </summary>
    /// <remarks>
    /// フィールドは「何が欲しいか」と「届いたら何が変わるか」だけを持ちます。いつ欲しい物を出すかは、呼ぶ側（Flow の場面）が決めます。
    /// 食べ物は、名前ではなく元の Prefab（<see cref="IFoodItem.Kind"/>）で指します。
    /// 1 つのフィールドに、同時に複数の欲しい物が出ていることがあります。メインスレッドから使います。
    /// </remarks>
    public interface IFieldRequests
    {
        FieldId Field { get; }

        /// <summary>欲しい物が 1 つ以上出ています。</summary>
        bool IsOpen { get; }

        /// <summary>次の欲しい物を出せます。出ている数が上限より少なく、まだ出していない欲しい物が残っているときです。</summary>
        bool CanOpen { get; }

        /// <summary>次の欲しい物を出します。出せなければ false です。</summary>
        bool Open();

        /// <summary>food を欲しがる、まだ済んでいないステップを出します。並び順は見ません。出せなければ false です。</summary>
        bool Open(GameObject food);

        /// <summary>
        /// 物がこのフィールドに届きました。何が届いても受け取り、何だったかを返します。<see cref="Received"/> は必ず 1 回出ます。
        /// 出ている欲しい物と同じ物なら、そのステップが済んでから Received が出ます。
        /// </summary>
        DeliveryKind Receive(GameObject food);

        /// <summary>出ている欲しい物を、すべて届かないまま終えます。</summary>
        void Expire();

        /// <summary>すべてのステップを、まだの状態に戻します。出ていた欲しい物も終えます。</summary>
        void Restart();

        /// <summary>物が届いたときに、届くたびに 1 回出ます。採点はこれを数えます。</summary>
        event Action<Delivered> Received;
    }
}
