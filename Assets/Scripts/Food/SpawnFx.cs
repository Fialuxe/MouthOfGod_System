using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// 食べ物が口から生まれた瞬間に、食べ物の色の粒と、短いベル音を出します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FoodSpawner"/> と同じオブジェクトに付けます。
    /// 生んだ直後の <see cref="FoodSpawner.Spawned"/> を受けて、生まれた位置に出します。
    /// </para>
    /// <para>
    /// 音は、続けて生むほど 1 音ずつ高くなります（ペンタトニックの 8 音まで）。
    /// 上がっていく音で、体験者が「うまく続けられている」と気づけるようにするためです。
    /// 間が <c>Combo Window Seconds</c> より空くと、いちばん低い音に戻ります。
    /// 音は起動時にコードで作るので、音声ファイルは要りません。
    /// </para>
    /// <para>
    /// 届いたときの反応は <c>DeliveryFx</c>（Village）が出します。
    /// こちらの音は高く短く、粒は食べ物の色にして、「生まれた」と「届いた」を聞き分けられるようにしています。
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(FoodSpawner))]
    public sealed class SpawnFx : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float ChimeDurationSeconds = 0.45f;
        private const float BaseFrequencyHertz = 659.25f;
        private const float ParticleLifetimeSeconds = 3f;
        private const float ParticleTintStrength = 0.6f;

        // 長調ペンタトニックの音程です。半音の数で表します。
        private static readonly int[] ChimeSemitones = { 0, 2, 4, 7, 9, 12, 14, 16 };

        [SerializeField]
        [Tooltip("生まれた位置に出す粒です。一度だけ出て、終わると自分で消える Prefab にします（Stop Action を Destroy にします）。空なら粒を出しません。")]
        private GameObject _burstPrefab;

        [SerializeField]
        [Min(0f)]
        [Tooltip("粒の大きさの倍率です。口元で目立たせたいときは、1 より大きくします。")]
        private float _burstScale = 1.2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("ベル音の大きさです（0〜1）。0 なら音を出しません。")]
        private float _volume = 0.7f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("この秒数以内に続けて生むと、音が 1 音ずつ高くなります。")]
        private float _comboWindowSeconds = 6f;

        private FoodSpawner _spawner;
        private AudioClip[] _chimes;
        private int _comboIndex = -1;
        private float _lastSpawnTime = float.NegativeInfinity;

        private void Awake()
        {
            _spawner = GetComponent<FoodSpawner>();
            _chimes = new AudioClip[ChimeSemitones.Length];
            for (int i = 0; i < _chimes.Length; i++)
            {
                _chimes[i] = CreateBellChime(ChimeSemitones[i]);
            }
        }

        private void OnEnable()
        {
            _spawner.Spawned += FoodSpawner_Spawned;
        }

        private void OnDisable()
        {
            _spawner.Spawned -= FoodSpawner_Spawned;
        }

        private void OnDestroy()
        {
            if (_chimes == null)
            {
                return;
            }

            foreach (AudioClip chime in _chimes)
            {
                Destroy(chime);
            }
        }

        private void FoodSpawner_Spawned(Spawned spawned)
        {
            PlayChimeAt(spawned.Position);
            EmitBurstAt(spawned);
        }

        private void PlayChimeAt(Vector3 position)
        {
            _comboIndex = NextComboIndex(_comboIndex, Time.unscaledTime - _lastSpawnTime);
            _lastSpawnTime = Time.unscaledTime;
            if (_volume > 0f)
            {
                AudioSource.PlayClipAtPoint(_chimes[_comboIndex], position, _volume);
            }
        }

        // 間が空いたら 0 に戻し、続けているあいだは最後の音で止める。
        private int NextComboIndex(int previousIndex, float secondsSinceLastSpawn)
        {
            if (previousIndex < 0 || secondsSinceLastSpawn > _comboWindowSeconds)
            {
                return 0;
            }

            return Mathf.Min(previousIndex + 1, ChimeSemitones.Length - 1);
        }

        private void EmitBurstAt(Spawned spawned)
        {
            if (_burstPrefab == null)
            {
                return;
            }

            GameObject burst = Instantiate(_burstPrefab, spawned.Position, Quaternion.identity);
            burst.transform.localScale *= _burstScale;

            if (TryGetFoodColor(spawned.Food, out Color foodColor))
            {
                Color tint = Color.Lerp(Color.white, foodColor, ParticleTintStrength);
                foreach (ParticleSystem particles in burst.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = particles.main;
                    main.startColor = tint;
                }
            }

            // Stop Action が Destroy でない粒でも、残り続けないようにする。
            Destroy(burst, ParticleLifetimeSeconds);
        }

        // 食べ物ごとに粒の色を変えるため、見た目の主な色を Prefab の最初の Renderer から取る。
        private static bool TryGetFoodColor(GameObject foodPrefab, out Color foodColor)
        {
            foodColor = Color.white;
            if (foodPrefab == null)
            {
                return false;
            }

            Renderer foodRenderer = foodPrefab.GetComponentInChildren<Renderer>();
            Material material = foodRenderer != null ? foodRenderer.sharedMaterial : null;
            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_BaseColor"))
            {
                foodColor = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                foodColor = material.GetColor("_Color");
                return true;
            }

            return false;
        }

        // 立ち上がりが速く、倍音のぶんだけ先に消える音にして、ベルらしく聞かせる。
        private static AudioClip CreateBellChime(int semitonesAboveBase)
        {
            float frequency = BaseFrequencyHertz * Mathf.Pow(2f, semitonesAboveBase / 12f);
            int sampleCount = Mathf.CeilToInt(SampleRate * ChimeDurationSeconds);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float seconds = i / (float)SampleRate;
                float phase = 2f * Mathf.PI * frequency * seconds;
                float attack = Mathf.Clamp01(seconds / 0.004f);
                float body = Mathf.Sin(phase) * Mathf.Exp(-seconds * 8f);
                float overtone = 0.4f * Mathf.Sin(2f * phase) * Mathf.Exp(-seconds * 14f);
                float shimmer = 0.2f * Mathf.Sin(3f * phase) * Mathf.Exp(-seconds * 22f);
                samples[i] = 0.5f * attack * (body + overtone + shimmer);
            }

            AudioClip chime = AudioClip.Create("SpawnChime", sampleCount, 1, SampleRate, false);
            chime.SetData(samples, 0);
            return chime;
        }
    }
}
