using System.Collections.Generic;
using System.Text;
using MouthOfGod.Core;
using MouthOfGod.Tutorial;
using UnityEngine;

namespace MouthOfGod.DevTools
{
    /// <summary>
    /// Quest なしで試すときの表示。Game ビューの左上に、いまの場面・立っている場所・食感・フォーク・欲しい物・覚えたこと・最近の合図を出す。
    /// ボタンで、チュートリアルを始める・止める、立つ場所へ移る、ができる。
    /// </summary>
    /// <remarks>
    /// 読むのは Core の置き場と、<see cref="TutorialRunner"/> だけ。展示では使わない（Quest の中には出ない）。
    /// </remarks>
    public sealed class TutorialDebugHud : MonoBehaviour
    {
        [Tooltip("始める・止めるボタンと、覚えたことの表示に使う")]
        [SerializeField] TutorialRunner runner;

        [Tooltip("移るボタンで動かす頭。空なら、Main Camera の DesktopHead")]
        [SerializeField] DesktopHead head;

        [Tooltip("移るボタンの行き先。ふつうは各フィールドの StandArea")]
        [SerializeField] Transform[] teleportTargets = new Transform[0];

        [Tooltip("表示する、覚えたことの名前（docs/progress-keys.md）")]
        [SerializeField] string[] memoryKeys =
        {
            "fork-learned", "food-born", "texture-shadow-seen", "request-seen", "delivered-by-standing",
        };

        [Tooltip("最近の合図を、いくつまで出すか")]
        [SerializeField, Min(1)] int cueLines = 12;

        [Tooltip("表示の幅（ピクセル）")]
        [SerializeField, Min(200f)] float width = 420f;

        readonly Queue<string> _cues = new Queue<string>();
        readonly StringBuilder _text = new StringBuilder();
        GUIStyle _style;

        void OnEnable() => Cues.Requested += OnCue;

        void OnDisable() => Cues.Requested -= OnCue;

        void OnCue(CueRequested request)
        {
            _cues.Enqueue($"{Time.time,6:0.0}  {request.Cue.Value}");
            while (_cues.Count > cueLines) _cues.Dequeue();
        }

        void OnGUI()
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true };

            GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height - 20f));
            GUILayout.Box(Describe(), _style);

            GUILayout.BeginHorizontal();
            if (runner != null)
            {
                if (GUILayout.Button(runner.IsRunning ? "Restart" : "Begin"))
                {
                    runner.Stop();
                    runner.Begin();
                }
                if (GUILayout.Button("Stop")) runner.Stop();
                if (GUILayout.Button("Skip")) runner.Skip();
            }
            GUILayout.EndHorizontal();

            DesktopHead target = head != null ? head : (Camera.main != null ? Camera.main.GetComponent<DesktopHead>() : null);
            if (target != null)
            {
                GUILayout.BeginHorizontal();
                foreach (Transform t in teleportTargets)
                {
                    if (t == null) continue;
                    if (GUILayout.Button("Go " + t.name)) target.Teleport(t.position, t.eulerAngles.y);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        string Describe()
        {
            _text.Clear();
            _text.AppendLine($"<b>Phase</b>     {Show(Phases.Current.Value)}");
            _text.AppendLine($"<b>Standing</b>  {Show(Standing.Field.Value)}");

            IDevice device = Devices.Current;
            _text.AppendLine($"<b>Texture</b>   {(device == null ? "(no device)" : Show(device.Current.Value))}");

            IFork fork = Forks.Current;
            _text.AppendLine(fork == null
                ? "<b>Fork</b>      (no fork)"
                : $"<b>Fork</b>      {(fork.IsLoaded ? "loaded" : "empty")}{(fork.InMouth ? ", in mouth" : "")}");

            _text.Append("<b>Requests</b> ");
            foreach (IFieldRequests field in Fields.All)
                _text.Append($" {field.Field.Value}{(field.IsOpen ? "(open)" : "")}");
            _text.AppendLine();

            if (runner != null)
            {
                _text.Append("<b>Memory</b>  ");
                foreach (string key in memoryKeys)
                    if (runner.Memory.Has(key)) _text.Append(' ').Append(key);
                _text.AppendLine();
            }

            _text.AppendLine();
            _text.AppendLine("WASD walk / Shift run / right-drag look");
            _text.AppendLine("P pierce / M (hold) mouth / Space (hold) bite / Enter confirm");
            _text.AppendLine();
            _text.AppendLine("<b>Recent cues</b>");
            foreach (string cue in _cues) _text.AppendLine(cue);
            return _text.ToString();
        }

        static string Show(string value) => string.IsNullOrEmpty(value) ? "-" : value;
    }
}
