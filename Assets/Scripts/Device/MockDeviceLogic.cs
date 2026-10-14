using System;
using MouthOfGod.Core;

namespace MouthOfGod.Device
{
    /// <summary>
    /// MockDevice の中身。入力（キー）にも Unity にも依存しない。
    /// 噛み始め → 食感が一定間隔で順に切り替わる → 決定、の流れだけを持つ。
    /// </summary>
    internal sealed class MockDeviceLogic
    {
        /// <summary>切替間隔の下限（秒）。0 以下だと切替が止まらなくなるため。</summary>
        internal const float MinIntervalSeconds = 0.05f;

        readonly string[] _textureIds;
        readonly float _interval;
        int _index;
        float _elapsed;

        internal MockDeviceLogic(string[] textureIds, float intervalSeconds)
        {
            _textureIds = textureIds ?? Array.Empty<string>();
            _interval = intervalSeconds < MinIntervalSeconds ? MinIntervalSeconds : intervalSeconds;
        }

        internal event Action<TextureId> TextureChanged;
        internal event Action Confirmed;
        internal event Action Bitten;

        internal bool IsBiting { get; private set; }

        /// <summary>デバイス先端に出す内容の割合（0〜100）。噛んでいる間は、現在の食感が何番目か（最後で 100）。噛んでいなければ 0。</summary>
        internal float FillPercent =>
            !IsBiting || _textureIds.Length == 0 ? 0f : 100f * (_index + 1) / _textureIds.Length;

        /// <summary>噛み始め。Bitten のあと、最初の食感をすぐ通知する。噛んでいる間の呼び出しは無視する。</summary>
        internal void BeginBite()
        {
            if (IsBiting) return;
            IsBiting = true;
            _index = 0;
            _elapsed = 0f;
            Bitten?.Invoke();
            EmitCurrent();
        }

        /// <summary>噛むのをやめる。食感の切替が止まる。</summary>
        internal void EndBite()
        {
            IsBiting = false;
        }

        /// <summary>経過時間を進める。噛んでいる間だけ、間隔ごとに次の食感へ切り替える（最後の次は最初に戻る）。</summary>
        internal void Tick(float deltaSeconds)
        {
            if (!IsBiting || deltaSeconds <= 0f || _textureIds.Length == 0) return;
            _elapsed += deltaSeconds;
            while (_elapsed >= _interval)
            {
                _elapsed -= _interval;
                _index = (_index + 1) % _textureIds.Length;
                EmitCurrent();
            }
        }

        /// <summary>決定。噛んでいるかどうかに関係なく Confirmed を出す（ボタン操作なので）。</summary>
        internal void Confirm()
        {
            Confirmed?.Invoke();
        }

        void EmitCurrent()
        {
            if (_textureIds.Length == 0) return;
            var id = new TextureId(_textureIds[_index]);
            if (id.IsEmpty) return; // 空欄の要素は通知しない
            TextureChanged?.Invoke(id);
        }
    }
}
