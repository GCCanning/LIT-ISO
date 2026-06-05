using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime component placed on a building's world GameObject when constructed.
/// Handles the construction-in-progress timer and exposes the building's definition.
///
/// Added automatically by TownManager.PlaceBuilding() — do not add manually.
/// </summary>
public class BuildingInstance : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public SettlementDefinition definition;
    public TownManager.Settlement settlement;

    public enum BuildState { UnderConstruction, Complete }
    public BuildState State { get; private set; } = BuildState.Complete;

    /// <summary>0–1 build progress. 1 = complete.</summary>
    public float BuildProgress { get; private set; } = 1f;

    public static event System.Action<BuildingInstance> OnConstructionComplete;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Visual")]
    [Tooltip("Optional ghost shader material applied while under construction.")]
    public Material constructionMaterial;

    // Cached original material on the root renderer
    private Material _originalMaterial;
    private SpriteRenderer _spriteRenderer;

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    /// <summary>Called by TownManager immediately after instantiation.</summary>
    public void StartConstruction()
    {
        if (definition == null || definition.buildTimeSeconds <= 0f) return;

        State = BuildState.UnderConstruction;
        BuildProgress = 0f;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null && constructionMaterial != null)
        {
            _originalMaterial = _spriteRenderer.material;
            _spriteRenderer.material = constructionMaterial;
        }

        StartCoroutine(ConstructionRoutine(definition.buildTimeSeconds));
    }

    private IEnumerator ConstructionRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            BuildProgress = Mathf.Clamp01(elapsed / duration);

            // Pulse the alpha while building
            if (_spriteRenderer != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 4f);
                var c = _spriteRenderer.color;
                c.a = pulse;
                _spriteRenderer.color = c;
            }

            // Progress float text every 25 %
            int pct = Mathf.FloorToInt(BuildProgress * 100f);
            if (pct % 25 == 0 && pct > 0 && pct < 100)
                WorldFloatingText.Spawn(
                    transform.position + Vector3.up * 0.6f,
                    $"Building… {pct}%",
                    new Color(0.9f, 0.75f, 0.2f));

            yield return null;
        }

        CompleteConstruction();
    }

    private void CompleteConstruction()
    {
        State = BuildState.Complete;
        BuildProgress = 1f;

        if (_spriteRenderer != null)
        {
            if (_originalMaterial != null)
                _spriteRenderer.material = _originalMaterial;

            var c = _spriteRenderer.color;
            c.a = 1f;
            _spriteRenderer.color = c;
        }

        WorldFloatingText.Spawn(
            transform.position + Vector3.up,
            $"{definition.displayName} Complete!",
            new Color(0.4f, 1f, 0.4f));

        SystemNotifier.Instance?.Announce(
            $"{definition.displayName} construction finished in {settlement?.name}.",
            SystemNotifier.MessageType.Info);

        OnConstructionComplete?.Invoke(this);
    }

    // -------------------------------------------------------------------------
    // Context menu helpers (editor quality-of-life)
    // -------------------------------------------------------------------------

    [ContextMenu("Force Complete Construction")]
    private void EditorForceComplete()
    {
        StopAllCoroutines();
        CompleteConstruction();
    }
}
