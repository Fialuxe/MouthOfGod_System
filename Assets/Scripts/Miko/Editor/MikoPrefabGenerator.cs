using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MouthOfGod.Miko.Editor
{
    /// <summary>
    /// 巫女の Prefab と、身振りのアニメーション（浮いて待つ・指さす）を、Assets/Resources/Prefabs/Miko/ に作る。
    /// 素材は Assets/ArtisticResources/ThirdParty/Miko-san.fbx（Generic）。
    /// 骨の名前と、取り込んだ骨の位置から、向きと左右を調べて作る。軸の値は決め打ちしない。
    /// 何度実行してもよい。Prefab とクリップは同じ場所に上書きするので、GUID は変わらない。
    /// </summary>
    public static class MikoPrefabGenerator
    {
        const string ModelPath = "Assets/ArtisticResources/ThirdParty/Miko-san.fbx";
        const string OutputFolder = "Assets/Resources/Prefabs/Miko/";
        const float Height = 0.3f; // 巫女の背たけ（m）。村（0.54 m 四方）の横に浮かべて見やすい大きさ
        const float FloatSeconds = 2.5f;
        const float PointSeconds = 2f;
        const float SampleStep = 1f / 30f;

        [MenuItem("MouthOfGod/Miko/Generate Prefab")]
        public static void Generate()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[Miko] 素材が見つからない: {ModelPath}");
                return;
            }
            Directory.CreateDirectory(OutputFolder);

            var root = new GameObject("Miko");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);

            var rig = Rig.Find(instance.transform);
            if (rig == null)
            {
                Object.DestroyImmediate(root);
                return;
            }

            var floatClip = SaveClip("Miko_Float", BuildClip(rig, FloatSeconds, t => Pose(rig, t / FloatSeconds, 0f)), loop: true);
            var pointClip = SaveClip("Miko_Point", BuildClip(rig, PointSeconds, t => Pose(rig, 0f, PointWeight(t))), loop: false);
            var controller = BuildController(floatClip, pointClip);

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is Avatar avatar) animator.avatar = avatar;

            var eyes = AddEyes(instance, rig);

            // 骨の位置から調べた「前」を巫女の前（+Z）に合わせ、背たけを合わせ、体の真ん中を原点に置く。
            var bounds = BoundsOf(instance);
            var scale = bounds.size.y > 0f ? Height / bounds.size.y : 1f;
            instance.transform.localRotation = Quaternion.FromToRotation(rig.Forward, Vector3.forward);
            instance.transform.localScale = Vector3.one * scale;
            var scaled = BoundsOf(instance);
            instance.transform.localPosition = -scaled.center;

            root.AddComponent<MikoFloat>();
            root.AddComponent<MikoCueReceiver>();
            if (eyes.HasValue)
            {
                foreach (var eye in new[] { eyes.Value.ViewerLeft, eyes.Value.ViewerRight })
                {
                    var t = eye.transform;
                    Debug.Log($"[Miko] {eye.name}（最終）: 世界の大きさ {t.lossyScale.x:F4}、前との内積 {Vector3.Dot(t.forward, root.transform.TransformDirection(Vector3.forward)):F2}（-1 なら前を向く）、範囲の大きさ {eye.bounds.size.x:F4}、頭の拡大 {t.parent.lossyScale}");
                }
                var face = root.AddComponent<MikoFace>();
                var so = new SerializedObject(face);
                so.FindProperty("viewerLeftEye").objectReferenceValue = eyes.Value.ViewerLeft;
                so.FindProperty("viewerRightEye").objectReferenceValue = eyes.Value.ViewerRight;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, OutputFolder + "Miko.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Miko] Prefab を作った → {OutputFolder}Miko.prefab（調べた前: {rig.Forward}、右腕の側: {rig.Right}）");
        }

        // 指さしの強さ（0〜1）。0.35 秒で上げ、1.4 秒まで保ち、2 秒までに戻す。
        static float PointWeight(float t)
        {
            var up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f));
            var down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.4f) / (PointSeconds - 1.4f)));
            return up * (1f - down);
        }

        // 骨。位置・向きは、取り込んだままの姿（バインドポーズ）。
        sealed class Rig
        {
            public Transform Model;
            public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public Vector3 Forward;
            public Vector3 Right; // 巫女の右腕（.R）がある側
            public Vector3 Up = Vector3.up;

            public Transform this[string name] => Bones[name];

            static readonly string[] Names =
            {
                "Head", "Head_end",
                "UpperArm.L", "LowerArm.L", "Fist.L", "UpperArm.R", "LowerArm.R", "Fist.R",
                "UpperLeg.L", "LowerLeg.L", "Foot.L", "Foot.L_end", "UpperLeg.R", "LowerLeg.R", "Foot.R", "Foot.R_end",
            };

            public static Rig Find(Transform model)
            {
                var rig = new Rig { Model = model };
                foreach (var name in Names)
                {
                    var bone = FindDeep(model, name);
                    if (bone == null)
                    {
                        Debug.LogWarning($"[Miko] 骨が見つからない: {name}");
                        return null;
                    }
                    rig.Bones[name] = bone;
                }

                // 前: 足首から爪先へ向かう向き（水平）。右: 前と上から決まる右。
                var toes = (rig["Foot.L_end"].position - rig["Foot.L"].position) + (rig["Foot.R_end"].position - rig["Foot.R"].position);
                toes.y = 0f;
                rig.Forward = toes.sqrMagnitude > 1e-8f ? toes.normalized : model.forward;
                rig.Right = Vector3.Cross(Vector3.up, rig.Forward);
                // .R の肩が右の側にない（左右が逆）なら、右を入れ替える。
                var side = Vector3.Dot(rig["UpperArm.R"].position - rig["UpperArm.L"].position, rig.Right);
                if (side < 0f) rig.Right = -rig.Right;
                return rig;
            }

            static Transform FindDeep(Transform t, string name)
            {
                if (t.name == name) return t;
                foreach (Transform child in t)
                {
                    var found = FindDeep(child, name);
                    if (found != null) return found;
                }
                return null;
            }
        }

        // 順運動学。バインドポーズから、動かした骨だけ向きを差し替えて、骨ごとの今の向き・位置を求める。
        sealed class Posing
        {
            readonly Dictionary<Transform, Quaternion> _over = new Dictionary<Transform, Quaternion>();

            public IEnumerable<KeyValuePair<Transform, Quaternion>> Posed => _over;

            public Quaternion WorldRotation(Transform bone)
            {
                if (_over.TryGetValue(bone, out var q)) return q;
                if (bone.parent == null) return bone.rotation;
                return WorldRotation(bone.parent) * bone.localRotation;
            }

            public Vector3 WorldPosition(Transform bone)
            {
                if (bone.parent == null) return bone.position;
                var parent = bone.parent;
                var delta = WorldRotation(parent) * Quaternion.Inverse(parent.rotation);
                return WorldPosition(parent) + delta * (bone.position - parent.position);
            }

            // bone から child へ向かう線を、世界の direction に向ける。親から順に呼ぶ。
            public void Aim(Transform bone, Transform child, Vector3 direction)
            {
                var current = (WorldPosition(child) - WorldPosition(bone)).normalized;
                _over[bone] = Quaternion.FromToRotation(current, direction.normalized) * WorldRotation(bone);
            }

            public Quaternion LocalRotation(Transform bone) => Quaternion.Inverse(WorldRotation(bone.parent)) * _over[bone];
        }

        // 浮いて待つ姿（phase は 0〜1 の 1 周期）と、指さし（point は 0〜1）。phase 0・point 0 が基本の姿。
        static Posing Pose(Rig rig, float phase, float point)
        {
            var p = new Posing();
            var sway = Mathf.Sin(phase * Mathf.PI * 2f);
            var swing = Mathf.Cos(phase * Mathf.PI * 2f) - 1f; // phase 0 で 0
            var f = rig.Forward;
            var r = rig.Right;
            var up = rig.Up;

            // 頭: ほんの少し前へ傾け、ゆっくりうなずく。
            p.Aim(rig["Head"], rig["Head_end"], up + f * (0.05f + 0.04f * sway));

            // 脚: 浮いているので、ひざから下が後ろへ流れる。ゆれは左右で少しずらす。
            p.Aim(rig["UpperLeg.L"], rig["LowerLeg.L"], -up - f * (0.15f - 0.05f * swing) - r * 0.05f);
            p.Aim(rig["LowerLeg.L"], rig["Foot.L"], -up - f * (0.7f - 0.1f * swing));
            p.Aim(rig["UpperLeg.R"], rig["LowerLeg.R"], -up - f * (0.15f - 0.05f * swing) + r * 0.05f);
            p.Aim(rig["LowerLeg.R"], rig["Foot.R"], -up - f * (0.7f - 0.1f * swing));

            // 左腕: 体の横に下げ、ひじを軽く前に曲げる。
            p.Aim(rig["UpperArm.L"], rig["LowerArm.L"], -up - r * (0.35f + 0.04f * sway) + f * 0.1f);
            p.Aim(rig["LowerArm.L"], rig["Fist.L"], -up - r * 0.2f + f * 0.45f);

            // 右腕: 左と同じ構えから、指さしで前（やや上）へ伸ばす。
            var armRest = (-up + r * (0.35f + 0.04f * sway) + f * 0.1f).normalized;
            var foreRest = (-up + r * 0.2f + f * 0.45f).normalized;
            var armPoint = (f + up * 0.25f + r * 0.1f).normalized;
            p.Aim(rig["UpperArm.R"], rig["LowerArm.R"], Vector3.Slerp(armRest, armPoint, point));
            p.Aim(rig["LowerArm.R"], rig["Fist.R"], Vector3.Slerp(foreRest, armPoint, point));
            return p;
        }

        static AnimationClip BuildClip(Rig rig, float seconds, Func<float, Posing> pose)
        {
            var clip = new AnimationClip { frameRate = 30f };
            var curves = new Dictionary<Transform, AnimationCurve[]>();
            var count = Mathf.CeilToInt(seconds / SampleStep);
            for (var i = 0; i <= count; i++)
            {
                var t = Mathf.Min(i * SampleStep, seconds);
                var posing = pose(t);
                foreach (var pair in posing.Posed)
                {
                    if (!curves.TryGetValue(pair.Key, out var c))
                        curves[pair.Key] = c = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    var q = posing.LocalRotation(pair.Key);
                    c[0].AddKey(t, q.x);
                    c[1].AddKey(t, q.y);
                    c[2].AddKey(t, q.z);
                    c[3].AddKey(t, q.w);
                }
            }
            foreach (var pair in curves)
            {
                var path = AnimationUtility.CalculateTransformPath(pair.Key, rig.Model);
                clip.SetCurve(path, typeof(Transform), "localRotation.x", pair.Value[0]);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", pair.Value[1]);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", pair.Value[2]);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", pair.Value[3]);
            }
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        // 既にあれば同じ場所に上書きして、GUID を保つ。
        static AnimationClip SaveClip(string name, AnimationClip clip, bool loop)
        {
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var path = OutputFolder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            EditorUtility.CopySerialized(clip, existing);
            return existing;
        }

        static AnimatorController BuildController(AnimationClip floatClip, AnimationClip pointClip)
        {
            var path = OutputFolder + "Miko.controller";
            AssetDatabase.DeleteAsset(path); // 状態の作り直しが簡単なので、これだけは作り直す
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Point", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Float");
            idle.motion = floatClip;
            var point = machine.AddState("Point");
            point.motion = pointClip;
            machine.defaultState = idle;

            var toPoint = idle.AddTransition(point);
            toPoint.hasExitTime = false;
            toPoint.duration = 0.15f;
            toPoint.AddCondition(AnimatorConditionMode.If, 0f, "Point");

            var back = point.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.2f;
            return controller;
        }

        // 目: 材質 Face（元の目）を肌の色に差し替えて消し、同じ位置に、絵を映す板（Quad）を 2 枚置く。
        // 板は Head の子にするので、頭といっしょに動く。位置と大きさは、元の目の頂点から決める。
        static (Renderer ViewerLeft, Renderer ViewerRight)? AddEyes(GameObject instance, Rig rig)
        {
            SkinnedMeshRenderer body = null;
            int faceSlot = -1, skinSlot = -1;
            foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = smr.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].name.StartsWith("Face")) { body = smr; faceSlot = i; }
                    if (mats[i].name.StartsWith("skin")) skinSlot = i;
                }
                if (body != null) break;
            }
            if (body == null || skinSlot < 0 || faceSlot >= body.sharedMesh.subMeshCount)
            {
                Debug.LogWarning("[Miko] 目の材質（Face）か肌の材質（skin）が見つからないので、目は変えない");
                return null;
            }

            // 元の目の頂点（ワールド座標）。右（rig.Right）の側と左の側に分ける。
            var mesh = body.sharedMesh;
            // 頂点の世界座標。BakeMesh は骨の拡大とずれることがあるので、骨の重みと bindpose から自分で求める。
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            var bones = body.bones;
            var used = new HashSet<int>(mesh.GetTriangles(faceSlot));
            var points = new List<Vector3>();
            var indices = new List<int>();
            foreach (var i in used)
            {
                indices.Add(i);
                var w = weights[i];
                var sum = Vector3.zero;
                void Add(int index, float weight)
                {
                    if (weight <= 0f || bones[index] == null) return;
                    sum += (bones[index].localToWorldMatrix * bindposes[index]).MultiplyPoint3x4(vertices[i]) * weight;
                }
                Add(w.boneIndex0, w.weight0);
                Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2);
                Add(w.boneIndex3, w.weight3);
                points.Add(sum);
            }
            var mean = Vector3.zero;
            foreach (var point in points) mean += point;
            mean /= points.Count;
            var sideA = new List<int>(); // 巫女の右（+Right）。見ている人には左に見える
            var sideB = new List<int>();
            for (var k = 0; k < points.Count; k++) (Vector3.Dot(points[k] - mean, rig.Right) > 0f ? sideA : sideB).Add(k);
            if (sideA.Count == 0 || sideB.Count == 0)
            {
                Debug.LogWarning("[Miko] 目が左右に分けられないので、目は変えない");
                return null;
            }

            // 元の目（材質 Face の面）をメッシュから取り除く。後ろの肌の面は残る。
            var flat = Object.Instantiate(mesh);
            flat.name = "Miko_Body";
            flat.SetTriangles(new int[0], faceSlot);
            var planes = new float[2];
            var groups = new[] { sideA, sideB };
            for (var g = 0; g < 2; g++)
            {
                var rear = float.MaxValue;
                foreach (var k in groups[g]) rear = Mathf.Min(rear, Vector3.Dot(points[k], rig.Forward));
                planes[g] = rear;
            }
            // 左右で奥の面の深さが違うと、片目だけうまる。前にある側にそろえる。
            Debug.Log($"[Miko] 目の奥の面の深さ: 左 {planes[0]:F5}、右 {planes[1]:F5}");
            planes[0] = planes[1] = Mathf.Max(planes[0], planes[1]);
            flat.RecalculateBounds();
            // 2 回目以降は、同じ Mesh の中身だけを入れ替える。作り直すと、参照が切れて体が消えることがある。
            var meshPath = OutputFolder + "Miko_Body.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(flat, meshPath);
                saved = flat;
            }
            else
            {
                EditorUtility.CopySerialized(flat, saved);
                Object.DestroyImmediate(flat);
            }
            EditorUtility.SetDirty(saved);
            body.sharedMesh = saved;

            var texture = MikoEyeAtlas.Save(OutputFolder + "Miko_Eyes.png");
            var material = LoadOrCreateEyeMaterial(texture);

            var materials = body.sharedMaterials;
            materials[faceSlot] = materials[skinSlot];
            body.sharedMaterials = materials;

            var left = CreateEye("Eye_ViewerLeft", rig, Pick(points, sideA), planes[0], material);
            var right = CreateEye("Eye_ViewerRight", rig, Pick(points, sideB), planes[1], material);
            // 手で合わせた位置（Head から見た位置）に置く。計算で出した位置は、ここで上書きする。
            left.transform.localPosition = ViewerLeftEyeLocal;
            right.transform.localPosition = ViewerRightEyeLocal;
            return (left, right);
        }

        static readonly Vector3 ViewerLeftEyeLocal = new Vector3(0.00212f, 0.00207f, 0.00516f);
        static readonly Vector3 ViewerRightEyeLocal = new Vector3(-0.00212f, 0.002f, 0.005f);
        const float EyeLift = 0.8f; // 大きくするほど、目が顔の手前に出る

        static List<Vector3> Pick(List<Vector3> points, List<int> ks)
        {
            var list = new List<Vector3>();
            foreach (var k in ks) list.Add(points[k]);
            return list;
        }

        static Renderer CreateEye(string name, Rig rig, List<Vector3> points, float rearPlane, Material material)
        {
            var center = Vector3.zero;
            foreach (var point in points) center += point;
            center /= points.Count;
            float front = float.MinValue, minUp = float.MaxValue, maxUp = float.MinValue;
            foreach (var point in points)
            {
                front = Mathf.Max(front, Vector3.Dot(point - center, rig.Forward));
                var up = Vector3.Dot(point - center, rig.Up);
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
            }
            var eyeHeight = maxUp - minUp;
            var ratio = 1.5f;
            var size = eyeHeight * ratio; // 絵のコマは正方形。絵は、コマの中に 7 割ほどでおさまる

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(rig["Head"], false);
            // 目の奥の面から、目の高さの EyeLift 倍だけ前に置く。顔にうまらないように、引っ張り出す。
            var onPlane = center - rig.Forward * Vector3.Dot(center, rig.Forward) + rig.Forward * rearPlane;
            quad.transform.position = onPlane + rig.Forward * (eyeHeight * EyeLift);
            quad.transform.rotation = Quaternion.LookRotation(-rig.Forward, rig.Up); // 板の表（法線）が、前を向く
            quad.transform.localScale = Vector3.one; // 親の拡大を、あとで打ち消す
            var parentScale = quad.transform.lossyScale.x;
            quad.transform.localScale = Vector3.one * (size / parentScale);
            Debug.Log($"[Miko] {name}: 位置 {quad.transform.position}、大きさ {size}、元の目の高さ {eyeHeight}、頂点 {points.Count} 個");

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // URP の Unlit。半透明（アルファ合成）で、深度は書かない。
        static Material LoadOrCreateEyeMaterial(Texture2D texture)
        {
            var path = OutputFolder + "Miko_Eye.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                // 前に作った材質にも、両面描画を効かせる。
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(material);
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[Miko] URP の Unlit が見つからないので、Sprites/Default で作る");
                shader = Shader.Find("Sprites/Default");
            }
            material = new Material(shader);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f); // Alpha
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
            }
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f); // 両面。骨の拡大が反転していても消えないように
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
