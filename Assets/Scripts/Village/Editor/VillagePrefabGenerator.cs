using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MouthOfGod.Village.Editor
{
    /// <summary>
    /// VillageLayout が使う Prefab を、素材（FBX）から Assets/Resources/Prefabs/Village/ に作る。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。既にある Prefab は上書きしない。
    /// 素材の置き場所は docs/quaternius-assets.md。
    /// </summary>
    public static class VillagePrefabGenerator
    {
        const string Quaternius = "Assets/ArtisticResources/ThirdParty/Quaternius/";
        const string OutputFolder = "Assets/Resources/Prefabs/Village/";

        // Prefab 名（VillageLayout で使う名前）→ FBX の場所。House_Sample は既にある Prefab を使うので載せない。
        static readonly Dictionary<string, string> Sources = new Dictionary<string, string>
        {
            { "Well", Quaternius + "Farm Buildings - Sept 2018/FBX/Well.fbx" },
            { "Windmill", Quaternius + "Farm Buildings - Sept 2018/FBX/Windmill.fbx" },
            { "Barn", Quaternius + "Farm Buildings - Sept 2018/FBX/Barn.fbx" },
            { "Fence", Quaternius + "Farm Buildings - Sept 2018/FBX/Fence.fbx" },
            { "Casual_Male", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Casual_Male.fbx" },
            { "Carrot", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Carrot.fbx" },
            { "Turnip", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Turnip.fbx" },
            { "Pumpkin", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Pumpkin.fbx" },
        };

        const string VillagerName = "Casual_Male";
        const string ControllerPath = OutputFolder + "Villager.controller";

        // 村人のアニメーション（TPO: 飢饉の村で、食べ物を待つ → 届いたら喜ぶ）。
        //   足りない状態: Idle（ループ）。届いた後: Victory を 1 回 → Idle に戻る。
        //   使わない: Walk/Run（どこかへ行ってしまう）、Defeat/Death（暗すぎる）、Punch/SwordSlash/Shoot/Roll/RecieveHit（戦闘）。
        //   Walk_Carry / PickUp は、届け先まで運ぶ・収穫する場面で使えるので、そのときに足す。
        static void AttachVillagerAnimator(GameObject instance, string fbxPath)
        {
            SetLoop(fbxPath, "Idle", true);
            SetLoop(fbxPath, "Victory", false);

            var idle = LoadClip(fbxPath, "Idle");
            var victory = LoadClip(fbxPath, "Victory");
            if (idle == null || victory == null)
            {
                Debug.LogWarning($"[Village] {VillagerName} に Idle / Victory のクリップが無い。アニメーションは付けない");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller.layers[0].stateMachine.states.Length == 0)
            {
                controller.AddParameter("Delivered", AnimatorControllerParameterType.Trigger);
                var machine = controller.layers[0].stateMachine;
                var idleState = machine.AddState("Idle");
                idleState.motion = idle;
                var victoryState = machine.AddState("Victory");
                victoryState.motion = victory;
                machine.defaultState = idleState;

                var toVictory = idleState.AddTransition(victoryState);
                toVictory.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Delivered");
                toVictory.hasExitTime = false;
                toVictory.duration = 0.2f;

                var back = victoryState.AddTransition(idleState);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0.2f;
                EditorUtility.SetDirty(controller);
            }

            var animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
        }

        // FBX の中のクリップは "CharacterArmature|Idle" の名前。
        static AnimationClip LoadClip(string fbxPath, string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name.EndsWith("|" + name))
                    return clip;
            return null;
        }

        static void SetLoop(string fbxPath, string name, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            var changed = false;
            foreach (var clip in clips)
                if (clip.name.EndsWith("|" + name) && clip.loopTime != loop) { clip.loopTime = loop; changed = true; }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        [MenuItem("MouthOfGod/Village/Generate Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            int created = 0, skipped = 0;
            foreach (var pair in Sources)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                if (File.Exists(path)) { skipped++; continue; }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                if (model == null)
                {
                    Debug.LogWarning($"[Village] 素材が見つからない: {pair.Value}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (pair.Key == VillagerName) AttachVillagerAnimator(instance, pair.Value);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
                created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] Prefab を {created} 件作った（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }
    }
}
