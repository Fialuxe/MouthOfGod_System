using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MouthOfGod.Village.Editor
{
    /// <summary>
    /// 村の演出（煙・喜びの粒）の Prefab を、Assets/Resources/Prefabs/Village/ に作る。
    /// 粒の絵は Kenney の Particle Pack（CC0）。素材の場所は Assets/ArtisticResources/ThirdParty/Kenney/。
    /// 既にある Prefab は上書きしない。作り直すときは、消してから実行する。
    /// 「Generate Prefabs」（VillagePrefabGenerator）からも呼ばれる。
    /// Quest で重くならないよう、粒の数を少なくし、衝突・軌跡・ライトは使わない。
    /// </summary>
    public static class VillageFxGenerator
    {
        public const string SmokeName = "Fx_Smoke";
        public const string JoyName = "Fx_Joy";

        const string OutputFolder = "Assets/Resources/Prefabs/Village/";
        const string Particles = "Assets/ArtisticResources/ThirdParty/Kenney/Particles/";

        [MenuItem("MouthOfGod/Village/Generate Effects")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            var created = 0;
            created += Save(SmokeName, () => CreateSmoke());
            created += Save(JoyName, () => CreateJoy());
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] 演出の Prefab を {created} 件作った → {OutputFolder}");
        }

        static int Save(string name, System.Func<GameObject> create)
        {
            var path = OutputFolder + name + ".prefab";
            if (File.Exists(path)) return 0;
            var instance = create();
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return 1;
        }

        // 煙突の煙: ゆっくり上って広がり、薄くなって消える。出し続ける。村の大きさ（0.54 m 四方）に合わせて、粒は数 cm。
        static GameObject CreateSmoke()
        {
            var go = new GameObject(SmokeName);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 3f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.004f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeInOut(Color.white, 0.2f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            SetRenderer(go, LoadOrCreateParticleMaterial("Fx_Smoke", "smoke_07.png"));
            return go;
        }

        // 喜びの粒: 村人の頭の上から、星がふわっと上がって消える。1 回だけ出して、終わると Prefab ごと消える。
        static GameObject CreateJoy()
        {
            var go = new GameObject(JoyName);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 1.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.35f, 1f), new Color(1f, 0.6f, 0.65f, 1f));
            main.gravityModifier = -0.02f; // ふわっと上がる
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeInOut(Color.white, 0.1f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            SetRenderer(go, LoadOrCreateParticleMaterial("Fx_Joy", "star_04.png"));
            return go;
        }

        // 出だしで急に現れず、終わりでふっと消える（アルファ: 0 → 1 → 0）。
        static ParticleSystem.MinMaxGradient FadeInOut(Color color, float fadeIn)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        static void SetRenderer(GameObject go, Material material)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // URP の Particles/Unlit。半透明（アルファ合成）で、深度は書かない。絵が無ければ警告して、絵なしで作る。
        static Material LoadOrCreateParticleMaterial(string name, string textureFile)
        {
            var path = OutputFolder + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[Village] URP の Particles/Unlit が見つからないので、Sprites/Default で作る");
                shader = Shader.Find("Sprites/Default");
            }
            var material = new Material(shader);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Particles + textureFile);
            if (texture == null) Debug.LogWarning($"[Village] 粒の絵が無い: {Particles}{textureFile}（Unity にインポートされてからもう一度実行する）");
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);   // Transparent
                material.SetFloat("_Blend", 0f);     // Alpha
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
            }
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
