using System;
using System.Collections.Generic;

namespace MouthOfGod.Village.Editor.Draft
{
    /// <summary>フィールドの 1 つの置き物（下書き）。Unity に依存しない（テストできる）。</summary>
    public sealed class VillageItem
    {
        /// <summary>Resources/Prefabs/Village/ の Prefab 名。無ければ VillageBuilder が仮の箱を置く。</summary>
        public string Prefab;
        /// <summary>
        /// このステップ（1 から。<see cref="VillageLayout.Steps"/> の何番目か）が済むと出る。0 なら、はじめからある。
        /// Bake すると、そのステップ（<see cref="FieldStep"/>）の子になる。
        /// </summary>
        public int AppearsAt;
        /// <summary>このステップが済むと消える。0 なら消えない。Bake すると、そのステップの「消す物」に入る。</summary>
        public int GoneAt;
        /// <summary>
        /// 村人だけ使う。このステップで困りごとが解ける。-1 は村人ではない。Bake すると、そのステップの「困りごとが解ける人」に入る。
        /// </summary>
        public int HelpedAt = -1;
        /// <summary>フィールド（シーンの Village）の中心からの位置（メートル。実物のミニチュアの寸法。村は 0.54 m 四方なので ±0.27 の中）。</summary>
        public float X, Z;
        /// <summary>地面からの高さ（メートル）。畑の畝の上に乗せる作物などに使う。</summary>
        public float Y;
        public float YawDegrees;
        /// <summary>
        /// 見た目の一番長い辺の長さ（メートル。実物のミニチュアの寸法）。
        /// 素材ごとの元の大きさ・原点のずれによらず、この長さに合わせて置く。
        /// </summary>
        public float Size = 0.05f;
        /// <summary>
        /// 道のように、縦横を別々に伸ばす物の長さ（Z）と幅（X）（メートル）。0 なら使わず、Size で縦横同じ倍率にする。
        /// 伸ばす物の Prefab は、一辺 1 の板として作る（VillagePrefabGenerator）。
        /// </summary>
        public float Length, Width;

        public bool IsVillager => HelpedAt >= 0;
    }

    /// <summary>戻り方の 1 段（下書き）。Bake すると <see cref="FieldStep"/> になる。</summary>
    public sealed class VillageStep
    {
        public string Name;
        /// <summary>欲しい物の FoodId（仮。食べ物の ID は #31 で決まる）。</summary>
        public string Food;
    }

    public sealed class VillageField
    {
        public string Id;
        /// <summary>台全体の中での、フィールドの中心（メートル。実物のミニチュアの寸法）。</summary>
        public float CenterX, CenterZ;
        public List<VillageItem> Items = new List<VillageItem>();
    }

    /// <summary>
    /// 村の配置の下書き。メニュー MouthOfGod > Village > Bake Draft Village Prefab で、ここから Village.prefab を作る。
    /// 作った後の配置は Prefab を手で直す（ここは最初の下書きとして残す。Prefab と同じに保つ必要はない）。
    /// 数字はすべて実物のミニチュアの寸法（メートル）。台は 1.08 m 四方、村はその中の 0.54 m 四方。
    ///
    /// 村らしく見せるための決まり（README「配置の考え方」）:
    /// 1. 中心に広場と井戸（目が最初に行く所）。道は広場で交わり、村の外（隣のフィールド）へ抜ける。
    /// 2. 家は道か広場に正面を向け、少しずつ角度と下がり具合を変える（一直線にそろえない）。
    /// 3. 道の突き当たりに目印（風車）。
    /// 4. 畑・納屋・家畜は外側。家の集まりと畑の間を細い道でつなぐ。
    /// 5. 外周に木を置いて、村の端をやわらげる。詰め込みすぎず、広場のまわりは空ける。
    /// 6. 村人は、何かをしている場所にいる。1 か所に集めない。
    ///
    /// 村は届けるたびに少しずつ戻る（README「ステップ」）。はじめは飢饉（畑は枯れ、木は茶色、家畜はいない、村人は全員悩む）:
    ///   1 畑に芽が出る（畑の 2 人が元気になる）
    ///   2 作物が実り、木が緑に戻る（井戸の人が元気になる）
    ///   3 羊が戻る（羊の番の長老が元気になる）
    ///   4 豚も戻る（戸口の人も元気になり、全員が元気）
    /// </summary>
    public static class VillageLayout
    {
        public const string Village = "village";

        // ステップの番号（下の Steps の何番目か。1 から）。下書きの中だけで使い、Bake した Prefab には番号は残らない。
        const int Sprout = 1, Crops = 2, Sheep = 3, Pig = 4;

        /// <summary>村の戻り方の順番。欲しい物の ID は仮。</summary>
        public static readonly IReadOnlyList<VillageStep> Steps = new[]
        {
            new VillageStep { Name = "Sprout", Food = "carrot" },
            new VillageStep { Name = "Crops", Food = "pumpkin" },
            new VillageStep { Name = "Sheep", Food = "sheep" },
            new VillageStep { Name = "Pig", Food = "pig" },
        };

        const float HouseSize = 0.10f;
        const float VillagerSize = 0.065f;
        const float RoadWidth = 0.035f, LaneWidth = 0.022f;
        // House_Sample の正面（玄関）が、向き 0° のときにどちらを向くか。家が道に背を向けていたら 180 にする。
        const float HouseFrontYaw = 0f;
        // 人物の素材は、向き 0° で +Z を向く。
        const float CharacterFrontYaw = 0f;

        const float FarmX = 0.15f, FarmZ = -0.165f, FarmSize = 0.15f;
        const float FenceLength = 0.05f; // 柵 1 枚の長さ。畑の一辺 FarmSize に 3 枚
        const int CropsPerRidge = 4;
        // 畑の Prefab（VillagePrefabGenerator.CreateFarmland）の、畝の位置（畑の一辺を 1 とした X）と、畝の上面の高さ（同じく 1 とした値）。
        static readonly float[] RidgeX = { -0.375f, -0.125f, 0.125f, 0.375f };
        const float RidgeTop = 0.06f;

        // 外周の木 (X, Z, 向き, 大きさ)。飢饉の間は葉が茶色い木（Tree_Bare）、作物が実ると緑の木（Tree）。
        static readonly (float X, float Z, float Yaw, float Size)[] Trees =
        {
            (-0.24f, 0.24f, 0f, 0.08f),
            (-0.245f, 0.10f, 70f, 0.065f),
            (0.23f, 0.22f, 140f, 0.075f),
            (0.25f, 0.13f, 20f, 0.06f),
            (0.25f, -0.05f, 200f, 0.06f),
            (-0.245f, -0.245f, 260f, 0.07f),
        };

        public static List<VillageField> CreateDefault()
        {
            var village = new VillageField
            {
                Id = Village, CenterX = 0f, CenterZ = 0f,
                Items =
                {
                    // 1. 広場と井戸
                    new VillageItem { Prefab = "Plaza", Length = 0.13f, Width = 0.13f }, // 厚さは変えず、直径だけ伸ばす
                    Item("Well", 0f, 0f, 0f, 0.05f),

                    // 道。東西の大通りは広場で途切れ、少し曲がって村の外へ抜ける。北は風車へ、南は畑と納屋へ
                    Path(-0.27f, -0.025f, -0.06f, 0f, RoadWidth),
                    Path(0.06f, 0f, 0.27f, 0.03f, RoadWidth),
                    Path(0f, 0.06f, 0.02f, 0.15f, LaneWidth),
                    Path(0.02f, -0.06f, 0.07f, -0.10f, LaneWidth),
                    Path(-0.02f, -0.06f, -0.01f, -0.15f, LaneWidth),

                    // 2. 家は道か広場に正面を向ける
                    House(-0.12f, 0.09f, faceX: -0.12f, faceZ: 0f),
                    House(-0.19f, -0.08f, faceX: -0.19f, faceZ: 0f),
                    House(-0.09f, -0.10f, faceX: 0f, faceZ: 0f),
                    House(0.13f, 0.11f, faceX: 0.10f, faceZ: 0f),

                    // 3. 北の道の突き当たりに風車
                    Item("Windmill", 0.02f, 0.21f, 200f, 0.11f),

                    // 4. 外側: 納屋・鶏小屋・荷車・木箱。家畜は村が戻ってから現れる
                    Item("Barn", -0.01f, -0.21f, 0f, 0.10f),
                    Item("ChickenCoop", -0.17f, -0.22f, 30f, 0.05f),
                    Item("Wagon", -0.10f, -0.03f, 85f, 0.04f),
                    Item("Crate", 0.05f, -0.235f, 10f, 0.015f),
                    Item("Crate", 0.058f, -0.215f, 35f, 0.015f),
                    Appears(Item("Sheep", -0.22f, 0.19f, 120f, 0.035f), at: Sheep),
                    Appears(Item("Sheep", -0.18f, 0.22f, 200f, 0.035f), at: Sheep),
                    Appears(Item("Pig", -0.12f, -0.23f, 60f, 0.03f), at: Pig),

                    // 畑（中心 (FarmX, FarmZ)、一辺 FarmSize、畝 4 本は X 方向に並ぶ）。北側は道に向けて開け、残る 3 辺を柵で囲む
                    Item("Farmland", FarmX, FarmZ, 0f, FarmSize),
                    Fence(-1, -1, 0f), Fence(0, -1, 0f), Fence(1, -1, 0f),
                    Fence(2, -1, 90f), Fence(2, 0, 90f), Fence(2, 1, 90f),
                    Fence(-2, -1, 90f), Fence(-2, 0, 90f),

                    // 村人（困っている人）。1 か所に集めず、それぞれ何かをしている場所に置く。家に対して少し大きめ（誇張）
                    // 悩むのをやめるのは、その人の困りごとが解けるステップ（畑の人は芽が出たら、長老は羊が戻ったら）。1 人目が吹き出しを出す
                    Villager("Worker_Male", 0.10f, -0.07f, FarmX, FarmZ, helpedAt: Sprout),       // 畑の入り口で、畑を見る
                    Villager("Worker_Female", 0.19f, -0.13f, 0.15f, -0.20f, helpedAt: Sprout),    // 畑の中で、畝を見下ろす
                    Villager("Casual_Female", 0.035f, -0.02f, 0f, 0f, helpedAt: Crops),           // 井戸で水をくむ
                    Villager("OldClassy_Male", -0.20f, 0.15f, -0.20f, 0.21f, helpedAt: Sheep),    // 長老が羊の番をする
                    Villager("Casual_Male", -0.10f, 0.03f, -0.05f, -0.02f, helpedAt: Pig),        // 家の戸口で、大通りの方を見る
                },
            };

            // 5. 外周の木。飢饉の間は茶色、作物が実ると緑
            foreach (var t in Trees)
            {
                village.Items.Add(Appears(Item("Tree_Bare", t.X, t.Z, t.Yaw, t.Size), at: 0, goneAt: Crops));
                village.Items.Add(Appears(Item("Tree", t.X, t.Z, t.Yaw, t.Size), at: Crops));
            }

            // 畑。飢饉の間は枯れた茎、芽が出て、作物が実る。畝 1 本に CropsPerRidge 個ずつ
            for (var ridge = 0; ridge < RidgeX.Length; ridge++)
            {
                village.Items.AddRange(CropRow("Crop_Withered", ridge, 0.02f, 0, goneAt: Sprout));
                village.Items.AddRange(CropRow("Crop_Sprout", ridge, 0.012f, Sprout, goneAt: Crops));
            }
            village.Items.AddRange(CropRow("Carrot", 0, 0.025f, Crops));
            village.Items.AddRange(CropRow("Turnip", 1, 0.024f, Crops));
            village.Items.AddRange(CropRow("Carrot", 2, 0.025f, Crops));
            village.Items.AddRange(CropRow("Pumpkin", 3, 0.03f, Crops));

            return new List<VillageField> { village };
        }

        /// <summary>はじめから順に done 個のステップが済んだときに、見える置き物か。</summary>
        public static bool IsVisible(VillageItem item, int done)
            => item.AppearsAt <= done && (item.GoneAt == 0 || done < item.GoneAt);

        /// <summary>はじめから順に done 個のステップが済んだときに、村人が悩んでいるか。村人でなければ false。</summary>
        public static bool IsWorried(VillageItem item, int done)
            => item.IsVillager && done < item.HelpedAt;

        // (x1, z1) から (x2, z2) までの道。道の Prefab は一辺 1 の板で、長さを Z に伸ばす。
        static VillageItem Path(float x1, float z1, float x2, float z2, float width)
        {
            var dx = x2 - x1;
            var dz = z2 - z1;
            var item = Item("Path", (x1 + x2) / 2f, (z1 + z2) / 2f, YawToward(dx, dz));
            item.Length = (float)Math.Sqrt(dx * dx + dz * dz) + width; // 端どうしが重なるよう、幅の分だけ長くする
            item.Width = width;
            return item;
        }

        static VillageItem House(float x, float z, float faceX, float faceZ)
            => Item("House_Sample", x, z, YawToward(faceX - x, faceZ - z) + HouseFrontYaw, HouseSize);

        static VillageItem Villager(string prefab, float x, float z, float faceX, float faceZ, int helpedAt)
        {
            var item = Item(prefab, x, z, YawToward(faceX - x, faceZ - z) + CharacterFrontYaw, VillagerSize);
            item.HelpedAt = helpedAt;
            return item;
        }

        // +Z を 0° として、(dx, dz) の方を向く Y の角度。
        static float YawToward(float dx, float dz)
            => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        // 畝 1 本に、奥行き（Z）に等間隔に並べる。
        static IEnumerable<VillageItem> CropRow(string prefab, int ridge, float size, int at, int goneAt = 0)
        {
            var spacing = FarmSize * 0.9f / CropsPerRidge;
            for (var i = 0; i < CropsPerRidge; i++)
            {
                var z = FarmZ + (i - (CropsPerRidge - 1) / 2f) * spacing;
                var item = Item(prefab, FarmX + RidgeX[ridge] * FarmSize, z, ridge * 37f + i * 53f, size);
                item.Y = RidgeTop * FarmSize;
                yield return Appears(item, at, goneAt);
            }
        }

        // 畑のまわりの柵。column は畑の中心からの柵 1 枚分の数（-2 と 2 は左右の辺）、row は Z（-1 は手前の辺）。
        static VillageItem Fence(int column, int row, float yaw)
        {
            var half = FarmSize / 2f + 0.01f;
            float x, z;
            if (column == -2 || column == 2) { x = FarmX + (column < 0 ? -half : half); z = FarmZ + row * FenceLength; }
            else { x = FarmX + column * FenceLength; z = FarmZ + row * half; }
            return Item("Fence", x, z, yaw, FenceLength);
        }

        static VillageItem Appears(VillageItem item, int at, int goneAt = 0)
        {
            item.AppearsAt = at;
            item.GoneAt = goneAt;
            return item;
        }

        static VillageItem Item(string prefab, float x, float z, float yaw = 0f, float size = 0.05f)
            => new VillageItem { Prefab = prefab, X = x, Z = z, YawDegrees = yaw, Size = size };
    }
}
