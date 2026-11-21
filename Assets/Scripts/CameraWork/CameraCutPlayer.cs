using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 付けたオブジェクトを、決めた軌道と速さで動かします。
    /// アニメのカット割りのように、直線・曲線・円弧のカットをつないで撮ります。
    /// </summary>
    /// <remarks>
    /// <para>使い方は次のとおりです。</para>
    /// <list type="number">
    /// <item><description>カメラか、カメラの親に付けます。Shots に、カットを順に足します。</description></item>
    /// <item><description>位置の目印として、シーンに空の GameObject を置きます。それを End・Control Points・Center に入れます。このオブジェクトを選ぶと、軌道の線がシーンビューに出ます。目印を動かすと、線も変わります。</description></item>
    /// <item><description><see cref="Play()"/> を呼びます。Play On Start で自動にもできます。Play 中は、右クリックメニューの「Play」でも試せます。</description></item>
    /// </list>
    /// <para>カットの種類は次のとおりです。</para>
    /// <list type="bullet">
    /// <item><description>Line: 出発点から End へ直線で進みます。End が空なら、Direction From の前向きに Distance 進みます。Direction From も空なら、カメラ自身の前向きです。</description></item>
    /// <item><description>Curve: Control Points に引き寄せられるベジェ曲線で、End へ進みます。</description></item>
    /// <item><description>Orbit: Center のまわりを、Axis を軸に Angle 度まわります。360 で一周です。Radius Scale と Height Change で、らせんや寄り引きになります。</description></item>
    /// <item><description>Hold: その場で Seconds 待ちます。向きだけ変えたいときや、間を置きたいときに使います。</description></item>
    /// </list>
    /// <para>
    /// Use Speed を入れると m / 秒で、外すと秒数で動きます。
    /// Ease と Ease Strength で、出だしと着き際の緩急をつけます。
    /// </para>
    /// <para>
    /// 出発点は、前のカットの到着点です。
    /// 最初のカットは、再生を始めたときの位置から出ます。
    /// 目印の位置は、そのカットが始まる瞬間に読みます。
    /// </para>
    /// </remarks>
    public sealed class CameraCutPlayer : MonoBehaviour
    {
        [Tooltip("撮るカット。上から順に撮る。前のカットの到着点が、次のカットの出発点になる。空の要素は飛ばす")]
        [SerializeField] CameraShot[] shots = new CameraShot[0];

        [Tooltip("Play を始めたときに、自動で再生する")]
        [SerializeField] bool playOnStart;

        [Tooltip("最後のカットのあと、最初に戻ってくり返す。最初のカットの出発点は、そのときの位置になる")]
        [SerializeField] bool loop;

        [Tooltip("動かすのを Time.timeScale に左右されない時間にする（ポーズ中でも撮る）")]
        [SerializeField] bool unscaledTime;

        [Tooltip("全カットが終わったときに呼ぶ。Loop がオンのときと、Stop でやめたときは呼ばない")]
        [SerializeField] UnityEvent onFinished = new UnityEvent();

        Coroutine _running;
        // 再生していないときは -1 です。
        int _index = -1;

        public bool IsPlaying => _running != null;

        /// <summary>
        /// いま撮っているカットの番号を返します。
        /// </summary>
        /// <value>0 から始まる番号です。再生していなければ -1 です。</value>
        public int CurrentIndex => IsPlaying ? _index : -1;

        /// <summary>
        /// 撮るカットの並びです。
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> を入れると、空の配列として扱います。
        /// 配列への参照をそのまま保持します。
        /// </remarks>
        public CameraShot[] Shots
        {
            get => shots;
            set => shots = value ?? new CameraShot[0];
        }

        /// <summary>
        /// カットを撮り始めたときに発火します。引数はカットの番号です。
        /// </summary>
        public event Action<int> ShotStarted;

        /// <summary>
        /// 全カットを撮り終えたときに発火します。
        /// </summary>
        /// <remarks>
        /// Loop がオンのときと、<see cref="Stop"/> でやめたときは発火しません。
        /// </remarks>
        public event Action Finished;

        void Start()
        {
            if (playOnStart) Play();
        }

        /// <summary>
        /// 最初のカットから撮ります。再生中なら、やり直します。
        /// </summary>
        public void Play() => Play(0);

        /// <summary>
        /// 指定した番号のカットから撮ります。いまの位置が出発点になります。
        /// </summary>
        /// <remarks>
        /// 再生中なら、止めてからやり直します。
        /// カットが 1 つも無いときは、止めるだけです。
        /// </remarks>
        /// <param name="fromIndex">0 から始まるカットの番号です。範囲の外なら、端の番号に丸めます。</param>
        public void Play(int fromIndex)
        {
            Stop();
            if (shots == null || shots.Length == 0) return;
            _running = StartCoroutine(Run(Mathf.Clamp(fromIndex, 0, shots.Length - 1)));
        }

        /// <summary>
        /// その場でやめます。位置と向きはそのまま残ります。
        /// </summary>
        public void Stop()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
            _index = -1;
        }

        IEnumerator Run(int first)
        {
            do
            {
                for (int i = first; i < shots.Length; i++)
                {
                    CameraShot shot = shots[i];
                    if (shot == null) continue;
                    _index = i;
                    ShotStarted?.Invoke(i);
                    yield return PlayShot(shot);
                    shot.onArrived.Invoke();
                }
                first = 0;
            }
            while (loop);

            _running = null;
            _index = -1;
            onFinished.Invoke();
            Finished?.Invoke();
        }

        IEnumerator PlayShot(CameraShot shot)
        {
            var path = new ShotPath(shot, transform.position, transform.rotation, transform.forward);
            Quaternion startRotation = transform.rotation;
            Camera zoomCamera = shot.fieldOfViewEnd > 0f ? GetComponent<Camera>() : null;
            float startFov = zoomCamera != null ? zoomCamera.fieldOfView : 0f;
            float elapsed = 0f;
            float blend = 0f;

            while (true)
            {
                float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += dt;
                blend += dt;
                float t = Mathf.Clamp01(elapsed / path.Duration);
                float u = CameraPathMath.Ease(shot.ease, t, shot.easeStrength, shot.easeCurve);

                transform.position = path.Position(u);
                if (zoomCamera != null) zoomCamera.fieldOfView = Mathf.Lerp(startFov, shot.fieldOfViewEnd, u);
                ApplyAim(shot, path, startRotation, u, blend);

                if (t >= 1f) yield break;
                yield return null;
            }
        }

        void ApplyAim(CameraShot shot, ShotPath path, Quaternion startRotation, float u, float blend)
        {
            Quaternion wanted;
            switch (shot.aim)
            {
                case ShotAim.AlongPath:
                    Vector3 forward = path.Tangent(u, transform.forward);
                    wanted = Quaternion.LookRotation(forward, Vector3.up);
                    break;
                case ShotAim.LookAtTarget:
                    if (shot.aimTarget == null) return;
                    Vector3 aimPoint = shot.aimTarget.position;
                    if (shot.aimTargetEnd != null) aimPoint = Vector3.Lerp(aimPoint, shot.aimTargetEnd.position, u);
                    Vector3 toTarget = aimPoint - transform.position;
                    if (toTarget.sqrMagnitude < 1e-8f) return;
                    wanted = Quaternion.LookRotation(toTarget, Vector3.up);
                    break;
                case ShotAim.MatchEnd:
                    // 着いたときにちょうど End の向きになるよう、なじませずに進み具合で補間します。
                    transform.rotation = Quaternion.Slerp(startRotation, path.EndRotation, u);
                    return;
                default:
                    return;
            }

            // 前のカットから向きが急に変わらないよう、新しい向きへなじませます。
            // なじむ間は、カットの頭の向きから補間します。
            float k = shot.aimBlendSeconds <= 0f ? 1f : Mathf.Clamp01(blend / shot.aimBlendSeconds);
            transform.rotation = Quaternion.Slerp(startRotation, wanted, k);
        }

        void OnDisable() => Stop();

        [ContextMenu("Play")]
        void PlayFromMenu() => Play();

        [ContextMenu("Stop")]
        void StopFromMenu() => Stop();

#if UNITY_EDITOR
        // 選んだとき、軌道を線で見せます。目印を動かすと、線も変わります。
        // 再生中と同じく、前のカットの到着点を次の出発点にしてつなぎます。
        void OnDrawGizmosSelected()
        {
            if (shots == null) return;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 forward = transform.forward;
            for (int i = 0; i < shots.Length; i++)
            {
                CameraShot shot = shots[i];
                if (shot == null) continue;
                var path = new ShotPath(shot, position, rotation, forward);
                Gizmos.color = Color.HSVToRGB((i * 0.17f) % 1f, 0.7f, 1f);
                Vector3 previous = path.Position(0f);
                const int Segments = 32;
                for (int s = 1; s <= Segments; s++)
                {
                    Vector3 current = path.Position(s / (float)Segments);
                    Gizmos.DrawLine(previous, current);
                    previous = current;
                }
                Gizmos.DrawWireSphere(path.EndPosition, 0.06f);
                UnityEditor.Handles.Label(path.EndPosition, (i + 1) + ": " + shot.name);
                if (shot.kind == ShotKind.Curve && shot.controlPoints != null)
                    foreach (Transform p in shot.controlPoints)
                        if (p != null) Gizmos.DrawWireCube(p.position, Vector3.one * 0.05f);

                position = path.EndPosition;
                rotation = path.EndRotation;
                if (shot.aim == ShotAim.AlongPath) forward = path.Tangent(1f, forward);
            }
        }
#endif
    }
}
