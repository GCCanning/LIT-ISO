using UnityEngine;

/// <summary>
/// Renders a sprite outline behind a ResourceNode when the mouse hovers over it.
///
/// Works by creating a child SpriteRenderer with the same sprite, scaled slightly
/// larger, rendered one sorting order below the main sprite, tinted in the hover
/// colour.  No custom shader required — compatible with any render pipeline.
///
/// IsoHoverController adds this component automatically if it is missing on a node,
/// so you do not need to add it by hand.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class InteractableHighlight : MonoBehaviour
{
    [Header("Outline style")]
    public Color hoverColour  = new Color(1.00f, 0.92f, 0.22f, 0.88f);
    public float outlineScale = 1.14f;

    [Header("Hover pulse")]
    [Tooltip("How fast the outline breathes in and out.")]
    public float pulseSpeed  = 2.8f;
    [Tooltip("Amplitude of the scale pulse (added on top of outlineScale).")]
    public float pulseAmount = 0.03f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private SpriteRenderer mainSr;
    private SpriteRenderer outlineSr;
    private bool hovered;
    private float pulseTimer;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        mainSr = GetComponent<SpriteRenderer>();
        BuildOutline();
    }

    private void Update()
    {
        if (!hovered || outlineSr == null) return;

        pulseTimer += Time.deltaTime * pulseSpeed;
        float scale = outlineScale + Mathf.Sin(pulseTimer) * pulseAmount;
        outlineSr.transform.localScale = Vector3.one * scale;
    }

    // -------------------------------------------------------------------------
    // Public API — called by IsoHoverController
    // -------------------------------------------------------------------------

    public void SetHovered(bool state)
    {
        hovered = state;
        pulseTimer = 0f;

        if (outlineSr != null)
        {
            if (!state)
                outlineSr.transform.localScale = Vector3.one * outlineScale;

            outlineSr.gameObject.SetActive(state);
        }
    }

    /// <summary>
    /// Call this whenever the parent SpriteRenderer's sprite changes so the
    /// outline stays in sync (e.g. after harvest visual swap).
    /// </summary>
    public void SyncSprite()
    {
        if (outlineSr != null && mainSr != null)
            outlineSr.sprite = mainSr.sprite;
    }

    // -------------------------------------------------------------------------
    // Outline construction
    // -------------------------------------------------------------------------

    private void BuildOutline()
    {
        // Reuse an existing child outline if present (e.g. re-entrance after reload)
        Transform existing = transform.Find("_Outline");
        if (existing != null)
        {
            outlineSr = existing.GetComponent<SpriteRenderer>();
            if (outlineSr != null)
            {
                outlineSr.gameObject.SetActive(false);
                return;
            }
        }

        var outlineGO = new GameObject("_Outline");
        outlineGO.transform.SetParent(transform, false);
        outlineGO.transform.localPosition = Vector3.zero;
        outlineGO.transform.localScale    = Vector3.one * outlineScale;

        outlineSr                   = outlineGO.AddComponent<SpriteRenderer>();
        outlineSr.sprite            = mainSr.sprite;
        outlineSr.color             = hoverColour;
        outlineSr.sortingLayerID    = mainSr.sortingLayerID;
        outlineSr.sortingOrder      = mainSr.sortingOrder - 1;
        outlineSr.spriteSortPoint   = mainSr.spriteSortPoint;

        outlineGO.SetActive(false);
    }
}
