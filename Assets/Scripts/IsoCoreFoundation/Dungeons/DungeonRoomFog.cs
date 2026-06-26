using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// DISABLED (owner request, 2026-06-13): dungeon rooms are no longer fogged.
    /// <see cref="Init"/> is now a no-op so this component renders nothing; the
    /// original per-room dark-overlay implementation is kept below (dead code,
    /// CS0162-suppressed) in case the feature returns.
    ///
    /// Original behaviour: dark overlay over dungeon rooms the player hasn't entered
    /// yet. Built from <see cref="FoundationDungeonRoomMarker"/>s: every non-spawn room
    /// (combat/arena/exit/junction) got a black iso-footprint quad covering its cell
    /// bounds, which faded out once the player's current cell entered the room's
    /// bounds (with a 1-cell margin so it lifted right at the doorway).
    /// Created/destroyed alongside the dungeon instance by FoundationInstanceSystem.
    /// </summary>
    public class DungeonRoomFog : MonoBehaviour
    {
        const float FadeSpeed = 2.0f;   // alpha units/sec
        const float MaxAlpha = 0.92f;
        const int FogSortingOrder = 20000; // above all floor/wall/prop/particle sorting

        sealed class FogRoom
        {
            public RectInt cellBounds; // world cell coords, expanded by margin
            public MeshRenderer renderer;
            public Material material;
            public float alpha = MaxAlpha;
            public bool fading;
            public bool done;
        }

        IsoFoundationPlayer _player;
        readonly List<FogRoom> _rooms = new();

        public void Init(IsoFoundationPlayer player, FoundationDungeonRoomMarker[] markers)
        {
            _player = player;

            // 2026-06-13 (owner request): the per-room dark overlay was removed —
            // dungeons are no longer fogged. Left as a no-op (rather than deleting the
            // component/call sites) so it can be re-enabled later by restoring the loop
            // below. FoundationInstanceSystem still calls Init()/Update()/OnDestroy()
            // safely with zero rooms.
            return;

#pragma warning disable CS0162 // unreachable code kept intentionally for re-enable
            if (markers == null) return;

            foreach (var m in markers)
            {
                if (m.kind == FoundationDungeonRoomKind.Spawn) continue;

                int w = Mathf.Max(1, m.width);
                int h = Mathf.Max(1, m.height);
                int minX = m.x - w / 2;
                int minY = m.y - h / 2;

                var go = new GameObject($"DungeonFog_{m.label}_{m.x}_{m.y}");
                go.transform.SetParent(transform, false);
                go.transform.position = IsoGrid.CellToWorld(minX, minY, 0);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildFootprintMesh(w, h);

                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Sprites/Default")) { name = "DungeonFogMat" };
                mat.color = new Color(0f, 0f, 0f, MaxAlpha);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sortingOrder = FogSortingOrder;

                // 1-cell margin so the fog lifts as soon as the player steps through
                // the doorway, not only once fully inside the room.
                var bounds = new RectInt(minX - 1, minY - 1, w + 2, h + 2);
                _rooms.Add(new FogRoom { cellBounds = bounds, renderer = mr, material = mat });
            }
#pragma warning restore CS0162
        }

        /// <summary>
        /// Builds a quad matching the iso footprint of a w x h cell room: the
        /// parallelogram spanned by the cell-grid x/y basis vectors
        /// (TileHalfW, TileHalfH) and (-TileHalfW, TileHalfH).
        /// </summary>
        static Mesh BuildFootprintMesh(int w, int h)
        {
            float hw = IsoGrid.TileHalfW;
            float hh = IsoGrid.TileHalfH;
            Vector3 v00 = Vector3.zero;
            Vector3 v10 = new Vector3(w * hw, w * hh, 0f);
            Vector3 v01 = new Vector3(-h * hw, h * hh, 0f);
            Vector3 v11 = new Vector3((w - h) * hw, (w + h) * hh, 0f);

            var mesh = new Mesh { name = "DungeonFogQuad" };
            mesh.vertices = new[] { v00, v10, v11, v01 };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if (_player == null || _rooms.Count == 0) return;
            var cell = _player.CurrentCell;
            float dt = Time.deltaTime;

            for (int i = 0; i < _rooms.Count; i++)
            {
                var r = _rooms[i];
                if (r.done) continue;

                if (!r.fading && r.cellBounds.Contains(cell))
                    r.fading = true;

                if (!r.fading) continue;

                r.alpha = Mathf.Max(0f, r.alpha - FadeSpeed * dt);
                var c = r.material.color;
                c.a = r.alpha;
                r.material.color = c;

                if (r.alpha <= 0f)
                {
                    r.renderer.enabled = false;
                    r.done = true;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var r in _rooms)
                if (r.material != null) Destroy(r.material);
        }
    }
}
