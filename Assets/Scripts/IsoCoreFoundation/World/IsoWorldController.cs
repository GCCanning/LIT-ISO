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
        FoundationInstanceSystem _instances;
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
        readonly List<Vector2Int> _instanceRenderCells = new();
        int _showCursor;
        const int StreamCellsPerFrame = 720;

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

        public void SetInstanceSystem(FoundationInstanceSystem instances)
        {
            if (_instances != null)
            {
                _instances.Entered -= HandleInstanceChanged;
                _instances.Exited -= HandleInstanceChanged;
            }

            _instances = instances;
            if (_instances != null)
            {
                _instances.Entered += HandleInstanceChanged;
                _instances.Exited += HandleInstanceChanged;
            }
        }

        void OnDestroy()
        {
            if (World != null) World.OnCellChanged -= HandleCellChanged;
            if (_instances != null)
            {
                _instances.Entered -= HandleInstanceChanged;
                _instances.Exited -= HandleInstanceChanged;
            }
        }

        void HandleInstanceChanged(string id, string display)
        {
            if (!_ready) return;
            Retarget(PlayerChunk());
            DrainStreaming(int.MaxValue);
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
            int minX;
            int maxX;
            int minY;
            int maxY;
            bool explicitInstanceCells = false;

            if (_instances != null && _instances.IsInsideInstance)
            {
                explicitInstanceCells = _instances.CopyActiveRenderCells(_instanceRenderCells);
                if (explicitInstanceCells)
                {
                    minX = int.MaxValue; maxX = int.MinValue;
                    minY = int.MaxValue; maxY = int.MinValue;
                    foreach (var c in _instanceRenderCells)
                    {
                        minX = Mathf.Min(minX, c.x);
                        maxX = Mathf.Max(maxX, c.x);
                        minY = Mathf.Min(minY, c.y);
                        maxY = Mathf.Max(maxY, c.y);
                        World.GetCell(c.x, c.y);
                    }
                }
                else
                {
                    var min = _instances.ActiveRenderMin;
                    var max = _instances.ActiveRenderMax;
                    minX = min.x; maxX = max.x;
                    minY = min.y; maxY = max.y;

                    for (int wy = minY; wy <= maxY; wy++)
                    for (int wx = minX; wx <= maxX; wx++)
                        World.GetCell(wx, wy);
                }
            }
            else
            {
                int r = Config.viewRadiusChunks, s = World.ChunkSize;
                minX = (center.x - r) * s; maxX = (center.x + r + 1) * s - 1;
                minY = (center.y - r) * s; maxY = (center.y + r + 1) * s - 1;

                // Ensure chunk terrain exists (sampled once per chunk; cell reads are cached).
                for (int cy = center.y - r; cy <= center.y + r; cy++)
                for (int cx = center.x - r; cx <= center.x + r; cx++)
                    World.GetOrCreateChunk(cx, cy);
            }

            // Rebuild the desired-cell set.
            _desired.Clear();
            if (explicitInstanceCells)
            {
                foreach (var c in _instanceRenderCells)
                    _desired.Add(Key(c.x, c.y));
            }
            else
            {
                for (int wy = minY; wy <= maxY; wy++)
                for (int wx = minX; wx <= maxX; wx++)
                    _desired.Add(Key(wx, wy));
            }

            // Hide tiles + despawn nodes that left the view (cheap, done immediately).
            _renderer.RetainOnly(_desired);
            _despawn.Clear();
            foreach (var kv in _nodes)
                if (!_desired.Contains(kv.Key)) _despawn.Add(kv.Key);
            foreach (var k in _despawn)
            {
                if (_nodes[k]) DestroyNodeObject(_nodes[k].gameObject);
                _nodes.Remove(k);
            }

            // Queue cells that still need revealing, nearest to the player first so the
            // on-screen area fills before the off-screen margin.
            Vector2Int p = _isoPlayer != null ? _isoPlayer.CurrentCell
                                              : IsoGrid.WorldToCell(_player.position);
            _showQueue.Clear();
            if (explicitInstanceCells)
            {
                foreach (var c in _instanceRenderCells)
                    if (!_renderer.IsShown(c.x, c.y)) _showQueue.Add(c);
            }
            else
            {
                for (int wy = minY; wy <= maxY; wy++)
                for (int wx = minX; wx <= maxX; wx++)
                    if (!_renderer.IsShown(wx, wy)) _showQueue.Add(new Vector2Int(wx, wy));
            }
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
            // Ground it, fade it when it hides the player, and cast a sun/moon shadow.
            FoundationDepthPolish.Attach(go, fadeWhenOccluding: true, castLongShadow: true,
                contactScale: 1f, contactAlpha: 0.30f);
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

        public ResourceNode NodeAtCell(int wx, int wy)
        {
            _nodes.TryGetValue(Key(wx, wy), out var node);
            return node;
        }

        public bool TryGetNodeUnderCursor(Camera cam, Vector2 screenPosition, out ResourceNode node)
        {
            node = null;
            if (cam == null || _nodes.Count == 0)
                return false;

            var wp = cam.ScreenToWorldPoint(screenPosition);
            var point = new Vector2(wp.x, wp.y);
            int bestOrder = int.MinValue;
            foreach (var candidate in _nodes.Values)
            {
                if (candidate == null || !candidate.ContainsWorldPoint(point))
                    continue;

                int order = candidate.SortingOrder;
                if (node != null && order < bestOrder)
                    continue;

                node = candidate;
                bestOrder = order;
            }

            return node != null;
        }

        void HandleCellChanged(int wx, int wy)
        {
            _renderer.RefreshCell(World, wx, wy);
            long nk = Key(wx, wy);
            var cell = World.GetCell(wx, wy);
            if (!cell.HasNode && _nodes.TryGetValue(nk, out var node))
            {
                if (node) DestroyNodeObject(node.gameObject);
                _nodes.Remove(nk);
            }
        }

        static void DestroyNodeObject(GameObject go)
        {
            if (!go) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
