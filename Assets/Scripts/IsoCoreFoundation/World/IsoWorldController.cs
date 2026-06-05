using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Streams chunks within viewRadius of the player, drives the ground renderer,
    /// and spawns/despawns resource-node objects for resident chunks. Only re-streams
    /// when the player crosses a chunk boundary (bounded per-frame work).
    /// </summary>
    public class IsoWorldController : MonoBehaviour
    {
        public IsoWorld World { get; private set; }
        public FoundationContent Content { get; private set; }
        public FoundationConfig Config { get; private set; }

        IsoWorldRenderer _renderer;
        Transform _player;
        IsoFoundationPlayer _isoPlayer;
        Transform _nodeParent;

        readonly Dictionary<long, ResourceNode> _nodes = new();
        readonly List<long> _despawn = new();
        Vector2Int _lastChunk = new(int.MinValue, int.MinValue);
        bool _ready;

        static long Key(int wx, int wy) => ((long)(uint)wx << 32) | (uint)wy;

        public void Init(IsoWorld world, FoundationContent content, FoundationConfig cfg, Transform player)
        {
            World = world; Content = content; Config = cfg; _player = player;
            _isoPlayer = player.GetComponent<IsoFoundationPlayer>();

            var renderRoot = new GameObject("GroundPool").transform;
            renderRoot.SetParent(transform, false);
            _renderer = new IsoWorldRenderer(renderRoot, content);

            _nodeParent = new GameObject("Nodes").transform;
            _nodeParent.SetParent(transform, false);

            World.OnCellChanged += HandleCellChanged;
            _ready = true;
            Restream(PlayerChunk());
        }

        void OnDestroy()
        {
            if (World != null) World.OnCellChanged -= HandleCellChanged;
        }

        Vector2Int PlayerChunk()
        {
            // Use the player's height-0 cell, NOT the height-lifted transform position
            // (WorldToCell assumes the height-0 plane; a lift mis-centers streaming).
            var cell = _isoPlayer != null ? _isoPlayer.CurrentCell : IsoGrid.WorldToCell(_player.position);
            return new Vector2Int(World.ChunkCoord(cell.x), World.ChunkCoord(cell.y));
        }

        void Update()
        {
            if (!_ready) return;
            var pc = PlayerChunk();
            if (pc != _lastChunk) Restream(pc);
        }

        void Restream(Vector2Int center)
        {
            _lastChunk = center;
            int r = Config.viewRadiusChunks;
            int s = World.ChunkSize;
            var cells = new List<Vector2Int>((2 * r + 1) * (2 * r + 1) * s * s);
            var residentNodes = new HashSet<long>();

            for (int cy = center.y - r; cy <= center.y + r; cy++)
            for (int cx = center.x - r; cx <= center.x + r; cx++)
            {
                World.GetOrCreateChunk(cx, cy);
                int baseX = cx * s, baseY = cy * s;
                for (int ly = 0; ly < s; ly++)
                for (int lx = 0; lx < s; lx++)
                {
                    int wx = baseX + lx, wy = baseY + ly;
                    cells.Add(new Vector2Int(wx, wy));
                    var cell = World.GetCell(wx, wy);
                    if (cell.HasNode)
                    {
                        long nk = Key(wx, wy);
                        residentNodes.Add(nk);
                        if (!_nodes.ContainsKey(nk)) SpawnNode(wx, wy, cell.NodeId);
                    }
                }
            }

            _renderer.SetVisible(World, cells);

            _despawn.Clear();
            foreach (var kv in _nodes)
                if (!residentNodes.Contains(kv.Key)) _despawn.Add(kv.Key);
            foreach (var k in _despawn)
            {
                if (_nodes[k]) Destroy(_nodes[k].gameObject);
                _nodes.Remove(k);
            }
        }

        void SpawnNode(int wx, int wy, string nodeId)
        {
            var def = Content.Nodes.Get(nodeId);
            if (def == null) return;
            var go = new GameObject($"Node_{nodeId}_{wx}_{wy}");
            go.transform.SetParent(_nodeParent, false);
            var node = go.AddComponent<ResourceNode>();
            node.Init(def, World, wx, wy);
            _nodes[Key(wx, wy)] = node;
        }

        /// <summary>Nearest active resource node to a world position within range.</summary>
        public ResourceNode NearestNode(Vector3 worldPos, float range)
        {
            ResourceNode best = null;
            float bd = range * range;
            foreach (var n in _nodes.Values)
            {
                if (!n) continue;
                float d = ((Vector2)(n.transform.position - worldPos)).sqrMagnitude;
                if (d <= bd) { bd = d; best = n; }
            }
            return best;
        }

        void HandleCellChanged(int wx, int wy)
        {
            _renderer.RefreshCell(World, wx, wy);
            long nk = Key(wx, wy);
            var cell = World.GetCell(wx, wy);
            if (!cell.HasNode && _nodes.TryGetValue(nk, out var node))
            {
                if (node) Destroy(node.gameObject);
                _nodes.Remove(nk);
            }
        }
    }
}
