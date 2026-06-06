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

        // Frame-amortized streaming: when the player crosses a chunk boundary we compute
        // the newly-needed cells and reveal them a bounded number per frame, so streaming
        // never spikes a frame. The wide view radius gives a big off-screen margin, so the
        // queue drains long before the player can reach the unloaded edge.
        readonly HashSet<long> _desired = new();
        readonly List<Vector2Int> _showQueue = new();
        int _showCursor;
        const int StreamCellsPerFrame = 110;

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
            Retarget(PlayerChunk());
            DrainStreaming(int.MaxValue); // full build once at load (behind the scene load)
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
            if (pc != _lastChunk) Retarget(pc);
            DrainStreaming(StreamCellsPerFrame); // bounded reveal each frame
        }

        /// <summary>
        /// Player crossed into a new chunk: recompute the desired view, hide/despawn what
        /// left it, and queue the newly-needed cells (nearest-first) to be revealed over the
        /// next few frames. Cheap work (hide + despawn) happens now; the expensive reveal is
        /// amortized in DrainStreaming so no single frame spikes.
        /// </summary>
        void Retarget(Vector2Int center)
        {
            _lastChunk = center;
            int r = Config.viewRadiusChunks, s = World.ChunkSize;
            int minX = (center.x - r) * s, maxX = (center.x + r + 1) * s - 1;
            int minY = (center.y - r) * s, maxY = (center.y + r + 1) * s - 1;

            // Ensure chunk terrain exists (sampled once per chunk; cell reads are cached).
            for (int cy = center.y - r; cy <= center.y + r; cy++)
            for (int cx = center.x - r; cx <= center.x + r; cx++)
                World.GetOrCreateChunk(cx, cy);

            // Rebuild the desired-cell set.
            _desired.Clear();
            for (int wy = minY; wy <= maxY; wy++)
            for (int wx = minX; wx <= maxX; wx++)
                _desired.Add(Key(wx, wy));

            // Hide tiles + despawn nodes that left the view (cheap, done immediately).
            _renderer.RetainOnly(_desired);
            _despawn.Clear();
            foreach (var kv in _nodes)
                if (!_desired.Contains(kv.Key)) _despawn.Add(kv.Key);
            foreach (var k in _despawn)
            {
                if (_nodes[k]) Destroy(_nodes[k].gameObject);
                _nodes.Remove(k);
            }

            // Queue cells that still need revealing, nearest to the player first so the
            // on-screen area fills before the off-screen margin.
            Vector2Int p = _isoPlayer != null ? _isoPlayer.CurrentCell
                                              : IsoGrid.WorldToCell(_player.position);
            _showQueue.Clear();
            for (int wy = minY; wy <= maxY; wy++)
            for (int wx = minX; wx <= maxX; wx++)
                if (!_renderer.IsShown(wx, wy)) _showQueue.Add(new Vector2Int(wx, wy));
            _showQueue.Sort((a, b) =>
            {
                int da = (a.x - p.x) * (a.x - p.x) + (a.y - p.y) * (a.y - p.y);
                int db = (b.x - p.x) * (b.x - p.x) + (b.y - p.y) * (b.y - p.y);
                return da.CompareTo(db);
            });
            _showCursor = 0;
        }

        /// <summary>Reveals up to <paramref name="budget"/> queued cells (and their nodes).</summary>
        void DrainStreaming(int budget)
        {
            if (_showCursor >= _showQueue.Count) return;
            int end = (budget == int.MaxValue) ? _showQueue.Count
                                               : Mathf.Min(_showCursor + budget, _showQueue.Count);
            for (; _showCursor < end; _showCursor++)
            {
                var c = _showQueue[_showCursor];
                if (_renderer.ShowCell(World, c.x, c.y))
                {
                    var cell = World.GetCell(c.x, c.y);
                    if (cell.HasNode)
                    {
                        long nk = Key(c.x, c.y);
                        if (!_nodes.ContainsKey(nk)) SpawnNode(c.x, c.y, cell.NodeId);
                    }
                }
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
            // Fade the prop out when it would hide the player behind it (e.g. walking
            // behind a tree), preserving the depth illusion without losing the player.
            go.AddComponent<PropOcclusionFader>();
            // Cast a sun/moon-driven shadow onto the ground.
            go.AddComponent<DecorationShadow>();
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
