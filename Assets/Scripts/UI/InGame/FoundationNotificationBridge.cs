using System;
using System.Collections.Generic;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Turns silent Foundation progression state into player-facing System messages.
    ///
    /// Subscribes to <see cref="FoundationPlayerStats.Changed"/> (diffing level and the
    /// six core stats) and to <see cref="FoundationProgression.QuestStarted"/> /
    /// <see cref="FoundationProgression.QuestCompleted"/>, then routes warm one-line
    /// announcements through <see cref="SystemNotifier"/>. Any view listening to
    /// <c>SystemNotifier.OnMessage</c> (e.g. <see cref="InGameNotificationView"/>) renders them.
    ///
    /// This is the Foundation-facing half of the feature (it reads IsoCore.Foundation
    /// types) — mirroring the adapter role. The view stays Foundation-free.
    ///
    /// Created + disposed by <see cref="GameHudInitializer"/> on FoundationBootstrap.Ready.
    /// It deliberately subscribes AFTER bootstrap has selected the starting Calling and
    /// started the starter quests, so the player isn't spammed with toasts at world load.
    /// </summary>
    public sealed class FoundationNotificationBridge : IDisposable
    {
        readonly FoundationProgression _progression;
        readonly FoundationPlayerStats _stats;

        int _prevLevel;
        int _prevStr, _prevDex, _prevInt, _prevVit, _prevDef, _prevLuck;
        string _prevClass;

        // Warm, bible-tone flavour for each stat gain. Falls back to a bare line if absent.
        static readonly Dictionary<string, string> StatFlavor = new()
        {
            { "STR",  "Heavy tools feel lighter." },
            { "DEX",  "Your hands move with surer purpose." },
            { "INT",  "The world's patterns come clearer." },
            { "VIT",  "Long days tire you less." },
            { "DEF",  "Knocks and scrapes sting less." },
            { "LUCK", "Fortune leans a little your way." },
        };

        public FoundationNotificationBridge(FoundationProgression progression)
        {
            _progression = progression;
            _stats = progression?.Stats;

            if (_stats != null)
            {
                Snapshot();
                _stats.Changed += OnStatsChanged;
            }

            if (_progression != null)
            {
                _progression.QuestStarted   += OnQuestStarted;
                _progression.QuestCompleted += OnQuestCompleted;
            }
        }

        public void Dispose()
        {
            if (_stats != null) _stats.Changed -= OnStatsChanged;
            if (_progression != null)
            {
                _progression.QuestStarted   -= OnQuestStarted;
                _progression.QuestCompleted -= OnQuestCompleted;
            }
        }

        void Snapshot()
        {
            _prevLevel = _stats.Level;
            _prevStr = _stats.STR; _prevDex = _stats.DEX; _prevInt = _stats.INT;
            _prevVit = _stats.VIT; _prevDef = _stats.DEF; _prevLuck = _stats.LUCK;
            _prevClass = _stats.Class;
        }

        // FoundationPlayerStats raises Changed on vitals too (damage/heal/mana). We only
        // announce when level, class, or a core stat actually increased — so routine XP
        // ticks and combat chip damage stay silent.
        void OnStatsChanged()
        {
            if (_stats == null) return;

            if (_stats.Level > _prevLevel)
            {
                for (int lvl = _prevLevel + 1; lvl <= _stats.Level; lvl++)
                    Announce($"Level {lvl}.", SystemNotifier.MessageType.LevelUp);
            }

            if (!string.Equals(_stats.Class, _prevClass, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(_stats.Class))
            {
                Announce($"Calling awakened: {_stats.Class}.", SystemNotifier.MessageType.ClassAssign);
            }

            ReportStat("STR",  _stats.STR,  ref _prevStr);
            ReportStat("DEX",  _stats.DEX,  ref _prevDex);
            ReportStat("INT",  _stats.INT,  ref _prevInt);
            ReportStat("VIT",  _stats.VIT,  ref _prevVit);
            ReportStat("DEF",  _stats.DEF,  ref _prevDef);
            ReportStat("LUCK", _stats.LUCK, ref _prevLuck);

            _prevLevel = _stats.Level;
            _prevClass = _stats.Class;
        }

        void ReportStat(string code, int current, ref int prev)
        {
            if (current > prev)
            {
                int delta = current - prev;
                string flavor = StatFlavor.TryGetValue(code, out var f) ? " " + f : "";
                string by = delta == 1 ? "" : $" by {delta}";
                Announce($"{code} increased{by}.{flavor}", SystemNotifier.MessageType.Info);
            }
            prev = current;
        }

        void OnQuestStarted(FoundationQuestDefinition quest)
        {
            if (quest == null) return;
            Announce($"New quest: {Title(quest)}.", SystemNotifier.MessageType.QuestNew);
        }

        void OnQuestCompleted(FoundationQuestDefinition quest)
        {
            if (quest == null) return;
            Announce($"Quest complete: {Title(quest)}.", SystemNotifier.MessageType.QuestComplete);
        }

        static string Title(FoundationQuestDefinition quest)
        {
            string display = quest.Display; // displayName, or id when unset
            return string.IsNullOrWhiteSpace(display) ? quest.id : display;
        }

        static void Announce(string message, SystemNotifier.MessageType type)
        {
            // Null-safe: GameHudInitializer guarantees a SystemNotifier instance, but if
            // one isn't present the bridge simply no-ops rather than throwing.
            SystemNotifier.Instance?.Announce(message, type);
        }
    }
}
