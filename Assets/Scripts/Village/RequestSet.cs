using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>村人の要求ひとつ。ほしい食べ物と、届け先のフィールド。</summary>
    [Serializable]
    public struct Request
    {
        [SerializeField] string _food;
        [SerializeField] string _field;

        public Request(FoodId food, FieldId field)
        {
            _food = food.Value;
            _field = field.Value;
        }

        public FoodId Food => new FoodId(_food);

        public FieldId Field => new FieldId(_field);
    }

    /// <summary>村人が順に出す要求の一覧。</summary>
    [CreateAssetMenu(menuName = "MouthOfGod/Request Set")]
    public class RequestSet : ScriptableObject
    {
        [SerializeField] Request[] _requests = new Request[0];

        public IReadOnlyList<Request> Requests => _requests ?? Array.Empty<Request>();
    }
}
