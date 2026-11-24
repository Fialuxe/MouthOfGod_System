using System;
using MouthOfGod.Core;

namespace MouthOfGod.Device
{
    /// <summary>
    /// <see cref="MockDevice"/> の、噛んでから決定するまでの流れを持ちます。
    /// </summary>
    /// <remarks>
    /// 噛み始めると、食感が一定間隔で順に切り替わります。決定すると、いまの食感を知らせます。
    /// 入力にも Unity にも依存しないので、EditMode のテストで確かめられます。
    /// </remarks>
    internal sealed class MockDeviceLogic
    {
        /// <summary>切り替える間隔の下限（秒）です。</summary>
        /// <remarks>0 以下を許すと、<see cref="Tick"/> の中で切り替えが止まらなくなるためです。</remarks>
        internal const float MinIntervalSeconds = 0.05f;

        readonly string[] _textureIds;
        readonly float _interval;
        int _index;
        float _elapsed;

        /// <param name="textureIds">
        /// 噛んでいる間に、順に切り替える食感の ID です。
        /// <see langword="null"/> は空として扱います。空欄の要素は知らせません。
        /// 配列への参照を持ち続けます。
        /// </param>
        /// <param name="intervalSeconds">
        /// 切り替える間隔（秒）です。<see cref="MinIntervalSeconds"/> より小さければ、それにそろえます。
        /// </param>
        internal MockDeviceLogic(string[] textureIds, float intervalSeconds)
        {
            _textureIds = textureIds ?? Array.Empty<string>();
            _interval = intervalSeconds < MinIntervalSeconds ? MinIntervalSeconds : intervalSeconds;
        }

        internal event Action<TextureId> TextureChanged;
        internal event Action<TextureId> Confirmed;
        internal event Action Bitten;

        internal bool IsBiting { get; private set; }

        /// <summary>いまの食感です。まだ噛んでいなければ <see cref="TextureId.None"/> です。</summary>
        /// <remarks>噛むのをやめても残します。離してから決定できるようにするためです。</remarks>
        internal TextureId Current { get; private set; }

        /// <summary>
        /// 噛み始めます。<see cref="Bitten"/> のあと、最初の食感をすぐ知らせます。
        /// </summary>
        /// <remarks>噛んでいる間に呼んでも、何もしません。</remarks>
        internal void BeginBite()
        {
            if (IsBiting) return;
            IsBiting = true;
            _index = 0;
            _elapsed = 0f;
            Current = TextureId.None;  // 最初の食感が空欄でも、前の噛みの食感を残しません
            Bitten?.Invoke();
            EmitCurrent();
        }

        /// <summary>噛むのをやめ、食感の切り替えを止めます。<see cref="Current"/> は残します。</summary>
        internal void EndBite()
        {
            IsBiting = false;
        }

        /// <summary>
        /// 経過時間を進めます。噛んでいる間だけ、間隔ごとに次の食感へ切り替えます。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数です。0 以下なら何もしません。</param>
        /// <remarks>
        /// 最後の食感の次は、最初に戻ります。
        /// 1 回で間隔を何回も越えたときは、越えた回数だけ切り替えて知らせます。
        /// </remarks>
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

        /// <summary>
        /// いまの食感を付けて、<see cref="Confirmed"/> を出します。
        /// </summary>
        /// <remarks>
        /// 決定はボタン操作なので、噛んでいるかどうかは見ません。
        /// 噛む前なら、空の食感を付けて出します。
        /// </remarks>
        internal void Confirm()
        {
            Confirmed?.Invoke(Current);
        }

        void EmitCurrent()
        {
            if (_textureIds.Length == 0) return;
            var id = new TextureId(_textureIds[_index]);
            if (id.IsEmpty) return;  // 空欄の要素は知らせません
            Current = id;
            TextureChanged?.Invoke(id);
        }
    }
}
