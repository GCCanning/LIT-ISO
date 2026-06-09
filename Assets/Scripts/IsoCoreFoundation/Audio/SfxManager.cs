using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Tiny pooled 2D sound player. Clips resolve from Resources/Audio/SFX/&lt;key&gt; by name,
    /// cached (including misses), so the game runs silently until the audio art is dropped in
    /// — then it auto-plays with no code change. Volume is read from PlayerPrefs ("vol_sfx",
    /// "vol_master") so the pause-menu sliders affect it immediately.
    /// </summary>
    public class SfxManager : MonoBehaviour
    {
        static SfxManager _inst;
        AudioSource[] _pool;
        int _next;
        readonly Dictionary<string, AudioClip> _clips = new();

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("SfxManager");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            _inst = go.AddComponent<SfxManager>();
            _inst._pool = new AudioSource[8];
            for (int i = 0; i < _inst._pool.Length; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f; // 2D
                _inst._pool[i] = s;
            }
        }

        static float Master => Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
        static float SfxVol => Mathf.Clamp01(PlayerPrefs.GetFloat("vol_sfx", 1f));

        AudioClip Load(string key)
        {
            if (_clips.TryGetValue(key, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/SFX/" + key);
            _clips[key] = c; // cache misses too
            return c;
        }

        /// <summary>Play a one-shot SFX by key with a little random pitch for variety.</summary>
        public static void Play(string key, float volume = 1f, float pitchVariation = 0.08f)
        {
            if (string.IsNullOrEmpty(key)) return;
            Ensure();
            var clip = _inst.Load(key);
            if (clip == null) return; // art not present yet — silent, no error
            var src = _inst._pool[_inst._next];
            _inst._next = (_inst._next + 1) % _inst._pool.Length;
            src.clip = clip;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.volume = Mathf.Clamp01(volume) * SfxVol * Master;
            src.Play();
        }

        /// <summary>Clears the clip cache (after dropping in new audio during a session).</summary>
        public static void ClearCache() { if (_inst != null) _inst._clips.Clear(); }
    }
}
