using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Soft death + respawn (audit rec #3). Watches FoundationPlayerStats; when Health
    /// reaches 0 the screen fades to black, any active dungeon/interior is abandoned,
    /// and the player wakes beside their nearest placed campfire (or the spawn clearing
    /// when none exists) with half Health restored. No items are lost.
    /// FoundationPlayerStats.RecalculateVitals only clamps and never revives (fix #23),
    /// so this system's explicit Heal is the single path back from 0 HP.
    /// </summary>
    public sealed class FoundationDeathSystem : MonoBehaviour
    {
        IsoFoundationPlayer _player;
        IsoWorld _world;
        FoundationProgression _progression;
        PlacementSystem _placement;
        FoundationInstanceSystem _instances;
        FoundationDungeonPortalSystem _dungeonPortals;
        FoundationInteractionOverlay _overlay;

        bool _respawning;
        CanvasGroup _fade;

        const float RespawnHealthFraction = 0.5f;
        const float FadeSeconds = 0.7f;
        const float HoldSeconds = 0.8f;

        public void Init(IsoFoundationPlayer player, IsoWorld world, FoundationProgression progression,
            PlacementSystem placement, FoundationInstanceSystem instances,
            FoundationDungeonPortalSystem dungeonPortals, FoundationInteractionOverlay overlay)
        {
            _player = player;
            _world = world;
            _progression = progression;
            _placement = placement;
            _instances = instances;
            _dungeonPortals = dungeonPortals;
            _overlay = overlay;

            if (_progression?.Stats != null)
                _progression.Stats.Changed += OnStatsChanged;
        }

        void OnDestroy()
        {
            if (_progression?.Stats != null)
                _progression.Stats.Changed -= OnStatsChanged;
        }

        // ---- "I'm stuck" rescue (owner, 2026-07-02) --------------------------------
        // Worldgen can still produce walkable pockets (deep pits, over-wide water) the
        // jump cannot escape. Holding F9 for 1.5 s carries the player to the same wake
        // point the death system uses (beside the nearest campfire, else the spawn
        // clearing). No penalty — being trapped by the map is never the player's fault.
        float _rescueHold;

        void Update()
        {
            if (_respawning || _player == null) return;

            if (Input.GetKey(KeyCode.F9))
            {
                if (Input.GetKeyDown(KeyCode.F9))
                    _overlay?.Flash("Hold F9 to be carried to safety…", 1.4f);
                _rescueHold += Time.deltaTime;
                if (_rescueHold >= 1.5f)
                {
                    _rescueHold = -3f; // debounce so a continued hold doesn't re-fire
                    if (_dungeonPortals != null && _dungeonPortals.IsActiveDungeon)
                        _dungeonPortals.AbandonAndExit();
                    else if (_instances != null && _instances.IsInsideInstance)
                        _instances.Exit();
                    _player.SetGround(WakePoint());
                    _progression?.SystemFeed.Queue(SystemMessageChannel.Notice,
                        "The System untangles the world around you.", "rescue", 2);
                    _overlay?.Flash("Carried to safety.", 3f);
                }
            }
            else
            {
                _rescueHold = _rescueHold < 0f ? Mathf.Min(0f, _rescueHold + Time.deltaTime) : 0f;
            }
        }

        void OnStatsChanged()
        {
            var stats = _progression?.Stats;
            if (_respawning || stats == null || stats.Health > 0f)
                return;

            _respawning = true;
            FoundationUiBridge.RequestDeathScreen();
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            var stats = _progression.Stats;
            _progression.SystemFeed.Queue(SystemMessageChannel.Warning, "You collapse...", "death", 3);
            SfxManager.Play("hit", 0.9f);

            EnsureFade();
            yield return FadeTo(1f, FadeSeconds);

            // Leave any dungeon/interior first (abandon — no completion or reward),
            // so the wake point is computed in the overworld.
            if (_dungeonPortals != null && _dungeonPortals.IsActiveDungeon)
                _dungeonPortals.AbandonAndExit();
            else if (_instances != null && _instances.IsInsideInstance)
                _instances.Exit();

            _player?.SetGround(WakePoint());

            // Explicit revive: half of max Health. Heal clamps at MaxHealth, so this is
            // safe even if camp recovery already nudged Health above zero mid-fade.
            float target = stats.MaxHealth * RespawnHealthFraction;
            if (stats.Health < target)
                stats.Heal(target - stats.Health);

            _progression.SystemFeed.Queue(SystemMessageChannel.Notice,
                "You collapse... you wake at camp.", "death", 2);
            _overlay?.Flash("You collapse... you wake at camp.", 4f);

            yield return new WaitForSeconds(HoldSeconds);
            yield return FadeTo(0f, FadeSeconds);
            _respawning = false;
        }

        /// <summary>
        /// Wake position: a walkable cell beside the nearest placed campfire, else the
        /// spawn clearing center (world origin — always flat and walkable by the sampler).
        /// </summary>
        Vector2 WakePoint()
        {
            if (_placement != null && _player != null &&
                _placement.TryFindNearestCampsite(_player.Ground, out var camp))
            {
                // Ring search around the camp cell for the closest walkable neighbour
                // (the campfire's own cell may be occupied by the placeable).
                for (int r = 0; r <= 3; r++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int nx = camp.Wx + dx, ny = camp.Wy + dy;
                        if (_world == null || !_world.IsWalkable(nx, ny)) continue;
                        var g = IsoGrid.CellToWorld(nx, ny, 0);
                        return new Vector2(g.x, g.y);
                    }
                }
            }

            return Vector2.zero; // spawn clearing center
        }

        // ---- minimal full-screen fade (self-contained; no UiBuilder dependency) ----

        void EnsureFade()
        {
            if (_fade != null) return;

            var go = new GameObject("DeathFadeCanvas", typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // above panels/overlays

            var imgGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var rect = imgGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = imgGo.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false; // never block clicks; world input isn't gated here

            _fade = go.GetComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            _fade.interactable = false;
        }

        IEnumerator FadeTo(float targetAlpha, float seconds)
        {
            if (_fade == null) yield break;
            float start = _fade.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                _fade.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
                yield return null;
            }
            _fade.alpha = targetAlpha;
        }
    }
}
