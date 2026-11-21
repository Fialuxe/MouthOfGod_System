using System.Collections;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女の部品をまとめて動かします。話す・動く・表情・指さしを、1 か所から指示できます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 巫女の Prefab の Body に付けます。
    /// 部品どうしは互いを知りません。組み合わせるのは、このクラスだけです。
    /// </para>
    /// <para>
    /// 1 行を話すときに、表情・指さし・移動を一緒に指定できます。
    /// 移動を指定すると、着いてから話し始めます。
    /// 表情は、話し終わると Normal に戻ります。
    /// </para>
    /// <para>
    /// <see cref="MikoMover"/>・<see cref="MikoFace"/>・<see cref="MikoGesture"/> は無くてもかまいません。
    /// 無い部品への指示は、何もしません。
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(MikoSpeech))]
    public sealed class MikoController : MonoBehaviour
    {
        MikoSpeech _speech;
        MikoMover _mover;
        MikoFace _face;
        MikoGesture _gesture;
        // 着くのを待っているセリフのコルーチンです。待っていなければ null です。
        Coroutine _pending;
        bool _restoreFace;

        /// <summary>話している途中か、話すために移動している途中なら <see langword="true"/> です。</summary>
        public bool IsBusy => _pending != null || _speech.IsSpeaking;

        /// <summary>
        /// 1 行を話します。表情・指さし・移動も一緒に指定できます。
        /// </summary>
        /// <param name="line">話すセリフです。</param>
        /// <param name="face">話す間だけ変える表情です。<see langword="null"/> なら変えません。</param>
        /// <param name="pointAt">話し始めに指さす先です。<see langword="null"/> なら指さしません。</param>
        /// <param name="moveTo">
        /// 話す前に飛んで行く先です。着いてから話し始めます。
        /// <see langword="null"/> なら、その場ですぐ話します。
        /// </param>
        /// <remarks>
        /// 着くのを待っているセリフがあれば、捨ててから話します。
        /// </remarks>
        public void Say(MikoLine line, MikoExpression? face = null, Transform pointAt = null, Transform moveTo = null)
        {
            CancelPending();
            if (moveTo != null && _mover != null)
            {
                _mover.GoTo(moveTo);
                _pending = StartCoroutine(SayAfterMove(line, face, pointAt));
                return;
            }
            Begin(line, face, pointAt);
        }

        /// <summary>話すのをやめ、字幕を消します。待っているセリフも捨てます。</summary>
        public void Hush()
        {
            CancelPending();
            _speech.Hush();
            RestoreFace();
        }

        /// <summary>目印へ向かって移動している途中なら <see langword="true"/> です。</summary>
        public bool IsMoving => _mover != null && _mover.Mode == MikoMode.Go;

        /// <summary>目印へ飛んで行きます。<see cref="MikoMover"/> が無ければ何もしません。</summary>
        /// <param name="spot">行き先の目印です。<see langword="null"/> なら何もしません。</param>
        public void GoTo(Transform spot) => _mover?.GoTo(spot);

        /// <inheritdoc cref="MikoMover.Follow"/>
        public void Follow() => _mover?.Follow();

        /// <inheritdoc cref="MikoMover.Stay"/>
        public void Stay() => _mover?.Stay();

        /// <inheritdoc cref="MikoGesture.PointAt"/>
        public void PointAt(Transform target) => _gesture?.PointAt(target);

        /// <summary>表情を変えます。</summary>
        /// <param name="face">変える表情です。</param>
        /// <remarks>
        /// <see cref="Say"/> の face と違い、話し終わっても Normal に戻しません。
        /// ただし、表情つきの <see cref="Say"/> の途中で呼ぶと、話し終わりに Normal へ戻ります。
        /// </remarks>
        public void SetFace(MikoExpression face) => _face?.Set(face);

        void Awake()
        {
            _speech = GetComponent<MikoSpeech>();
            _mover = GetComponent<MikoMover>();
            _face = GetComponent<MikoFace>();
            _gesture = GetComponent<MikoGesture>();
        }

        void Update()
        {
            if (_restoreFace && !IsBusy) RestoreFace();
        }

        IEnumerator SayAfterMove(MikoLine line, MikoExpression? face, Transform pointAt)
        {
            while (_mover.Mode == MikoMode.Go) yield return null;
            _pending = null;
            Begin(line, face, pointAt);
        }

        void Begin(MikoLine line, MikoExpression? face, Transform pointAt)
        {
            if (face.HasValue && _face != null)
            {
                _face.Set(face.Value);
                _restoreFace = true;
            }
            if (pointAt != null) _gesture?.PointAt(pointAt);
            _speech.Say(line);
        }

        void CancelPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
        }

        void RestoreFace()
        {
            if (!_restoreFace) return;
            _restoreFace = false;
            _face?.Set(MikoExpression.Normal);
        }
    }
}
