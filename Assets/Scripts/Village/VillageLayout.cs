using System.Collections.Generic;

namespace MouthOfGod.Village
{
    /// <summary>置く物が、どの状態のときに見えるか。</summary>
    public enum ShowWhen
    {
        Always,
        Lacking,   // 足りない状態（始まり）だけ
        Delivered, // 届いた後だけ
    }

    /// <summary>フィールドの 1 つの置き物。Unity に依存しない（テストできる）。</summary>
    public sealed class VillageItem
    {
        /// <summary>Resources/Prefabs/Village/ の Prefab 名。無ければ VillageBuilder が仮の箱を置く。</summary>
        public string Prefab;
        public ShowWhen ShowWhen;
        /// <summary>フィールドの中心からの位置（メートル。ミニチュアに縮める前）。</summary>
        public float X, Z;
        public float YawDegrees;
        /// <summary>大きさの倍率。食べ物の Prefab は小さいので大きくする。</summary>
        public float Scale = 1f;
    }

    public sealed class VillageField
    {
        public string Id;
        /// <summary>村全体の中での、フィールドの中心（メートル。ミニチュアに縮める前）。</summary>
        public float CenterX, CenterZ;
        /// <summary>地面の一辺（メートル）。</summary>
        public float Size = 10f;
        public (float R, float G, float B) LackingGround;
        public (float R, float G, float B) DeliveredGround;
        public List<VillageItem> Items = new List<VillageItem>();
    }

    /// <summary>
    /// 村の配置（仮）。いまは「村」のフィールドだけ（草原・海・山は別に作る。docs/fields.md）。
    /// 配置図（#79）が決まったら、ここの数字を書き換える。置き物は素材の名前だけで指定する。
    /// </summary>
    public static class VillageLayout
    {
        public const string Village = "village";

        public static List<VillageField> CreateDefault()
        {
            return new List<VillageField>
            {
                new VillageField
                {
                    Id = Village, CenterX = 0f, CenterZ = 0f,
                    LackingGround = (0.45f, 0.36f, 0.24f), DeliveredGround = (0.40f, 0.62f, 0.28f),
                    Items =
                    {
                        Item("House_Sample", ShowWhen.Always, -3f, 1f),
                        Item("Well", ShowWhen.Always, 0f, 0f),
                        Item("Windmill", ShowWhen.Always, 3f, 2f, 200f),
                        Item("Barn", ShowWhen.Always, -2.5f, -3f, 20f),
                        Item("Fence", ShowWhen.Always, 2f, -3.5f),
                        Item("Casual_Male", ShowWhen.Always, 1f, 1.5f, 150f), // 困っている人: 村人
                        Item("Carrot", ShowWhen.Delivered, 1.5f, -1.5f, 0f, 4f),
                        Item("Turnip", ShowWhen.Delivered, 2.5f, -2f, 40f, 4f),
                        Item("Pumpkin", ShowWhen.Delivered, 3.5f, -1.5f, 80f, 4f),
                    },
                },
            };
        }

        static VillageItem Item(string prefab, ShowWhen when, float x, float z, float yaw = 0f, float scale = 1f)
            => new VillageItem { Prefab = prefab, ShowWhen = when, X = x, Z = z, YawDegrees = yaw, Scale = scale };

        /// <summary>この状態で見える置き物か。</summary>
        public static bool IsVisible(VillageItem item, bool delivered)
            => item.ShowWhen == ShowWhen.Always
               || (item.ShowWhen == ShowWhen.Delivered && delivered)
               || (item.ShowWhen == ShowWhen.Lacking && !delivered);
    }
}
