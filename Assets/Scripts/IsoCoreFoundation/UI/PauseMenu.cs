using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Esc-toggled pause overlay built entirely in code (uGUI). Pauses the game (timeScale=0),
    /// offers Resume / Quit-to-Menu, exposes Master/Music/SFX volume sliders persisted to
    /// PlayerPrefs (consumed live by SfxManager and WorldAudioController), and shows the
    /// control hints. Self-contained: ensures its own Canvas + EventSystem.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        CanvasGroup _group;
        bool _open;
        Font _font;

        void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Apply saved master volume immediately.
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));

            EnsureEventSystem();
            Build();
            SetOpen(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetOpen(!_open);
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_group != null)
            {
                _group.alpha = open ? 1f : 0f;
                _group.interactable = open;
                _group.blocksRaycasts = open;
            }
            Time.timeScale = open ? 0f : 1f;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        void Build()
        {
            // Canvas (drawn above the HUD).
            var canvasGo = new GameObject("PauseCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();

            // Dim background.
            var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.7f));
            Stretch(dim.rectTransform);

            // Center panel.
            var panel = NewImage("Panel", canvasGo.transform, new Color(0.12f, 0.13f, 0.18f, 0.98f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(560, 620);

            float y = 250f;
            MakeLabel("PAUSED", panel.transform, new Vector2(0, y), 44, FontStyle.Bold,
                new Color(1f, 0.95f, 0.8f)); y -= 80f;

            MakeButton("Resume", panel.transform, new Vector2(0, y), () => SetOpen(false)); y -= 64f;

            // Volume sliders.
            MakeSlider("Master", "vol_master", 1f, panel.transform, new Vector2(0, y),
                v => AudioListener.volume = v); y -= 56f;
            MakeSlider("Music", "vol_music", 0.7f, panel.transform, new Vector2(0, y), null); y -= 56f;
            MakeSlider("SFX", "vol_sfx", 1f, panel.transform, new Vector2(0, y), null); y -= 80f;

            MakeButton("Quit to Menu", panel.transform, new Vector2(0, y), QuitToMenu); y -= 80f;

            MakeLabel("WASD / Arrows: move    E: interact    LMB: place    RMB: remove\n" +
                      "I: inventory    C: craft    1-9 / scroll: hotbar    Esc: pause",
                      panel.transform, new Vector2(0, y), 18, FontStyle.Normal,
                      new Color(0.75f, 0.78f, 0.85f), 520);
        }

        void QuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MenuScene");
        }

        // ---- uGUI builders ----
        Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        Text MakeLabel(string text, Transform parent, Vector2 pos, int size, FontStyle style, Color color, float width = 480)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(width, size + 40);
            return t;
        }

        void MakeButton(string label, Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage("Button_" + label, parent, new Color(0.25f, 0.45f, 0.65f, 1f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(320, 50);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            MakeLabel(label, img.transform, Vector2.zero, 24, FontStyle.Bold, Color.white, 320);
        }

        void MakeSlider(string label, string prefKey, float def, Transform parent, Vector2 pos,
            UnityEngine.Events.UnityAction<float> extra)
        {
            // Label
            var lab = MakeLabel(label, parent, pos + new Vector2(-200, 0), 20, FontStyle.Normal,
                new Color(0.85f, 0.88f, 0.95f), 140);
            lab.alignment = TextAnchor.MiddleLeft;

            var sliderGo = new GameObject("Slider_" + label);
            sliderGo.transform.SetParent(parent, false);
            var srt = sliderGo.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = pos + new Vector2(70, 0);
            srt.sizeDelta = new Vector2(240, 20);
            var slider = sliderGo.AddComponent<Slider>();

            var bg = NewImage("BG", sliderGo.transform, new Color(0.08f, 0.08f, 0.1f, 1f));
            Stretch(bg.rectTransform);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fart = fillArea.AddComponent<RectTransform>();
            Stretch(fart);
            var fill = NewImage("Fill", fillArea.transform, new Color(0.45f, 0.75f, 0.55f, 1f));
            Stretch(fill.rectTransform);

            slider.targetGraphic = bg;
            slider.fillRect = fill.rectTransform;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(PlayerPrefs.GetFloat(prefKey, def));
            slider.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(prefKey, v);
                extra?.Invoke(v);
            });
        }
    }
}
