using UnityEngine;

/// <summary>
/// Slow "Ken Burns" cinematic drift for the main-menu background image:
/// a gentle scale breathe plus a slow figure-eight pan. Sized so the
/// EnvelopeParent cover-fit always keeps the screen covered (base scale 1.06).
/// Purely visual; no input, no allocation per frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuCinematicBackground : MonoBehaviour
{
    [Tooltip("Extra zoom applied so panning never reveals edges.")]
    public float baseScale = 1.06f;
    [Tooltip("Additional slow zoom amplitude.")]
    public float zoomAmplitude = 0.025f;
    [Tooltip("Pan radius in canvas pixels.")]
    public float panRadius = 14f;
    [Tooltip("Seconds per full drift cycle.")]
    public float cycleSeconds = 26f;

    RectTransform _rt;
    Vector2 _basePos;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _basePos = _rt.anchoredPosition;
    }

    void Update()
    {
        float t = Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(1f, cycleSeconds));
        float s = baseScale + zoomAmplitude * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
        _rt.localScale = new Vector3(s, s, 1f);
        // figure-eight drift: x at half the frequency of y
        _rt.anchoredPosition = _basePos + new Vector2(
            panRadius * Mathf.Sin(t * 0.5f),
            panRadius * 0.6f * Mathf.Sin(t));
    }
}
