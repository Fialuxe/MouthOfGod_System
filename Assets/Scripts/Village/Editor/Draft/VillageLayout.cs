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

        /// <summary>煙などの演出（Prefab 名が Fx_ で始まる）。見た目の大きさを持たないので、大きさは合わせず、位置だけ置く。</summary>
        public bool IsEffect => Prefab != null && Prefab.StartsWith("Fx_");
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
    /// 3. 北は山に接するので、風車や家畜は置かない（道は北へ延ばさない）。
    /// 4. 畑・納屋・家畜は外側。家の集まりと畑の間を細い道でつなぐ。
    /// 5. 外周に木を置いて、村の端をやわらげる。詰め込みすぎず、広場のまわりは空ける。
    /// 6. 村人は、何かをしている場所にいる。1 か所に集めない。
    ///
    /// 村は届けるたびに少しずつ戻る（README「ステップ」）。はじめは飢饉（畑は枯れ、木は茶色、家は屋根が無く、村人は全員悩む）。
    /// どのステップの欲しい物も農作物で、1 ステップに戻るものは、畝 1 本・木 1 本・家 1 軒・村人 1 人:
    ///   1 かぼちゃ（畑の入り口の人が元気になる）
    ///   2 にんじん（井戸の人が元気になる）
    ///   3 かぶ（畑の中の人が元気になる）
    ///   4 トマト（戸口の人も元気になり、全員が元気）
    /// 北（山のフィールド）と東（海のフィールド）の端には、何も置かない・畑を寄せない（隣の景色とぶつけないため）。
    /// </summary>
    public static class VillageLayout
    {
        public const string Village = "village";

        // ステップの番号（下の Steps の何番目か。1 から）。下書きの中だけで使い、Bake した Prefab には番号は残らない。
        const int Pumpkin = 1, Carrot = 2, Turnip = 3, Tomato = 4;

        /// <summary>村の戻り方の順番。欲しい物の ID は仮。</summary>
        public static readonly IReadOnlyList<VillageStep> Steps = new[]
        {
            new VillageStep { Name = "Pumpkin", Food = "pumpkin" },
            new VillageStep { Name = "Carrot", Food = "carrot" },
            new VillageStep { Name = "Turnip", Food = "turnip" },
            new VillageStep { Name = "Tomato", Food = "tomato" },
        };

        const float HouseSize = 0.10f;
        // 家の中心から、正面の壁の外側までの距離（HouseSize に合わせた値）。
        const float DoorFrontDistance = 0.045f;
        // 煙の出る高さ（家の屋根のあたり。地面から）。家の大きさ HouseSize に合わせた仮の値で、Prefab を開いて直す。
        const float SmokeHeight = 0.075f;
        const float VillagerSize = 0.085f;
        const float RoadWidth = 0.035f, LaneWidth = 0.022f;
        // House_Sample の玄関は、Prefab の -Z 側にある。向き 0° で +Z を向かせるため 180 にする。
        const float HouseFrontYaw = 180f;
        // 人物の素材は、向き 0° で +Z を向く。
        const float CharacterFrontYaw = 0f;

        const float FarmX = 0.11f, FarmZ = -0.165f, FarmSize = 0.15f;
        const float FenceLength = 0.05f; // 柵 1 枚の長さ。畑の一辺 FarmSize に 3 枚
        const int CropsPerRidge = 3; // 作物を大きくしたので、4 個だと重なる
        // 畑の Prefab（VillagePrefabGenerator.CreateFarmland）の、畝の位置（畑の一辺を 1 とした X）と、畝の上面の高さ（同じく 1 とした値）。
        static readonly float[] RidgeX = { -0.375f, -0.125f, 0.125f, 0.375f };
        const float RidgeTop = 0.06f;

        // 外周の木 (X, Z, 向き, 大きさ)。並びの順に、1 本ずつステップ 1〜4 で緑に戻る（飢饉の間は葉が茶色い Tree_Bare）。北の端（山側）には置かない。
        static readonly (float X, float Z, float Yaw, float Size)[] Trees =
        {
            (-0.245f, -0.245f, 260f, 0.07f),
            (-0.245f, 0.10f, 70f, 0.065f),
            (0.25f, -0.05f, 200f, 0.06f),
            (0.25f, 0.13f, 20f, 0.06f),
        };

        // 家 (X, Z, 向く先 X, 向く先 Z)。並びの順に、1 軒ずつステップ 1〜4 で屋根が直る（はじめは屋根が無い House_Ruined）。
        static readonly (float X, float Z, float FaceX, float FaceZ)[] Houses =
        {
            (-0.09f, -0.10f, 0f, 0f),
            (-0.19f, -0.08f, -0.19f, 0f),
            (-0.12f, 0.09f, -0.12f, 0f),
            (0.13f, 0.11f, 0.10f, 0f),
        };

        // 畝の作物 (Prefab, 大きさ)。畝 0（畑の西端）から順に、ステップ 1〜4 で枯れた茎から作物に変わる。
        static readonly (string Prefab, float Size)[] CropKinds =
        {
            ("Pumpkin", 0.06f), ("Carrot", 0.055f), ("Turnip", 0.05f), ("Tomato", 0.05f),
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

                    // 道。東西の大通りは広場で途切れ、少し曲がって村の外へ抜ける。南は畑と納屋へ（北は山に接するので道を出さない）
                    Path(-0.27f, -0.025f, -0.06f, 0f, RoadWidth),
                    Path(0.06f, 0f, 0.27f, 0.03f, RoadWidth),
                    Path(0.02f, -0.06f, 0.03f, -0.10f, LaneWidth),
                    Path(-0.02f, -0.06f, -0.03f, -0.15f, LaneWidth),

                    // 2. 家は道か広場に正面を向ける（下の Houses から置く。ステップごとに 1 軒ずつ直る）

                    // 3. 外側（南西）: 納屋・荷車・木箱
                    Item("Barn", -0.04f, -0.21f, 0f, 0.10f),
                    Item("Wagon", -0.10f, -0.03f, 85f, 0.04f),
                    Item("Crate", -0.105f, -0.235f, 10f, 0.015f),
                    Item("Crate", -0.097f, -0.215f, 35f, 0.015f),

                    // 畑（中心 (FarmX, FarmZ)、一辺 FarmSize、畝 4 本は X 方向に並ぶ）。北側は道に向けて開け、残る 3 辺を柵で囲む
                    Item("Farmland", FarmX, FarmZ, 0f, FarmSize),
                    Fence(-1, -1, 0f), Fence(0, -1, 0f), Fence(1, -1, 0f),
                    Fence(2, -1, 90f), Fence(2, 0, 90f), Fence(2, 1, 90f),
                    Fence(-2, -1, 90f), Fence(-2, 0, 90f),

                    // 村人（困っている人）。1 か所に集めず、それぞれ何かをしている場所に置く。家に対して少し大きめ（誇張）
                    // 悩むのをやめるのは、その人の困りごとが解けるステップ（1 ステップに 1 人）。1 人目が吹き出しを出す
                    Villager("Worker_Male", FarmX - 0.05f, -0.07f, FarmX, FarmZ, helpedAt: Pumpkin),      // 畑の入り口で、畑を見る
                    Villager("Worker_Female", FarmX + 0.08f, -0.13f, FarmX, -0.20f, helpedAt: Turnip),    // 畑の中で、畝を見下ろす
                    Villager("Casual_Female", 0.035f, -0.02f, 0f, 0f, helpedAt: Carrot),          // 井戸で水をくむ
                    Villager("Casual_Male", -0.10f, 0.03f, -0.05f, -0.02f, helpedAt: Tomato),    // 家の戸口で、大通りの方を見る
                },
            };

            // 外周の木。飢饉の間は茶色、1 本ずつ緑に戻る
            for (var i = 0; i < Trees.Length; i++)
            {
                var t = Trees[i];
                village.Items.Add(Appears(Item("Tree_Bare", t.X, t.Z, t.Yaw, t.Size), at: 0, goneAt: i + 1));
                village.Items.Add(Appears(Item("Tree", t.X, t.Z, t.Yaw, t.Size), at: i + 1));
            }

            // 家。はじめは屋根が無く、1 軒ずつ屋根が直る
            for (var i = 0; i < Houses.Length; i++)
            {
                var h = Houses[i];
                var yaw = YawToward(h.FaceX - h.X, h.FaceZ - h.Z) + HouseFrontYaw;
                village.Items.Add(Appears(Item("House_Ruined", h.X, h.Z, yaw, HouseSize), at: 0, goneAt: i + 1));
                village.Items.Add(Appears(Item("House_Sample", h.X, h.Z, yaw, HouseSize), at: i + 1));

                // 直った家のまわり: 屋根の煙、玄関わきのランタンと窓の下の花壇。
                // 玄関は、家の Prefab の中で -Z 側にあり、中心から Prefab の +X 側へ少しずれている。
                // 玄関が向く先（toward）を基準に置く。
                var toward = YawToward(h.FaceX - h.X, h.FaceZ - h.Z);
                var rad = toward * (float)Math.PI / 180f;
                float fx = (float)Math.Sin(rad), fz = (float)Math.Cos(rad); // 玄関が向く先
                float rx = fz, rz = -fx;                                    // 玄関に向かって右
                var smoke = Item("Fx_Smoke", h.X - rx * 0.01f, h.Z - rz * 0.01f);
                smoke.Y = SmokeHeight;
                village.Items.Add(Appears(smoke, at: i + 1));
                // 玄関は中心より 0.0125 だけ左、窓は中心より 0.03 だけ右にある
                village.Items.Add(Appears(Item("Lantern", h.X + fx * DoorFrontDistance - rx * 0.032f, h.Z + fz * DoorFrontDistance - rz * 0.032f, toward, 0.03f), at: i + 1));
                village.Items.Add(Appears(Item("Flowerbed", h.X + fx * (DoorFrontDistance + 0.004f) + rx * 0.03f, h.Z + fz * (DoorFrontDistance + 0.004f) + rz * 0.03f, toward, 0.035f), at: i + 1));
            }

            // 畑。飢饉の間は枯れた茎。畝 1 本ずつ、枯れた茎が消えて作物が実る。畝 1 本に CropsPerRidge 個ずつ
            for (var ridge = 0; ridge < RidgeX.Length; ridge++)
            {
                village.Items.AddRange(CropRow("Crop_Withered", ridge, 0.02f, 0, goneAt: ridge + 1));
                village.Items.AddRange(CropRow(CropKinds[ridge].Prefab, ridge, CropKinds[ridge].Size, ridge + 1));
            }

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
