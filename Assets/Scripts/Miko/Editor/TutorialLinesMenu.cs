using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Miko.Editor
{
    /// <summary>
    /// 一時的なメニュー。プロローグの終わりと、チュートリアル 1〜4 のセリフ（英語）を、選んだ Miko の LineBook に足す。
    /// 同じ id がもうあれば、足さない（人が直した文は上書きしない）。
    /// シーンの LineBook に入れ終えたら、このファイルは消す（docs/architecture.md「データの作り方」）。
    /// </summary>
    static class TutorialLinesMenu
    {
        // id と英文。台本は docs/scenario-book.md の S1・S3〜S6。台本にない文は仮。
        static readonly string[,] Lines =
        {
            { "prologue-stand-up", "Please stand up and follow the booth staff." },
            { "t1-pierce", "Take the fork in your hand. Pierce that glowing cloud." },
            { "t1-mouth", "Now bring it to your mouth." },
            { "t1-mouth-again", "Bring it to your mouth." },
            { "t1-changing", "Keep it in your mouth. Feel it? The texture keeps changing." },
            { "t1-button", "When it feels right, press the button." },
            { "t1-good", "Good." },
            { "t2-remember", "Remember the apple the villagers offered? Pierce a cloud and put it in your mouth." },
            { "t2-remember-short", "Remember the apple the villagers offered? Try it again." },
            { "t2-hint-pierce", "Pierce a cloud with your fork." },
            { "t2-hint-mouth", "Bring it to your mouth." },
            { "t2-wait-apple", "As the feel changes, so does the blessing. Wait for the apple." },
            { "t2-wrong", "That too is a blessing — but now, the apple." },
            { "t2-born", "Look! A blessing is born from your mouth! Remember that feel." },
            { "t3-troubled", "Oh, the people by the sea are troubled. They want fish." },
            { "t3-which-feel", "Pierce a new cloud. Which feel was the fish?" },
            { "t3-wrong", "Not that one. Which feel was the fish?" },
            { "t3-fish-here", "That's the fish — but blessings fall where you stand. Fish belong in the sea." },
            { "t4-walk", "Walk to the sea, and create the fish there." },
            { "t4-here", "Now, create it here." },
            { "t4-not-here", "Not here — go to the place that glows." },
            { "t4-splendid", "Splendid!" },
            { "t4-true-god", "Find what each place needs, go there, and create it. Now you are a true god." },
        };

        [MenuItem("MouthOfGod/Miko/Add Tutorial Lines To Selected LineBook")]
        static void AddLines()
        {
            int books = 0, added = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                foreach (MikoLineBook book in selected.GetComponentsInChildren<MikoLineBook>(true))
                {
                    books++;
                    added += AddTo(book);
                }
            }

            if (books == 0)
                Debug.LogWarning("[TutorialLinesMenu] シーンの Miko（または LineBook）を選んでから実行してください。");
            else
                Debug.Log($"[TutorialLinesMenu] {books} 個の LineBook に、{added} 行を足しました。シーンを保存してください。");
        }

        static int AddTo(MikoLineBook book)
        {
            var so = new SerializedObject(book);
            SerializedProperty list = so.FindProperty("lines");
            int added = 0;
            for (int i = 0; i < Lines.GetLength(0); i++)
            {
                string id = Lines[i, 0];
                if (book.Find(id) != null) continue;
                list.arraySize++;
                SerializedProperty line = list.GetArrayElementAtIndex(list.arraySize - 1);
                line.FindPropertyRelative("id").stringValue = id;
                line.FindPropertyRelative("text").stringValue = Lines[i, 1];
                line.FindPropertyRelative("voice").objectReferenceValue = null;
                line.FindPropertyRelative("changeExpression").boolValue = false;
                line.FindPropertyRelative("point").boolValue = false;
                line.FindPropertyRelative("pointTarget").objectReferenceValue = null;
                line.FindPropertyRelative("moveTo").objectReferenceValue = null;
                line.FindPropertyRelative("speakAfterMove").boolValue = false;
                line.FindPropertyRelative("seconds").floatValue = 0f;
                added++;
            }
            so.ApplyModifiedProperties(); // Undo に入り、シーンが変更ありになる
            return added;
        }
    }
}
