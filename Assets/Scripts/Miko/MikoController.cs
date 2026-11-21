using System.Collections;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女の部品（話す・動く・表情・指さし）をまとめて動かします。巫女の Prefab の Body に付けます。
    /// </summary>
    /// <remarks>
    /// 1 行を話すときに、表情・指さし・移動を一緒に指定できます。
    /// 移動を指定すると、着いてから話し始めます。表情は、話し終わると Normal に戻ります。
    /// 部品どうしは互いを知りません。組み合わせるのは、このクラスだけです。
    /// </remarks>
    [RequireComponent(typeof(MikoSpeech))]
    public sealed class MikoController : MonoBehaviour
    {
        MikoSpeech _speech;
        MikoMover _mover;
        MikoFace _face;
        MikoGesture _gesture;
        Coroutine _pending; // 着くのを待っているセリフ
        bool _restoreFace;

        /// <summary>話している途中か、話すために移動している途中です。</summary>
        public bool IsBusy => _pending != null || _speech.IsSpeaking;

        /// <summary>
        /// 1 行を話します。face を渡すと、話す間だけ表情を変えます。pointAt を渡すと、話し始めに指さします。
        /// moveTo を渡すと、そこへ飛んで行き、着いてから話し始めます。
        /// </summary>
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

        public bool IsMoving => _mover != null && _mover.Mode == MikoMode.Go;
        public void GoTo(Transform spot) => _mover?.GoTo(spot);
        public void Follow() => _mover?.Follow();
        public void Stay() => _mover?.Stay();
        public void PointAt(Transform target) => _gesture?.PointAt(target);
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
