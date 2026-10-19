namespace MouthOfGod.Core
{
    /// <summary>
    /// 生まれた食べ物が持つ。届け先の判定など、機能をまたぐ側が「これはどの食べ物か」を読むために使う。
    /// 実装は Food の FoodItemTag で、食べ物を生むときに付く。
    /// </summary>
    /// <remarks>
    /// 受け取る側は、Food を参照せずに、この interface で食べ物の ID を読む。
    /// 例: <c>if (other.GetComponentInParent&lt;IFoodItem&gt;() is IFoodItem item) field.Receive(item.Food);</c>
    /// </remarks>
    public interface IFoodItem
    {
        /// <summary>食べ物の ID（食べ物の Prefab のファイル名）。</summary>
        FoodId Food { get; }
    }
}
