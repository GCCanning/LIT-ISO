using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal fade-to-black loading screen for scene transitions. Replaces the
/// frozen hard cut between menu and world with: fade in -> async load (world
/// title + rotating tip while it works) -> fade out -> self-destruct.
/// Usage: LoadingScreen.Go("IsoCoreFoundation", "Entering Greenwake…");
/// </summary>
public sealed class LoadingScreen : MonoBehaviour
{
    static readonly string[] Tips =
    {
        "The System is always watching. Variety scores.",
        "Rivers always reach the sea — follow one home.",
        "Trees fall faster with the right tool.",
        "Camp wards keep the night honest.",
        "Your first seven days decide who you become.",
        "Lost? The fire on the horizon is usually yours.",
    };

    public static void Go(string sceneName, string title)
    {
        var root = new GameObject("LoadingScreen");
        DontDestroyOnLoad(root);
        var ls = root.AddComponent<LoadingScreen>();
        ls.Build(title);
        ls.StartCoroutine(ls.Run(sceneName));
    }

    CanvasGroup _group;

    void Build(string title)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = true;

        var bg = new GameObject("BG", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(transform, false);
        bg.color = new Color(0.02f, 0.025f, 0.04f, 1f);
        Stretch(bg.rectTransform);

        var titleText = NewText("Title", title, 30, new Color(0.98f, 0.85f, 0.45f, 1f));
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 30f);

        var tip = NewText("Tip", Tips[Random.Range(0, Tips.Length)], 16,
            new Color(0.75f, 0.75f, 0.7f, 1f));
        tip.rectTransform.anchoredPosition = new Vector2(0f, -30f);
    }

    Text NewText(string name, string value, int size, Color color)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(transform, false);
        t.text = value;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        LitIsoFont.Apply(t, size);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200f, 44f);
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    IEnumerator Run(string sceneName)
    {
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 0.35f)
        {
            _group.alpha = a;
            yield return null;
        }
        _group.alpha = 1f;

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        yield return null;          // one frame for the new scene to initialise
        yield return new WaitForSecondsRealtime(0.25f);

        for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime / 0.5f)
        {
            _group.alpha = a;
            yield return null;
        }
        Destroy(gameObject);
    }
}
