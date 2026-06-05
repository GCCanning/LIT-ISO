using UnityEngine;

/// <summary>
/// World-space interactable that triggers dungeon entry.
/// Place on a dungeon entrance GameObject in the scene (spawned by IsoWorldChunkManager).
/// Assign a DungeonDefinition asset in the inspector.
///
/// Renders a floating rank label above the entrance and shows an interaction prompt
/// when the player is within range.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DungeonEntrance : MonoBehaviour
{
    [Header("Definition")]
    public DungeonDefinition definition;

    [Header("Interaction")]
    [Tooltip("Distance at which the interact prompt appears.")]
    [Min(0.5f)] public float interactRange = 1.5f;

    [Header("Label")]
    public WorldFloatingText labelPrefab;   // Optional — if null falls back to Debug.Log

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private bool _playerNearby;
    private bool _showing;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Spawn persistent label
        SpawnLabel();

        // Register with DungeonManager so it can be queried
        DungeonManager.Instance?.RegisterEntrance(this);
    }

    private void OnDestroy()
    {
        DungeonManager.Instance?.UnregisterEntrance(this);
    }

    private void Update()
    {
        if (definition == null) return;

        var player = PlayerHealth.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        bool near  = dist <= interactRange;

        if (near && !_playerNearby)
        {
            _playerNearby = true;
            ShowPrompt(true);
        }
        else if (!near && _playerNearby)
        {
            _playerNearby = false;
            ShowPrompt(false);
        }

        // E or F to enter
        if (_playerNearby && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)))
            TryEnter();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void TryEnter()
    {
        if (definition == null) return;
        DungeonManager.Instance?.EnterDungeon(definition, this);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void SpawnLabel()
    {
        if (definition == null) return;

        string rankStr = definition.rank.ToString();
        string label   = $"[{rankStr}] {definition.dungeonName}\nLv.{definition.recommendedLevel}+";

        WorldFloatingText.Spawn(
            transform.position + Vector3.up * 1.2f,
            label,
            definition.RankColour(),
            fontSize: 18);
    }

    private void ShowPrompt(bool show)
    {
        if (!show || definition == null) return;

        string rec = definition.RecommendedPartySize() == 1 ? "Solo" : $"Party {definition.RecommendedPartySize()}";
        SystemNotifier.Instance?.Announce(
            $"[{definition.rank}-Rank] {definition.dungeonName} — Press E to Enter ({rec}, Lv.{definition.recommendedLevel}+)",
            SystemNotifier.MessageType.Info);
    }

    // -------------------------------------------------------------------------
    // Gizmos (editor visualisation)
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, interactRange);
    }
#endif
}
