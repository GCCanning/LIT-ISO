namespace IsoCore.Foundation
{
    /// <summary>
    /// Per-cell world data — the single source of truth for height, biome, surface
    /// block, occupancy and blocking. Render reads from this; it never reads back.
    /// Mutable value type stored in IsoChunk's array (mutated via array indexing).
    /// </summary>
    public struct IsoCell
    {
        public byte Height;
        public byte BiomeIndex;
        public string SurfaceBlockId;

        public string OccupantId;   // placeable occupying this cell (null == none)
        public string NodeId;       // resource node on this cell (null == none)

        // Collision components — kept separate so clearing one recomputes correctly.
        public bool SolidBlock;     // surface block is solid (e.g. placed stone block)
        public bool Water;
        public bool OccupantBlocks;
        public bool NodeBlocks;

        // Remembered surface/height beneath a placed solid block, so removal restores it.
        public string UnderBlockId;
        public byte UnderHeight;

        public bool Modified;       // changed from sampler output (save delta)

        public bool Blocked => SolidBlock || Water || OccupantBlocks || NodeBlocks;
        public bool HasOccupant => !string.IsNullOrEmpty(OccupantId);
        public bool HasNode => !string.IsNullOrEmpty(NodeId);
    }
}
