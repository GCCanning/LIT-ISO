using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Companion component to IsoPlayerController — handles resource node harvesting
/// via right-click or E key without touching any movement code.
///
/// Attach to the same Player GameObject as IsoPlayerController.
/// Requires PlayerInventory on the same GameObject (added by GameplayLayerSetup).
///
/// How it works:
///   On right-click OR E key, an OverlapCircle is cast around the player's position.
///   The closest ResourceNode within range has its Harvest() method called.
///   IsoPlayerController still processes right-click for tile selection; both can
///   read the same input simultaneously with no conflict.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class IsoInteractionController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Keyboard shortcut for harvesting a nearby node.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Range")]
    [Tooltip("World-space radius used for the overlap search. Should be >= the largest node's harvestRadius.")]
    public float searchRadius = 1.6f;

    [Tooltip("Layer mask for the overlap search. Set to the layer your ResourceNode objects are on.")]
    public LayerMask nodeLayerMask = ~0;

    [Header("Debug")]
    [Tooltip("Draw the search radius as a gizmo in the Scene view.")]
    public bool drawGizmo = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private PlayerInventory inventory;

    // Small cooldown so a held E key doesn't fire every frame
    private float interactCooldown = 0f;
    private const float InteractCooldownDuration = 0.25f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (GameSettingsMenu.IsOpen)
        {
            return;
        }

        if (interactCooldown > 0f)
        {
            interactCooldown -= Time.deltaTime;
            return;
        }

        bool triggered = Input.GetMouseButtonDown(1) || Input.GetKeyDown(interactKey);
        if (!triggered) return;

        TryHarvestNearest();
        interactCooldown = InteractCooldownDuration;
    }

    // -------------------------------------------------------------------------
    // Core logic
    // -------------------------------------------------------------------------

    private void TryHarvestNearest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius, nodeLayerMask);

        ResourceNode nearest = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // Skip triggers that aren't resource nodes
            var node = hit.GetComponent<ResourceNode>();
            if (node == null || node.IsHarvested) continue;

            // Additional range check using the node's own definition radius
            if (!node.IsInRange(transform.position)) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = node;
            }
        }

        if (nearest != null)
        {
            nearest.Harvest(inventory);
        }
    }

    // -------------------------------------------------------------------------
    // Editor gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo) return;
        UnityEditor.Handles.color = new Color(0.3f, 0.9f, 0.4f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.back, searchRadius);
        UnityEditor.Handles.color = new Color(0.3f, 0.9f, 0.4f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.back, searchRadius);
    }
#endif
}
