using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// ステップの物が出るとき、<see cref="PopInEffect"/> で大きくしないための印です。
    /// </summary>
    /// <remarks>
    /// 出方を自分で作る物に付けます。
    /// <see cref="FieldStep"/> の直接の子に付けたときだけ効きます。
    /// </remarks>
    public sealed class SkipPopIn : MonoBehaviour { }
}
