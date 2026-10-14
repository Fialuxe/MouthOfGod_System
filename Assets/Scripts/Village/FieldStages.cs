using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールド（山・海・草原・村）の戻り具合（段階）を持つ。フィールドの一番上の物に 1 つ付ける。
    /// 段階が変わると、子の <see cref="IStageListener"/>（<see cref="StageVisibility"/>・<see cref="VillagerMood"/> など）に知らせ、
    /// C# の <see cref="StageChanged"/> と、Inspector でつなげる <see cref="onStageChanged"/> を出す。
    /// 届いたら 1 段階進める（<see cref="Advance"/>）のは、Core の Delivered を受ける側が呼ぶ（Bootstrap がつなぐ）。
    /// </summary>
    public sealed class FieldStages : MonoBehaviour
    {
        [Tooltip("フィールドの ID（docs/fields.md）。village / mountain / sea / grassland")]
        [SerializeField] string fieldId = "village";

        [Tooltip("最後の段階。0 が飢饉（始まり）で、この段階ですっかり戻る")]
        [SerializeField, Min(1)] int maxStage = 4;

        [Tooltip("いまの段階。開発中の確認用に、Play 中に動かすとその段階に切り替わる")]
        [SerializeField] int stage;

        [Tooltip("段階が変わったとき（引数は新しい段階）。音・光などを Inspector からつなぐ")]
        [SerializeField] UnityEvent<int> onStageChanged = new UnityEvent<int>();

        /// <summary>段階が変わったとき（前の段階、新しい段階）。</summary>
        public event Action<int, int> StageChanged;

        readonly List<IStageListener> _listeners = new List<IStageListener>();
        int _applied = -1;

        public string FieldId => fieldId;
        public int MaxStage => maxStage;
        public int Stage => _applied < 0 ? stage : _applied;
        public bool IsFullyRecovered => Stage >= maxStage;

        void Awake() => Refresh();

        void Start() => SetStage(stage);

        // Inspector で段階を動かしたら、その段階にする（開発中の確認用）。
        void Update()
        {
            if (stage != _applied) SetStage(stage);
        }

        void OnValidate() => stage = Stages.Clamp(stage, maxStage);

        /// <summary>1 段階進める（届けたとき）。最後の段階より先には進まない。</summary>
        public void Advance() => SetStage(Stage + 1);

        /// <summary>その段階にする。子の置き物・村人に知らせ、イベントを出す。</summary>
        public void SetStage(int value)
        {
            value = Stages.Clamp(value, maxStage);
            if (value == _applied) return;
            var previous = _applied;
            stage = _applied = value;

            foreach (var listener in _listeners) listener.OnStageChanged(previous, value);
            StageChanged?.Invoke(previous, value);
            onStageChanged.Invoke(value);
        }

        /// <summary>子の <see cref="IStageListener"/> を数え直す。Play 中に子を足したら呼ぶ。</summary>
        public void Refresh()
        {
            _listeners.Clear();
            GetComponentsInChildren(includeInactive: true, _listeners);
            if (_applied >= 0)
                foreach (var listener in _listeners) listener.OnStageChanged(-1, _applied);
        }
    }
}
