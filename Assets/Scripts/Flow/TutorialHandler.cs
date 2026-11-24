using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 体験の進行役です。子に並べた場面（<see cref="Chapter"/>）を、上から順に流します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// シーンに 1 つ置きます。
    /// スタッフは、今の場面を飛ばすことと、最初からやり直すことができます。
    /// </para>
    /// <para>
    /// 無効な子と、<see cref="Chapter"/> が付いていない子は飛ばします。
    /// 1 つの場面が例外で壊れても、体験は止めずに次の場面へ進みます。
    /// </para>
    /// </remarks>
    public sealed class TutorialHandler : MonoBehaviour
    {
        [Tooltip("今の場面を飛ばす操作です。左の X の 1 秒長押し（シナリオブックの決まり）と、開発用の N キーです。空のままなら、この 2 つを自動で割り当てます。")]
        [SerializeField] InputAction skip = new InputAction("Skip", InputActionType.Button);

        [Tooltip("最初からやり直す操作です。左のメニューボタンの 2 秒長押しと、開発用の Backspace キーです。空のままなら、この 2 つを自動で割り当てます。")]
        [SerializeField] InputAction restart = new InputAction("Restart", InputActionType.Button);

        CancellationTokenSource _run;     // 体験全体です。やり直すと取り消します。
        CancellationTokenSource _chapter; // 今の場面です。飛ばすと取り消します。

        /// <summary>今の場面の待ちに使う取り消しの合図です。<see cref="Wait"/> が使います。</summary>
        internal static CancellationToken Token { get; private set; }

        /// <summary>場面をまたいで覚えておくことです。最初からやり直すと消えます。</summary>
        internal static HashSet<string> Memory { get; } = new HashSet<string>();

        /// <summary>今の場面の名前です。場面を流していないときは空文字です。</summary>
        public string Current { get; private set; } = "";

        void Awake()
        {
            if (skip.bindings.Count == 0)
            {
                skip.AddBinding("<XRController>{LeftHand}/primaryButton").WithInteraction("hold(duration=1)");
                skip.AddBinding("<Keyboard>/n");
            }
            if (restart.bindings.Count == 0)
            {
                restart.AddBinding("<XRController>{LeftHand}/menuButton").WithInteraction("hold(duration=2)");
                restart.AddBinding("<Keyboard>/backspace");
            }
            skip.performed += _ => Skip();
            restart.performed += _ => Restart();
        }

        void OnEnable()
        {
            skip.Enable();
            restart.Enable();
        }

        void OnDisable()
        {
            skip.Disable();
            restart.Disable();
            _run?.Cancel();
        }

        void Start() => Restart();

        /// <summary>今の場面を飛ばして、次の場面へ進みます。</summary>
        public void Skip() => _chapter?.Cancel();

        /// <summary>フィールドと覚えたことを戻し、最初の場面からやり直します。</summary>
        /// <remarks>
        /// 流れている場面は取り消します。巫女の声と、残っていた食べ物も消します。
        /// Play を始めたときも、これで最初の場面を流します。
        /// </remarks>
        public void Restart()
        {
            _run?.Cancel();
            _run = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            Memory.Clear();
            foreach (Field field in Field.All) field.Restart();
            foreach (Chapter chapter in GetComponentsInChildren<Chapter>(true)) chapter.OnRestart();
            Miko.Hush();
            Foods.Clear();
            _ = RunAll(_run.Token);
        }

        // 子の場面を上から順に流します。run が取り消されたら、途中で抜けます。
        async Awaitable RunAll(CancellationToken run)
        {
            // ほかの部品の Start が済むよう、1 フレーム待ちます。
            try { await Awaitable.NextFrameAsync(run); }
            catch (OperationCanceledException) { return; }
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeInHierarchy || !child.TryGetComponent(out Chapter chapter)) continue;
                if (run.IsCancellationRequested) return;

                var chapterRun = CancellationTokenSource.CreateLinkedTokenSource(run);
                _chapter = chapterRun;
                Token = chapterRun.Token;
                Current = chapter.name;
                Debug.Log("[Flow] 場面: " + Current);
                try { await chapter.Play(); }
                catch (OperationCanceledException) when (!run.IsCancellationRequested) { Debug.Log("[Flow] 飛ばしました: " + Current); }
                catch (OperationCanceledException) { return; } // やり直しです。新しい RunAll が始まっています。
                catch (Exception e) { Debug.LogException(e); } // 1 つの場面が壊れても、体験は止めずに次へ進みます。
                finally
                {
                    // やり直したあとは、新しい RunAll の場面を消さないように、自分の分だけ片付けます。
                    if (_chapter == chapterRun)
                    {
                        Miko.Hush();
                        _chapter = null;
                    }
                    chapterRun.Dispose();
                }
            }
            Current = "";
            Debug.Log("[Flow] すべての場面が終わりました。");
        }
    }
}
