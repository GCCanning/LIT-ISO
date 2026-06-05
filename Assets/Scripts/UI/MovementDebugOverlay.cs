using UnityEngine;
using UnityEngine.UI;
using EthraClone.TrialWeek;

/// <summary>
/// Lightweight runtime overlay for checking isometric movement, height, and blocking state.
/// Visibility is controlled from GameSettingsMenu's Debug tab.
/// </summary>
public class MovementDebugOverlay : MonoBehaviour
{
    private Text label;
    private IsoPlayerController player;
    private IsoWorldChunkManager world;

    private void Awake()
    {
        BuildLabel();
    }

    private void Update()
    {
        if (!GameSettingsMenu.ShowDebugOverlay)
        {
            if (label != null) label.enabled = false;
            return;
        }

        if (label != null) label.enabled = true;
        if (player == null) player = FindFirstObjectByType<IsoPlayerController>();
        if (world == null) world = FindFirstObjectByType<IsoWorldChunkManager>();

        if (player == null || world == null)
        {
            label.text = "Debug\nPlayer/world missing";
            return;
        }

        Vector3 position = player.transform.position;
        IsoWorldChunkManager.GroundCellSample footSample = world.SampleWorldPosition(position);
        Vector3Int selected = player.selectedCell;
        IsoWorldChunkManager.GroundCellSample selectedSample = world.SampleGroundCell(selected);
        string biomeName = footSample.Biome != null
            ? footSample.Biome.name
            : "None";
        int stepDelta = selectedSample.Height - footSample.Height;

        label.text =
            "Debug\n" +
            $"Pos: {position.x:0.00}, {position.y:0.00}, {position.z:0.00}\n" +
            $"Foot: {footSample.Cell.x}, {footSample.Cell.y}, h{footSample.Height} edge={footSample.IsHeightEdge} transition={footSample.IsTransitionCell}\n" +
            $"Selected: {selectedSample.Cell.x}, {selectedSample.Cell.y}, h{selectedSample.Height} edge={selectedSample.IsHeightEdge} transition={selectedSample.IsTransitionCell}\n" +
            $"Step Delta: {stepDelta:+#;-#;0}\n" +
            $"Biome: {biomeName} | PlayerH: {player.CurrentGroundHeight}\n" +
            $"Blocked: {player.LastBlockedReason}";
    }

    private void BuildLabel()
    {
        GameObject go = new GameObject("MovementDebugOverlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -14f);
        rect.sizeDelta = new Vector2(680f, 180f);

        label = go.AddComponent<Text>();
        label.color = new Color(1f, 0.92f, 0.64f, 0.95f);
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        LitIsoFont.Apply(label, 13, FontStyle.Bold);
        label.enabled = false;
    }
}
