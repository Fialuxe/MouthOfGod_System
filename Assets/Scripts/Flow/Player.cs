using System;
using MouthOfGod.Core;
using UnityEngine;
using DeliveredFood = MouthOfGod.Core.Delivered;
using SpawnedFood = MouthOfGod.Core.Spawned;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 体験者への入口です。フォーク・デバイス・立つ場所・食べ物・届け先をまとめて見せます。
    /// 中身が Mock でも、Meta XR Simulator でも、実機でも、場面のスクリプトの書き方は変わりません。
    /// timeout は秒です。0 なら、起きるまで待ち続けます。
    /// </summary>
    public static class Player
    {
        /// <summary>いま前に立っているフィールドです。どこでもなければ空です。</summary>
        public static string Field => Standing.Field.IsEmpty ? "" : Standing.Field.ToString();

        /// <summary>いまの食感です。まだ噛んでいなければ空です。</summary>
        public static string Texture => Devices.Current == null || Devices.Current.Current.IsEmpty ? "" : Devices.Current.Current.ToString();

        /// <summary>雲を刺すまで待ちます。もう刺してあれば、すぐ true を返します。</summary>
        public static Awaitable<bool> Pierced(float timeout = 0f)
            => Wait.Until(() => Forks.Current != null && Forks.Current.IsLoaded, timeout);

        /// <summary>フォークの先を口に入れるまで待ちます。もう入っていれば、すぐ true を返します。</summary>
        public static Awaitable<bool> InMouth(float timeout = 0f)
            => Wait.Until(() => Forks.Current != null && Forks.Current.InMouth, timeout);

        /// <summary>field の前に立つまで待ちます。もう立っていれば、すぐ true を返します。</summary>
        public static Awaitable<bool> StandAt(string field, float timeout = 0f)
            => Wait.Until(() => Field == field, timeout);

        /// <summary>食感が times 回切り替わるまで待ちます。</summary>
        public static async Awaitable<bool> TextureChanged(int times, float timeout = 0f)
        {
            IDevice device = await Device();
            int count = 0;
            Action<TextureId> onChanged = _ => count++;
            device.TextureChanged += onChanged;
            try { return await Wait.Until(() => count >= times, timeout); }
            finally { device.TextureChanged -= onChanged; }
        }

        /// <summary>決定のボタンが押されるまで待ちます。</summary>
        public static async Awaitable<bool> Confirmed(float timeout = 0f)
        {
            IDevice device = await Device();
            bool pressed = false;
            Action<TextureId> onConfirmed = _ => pressed = true;
            device.Confirmed += onConfirmed;
            try { return await Wait.Until(() => pressed, timeout); }
            finally { device.Confirmed -= onConfirmed; }
        }

        /// <summary>食べ物が生まれるまで待ち、生まれた食べ物の元の Prefab を返します。時間切れなら null です。</summary>
        public static async Awaitable<GameObject> Spawned(float timeout = 0f)
        {
            await Wait.Until(() => FoodSpawners.Current != null);
            IFoodSpawner spawner = FoodSpawners.Current;
            GameObject food = null;
            Action<SpawnedFood> onSpawned = s => { if (food == null) food = s.Food; };
            spawner.Spawned += onSpawned;
            try { return await Wait.Until(() => food != null, timeout) ? food : null; }
            finally { spawner.Spawned -= onSpawned; }
        }

        /// <summary>どこかのフィールドに食べ物が届くまで待ちます。時間切れなら null です。</summary>
        public static async Awaitable<Delivery?> Delivered(float timeout = 0f)
        {
            Delivery? got = null;
            Action<DeliveredFood> onReceived = d =>
            {
                Debug.Log($"[Flow] 届きました: {d.Field} に {(d.Food != null ? d.Food.name : "?")}（{d.Kind}）");
                got ??= new Delivery(d.Field.ToString(), d.Food, d.WasRequested);
            };
            foreach (IFieldRequests field in Fields.All) field.Received += onReceived;
            try { return await Wait.Until(() => got.HasValue, timeout) ? got : null; }
            finally { foreach (IFieldRequests field in Fields.All) field.Received -= onReceived; }
        }

        static async Awaitable<IDevice> Device()
        {
            await Wait.Until(() => Devices.Current != null);
            return Devices.Current;
        }
    }

    /// <summary>届いた食べ物です。Field は届いたフィールド、Food は食べ物の元の Prefab、WasWanted は欲しがられていた物かどうかです。</summary>
    public readonly struct Delivery
    {
        public Delivery(string field, GameObject food, bool wasWanted)
        {
            Field = field;
            Food = food;
            WasWanted = wasWanted;
        }

        public string Field { get; }
        public GameObject Food { get; }
        public bool WasWanted { get; }
    }
}
