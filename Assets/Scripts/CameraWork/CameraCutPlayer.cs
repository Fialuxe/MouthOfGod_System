using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.CameraWork
{
    /// <summary>
    /// 付けたオブジェクト（カメラなど）を、決めた軌道と速さで動かす。アニメのカット割りのように、直線・曲線・円弧のカットをつないで撮る。
    /// </summary>
    /// <remarks>
    /// 使い方:
    /// 1. カメラ（またはカメラの親）に付ける。Shots に、カットを順に足す。
    /// 2. 位置の目印は、シーンに空の GameObject を置き、End・Control Points・Center に入れる。目印を動かすと、軌道の線がシーンビューに出る。
    /// 3. <see cref="Play"/> を呼ぶ（Play On Start で自動でも）。Play 中は、右クリックメニューの「Play」でも試せる。
    ///
    /// カットの種類:
    /// - Line: 出発点から End へ直線。End が空なら、Direction From（空ならカメラ自身）の前向きに Distance 進む。
    /// - Curve: Control Points に引き寄せられるベジェ曲線で、End へ。
    /// - Orbit: Center のまわりを、Axis を軸に Angle 度まわる。360 で一周。Radius Scale・Height Change でらせんや寄り引きになる。
    /// - Hold: その場で Seconds 待つ。向きだけ変えたいとき、間を置きたいときに使う。
    ///
    /// 速さ・緩急: Use Speed を入れると m / 秒で、外すと秒数で動く。Ease と Ease Strength で、出だしと着き際の緩急をつける。
    /// 出発点は、前のカットの到着点。最初のカットは、再生を始めたときの位置。目印の位置は、そのカットが始まる瞬間に読む。
    /// </remarks>
    public sealed class CameraCutPlayer : MonoBehaviour
    {
        [SerializeField] CameraShot[] shots = new CameraShot[0];

        [Tooltip("Play を始めたときに、自動で再生する")]
        [SerializeField] bool playOnStart;

        [Tooltip("最後のカットのあと、最初に戻ってくり返す。最初のカットの出発点は、そのときの位置になる")]
        [SerializeField] bool loop;

        [Tooltip("動かすのを Time.timeScale に左右されない時間にする（ポーズ中でも撮る）")]
        [SerializeField] bool unscaledTime;

        [Tooltip("全カットが終わったときに呼ぶ")]
        [SerializeField] UnityEvent onFinished = new UnityEvent();

        Coroutine _running;
        int _index = -1;

        /// <summary>再生中か。</summary>
        public bool IsPlaying => _running != null;

        /// <summary>いま撮っているカットの番号。再生していなければ -1。</summary>
        public int CurrentIndex => IsPlaying ? _index : -1;

        public CameraShot[] Shots
        {
            get => shots;
            set => shots = value ?? new CameraShot[0];
        }

        /// <summary>カットが始まった（番号）。</summary>
        public event Action<int> ShotStarted;

        /// <summary>全カットが終わった（Stop でやめたときは出ない）。</summary>
        public event Action Finished;

        void Start()
        {
            if (playOnStart) Play();
        }

        /// <summary>最初のカットから撮る。再生中なら、やり直す。</summary>
        public void Play() => Play(0);

        /// <summary>fromIndex 番のカットから撮る。いまの位置が出発点になる。</summary>
        public void Play(int fromIndex)
        {
            Stop();
            if (shots == null || shots.Length == 0) return;
            _running = StartCoroutine(Run(Mathf.Clamp(fromIndex, 0, shots.Length - 1)));
        }

        /// <summary>その場でやめる。位置はそのまま。</summary>
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
                    // 軌道の進み具合に合わせて、出発の向きから到着の向きへ
                    transform.rotation = Quaternion.Slerp(startRotation, path.EndRotation, u);
                    return;
                default:
                    return;
            }

            // 向きが前のカットから急に変わらないよう、新しい向きへなじませる。なじむ間は、カットの頭の向きから出発する。
            float k = shot.aimBlendSeconds <= 0f ? 1f : Mathf.Clamp01(blend / shot.aimBlendSeconds);
            transform.rotation = Quaternion.Slerp(startRotation, wanted, k);
        }

        void OnDisable() => Stop();

        [ContextMenu("Play")]
        void PlayFromMenu() => Play();

        [ContextMenu("Stop")]
        void StopFromMenu() => Stop();

#if UNITY_EDITOR
        // 選んだとき、軌道を線で見せる。目印を動かすと、線も変わる。
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
