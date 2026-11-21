using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 体験の進行役です。子に並べた場面（Chapter）を、上から順に流します。シーンに 1 つ置きます。
    /// スタッフは、今の場面を飛ばすことと、最初からやり直すことができます。
    /// </summary>
    public sealed class TutorialHandler : MonoBehaviour
    {
        [Tooltip("今の場面を飛ばす操作です。左の X の長押し（シナリオブックの決まり）と、開発用の N キーです")]
        [SerializeField] InputAction skip = new InputAction("Skip", InputActionType.Button);

        [Tooltip("最初からやり直す操作です。左のメニューボタンの長押しと、開発用の Backspace キーです")]
        [SerializeField] InputAction restart = new InputAction("Restart", InputActionType.Button);

        CancellationTokenSource _run;     // 体験全体。やり直すと取り消します
        CancellationTokenSource _chapter; // 今の場面。飛ばすと取り消します

        /// <summary>今の場面の待ちに使う取り消しの合図です。Wait が使います。</summary>
        internal static CancellationToken Token { get; private set; }

        /// <summary>場面をまたいで覚えておくことです。</summary>
        internal static HashSet<string> Memory { get; } = new HashSet<string>();

        /// <summary>今の場面の名前です。</summary>
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

        async Awaitable RunAll(CancellationToken run)
        {
            try { await Awaitable.NextFrameAsync(run); } // ほかの部品の準備（Start）を 1 フレーム待ちます
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
                catch (OperationCanceledException) { return; } // やり直し。新しい RunAll が始まっています
                catch (Exception e) { Debug.LogException(e); } // 1 つの場面が壊れても、体験は止めずに次へ進みます
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
