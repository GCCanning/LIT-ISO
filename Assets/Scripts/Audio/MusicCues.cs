using System.Collections;
using UnityEngine;
using IsoCore.Foundation;

namespace LitIso.Audio
{
    /// <summary>
    /// Static helper for the "situational" music tracks decided in the worldgen rules pass:
    ///   Audio/Music/menu    — main menu loop (already wired in WelcomeScreenManager)
    ///   Audio/Music/day     — overworld daytime loop (WorldAudioController)
    ///   Audio/Music/night   — overworld nighttime loop (WorldAudioController)
    ///   Audio/Music/dungeon — dungeon/instance loop
    ///   Audio/Music/boss    — boss-encounter sting/loop
    ///   Audio/Music/tavern  — settlement/tavern loop
    ///
    /// Dungeon, boss, and tavern cues crossfade in on a dedicated AudioSource and duck the
    /// overworld day/night bed via WorldAudioController.SetDuck so they don't layer into mud.
    /// Call PlayDungeon()/PlayTavern()/PlayBoss() on entering those contexts and StopCue() when
    /// leaving (e.g. exiting the dungeon instance or the boss arena).
    /// </summary>
    public static class MusicCues
    {
        const string kMenu    = "Audio/Music/menu";
        const string kDungeon = "Audio/Music/dungeon";
        const string kBoss    = "Audio/Music/boss";
        const string kTavern  = "Audio/Music/tavern";

        const float FadeSeconds = 1.5f;
        const float DuckTo      = 0.15f;

        static AudioSource _cueSource;
        static MonoBehaviour _runner;
        static Coroutine _fadeRoutine;

        /// <summary>Resources path for the main-menu loop (read directly by WelcomeScreenManager).</summary>
        public static string MenuTrackPath => kMenu;

        static void EnsureSource()
        {
            if (_cueSource != null) return;
            var go = new GameObject("MusicCue");
            Object.DontDestroyOnLoad(go);
            _cueSource = go.AddComponent<AudioSource>();
            _cueSource.loop = true;
            _cueSource.playOnAwake = false;
            _cueSource.spatialBlend = 0f;
            _cueSource.volume = 0f;
            _runner = go.AddComponent<CueRunner>();
        }

        /// <summary>Crossfade in the dungeon loop and duck the overworld bed.</summary>
        public static void PlayDungeon() => PlayCue(kDungeon);

        /// <summary>Crossfade in the boss-encounter track and duck the overworld bed.</summary>
        public static void PlayBoss() => PlayCue(kBoss);

        /// <summary>Crossfade in the tavern/settlement loop and duck the overworld bed.</summary>
        public static void PlayTavern() => PlayCue(kTavern);

        /// <summary>Fade out whatever cue is playing and restore the overworld day/night mix.</summary>
        public static void StopCue()
        {
            EnsureSource();
            if (_fadeRoutine != null) _runner.StopCoroutine(_fadeRoutine);
            _fadeRoutine = _runner.StartCoroutine(FadeOutAndStop());
        }

        static void PlayCue(string resourcePath)
        {
            EnsureSource();
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[MusicCues] No clip at Resources/{resourcePath}");
                return;
            }
            if (_fadeRoutine != null) _runner.StopCoroutine(_fadeRoutine);
            _fadeRoutine = _runner.StartCoroutine(FadeToClip(clip));
        }

        static float MusicVolume()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_music", 0.7f));
            return master * music;
        }

        static IEnumerator FadeToClip(AudioClip clip)
        {
            // Duck the overworld bed first so the new cue isn't fighting it.
            WorldAudioController.Instance?.SetDuck(DuckTo);

            float startVol = _cueSource.volume;
            if (_cueSource.clip != clip)
            {
                // Fade the current cue out, swap, then fade the new one in.
                for (float t = 0; t < FadeSeconds * 0.5f; t += Time.deltaTime)
                {
                    _cueSource.volume = Mathf.Lerp(startVol, 0f, t / (FadeSeconds * 0.5f));
                    yield return null;
                }
                _cueSource.clip = clip;
                _cueSource.Play();
            }

            float target = MusicVolume();
            for (float t = 0; t < FadeSeconds * 0.5f; t += Time.deltaTime)
            {
                _cueSource.volume = Mathf.Lerp(_cueSource.volume, target, t / (FadeSeconds * 0.5f));
                yield return null;
            }
            _cueSource.volume = target;
        }

        static IEnumerator FadeOutAndStop()
        {
            float startVol = _cueSource.volume;
            for (float t = 0; t < FadeSeconds; t += Time.deltaTime)
            {
                _cueSource.volume = Mathf.Lerp(startVol, 0f, t / FadeSeconds);
                yield return null;
            }
            _cueSource.volume = 0f;
            _cueSource.Stop();

            // Restore the overworld day/night bed.
            float dur = FadeSeconds;
            float t2 = 0f;
            while (t2 < dur)
            {
                t2 += Time.deltaTime;
                WorldAudioController.Instance?.SetDuck(Mathf.Lerp(DuckTo, 1f, t2 / dur));
                yield return null;
            }
            WorldAudioController.Instance?.SetDuck(1f);
        }

        /// <summary>Tiny MonoBehaviour just so the static class has somewhere to run coroutines.</summary>
        class CueRunner : MonoBehaviour { }
    }
}
