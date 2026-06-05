using System.Collections;
using UnityEngine;

/// <summary>
/// A world-space text popup that floats upward and fades out.
/// Spawned by ResourceNode.Harvest() to show what was picked up.
///
/// Uses TextMesh (built-in, zero TMP or shader dependency, works in all pipelines).
/// The MeshRenderer sortingOrder is set high so the text appears above tiles.
///
/// Usage:
///     WorldFloatingText.Spawn(transform.position, "+Wood  x2", Color.white);
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class WorldFloatingText : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Configuration — tweak here or pass overrides through Spawn()
    // -------------------------------------------------------------------------

    [Tooltip("Total time the text is visible including the fade.")]
    public float duration    = 1.5f;

    [Tooltip("How far (world units) the text rises before disappearing.")]
    public float liftAmount  = 0.85f;

    [Tooltip("Time fraction (0-1) at which the fade-out begins. 0.55 = visible for 55% of duration.")]
    public float fadeStart   = 0.55f;

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Spawn a floating text label at <paramref name="worldPos"/>.
    /// </summary>
    /// <param name="worldPos">World-space anchor (will be offset slightly upward).</param>
    /// <param name="text">Text to display, e.g. "+Wood  ×2".</param>
    /// <param name="colour">Text colour.</param>
    /// <param name="fontSize">TextMesh fontSize (default 26).</param>
    /// <param name="characterSize">TextMesh characterSize — controls world-space scale (default 0.07).</param>
    public static WorldFloatingText Spawn(
        Vector3 worldPos,
        string  text,
        Color   colour,
        int     fontSize      = 26,
        float   characterSize = 0.07f)
    {
        var go = new GameObject("_FloatingText");
        go.transform.position = worldPos + new Vector3(0f, 0.25f, 0f);

        var tm             = go.AddComponent<TextMesh>();
        tm.text            = text;
        tm.color           = colour;
        tm.characterSize   = characterSize;
        tm.anchor          = TextAnchor.MiddleCenter;
        tm.alignment       = TextAlignment.Center;
        LitIsoFont.Apply(tm, fontSize, FontStyle.Bold);

        // Render above tiles and sprites
        var mr             = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 200;

        return go.AddComponent<WorldFloatingText>();
    }

    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    private TextMesh tm;
    private Vector3  startPos;
    private Color    startColour;

    private void Awake()
    {
        tm          = GetComponent<TextMesh>();
        startPos    = transform.position;
        startColour = tm != null ? tm.color : Color.white;
    }

    private void Start()
    {
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Ease-out lift: accelerates quickly then slows
            float liftT   = 1f - Mathf.Pow(1f - t, 2f);
            transform.position = startPos + new Vector3(0f, liftAmount * liftT, 0f);

            // Fade-out after fadeStart
            float alpha = t < fadeStart
                ? 1f
                : 1f - (t - fadeStart) / (1f - fadeStart);

            if (tm != null)
                tm.color = new Color(startColour.r, startColour.g, startColour.b,
                                     startColour.a * Mathf.Clamp01(alpha));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
