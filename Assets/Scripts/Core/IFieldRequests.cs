using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 1 つのフィールドの欲しい物です。実装は Village の FieldStages です。
    /// フィールドは、自分で <see cref="Fields"/> に登録します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フィールドが持つのは「何が欲しいか」と「届いたら何が変わるか」だけです。
    /// いつ欲しい物を出すかは、呼ぶ側の Flow の場面が決めます。
    /// </para>
    /// <para>
    /// 食べ物は、名前ではなく元の Prefab で指します。<see cref="IFoodItem.Kind"/> と同じ物です。
    /// 1 つのフィールドに、同時に複数の欲しい物が出ていることがあります。
    /// メインスレッドから使います。
    /// </para>
    /// </remarks>
    public interface IFieldRequests
    {
        FieldId Field { get; }

        /// <summary>欲しい物が 1 つ以上出ているとき <see langword="true"/> です。</summary>
        bool IsOpen { get; }

        /// <summary>次の欲しい物を出せるとき <see langword="true"/> です。</summary>
        /// <remarks>出ている数が上限より少なく、まだ出していない欲しい物が残っているときです。</remarks>
        bool CanOpen { get; }

        /// <summary>次の欲しい物を出します。</summary>
        /// <returns>出せたら <see langword="true"/>、出せなければ <see langword="false"/> です。</returns>
        bool Open();

        /// <summary>指定した食べ物を欲しがる、まだ済んでいないステップを出します。</summary>
        /// <param name="food">欲しがらせる食べ物の元の Prefab です。</param>
        /// <returns>出せたら <see langword="true"/>、出せなければ <see langword="false"/> です。</returns>
        /// <remarks>ステップの並び順は見ません。</remarks>
        bool Open(GameObject food);

        /// <summary>物がこのフィールドに届いたことを伝え、何だったかを返します。</summary>
        /// <param name="food">届いた食べ物の元の Prefab です。</param>
        /// <returns>届いた物が、このフィールドにとって何だったかです。</returns>
        /// <remarks>
        /// 何が届いても受け取ります。<see cref="Received"/> は必ず 1 回出ます。
        /// 出ている欲しい物と同じ物なら、そのステップを済ませてから <see cref="Received"/> を出します。
        /// </remarks>
        DeliveryKind Receive(GameObject food);

        /// <summary>出ている欲しい物を、すべて届かないまま終えます。</summary>
        void Expire();

        /// <summary>すべてのステップを、まだの状態に戻します。出ていた欲しい物も終えます。</summary>
        void Restart();

        /// <summary>物が届くたびに 1 回出ます。採点はこれを数えます。</summary>
        event Action<Delivered> Received;
    }
}
