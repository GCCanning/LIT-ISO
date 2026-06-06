using System;
using System.Collections.Generic;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Owns the cached chunk grid and is the authoritative query/mutation API —
    /// the "ask the world, don't push physics" contract. Placement writes occupancy
    /// here so the player's movement query blocks against placed solids/objects.
    /// </summary>
    public class IsoWorld
    {
        readonly IsoTerrainSampler _sampler;
        readonly FoundationContent _content;
        readonly int _chunkSize;
        readonly Dictionary<long, IsoChunk> _chunks = new();

        public int ChunkSize => _chunkSize;
        public FoundationContent Content => _content;
        public IsoTerrainSampler Sampler => _sampler;

        /// <summary>Raised when a cell's render-relevant state changes (wx, wy).</summary>
        public event Action<int, int> OnCellChanged;

        public IsoWorld(IsoTerrainSampler sampler, FoundationContent content, int chunkSize)
        {
            _sampler = sampler; _content = content; _chunkSize = chunkSize;
        }

        // ---- coordinate helpers ----
        public int ChunkCoord(int w)
        {
            int q = w / _chunkSize;
            int r = w % _chunkSize;
            if (r != 0 && (r < 0)) q--;
            return q;
        }
        static long Key(int ccx, int ccy) => ((long)(uint)ccx << 32) | (uint)ccy;

        public IsoChunk GetOrCreateChunk(int ccx, int ccy)
        {
            long k = Key(ccx, ccy);
            if (_chunks.TryGetValue(k, out var chunk)) return chunk;

            chunk = new IsoChunk(ccx, ccy, _chunkSize);
            int baseX = ccx * _chunkSize, baseY = ccy * _chunkSize;
            for (int ly = 0; ly < _chunkSize; ly++)
            for (int lx = 0; lx < _chunkSize; lx++)
                chunk.Cells[chunk.Index(lx, ly)] = _sampler.Sample(baseX + lx, baseY + ly);

            _chunks.Add(k, chunk);
            return chunk;
        }

        public bool TryGetChunk(int ccx, int ccy, out IsoChunk chunk) =>
            _chunks.TryGetValue(Key(ccx, ccy), out chunk);

        bool Locate(int wx, int wy, out IsoChunk chunk, out int idx)
        {
            int ccx = ChunkCoord(wx), ccy = ChunkCoord(wy);
            chunk = GetOrCreateChunk(ccx, ccy);
            int lx = wx - ccx * _chunkSize;
            int ly = wy - ccy * _chunkSize;
            idx = chunk.Index(lx, ly);
            return true;
        }

        // ---- queries ----
        public IsoCell GetCell(int wx, int wy)
        {
            Locate(wx, wy, out var chunk, out int idx);
            return chunk.Cells[idx];
        }

        public int GetHeight(int wx, int wy) => GetCell(wx, wy).Height;
        public bool IsBlocked(int wx, int wy) => GetCell(wx, wy).Blocked;
        public bool IsWalkable(int wx, int wy) => !GetCell(wx, wy).Blocked;
        public int GetBiomeIndex(int wx, int wy) => GetCell(wx, wy).BiomeIndex;
        public BiomeDefinition GetBiome(int wx, int wy) => _sampler.BiomeAt(GetCell(wx, wy).BiomeIndex);

        // ---- mutations ----
        void Write(int wx, int wy, IsoCell cell)
        {
            Locate(wx, wy, out var chunk, out int idx);
            chunk.Cells[idx] = cell;
            OnCellChanged?.Invoke(wx, wy);
        }

        /// <summary>Place/replace the surface block. Fails on occupied/node/water cells.</summary>
        public bool TryPlaceBlock(int wx, int wy, BlockDefinition block)
        {
            if (block == null) return false;
            var cell = GetCell(wx, wy);
            if (cell.HasOccupant || cell.HasNode || cell.Water || cell.SolidBlock) return false;

            if (block.IsSolid)
            {
                // Stack: remember what was here, raise the column by one level.
                cell.UnderBlockId = cell.SurfaceBlockId;
                cell.UnderHeight = cell.Height;
                cell.SurfaceBlockId = block.id;
                cell.SolidBlock = true;
                cell.Height = (byte)System.Math.Min(cell.Height + 1, 7); // keep IsoWorld UnityEngine-free
            }
            else
            {
                // Floor/path: walkable surface, height unchanged.
                cell.SurfaceBlockId = block.id;
                cell.SolidBlock = false;
            }
            cell.Modified = true;
            Write(wx, wy, cell);
            return true;
        }

        /// <summary>Hoe a walkable ground cell into tilled soil (for planting).</summary>
        public bool TryTill(int wx, int wy)
        {
            var cell = GetCell(wx, wy);
            if (cell.HasOccupant || cell.HasNode || cell.Water || cell.SolidBlock) return false;
            if (cell.SurfaceBlockId == "soil") return false; // already tilled
            cell.SurfaceBlockId = "soil";
            cell.Modified = true;
            Write(wx, wy, cell);
            return true;
        }

        /// <summary>Remove a placed solid block, restoring the column beneath it.</summary>
        public bool RemoveSolidBlock(int wx, int wy)
        {
            var cell = GetCell(wx, wy);
            if (!cell.SolidBlock) return false;
            cell.SolidBlock = false;
            cell.Height = cell.UnderHeight;
            if (!string.IsNullOrEmpty(cell.UnderBlockId)) cell.SurfaceBlockId = cell.UnderBlockId;
            cell.UnderBlockId = null;
            cell.Modified = true;
            Write(wx, wy, cell);
            return true;
        }

        public bool TryPlaceOccupant(int wx, int wy, string placeableId, bool blocks)
        {
            var cell = GetCell(wx, wy);
            if (cell.Blocked || cell.HasOccupant || cell.HasNode) return false;

            cell.OccupantId = placeableId;
            cell.OccupantBlocks = blocks;
            cell.Modified = true;
            Write(wx, wy, cell);
            return true;
        }

        public bool ClearOccupant(int wx, int wy)
        {
            var cell = GetCell(wx, wy);
            if (!cell.HasOccupant) return false;
            cell.OccupantId = null;
            cell.OccupantBlocks = false;
            cell.Modified = true;
            Write(wx, wy, cell);
            return true;
        }

        public void ClearNode(int wx, int wy)
        {
            var cell = GetCell(wx, wy);
            if (!cell.HasNode) return;
            cell.NodeId = null;
            cell.NodeBlocks = false;
            cell.Modified = true;
            Write(wx, wy, cell);
        }

        /// <summary>Count of cells changed from sampler output (save-delta size).</summary>
        public int ModifiedCellCount()
        {
            int n = 0;
            foreach (var chunk in _chunks.Values)
                for (int i = 0; i < chunk.Cells.Length; i++)
                    if (chunk.Cells[i].Modified) n++;
            return n;
        }

        public FoundationSavedCell[] SnapshotModifiedCells()
        {
            var cells = new List<FoundationSavedCell>();
            foreach (var chunk in _chunks.Values)
            {
                int baseX = chunk.Cx * _chunkSize;
                int baseY = chunk.Cy * _chunkSize;
                for (int ly = 0; ly < _chunkSize; ly++)
                for (int lx = 0; lx < _chunkSize; lx++)
                {
                    var cell = chunk.Cells[chunk.Index(lx, ly)];
                    if (!cell.Modified) continue;
                    cells.Add(ToSavedCell(baseX + lx, baseY + ly, cell));
                }
            }
            return cells.ToArray();
        }

        public void RestoreModifiedCells(FoundationSavedCell[] cells)
        {
            if (cells == null) return;
            foreach (var saved in cells)
            {
                var cell = new IsoCell
                {
                    Height = saved.height,
                    BiomeIndex = saved.biomeIndex,
                    SurfaceBlockId = saved.surfaceBlockId,
                    OccupantId = saved.occupantId,
                    NodeId = saved.nodeId,
                    SolidBlock = saved.solidBlock,
                    Water = saved.water,
                    OccupantBlocks = saved.occupantBlocks,
                    NodeBlocks = saved.nodeBlocks,
                    UnderBlockId = saved.underBlockId,
                    UnderHeight = saved.underHeight,
                    Modified = true,
                };
                Write(saved.x, saved.y, cell);
            }
        }

        static FoundationSavedCell ToSavedCell(int wx, int wy, IsoCell cell)
        {
            return new FoundationSavedCell
            {
                x = wx,
                y = wy,
                height = cell.Height,
                biomeIndex = cell.BiomeIndex,
                surfaceBlockId = cell.SurfaceBlockId,
                occupantId = cell.OccupantId,
                nodeId = cell.NodeId,
                solidBlock = cell.SolidBlock,
                water = cell.Water,
                occupantBlocks = cell.OccupantBlocks,
                nodeBlocks = cell.NodeBlocks,
                underBlockId = cell.UnderBlockId,
                underHeight = cell.UnderHeight,
            };
        }
    }
}
