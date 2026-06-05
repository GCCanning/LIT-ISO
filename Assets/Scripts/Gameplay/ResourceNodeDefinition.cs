using UnityEngine;

/// One possible drop from a resource node.
[System.Serializable]
public struct ItemDrop
{
    [Tooltip("What item is dropped.")]
    public ItemDefinition item;

    [Min(1)]
    public int minAmount;

    [Min(1)]
    public int maxAmount;

    [Range(0f, 1f)]
    [Tooltip("Independent roll per harvest. 1.0 = always drops.")]
    public float chance;
}

/// <summary>
/// Defines a harvestable world object (tree, rock, etc.).
/// Create via Assets > Iso World > Resource Node Definition.
/// </summary>
[CreateAssetMenu(menuName = "Iso World/Resource Node Definition", fileName = "ResourceNodeDefinition")]
public class ResourceNodeDefinition : ScriptableObject
{
    [Header("Identity")]
    public string nodeName = "Node";

    [Header("Visuals")]
    [Tooltip("Sprite variants for the alive state. One is picked deterministically per world position.")]
    public Sprite[] nodeSprites;

    [Tooltip("Sprite shown after harvesting. Set to null to hide the renderer entirely until respawn.")]
    public Sprite harvestedSprite;

    [Header("Drops")]
    public ItemDrop[] drops;

    [Header("Behaviour")]
    [Min(1f)]
    [Tooltip("Seconds before the node re-enables after being harvested.")]
    public float harvestCooldown = 30f;

    [Range(0f, 1f)]
    [Tooltip("Per-tile probability that an eligible flat cell spawns this node during chunk generation.")]
    public float spawnChance = 0.08f;

    [Tooltip("World-space radius within which the player can trigger a harvest.")]
    public float harvestRadius = 1.2f;

    [Tooltip("Minimum world-space distance from another node of the same type (prevents clustering).")]
    public float minimumSpacing = 2f;

    [Header("Audio")]
    [Tooltip("Short SFX played on harvest. Leave null for no sound.")]
    public AudioClip harvestSound;

    [Range(0f, 1f)]
    public float harvestSoundVolume = 0.8f;
}
