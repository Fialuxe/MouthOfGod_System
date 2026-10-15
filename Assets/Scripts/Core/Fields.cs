using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いまシーンにあるフィールド（IFieldRequests）の掲示板。フィールドが自分で Register し、使う側（本編・チュートリアル・吹き出し・届いたかの判定）は
    /// All と Added で受け取る。誰も相手のクラスを知らずにつながる（docs/requests.md「API 仕様」）。
    /// </summary>
    /// <remarks>
    /// - 使う側は、All を見てから Added を購読する（後から置かれたフィールドも届く）。OnDisable で Added・Removed の購読を外す。
    /// - ID が空のフィールド、すでにある ID のフィールドは登録せず、警告を出す（Play は止めない）。データの間違いなので、先のフィールドが外れても、断ったフィールドを後から登録し直すことはしない。
    /// - すべてメインスレッドから呼ぶ。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public static class Fields
    {
        static readonly List<IFieldRequests> _all = new List<IFieldRequests>();

        // 購読先の例外と警告の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;
        internal static Action<string> ReportWarning = Debug.LogWarning;

        /// <summary>登録されているフィールド（登録した順）。</summary>
        public static IReadOnlyList<IFieldRequests> All => _all;

        /// <summary>フィールドが登録された。</summary>
        public static event Action<IFieldRequests> Added;

        /// <summary>フィールドの登録が外れた（シーンから消えた、無効になった）。</summary>
        public static event Action<IFieldRequests> Removed;

        /// <summary>その ID のフィールド。無ければ null。</summary>
        public static IFieldRequests Find(FieldId id)
        {
            foreach (var field in _all)
                if (field.Field == id) return field;
            return null;
        }

        /// <summary>フィールドを登録する。ID が空・同じ ID がすでにあるなら、登録せずに警告を出して false。同じものを 2 回登録しても 1 つ。</summary>
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

        /// <summary>登録を外す。登録されていなければ何もしない。</summary>
        public static void Unregister(IFieldRequests field)
        {
            if (field == null || !_all.Remove(field)) return;
            Raise(Removed, field);
        }

        // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
        static void Raise(Action<IFieldRequests> handlers, IFieldRequests field)
        {
            if (handlers == null) return;
            foreach (Action<IFieldRequests> handler in handlers.GetInvocationList())
            {
                try { handler(field); }
                catch (Exception e) { ReportException(e); }
            }
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の登録と購読を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            _all.Clear();
            Added = null;
            Removed = null;
        }
    }
}
