using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-entry cutscene for a NEW world (owner spec): you've been transmigrated —
/// the System "boots you up" while you come to, disoriented. Black screen, typed
/// System lines with flicker, a couple of double-vision pulses, slow wake into
/// the world. Pure procedural UI; skippable with any key after the first line.
///
/// WelcomeScreenManager arms it only when launching a world with no existing
/// save; it plays once after the scene loads, then destroys itself.
/// </summary>
public sealed class TransmigrationIntro : MonoBehaviour
{
    static bool s_armed;
    public static void Arm() => s_armed = true;

    static readonly string[] Lines =
    {
        "...",
        "vital signs ............ DETECTED",
        "soul signature ......... UNREGISTERED",
        "origin ................. [DATA EXPUNGED]",
        "translation matrix ..... calibrating",
        "body ................... reassembled (97.4%)",
        "",
        "Welcome, Otherworlder.",
        "TRIAL PROTOCOL: SEVEN DAYS.",
        "Show this world who you are.",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void TryPlay()
    {
        if (!s_armed) return;
        s_armed = false;
        var go = new GameObject("TransmigrationIntro");
        DontDestroyOnLoad(go);
        go.AddComponent<TransmigrationIntro>();
    }

    CanvasGroup _group;
    Text _text;
    bool _skip;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = true;

        var bg = new GameObject("BG", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(transform, false);
        bg.color = Color.black;
        var r = bg.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        _text = new GameObject("Sys", typeof(RectTransform)).AddComponent<Text>();
        _text.transform.SetParent(transform, false);
        _text.alignment = TextAnchor.MiddleCenter;
        _text.color = new Color(0.55f, 0.85f, 1f, 1f);
        LitIsoFont.Apply(_text, 20);
        var tr = _text.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(900f, 500f);

        StartCoroutine(Play());
    }

    void Update()
    {
        if (Input.anyKeyDown) _skip = true;
    }

    IEnumerator Play()
    {
        var sb = new System.Text.StringBuilder();
        for (int li = 0; li < Lines.Length; li++)
        {
            string line = Lines[li];
            // typewriter, with occasional flicker
            for (int c = 0; c < line.Length; c++)
            {
                sb.Append(line[c]);
                _text.text = sb.ToString();
                if (_skip) break;
                if (Random.value < 0.04f)   // glitch flicker
                {
                    _group.alpha = 0.55f;
                    yield return new WaitForSecondsRealtime(0.03f);
                    _group.alpha = 1f;
                }
                yield return new WaitForSecondsRealtime(line[c] == '.' ? 0.012f : 0.028f);
            }
            sb.Append('\n');
            _text.text = sb.ToString();
            if (!_skip)
                yield return new WaitForSecondsRealtime(li >= 7 ? 0.8f : 0.35f);
        }

        if (!_skip) yield return new WaitForSecondsRealtime(0.9f);

        // disoriented wake: two double-vision pulses, then a slow fade in
        for (int pulse = 0; pulse < 2 && !_skip; pulse++)
        {
            for (float a = 1f; a > 0.25f; a -= Time.unscaledDeltaTime / 0.35f)
            { _group.alpha = a; yield return null; }
            for (float a = 0.25f; a < 0.9f; a += Time.unscaledDeltaTime / 0.25f)
            { _group.alpha = a; yield return null; }
        }
        _text.text = "";
        for (float a = _group.alpha; a > 0f; a -= Time.unscaledDeltaTime / 1.4f)
        { _group.alpha = a; yield return null; }
        Destroy(gameObject);
    }
}
