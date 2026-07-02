using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Tracks which biomes the player has personally set foot in and turns first
    /// visits into a discovery moment: a HUD toast, a Trial evidence entry
    /// ("biome_discovered") and Explore progression. Also publishes the live biome
    /// display name so the HUD phase band can show the real region instead of the
    /// design-sample placeholder.
    ///
    /// Reference-integration note (2026-07-02): adapts the "discovery turns
    /// exploration into progression" principle (Elacoria) through LIT-ISO's own
    /// Trial-scoring spine. Original code; no reference content.
    /// </summary>
    public sealed class FoundationBiomeDiscovery : MonoBehaviour
    {
        const float PollInterval = 0.6f;
        const string EvidenceId = "biome_discovered";

        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationProgression _progression;
        FoundationInteractionOverlay _overlay;
        FoundationInstanceSystem _instances;

        readonly HashSet<string> _discovered = new();
        string _lastBiomeId;
        float _pollTimer;

        /// <summary>Display name of the biome the player is standing in ("" until known).
        /// Read by the HUD clock cluster; null-safe static so the UI assembly never
        /// needs a scene lookup.</summary>
        public static string ActiveBiomeDisplay { get; private set; } = "";

        /// <summary>Raised when the player steps into a biome. bool = first discovery.</summary>
        public event Action<BiomeDefinition, bool> BiomeChanged;

        public int DiscoveredCount => _discovered.Count;

        public void Init(IsoWorld world, IsoFoundationPlayer player, FoundationProgression progression,
            FoundationInteractionOverlay overlay, FoundationInstanceSystem instances)
        {
            _world = world;
            _player = player;
            _progression = progression;
            _overlay = overlay;
            _instances = instances;
        }

        void OnDestroy()
        {
            ActiveBiomeDisplay = "";
        }

        void Update()
        {
            if (_world == null || _player == null) return;

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;

            // Pocket interiors / dungeons are not overworld biomes — freeze the readout.
            if (_instances != null && _instances.IsInsideInstance) return;

            var cell = _player.CurrentCell;
            var biome = _world.GetBiome(cell.x, cell.y);
            if (biome == null || string.IsNullOrEmpty(biome.id) || biome.id == _lastBiomeId) return;

            _lastBiomeId = biome.id;
            ActiveBiomeDisplay = biome.Display;

            bool first = _discovered.Add(biome.id);
            if (first)
                AnnounceDiscovery(biome);
            BiomeChanged?.Invoke(biome, first);
        }

        void AnnounceDiscovery(BiomeDefinition biome)
        {
            // Prefer the Trial evidence path (scores the Proving Week + queues its own
            // System message); fall back to plain Explore XP if the content set predates
            // the biome_discovered evidence event.
            bool recorded = _progression != null && _progression.RecordEvidence(EvidenceId, 1, biome.id);
            if (!recorded && _progression != null)
            {
                _progression.AddActivityXp(FoundationProgressionActivity.Explore, 8, "exploration");
                _progression.SystemFeed.Queue(SystemMessageChannel.Notice,
                    $"New region charted: {biome.Display}.", biome.id, 1);
            }

            _overlay?.Flash($"Discovered: {biome.Display}", 3.5f);
        }

        /// <summary>Save spine: ids of every biome the player has discovered.</summary>
        public string[] Snapshot()
        {
            var result = new string[_discovered.Count];
            _discovered.CopyTo(result);
            return result;
        }

        /// <summary>Restore from save. Null/empty (pre-v13 saves) simply starts a fresh
        /// journal — the biome the player loads into is re-announced once.</summary>
        public void Restore(string[] discovered)
        {
            _discovered.Clear();
            _lastBiomeId = null;
            ActiveBiomeDisplay = "";
            if (discovered == null) return;
            foreach (var id in discovered)
                if (!string.IsNullOrEmpty(id))
                    _discovered.Add(id);
        }
    }
}
