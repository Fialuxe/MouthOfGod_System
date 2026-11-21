using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いまシーンにあるフィールドの掲示板です。
    /// フィールドと使う側が、互いのクラスを知らずにつながります。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フィールドは自分で <see cref="Register"/> します。
    /// 使う側は <see cref="All"/> と <see cref="Added"/> で受け取ります。
    /// 使う側は、本編・チュートリアル・吹き出し・届いたかの判定です。
    /// 詳しくは docs/requests.md の「API 仕様」を見てください。
    /// </para>
    /// <para>
    /// 使う側は、先に <see cref="All"/> を見てから <see cref="Added"/> を購読します。
    /// こうすると、後から置かれたフィールドも受け取れます。
    /// OnDisable で <see cref="Added"/> と <see cref="Removed"/> の購読を外します。
    /// </para>
    /// <para>
    /// すべてメインスレッドから呼びます。
    /// この形を変えると使う側すべてに効くので、別の PR にします。
    /// </para>
    /// </remarks>
    public static class Fields
    {
        static readonly List<IFieldRequests> _all = new List<IFieldRequests>();

        // 購読先の例外と警告の出し先です。テストで差し替えます。
        internal static Action<Exception> ReportException = Debug.LogException;
        internal static Action<string> ReportWarning = Debug.LogWarning;

        /// <summary>登録されているフィールドを、登録した順に返します。</summary>
        public static IReadOnlyList<IFieldRequests> All => _all;

        /// <summary>フィールドが登録された直後に出ます。</summary>
        public static event Action<IFieldRequests> Added;

        /// <summary>フィールドの登録が外れた直後に出ます。</summary>
        /// <remarks>シーンから消えたときや、無効になったときに外れます。</remarks>
        public static event Action<IFieldRequests> Removed;

        /// <summary>指定した ID のフィールドを返します。</summary>
        /// <param name="id">探すフィールドの ID です。</param>
        /// <returns>見つかったフィールドです。無ければ <see langword="null"/> です。</returns>
        public static IFieldRequests Find(FieldId id)
        {
            foreach (var field in _all)
                if (field.Field == id) return field;
            return null;
        }

        /// <summary>フィールドを登録します。</summary>
        /// <param name="field">登録するフィールドです。<see langword="null"/> は渡せません。</param>
        /// <returns>
        /// 登録できたら <see langword="true"/> です。
        /// 同じものをもう一度登録したときも <see langword="true"/> で、登録は 1 つのままです。
        /// ID が空のときと、同じ ID がすでにあるときは、警告を出して <see langword="false"/> を返します。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="field"/> が <see langword="null"/> の場合です。</exception>
        /// <remarks>
        /// 断ったときも Play は止めません。データの間違いなので、警告で知らせるだけにします。
        /// 先のフィールドが外れても、断ったフィールドを後から登録し直すことはしません。
        /// </remarks>
        public static bool Register(IFieldRequests field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (_all.Contains(field)) return true;
            if (field.Field.IsEmpty)
            {
                ReportWarning($"[Fields] {field} のフィールド ID が空なので、登録しません（village / mountain / sea / grassland のどれかにする）。");
                return false;
            }
            if (Find(field.Field) != null)
            {
                ReportWarning($"[Fields] フィールド ID「{field.Field}」はもう登録されているので、{field} は登録しません（ID が重なっていないか確かめる）。");
                return false;
            }

            _all.Add(field);
            Raise(Added, field);
            return true;
        }

        /// <summary>登録を外します。</summary>
        /// <param name="field">外すフィールドです。<see langword="null"/> や、登録されていないものなら何もしません。</param>
        public static void Unregister(IFieldRequests field)
        {
            if (field == null || !_all.Remove(field)) return;
            Raise(Removed, field);
        }

        // 1 つの購読先が例外を投げても、残りには届けます。例外は Console に出します。
        static void Raise(Action<IFieldRequests> handlers, IFieldRequests field)
        {
            if (handlers == null) return;
            foreach (Action<IFieldRequests> handler in handlers.GetInvocationList())
            {
                try { handler(field); }
                catch (Exception e) { ReportException(e); }
            }
        }

        // Play のたびに空にします。ドメインのリロードを切っていても、前回の登録と購読を残さないためです。
        // テストからも呼びます。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            _all.Clear();
            Added = null;
            Removed = null;
        }
    }
}
