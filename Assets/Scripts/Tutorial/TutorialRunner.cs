using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// 子の <see cref="TutorialScript"/>（人形劇スクリプト）を、Hierarchy の並び順に 1 つずつ動かす。どのチュートリアルでも同じコード。
    /// 動かしている間は、<see cref="Phases"/> を各スクリプトの場面にする。
    /// </summary>
    /// <remarks>
    /// - 動かすのは <see cref="Begin"/> を呼んだときから。いつ呼ぶか（台の前に立ったとき、など）は、呼ぶ側が決める。
    /// - 無効なスクリプト（enabled を外した、または非アクティブ）は飛ばす。特定のチュートリアルだけを試すときに使う。
    /// - スクリプトで例外が出たら、そのスクリプトだけをやめて、次へ進む。
    /// - 全部終わったら <see cref="Finished"/> を出し、場面を空に戻す。次の場面（本編など）は、<see cref="Finished"/> を受けた側が決める。
    /// - 覚えたこと（<see cref="Memory"/>）は、<see cref="Begin"/> で空にする。
    /// </remarks>
    public sealed class TutorialRunner : MonoBehaviour
    {
        // 警告と例外の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;
        internal static Action<Exception> ReportException = Debug.LogException;

        [Tooltip("入れると、Play を始めたときに自動で Begin する。入れないときは、呼ぶ側（台の前に立った、など）が Begin を呼ぶ")]
        [SerializeField] bool beginOnStart;

        [Tooltip("Begin On Start のとき、始めるまで待つ秒数")]
        [SerializeField, Min(0f)] float beginDelaySeconds;

        Coroutine _running;
        TutorialScript _current;

        /// <summary>チュートリアルのあいだで引き継ぐ、覚えたこと。</summary>
        public TutorialMemory Memory { get; } = new TutorialMemory();

        /// <summary>動いているか。</summary>
        public bool IsRunning => _running != null || _current != null;

        /// <summary>スクリプトを動かし始めた（その場面の名前を運ぶ）。</summary>
        public event Action<PhaseId> ScriptStarted;

        /// <summary>スクリプトが終わった（例外でやめた場合も含む）。</summary>
        public event Action<PhaseId> ScriptFinished;

        /// <summary>全部のスクリプトが終わった。<see cref="Stop"/> でやめたときは出ない。</summary>
        public event Action Finished;

        /// <summary>最初から動かす。すでに動いていれば、警告して false。</summary>
        public bool Begin()
        {
            if (IsRunning)
            {
                ReportWarning("[TutorialRunner] もう動いているので、Begin は無視します。");
                return false;
            }
            Memory.Clear();
            _running = StartCoroutine(Run());
            return true;
        }

        /// <summary>途中でやめる。待ちを外し、場面を空に戻す。動いていなければ何もしない。</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            if (_running != null) StopCoroutine(_running);
            _running = null;
            if (_current != null) _current.CancelWaits();
            _current = null;
            Phases.Set(PhaseId.None);
        }

        // 全体の流れ。テストからは、手で MoveNext して進める。
        internal IEnumerator Run()
        {
            foreach (TutorialScript script in CollectScripts())
            {
                _current = script;
                script.Attach(Memory);
                if (script.Phase.IsEmpty) ReportWarning("[TutorialRunner] " + script + " の Phase が空です。");
                Phases.Set(script.Phase);
                Raise(ScriptStarted, script.Phase);

                IEnumerator body = Safe(script);
                while (body.MoveNext()) yield return body.Current;

                script.CancelWaits();
                _current = null;
                Raise(ScriptFinished, script.Phase);
            }

            _running = null;
            Phases.Set(PhaseId.None);
            Raise(Finished);
        }

        // 子のスクリプトを、Hierarchy の並び順に集める。無効なものは飛ばす。
        internal TutorialScript[] CollectScripts()
        {
            var scripts = new List<TutorialScript>();
            foreach (TutorialScript script in GetComponentsInChildren<TutorialScript>())
                if (script.enabled) scripts.Add(script);
            return scripts.ToArray();
        }

        // 例外が出たら、そのスクリプトだけをやめる（体験を止めない）。yield は try の外で行う。
        internal static IEnumerator Safe(TutorialScript script)
        {
            IEnumerator inner;
            try { inner = script.Play(); }
            catch (Exception e)
            {
                ReportException(e);
                yield break;
            }

            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception e)
                {
                    ReportException(e);
                    yield break;
                }
                yield return current;
            }
        }

        static void Raise<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception e) { ReportException(e); }
            }
        }

        static void Raise(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { ReportException(e); }
            }
        }

        // 開発中の確かめ用（右クリックメニュー）。ふだんは呼ぶ側が Begin・Stop を呼ぶ。
        [ContextMenu("Begin")]
        void BeginFromMenu() => Begin();

        [ContextMenu("Stop")]
        void StopFromMenu() => Stop();

        IEnumerator Start()
        {
            if (!beginOnStart) yield break;
            if (beginDelaySeconds > 0f) yield return new WaitForSeconds(beginDelaySeconds);
            if (!IsRunning) Begin();
        }

        void OnDisable() => Stop();
    }
}
