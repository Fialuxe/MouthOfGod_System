using System.Collections.Generic;
using MouthOfGod.Core;
using MouthOfGod.Village;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 村・海・草原・山といったフィールドへの入口です。<c>Field.Get("sea")</c> で引きます。
    /// </summary>
    /// <remarks>
    /// ステップの進み方・村人の気持ち・吹き出し・届いたときの反応は、フィールドの部品が自分で行います。
    /// シーンに無いフィールドを引いても止まりません。そのときは、欲しい物と光の操作は何もしません。
    /// </remarks>
    public readonly struct Field
    {
        readonly IFieldRequests _requests;

        Field(string id, IFieldRequests requests)
        {
            Id = id;
            _requests = requests;
        }

        public string Id { get; }

        /// <summary>シーンにあるフィールドのとき <see langword="true"/> です。</summary>
        public bool Exists => _requests != null;

        /// <summary>欲しい物が 1 つ以上出ているとき <see langword="true"/> です。</summary>
        public bool IsOpen => _requests != null && _requests.IsOpen;

        /// <summary>指定した ID のフィールドを引きます。</summary>
        /// <param name="id">フィールドの ID です。"village"・"sea" などです。</param>
        /// <returns>
        /// 引いたフィールドです。見つからなくても止めず、警告を出して、何もしない Field を返します。
        /// </returns>
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

        /// <summary>指定した食べ物を欲しがらせます。</summary>
        /// <param name="food">欲しがらせる食べ物の Prefab です。<see langword="null"/> なら何もしません。</param>
        /// <remarks>
        /// もう何かを欲しがっていれば、何もしません。
        /// 出せなければ、フィールドを最初に戻してからもう一度出します。
        /// </remarks>
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

        /// <summary>フィールドを光らせるか、光を消します。</summary>
        /// <param name="on">光らせるなら <see langword="true"/> です。</param>
        /// <remarks>光らせる物は、フィールドの部品（FieldStages の Glow）が持っています。</remarks>
        public void Glow(bool on) => (_requests as FieldStages)?.SetGlow(on);

        /// <summary>立つ場所の床に、足あとを出すか消します。</summary>
        /// <param name="on">出すなら <see langword="true"/> です。</param>
        /// <remarks>シーン全体から立つ場所を探すので、毎フレームは呼びません。</remarks>
        public void Footprint(bool on)
        {
            foreach (StandArea area in Object.FindObjectsByType<StandArea>(FindObjectsSortMode.None))
                if (area.Field.ToString() == Id) area.ShowFootprint(on);
        }
    }
}
