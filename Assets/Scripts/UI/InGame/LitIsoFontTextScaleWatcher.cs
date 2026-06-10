using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Watches the shared text-scale PlayerPrefs key so Foundation-side UI sliders can
    /// update the uGUI HUD immediately without a cross-assembly reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LitIsoFontTextScaleWatcher : MonoBehaviour
    {
        float _lastScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Object.FindFirstObjectByType<LitIsoFontTextScaleWatcher>() != null)
                return;

            var go = new GameObject("[LitIsoFontTextScaleWatcher]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<LitIsoFontTextScaleWatcher>();
        }

        void Awake()
        {
            _lastScale = LitIsoFont.TextScale;
        }

        void Update()
        {
            float scale = LitIsoFont.TextScale;
            if (Mathf.Abs(_lastScale - scale) < 0.001f)
                return;

            _lastScale = scale;
            LitIsoFont.NotifyTextScaleChanged(scale);
        }
    }
}
