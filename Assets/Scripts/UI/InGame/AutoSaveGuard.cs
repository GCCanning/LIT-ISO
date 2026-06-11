using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Last-line-of-defence autosave: when the application quits by ANY route
    /// (Alt-F4, window close, Quit button), save the active Foundation world.
    /// Fixes the audit finding that closing the window lost everything since
    /// the last manual save. Cheap, silent, and idempotent — the System tab's
    /// explicit save buttons remain the primary path.
    /// </summary>
    public static class AutoSaveGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            Application.quitting -= SaveNow;
            Application.quitting += SaveNow;
        }

        static void SaveNow()
        {
            try
            {
                var bootstrap = Object.FindFirstObjectByType<FoundationBootstrap>();
                if (bootstrap != null && bootstrap.Save(bootstrap.DefaultSavePath))
                    Debug.Log("[AutoSaveGuard] World saved on quit.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AutoSaveGuard] Quit-save failed: " + e.Message);
            }
        }
    }
}
