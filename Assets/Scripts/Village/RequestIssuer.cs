using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>村人の要求を出す。Unity 非依存の通常クラス。出した要求は Issued で知らせる。</summary>
    public class RequestIssuer
    {
        readonly RequestSet _set;
        int _next;

        public RequestIssuer(RequestSet set)
        {
            _set = set;
        }

        public event Action<RequestIssued> Issued;

        /// <summary>要求をひとつ出す。ほしい物か届け先が空なら出さない。</summary>
        public void Issue(Request request)
        {
            var food = request.Food;
            var field = request.Field;
            if (food.IsEmpty || field.IsEmpty)
            {
                Debug.LogWarning("[RequestIssuer] ほしい物か届け先が空の要求は出しません。");
                return;
            }

            Debug.Log($"[RequestIssuer] 要求: {food} を {field} へ");
            Issued?.Invoke(new RequestIssued(food, field));
        }

        /// <summary>RequestSet の次の要求を出す。最後まで出したら（または一覧が無ければ）false。</summary>
        public bool IssueNext()
        {
            if (_set == null) return false;
            var requests = _set.Requests;
            if (_next >= requests.Count) return false;

            Issue(requests[_next]);
            _next++;
            return true;
        }
    }
}
