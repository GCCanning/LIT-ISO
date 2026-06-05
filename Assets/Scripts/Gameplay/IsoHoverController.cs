using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Detects which ResourceNode the mouse is currently hovering and drives the
/// InteractableHighlight + cursor states.
///
/// Add to the same Player GameObject as IsoInteractionController.
/// Does NOT modify IsoPlayerController movement.
///
/// Cursor behaviour:
///   • Hovering a harvestable node  → harvestCursor (or default if null)
///   • Hovering a harvested/cooldown node → blockedCursor (or default if null)
///   • Nothing hovered              → default OS cursor
///
/// Assign Texture2D cursor assets in the Inspector.
/// The component degrades gracefully when cursor textures are not assigned.
/// </summary>
public class IsoHoverController : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Overlap radius used to find nodes under the cursor. " +
             "Keep small (0.15–0.25) so only the node under the pointer responds.")]
    public float hoverRadius = 0.18f;
    public LayerMask nodeLayerMask = ~0;

    [Header("Cursor textures (optional — leave null to keep OS default)")]
    [Tooltip("Shown when a harvestable node is under the cursor.")]
    public Texture2D harvestCursor;
    public Vector2   harvestCursorHotspot = Vector2.zero;

    [Tooltip("Shown when the node is harvested / on cooldown.")]
    public Texture2D blockedCursor;
    public Vector2   blockedCursorHotspot = Vector2.zero;

    [Header("Debug")]
    public bool drawGizmo = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private ResourceNode currentHovered;
    private Camera       cam;

    private enum CursorState { Default, Harvest, Blocked }
    private CursorState activeCursorState = CursorState.Default;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (GameSettingsMenu.IsOpen)
        {
            ClearHover();
            ApplyCursor(CursorState.Default);
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        ResourceNode next = FindNodeAtPoint(mouseWorld);
        UpdateHoverState(next);
    }

    private void OnDisable()
    {
        ClearHover();
        ApplyCursor(CursorState.Default);
    }

    // -------------------------------------------------------------------------
    // Hover state machine
    // -------------------------------------------------------------------------

    private void UpdateHoverState(ResourceNode next)
    {
        if (next == currentHovered)
        {
            // Still hovering same node — update cursor in case harvest state changed
            if (currentHovered != null)
                ApplyCursorForNode(currentHovered);
            return;
        }

        // Exit previous
        if (currentHovered != null)
        {
            var hl = currentHovered.GetComponent<InteractableHighlight>();
            if (hl != null) hl.SetHovered(false);
        }

        currentHovered = next;

        // Enter next
        if (currentHovered != null)
        {
            // Auto-add highlight component if absent (supports nodes created before this feature)
            var hl = currentHovered.GetComponent<InteractableHighlight>();
            if (hl == null) hl = currentHovered.gameObject.AddComponent<InteractableHighlight>();

            hl.SetHovered(true);
            ApplyCursorForNode(currentHovered);
        }
        else
        {
            ApplyCursor(CursorState.Default);
        }
    }

    private void ClearHover()
    {
        if (currentHovered != null)
        {
            var hl = currentHovered.GetComponent<InteractableHighlight>();
            if (hl != null) hl.SetHovered(false);
            currentHovered = null;
        }
    }

    // -------------------------------------------------------------------------
    // Node detection
    // -------------------------------------------------------------------------

    private ResourceNode FindNodeAtPoint(Vector3 worldPoint)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, hoverRadius, nodeLayerMask);

        ResourceNode nearest  = null;
        float        bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var node = hit.GetComponent<ResourceNode>();
            if (node == null) continue;

            float dist = Vector2.Distance(worldPoint, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest  = node;
            }
        }

        return nearest;
    }

    // -------------------------------------------------------------------------
    // Cursor management
    // -------------------------------------------------------------------------

    private void ApplyCursorForNode(ResourceNode node)
    {
        ApplyCursor(node.IsHarvested ? CursorState.Blocked : CursorState.Harvest);
    }

    private void ApplyCursor(CursorState state)
    {
        if (state == activeCursorState) return;
        activeCursorState = state;

        switch (state)
        {
            case CursorState.Harvest:
                Cursor.SetCursor(harvestCursor, harvestCursorHotspot, CursorMode.Auto);
                break;
            case CursorState.Blocked:
                Cursor.SetCursor(blockedCursor, blockedCursorHotspot, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0f;

        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 0.2f);
        UnityEditor.Handles.DrawSolidDisc(mouse, Vector3.back, hoverRadius);
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 0.7f);
        UnityEditor.Handles.DrawWireDisc(mouse, Vector3.back, hoverRadius);
    }
#endif
}
