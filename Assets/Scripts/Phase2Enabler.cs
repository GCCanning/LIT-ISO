using UnityEngine;

namespace EthraClone.TrialWeek
{
    public static class Phase2Enabler
    {
        private static bool isActive = true;

        public static bool IsActive => isActive;

        public static void Enable()
        {
            isActive = true;
            Debug.Log("[Phase2Enabler] ENABLED");
        }

        public static void Disable()
        {
            isActive = false;
            Debug.Log("[Phase2Enabler] DISABLED");
        }

        internal static void SetActive(bool active)
        {
            isActive = active;
        }
    }
}
