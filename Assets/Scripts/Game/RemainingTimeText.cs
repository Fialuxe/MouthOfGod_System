using TMPro;
using UnityEngine;

namespace MouthOfGod.Game
{
    /// <summary>
    /// <see cref="GameRunner"/> の残り秒を、指定した TMP_Text に書く最小の表示部品。置き場所と見た目は、シーンを組む人が決める。
    /// </summary>
    public sealed class RemainingTimeText : MonoBehaviour
    {
        [Tooltip("残り秒を書く先")]
        [SerializeField] TMP_Text text;

        [Tooltip("残り秒を知らせる本編の進行役")]
        [SerializeField] GameRunner runner;

        void OnEnable()
        {
            if (runner == null) return;
            runner.RemainingChanged += Show;
            Show(Mathf.CeilToInt(runner.Remaining)); // すでに始まっているときの最初の値
        }

        void OnDisable()
        {
            if (runner != null) runner.RemainingChanged -= Show;
        }

        void Show(int seconds)
        {
            if (text != null) text.text = seconds.ToString();
        }
    }
}
