using System.Collections;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Cross-fades a day and a night music track (plus day birdsong / night crickets ambient
    /// beds) by the day/night cycle. All clips load from Resources by name and are optional —
    /// the world is simply quiet until the audio art is dropped in. Bus volumes come from
    /// PlayerPrefs ("vol_master", "vol_music") so the pause-menu sliders apply live.
    /// </summary>
    public class WorldAudioController : MonoBehaviour
    {
        public DayNightSystem dayNight;

        AudioSource _musicDay, _musicNight, _ambDay, _ambNight;

        /// <summary>Active instance, so MusicCues can duck the ambient bed for stings.</summary>
        public static WorldAudioController Instance { get; private set; }

        /// <summary>0–1 multiplier applied on top of the normal day/night mix (1 = normal).
        /// MusicCues lowers this while a dungeon/boss/tavern cue plays, then restores it.</summary>
        float _duck = 1f;

        // ---- per-biome ambient bed (Ring-of-Biomes weather showcase, 2026-06-13) ----
        // Optional: layered on top of the day/night beds above. Crossfades to a
        // biome-specific loop (Resources/Audio/Ambient/Biome/<biomeId>) when the player's
        // current biome changes. Silent no-op if no matching clip exists (Make() only
        // plays when clip != null), so normal play is unaffected unless clips are added.
        IsoWorld _world;
        IsoFoundationPlayer _player;
        int _seed;
        AudioSource _ambBiome;
        string _currentBiomeId;
        Coroutine _biomeFadeRoutine;

        void Awake()
        {
            Instance = this;
            if (dayNight == null) dayNight = Object.FindFirstObjectByType<DayNightSystem>();
            _musicDay   = Make("Audio/Music/day");
            _musicNight = Make("Audio/Music/night");
            _ambDay     = Make("Audio/Ambient/day");
            _ambNight   = Make("Audio/Ambient/night");

            _ambBiome = gameObject.AddComponent<AudioSource>();
            _ambBiome.loop = true;
            _ambBiome.playOnAwake = false;
            _ambBiome.spatialBlend = 0f;
            _ambBiome.volume = 0f;
        }

        /// <summary>Wire up the world/player so the per-biome ambient bed can track the
        /// player's current biome. Called once from FoundationBootstrap.</summary>
        public void SetBiomeSource(IsoWorld world, IsoFoundationPlayer player, int seed)
        {
            _world = world;
            _player = player;
            _seed = seed;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Smoothly duck (or restore) the ambient day/night music bed, e.g. while a
        /// dungeon, boss, or tavern cue from MusicCues is playing on top.</summary>
        public void SetDuck(float multiplier) => _duck = Mathf.Clamp01(multiplier);

        AudioSource Make(string resourcePath)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>(resourcePath);
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            if (s.clip != null) s.Play();
            return s;
        }

        void Update()
        {
            float night = dayNight != null ? Mathf.Clamp01(dayNight.NightFactor) : 0f;
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_music", 0.7f)) * master * _duck;

            SetVol(_musicDay,   (1f - night) * music * 0.6f);
            SetVol(_musicNight, night * music * 0.6f);
            SetVol(_ambDay,     (1f - night) * music * 0.45f);
            SetVol(_ambNight,   night * music * 0.45f);

            UpdateBiomeAmbient();
        }

        /// <summary>Crossfades the per-biome ambient bed when the player's current biome
        /// changes. Driven primarily by the biome-showcase world (seed 240611) so each
        /// quadrant gets its own ambience, but works for any world once biome ambient
        /// clips are dropped into Resources/Audio/Ambient/Biome/.</summary>
        void UpdateBiomeAmbient()
        {
            if (_world == null || _player == null) return;

            var cell = _player.CurrentCell;
            var biome = _world.GetBiome(cell.x, cell.y);
            string biomeId = biome != null ? biome.id : null;
            if (biomeId == _currentBiomeId) return;

            _currentBiomeId = biomeId;
            if (_biomeFadeRoutine != null) StopCoroutine(_biomeFadeRoutine);
            _biomeFadeRoutine = StartCoroutine(CrossfadeBiomeAmbient(biomeId));
        }

        IEnumerator CrossfadeBiomeAmbient(string biomeId)
        {
            const float fadeTime = 2.5f;

            // Fade current bed out.
            float startVol = _ambBiome.volume;
            float t = 0f;
            while (t < fadeTime && startVol > 0f)
            {
                t += Time.deltaTime;
                _ambBiome.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
                yield return null;
            }
            _ambBiome.Stop();
            _ambBiome.volume = 0f;

            var clip = !string.IsNullOrEmpty(biomeId)
                ? Resources.Load<AudioClip>($"Audio/Ambient/Biome/{biomeId}")
                : null;
            if (clip == null) yield break; // no clip for this biome — stay silent

            _ambBiome.clip = clip;
            _ambBiome.Play();

            float master = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_music", 0.7f)) * master * _duck;
            float targetVol = 0.4f * music;

            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                _ambBiome.volume = Mathf.Lerp(0f, targetVol, t / fadeTime);
                yield return null;
            }
            _ambBiome.volume = targetVol;
        }

        static void SetVol(AudioSource s, float v)
        {
            if (s != null && s.clip != null) s.volume = v;
        }
    }
}
