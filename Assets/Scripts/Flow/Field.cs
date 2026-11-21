using System.Collections.Generic;
using MouthOfGod.Core;
using MouthOfGod.Village;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// フィールド（村・海・草原・山）への入口です。<c>Field.Get("sea")</c> で引きます。
    /// ステップの進み方・村人の気持ち・吹き出し・届いたときの反応は、フィールドの部品が自分で行います。
    /// </summary>
    public readonly struct Field
    {
        readonly IFieldRequests _requests;

        Field(string id, IFieldRequests requests)
        {
            Id = id;
            _requests = requests;
        }

        public string Id { get; }

        /// <summary>シーンにあるフィールドです。</summary>
        public bool Exists => _requests != null;

        /// <summary>欲しい物が 1 つ以上出ています。</summary>
        public bool IsOpen => _requests != null && _requests.IsOpen;

        /// <summary>id のフィールドを引きます。見つからなくても止めず、何もしない Field を返します。</summary>
        public static Field Get(string id)
        {
            IFieldRequests requests = Fields.Find(new FieldId(id));
            if (requests == null) Debug.LogWarning($"[Flow.Field] フィールド「{id}」がシーンにありません。");
            return new Field(id, requests);
        }

        /// <summary>シーンにあるフィールドすべてです。</summary>
        public static IEnumerable<Field> All
        {
            get
            {
                foreach (IFieldRequests requests in Fields.All) yield return new Field(requests.Field.ToString(), requests);
            }
        }

        /// <summary>food（食べ物の Prefab）を欲しがらせます。もう欲しがっていれば何もしません。出せなければ、最初に戻してからもう一度出します。</summary>
        public void Want(GameObject food)
        {
            if (_requests == null || _requests.IsOpen || food == null) return;
            if (_requests.Open(food)) return;
            _requests.Restart();
            if (!_requests.Open(food)) Debug.LogWarning($"[Flow.Field] {Id} に {food.name} を欲しがらせられませんでした。");
        }

        /// <summary>出せるだけ、次の欲しい物を出します。</summary>
        public void WantAll()
        {
            if (_requests == null) return;
            while (_requests.CanOpen && _requests.Open()) { }
        }

        /// <summary>最初の状態に戻します。</summary>
        public void Restart() => _requests?.Restart();

        /// <summary>フィールドを光らせます。光らせる物は、フィールドの部品（FieldStages の Glow）が持っています。</summary>
        public void Glow(bool on) => (_requests as FieldStages)?.SetGlow(on);

        /// <summary>立つ場所の床に、足あとを出します。</summary>
        public void Footprint(bool on)
        {
            foreach (StandArea area in Object.FindObjectsByType<StandArea>(FindObjectsSortMode.None))
                if (area.Field.ToString() == Id) area.ShowFootprint(on);
        }
    }
}
