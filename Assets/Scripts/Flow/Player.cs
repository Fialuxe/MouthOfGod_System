using System;
using MouthOfGod.Core;
using UnityEngine;
using DeliveredFood = MouthOfGod.Core.Delivered;
using SpawnedFood = MouthOfGod.Core.Spawned;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 体験者への入口です。フォーク・デバイス・立つ場所・食べ物・届け先をまとめて見せます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 中身が Mock でも、Meta XR Simulator でも、実機でも、場面のスクリプトの書き方は変わりません。
    /// </para>
    /// <para>
    /// 待つメソッドの timeout は秒です。0 なら、起きるまで待ち続けます。
    /// 戻り値の <see langword="bool"/> は、起きたら <see langword="true"/>、時間切れなら <see langword="false"/> です。
    /// </para>
    /// </remarks>
    public static class Player
    {
        /// <summary>いま前に立っているフィールドです。どこでもなければ空文字です。</summary>
        public static string Field => Standing.Field.IsEmpty ? "" : Standing.Field.ToString();

        /// <summary>いまの食感です。まだ噛んでいないか、デバイスが無ければ空文字です。</summary>
        public static string Texture => Rig?.Device == null || Rig.Device.Current.IsEmpty ? "" : Rig.Device.Current.ToString();

        /// <summary>雲を刺すまで待ちます。もう刺してあれば、すぐ <see langword="true"/> を返します。</summary>
        public static Awaitable<bool> Pierced(float timeout = 0f)
            => Wait.Until(() => Rig?.Fork != null && Rig.Fork.IsLoaded, timeout);

        /// <summary>フォークの先を口に入れるまで待ちます。もう入っていれば、すぐ <see langword="true"/> を返します。</summary>
        public static Awaitable<bool> InMouth(float timeout = 0f)
            => Wait.Until(() => Rig?.Fork != null && Rig.Fork.InMouth, timeout);

        /// <summary>指定したフィールドの前に立つまで待ちます。もう立っていれば、すぐ <see langword="true"/> を返します。</summary>
        /// <param name="field">フィールドの ID です。"sea" などです。</param>
        /// <param name="timeout">待つ秒数です。0 なら待ち続けます。</param>
        public static Awaitable<bool> StandAt(string field, float timeout = 0f)
            => Wait.Until(() => Field == field, timeout);

        /// <summary>食感が指定した回数だけ切り替わるまで待ちます。</summary>
        /// <param name="times">待つ回数です。呼んだ後に切り替わった回数だけを数えます。</param>
        /// <param name="timeout">待つ秒数です。0 なら待ち続けます。</param>
        /// <remarks>デバイスがリグに入るまでの時間は、timeout に含みません。</remarks>
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
        /// <remarks>
        /// 呼ぶ前に押された分は数えません。
        /// デバイスがリグに入るまでの時間は、timeout に含みません。
        /// </remarks>
        public static async Awaitable<bool> Confirmed(float timeout = 0f)
        {
            IDevice device = await Device();
            bool pressed = false;
            Action<TextureId> onConfirmed = _ => pressed = true;
            device.Confirmed += onConfirmed;
            try { return await Wait.Until(() => pressed, timeout); }
            finally { device.Confirmed -= onConfirmed; }
        }

        /// <summary>食べ物が生まれるまで待ちます。</summary>
        /// <param name="timeout">待つ秒数です。0 なら待ち続けます。</param>
        /// <returns>最初に生まれた食べ物の元の Prefab です。時間切れなら <see langword="null"/> です。</returns>
        /// <remarks>食べ物を生む所がリグに入るまでの時間は、timeout に含みません。</remarks>
        public static async Awaitable<GameObject> Spawned(float timeout = 0f)
        {
            await Wait.Until(() => Rig?.FoodSpawner != null);
            IFoodSpawner spawner = Rig.FoodSpawner;
            GameObject food = null;
            Action<SpawnedFood> onSpawned = s => { if (food == null) food = s.Food; };
            spawner.Spawned += onSpawned;
            try { return await Wait.Until(() => food != null, timeout) ? food : null; }
            finally { spawner.Spawned -= onSpawned; }
        }

        /// <summary>どこかのフィールドに食べ物が届くまで待ちます。</summary>
        /// <param name="timeout">待つ秒数です。0 なら待ち続けます。</param>
        /// <returns>最初に届いた食べ物です。時間切れなら <see langword="null"/> です。</returns>
        /// <remarks>
        /// 呼んだときにシーンにあるフィールドだけを見ます。
        /// 待っている間に置かれたフィールドへ届いても、受け取りません。
        /// </remarks>
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

        static PlayerRig Rig => PlayerRig.Current;

        // リグにデバイスが入るまで待ちます。起動直後は、まだ入っていないことがあるためです。
        static async Awaitable<IDevice> Device()
        {
            await Wait.Until(() => Rig?.Device != null);
            return Rig.Device;
        }
    }

    /// <summary>届いた食べ物です。</summary>
    public readonly struct Delivery
    {
        /// <param name="field">届いたフィールドの ID です。</param>
        /// <param name="food">届いた食べ物の元の Prefab です。</param>
        /// <param name="wasWanted">欲しがられていた物なら <see langword="true"/> です。</param>
        public Delivery(string field, GameObject food, bool wasWanted)
        {
            Field = field;
            Food = food;
            WasWanted = wasWanted;
        }

        /// <summary>届いたフィールドの ID です。</summary>
        public string Field { get; }

        /// <summary>届いた食べ物の元の Prefab です。</summary>
        public GameObject Food { get; }

        /// <summary>欲しがられていた物なら <see langword="true"/> です。</summary>
        public bool WasWanted { get; }
    }
}
