using System;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    public static class Phase2IntegrationBridge
    {
        public static event Action<string, Vector3> OnPlayerMoved;
        public static event Action<string, Vector3> OnPlayerSpawned;
        public static event Action<TimeOfDay> OnWorldTimeOfDayChanged;
        public static event Action<int> OnGameSaved;
        public static event Action<int> OnGameLoaded;
        public static event Action OnPhase2Ready;

        public static void NotifyPlayerMoved(string playerId, Vector3 newPosition)
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnPlayerMoved?.Invoke(playerId, newPosition); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }

        public static void NotifyPlayerSpawned(string playerId, Vector3 spawnPosition)
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnPlayerSpawned?.Invoke(playerId, spawnPosition); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }

        public static void NotifyWorldTimeOfDayChanged(TimeOfDay newTimeOfDay)
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnWorldTimeOfDayChanged?.Invoke(newTimeOfDay); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }

        public static void NotifyGameSaved(int saveSlotId)
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnGameSaved?.Invoke(saveSlotId); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }

        public static void NotifyGameLoaded(int saveSlotId)
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnGameLoaded?.Invoke(saveSlotId); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }

        public static void NotifyPhase2Ready()
        {
            if (!Phase2Enabler.IsActive) return;
            try { OnPhase2Ready?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[Phase2IntegrationBridge] Error: {ex.Message}"); }
        }
    }
}
