using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    [Serializable]
    public struct FoundationDungeonDecoration
    {
        public const string ExitPortalSpriteKey = "portal_exit";

        public string spriteKey;
        public int x;
        public int y;
        public float heightUnits;
        public float yOffset;
    }

    [Serializable]
    public struct FoundationDungeonMobSpawn
    {
        public string mobId;
        public int x;
        public int y;
        public int level;
    }

    public enum FoundationDungeonRoomKind
    {
        Spawn,
        Combat,
        Arena,
        Junction,
        Exit,
    }

    [Serializable]
    public struct FoundationDungeonRoomMarker
    {
        public FoundationDungeonRoomKind kind;
        public string label;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    public sealed class FoundationDungeonBuild
    {
        public string instanceId;
        public string dungeonId;
        public string displayName;
        public int tier;
        public int layoutSeed;
        public Vector2Int renderMin;
        public Vector2Int renderMax;
        public Vector2Int spawnCell;
        public Vector2Int exitCell;
        public Vector2Int[] renderCells = Array.Empty<Vector2Int>();
        public FoundationSavedCell[] cells = Array.Empty<FoundationSavedCell>();
        public FoundationDungeonDecoration[] decorations = Array.Empty<FoundationDungeonDecoration>();
        public FoundationDungeonMobSpawn[] mobs = Array.Empty<FoundationDungeonMobSpawn>();
        public FoundationDungeonRoomMarker[] roomMarkers = Array.Empty<FoundationDungeonRoomMarker>();
    }
}
