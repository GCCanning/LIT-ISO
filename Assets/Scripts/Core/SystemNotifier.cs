using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "System" — the signature LitRPG mechanic.
/// Queues and broadcasts announcement messages (level-ups, class assignments,
/// dungeon clears, world events) to any listener — typically SystemMessageUI.
///
/// Add as a component on a persistent Managers GameObject.
///
/// Usage:
///   SystemNotifier.Instance.Announce("Level Up! You are now Level 5.", MessageType.LevelUp);
///
/// To add a new message type: extend the MessageType enum — no other changes required.
/// </summary>
public class SystemNotifier : MonoBehaviour
{
    public static SystemNotifier Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Message types — extend freely
    // -------------------------------------------------------------------------

    public enum MessageType
    {
        Info,          // General system info (white)
        LevelUp,       // Level-up notification (cyan)
        ClassAssign,   // Class assigned (gold)
        DungeonClear,  // Dungeon floor/boss cleared (green)
        WorldEvent,    // World boss spawn, Blood Moon, invasion (red/orange)
        Warning,       // Low HP, debuff, danger (yellow)
        Achievement,   // Title earned, achievement unlocked (purple)
        QuestNew,      // New quest started (soft purple)
        QuestComplete, // Quest completed (gold)
    }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired when a message is enqueued.
    /// SystemMessageUI subscribes to this and handles display.
    /// </summary>
    public static event Action<string, MessageType> OnMessage;

    /// <summary>
    /// Fired for world-wide announcements (first dungeon clears, world bosses, etc.).
    /// These should be broadcast to all connected players.
    /// </summary>
    public static event Action<string> OnWorldAnnouncement;

    // -------------------------------------------------------------------------
    // Queue (for burst logging — prevents overlap)
    // -------------------------------------------------------------------------

    private readonly Queue<(string text, MessageType type)> queue = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queue a System message. It will be displayed in order by SystemMessageUI.
    /// </summary>
    public void Announce(string message, MessageType type = MessageType.Info)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        queue.Enqueue((message, type));
        OnMessage?.Invoke(message, type);

        Debug.Log($"[SYSTEM] [{type}] {message}");
    }

    /// <summary>
    /// Broadcast a world-wide announcement (all players see it).
    /// Internally also calls Announce() so local player sees it too.
    /// </summary>
    public void AnnounceWorld(string message)
    {
        Announce(message, MessageType.WorldEvent);
        OnWorldAnnouncement?.Invoke(message);
    }

    // -------------------------------------------------------------------------
    // Convenience wrappers (keep call sites clean)
    // -------------------------------------------------------------------------

    public void AnnounceLevelUp(int newLevel) =>
        Announce($"Level Up! You are now Level {newLevel}.", MessageType.LevelUp);

    public void AnnounceClassAssigned(string className, string rarity) =>
        Announce($"Class Assigned: {className} ({rarity})", MessageType.ClassAssign);

    public void AnnounceDungeonClear(string dungeonName, string rank) =>
        Announce($"Dungeon Cleared: {dungeonName} (Rank {rank})", MessageType.DungeonClear);

    public void AnnounceWorldBoss(string bossName) =>
        AnnounceWorld($"World Boss Awakened: {bossName} — All players beware!");

    public void AnnounceBloodMoon() =>
        AnnounceWorld("Blood Moon rises. All enemies are empowered. XP doubled.");

    public void AnnounceFirstClear(string playerName, string dungeonName, string rank) =>
        AnnounceWorld($"[WORLD FIRST] {playerName} has cleared {dungeonName} (Rank {rank})!");

    public void AnnounceTitle(string titleName) =>
        Announce($"Title Earned: [{titleName}]", MessageType.Achievement);
}
