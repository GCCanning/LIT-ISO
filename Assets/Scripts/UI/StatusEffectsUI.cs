using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EthraClone.TrialWeek;

/// <summary>
/// Displays active status effect icons on the player HUD.
/// Subscribes to StatusEffectHandler events on the player.
///
/// Setup:
///   1. Add this component to a HUD panel.
///   2. Assign iconPrefab (an Image component) and iconContainer (a HorizontalLayoutGroup).
///   3. The icons auto-appear/disappear as effects are applied/removed.
/// </summary>
public class StatusEffectsUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab with an Image component. Will be instantiated per active effect.")]
    public GameObject iconPrefab;

    [Tooltip("Parent transform for icon instances. Use a HorizontalLayoutGroup.")]
    public RectTransform iconContainer;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private StatusEffectHandler playerHandler;
    private readonly Dictionary<string, GameObject> activeIcons = new();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        StatusEffectHandler.OnEffectApplied += HandleApplied;
        StatusEffectHandler.OnEffectRemoved += HandleRemoved;
    }

    private void OnDisable()
    {
        StatusEffectHandler.OnEffectApplied -= HandleApplied;
        StatusEffectHandler.OnEffectRemoved -= HandleRemoved;
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void HandleApplied(GameObject target, StatusEffectDefinition def)
    {
        // Only show player effects
        if (target.GetComponent<IsoPlayerController>() == null) return;
        if (iconPrefab == null || iconContainer == null) return;
        if (activeIcons.ContainsKey(def.effectId)) return;

        var go  = Instantiate(iconPrefab, iconContainer);
        var img = go.GetComponentInChildren<Image>();
        if (img != null && def.icon != null)
        {
            img.sprite = def.icon;
            img.color  = def.particleTint;
        }

        // Optional tooltip — set the GameObject name for hover-based tooltips
        go.name = def.displayName;

        activeIcons[def.effectId] = go;
    }

    private void HandleRemoved(GameObject target, string effectId)
    {
        if (target.GetComponent<IsoPlayerController>() == null) return;
        if (!activeIcons.TryGetValue(effectId, out var icon)) return;

        Destroy(icon);
        activeIcons.Remove(effectId);
    }
}
