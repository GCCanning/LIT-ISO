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

        void Awake()
        {
            if (dayNight == null) dayNight = Object.FindFirstObjectByType<DayNightSystem>();
            _musicDay   = Make("Audio/Music/day");
            _musicNight = Make("Audio/Music/night");
            _ambDay     = Make("Audio/Ambient/day");
            _ambNight   = Make("Audio/Ambient/night");
        }

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
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_music", 0.7f)) * master;

            SetVol(_musicDay,   (1f - night) * music * 0.6f);
            SetVol(_musicNight, night * music * 0.6f);
            SetVol(_ambDay,     (1f - night) * music * 0.45f);
            SetVol(_ambNight,   night * music * 0.45f);
        }

        static void SetVol(AudioSource s, float v)
        {
            if (s != null && s.clip != null) s.volume = v;
        }
    }
}
