using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Equips a fixed set of test items on every new session so the
    /// equipment→visual pipeline can be verified without a crafting/loot loop.
    /// Remove or gate behind a "new game" flag once real loot is wired up.
    /// </summary>
    [DefaultExecutionOrder(-799)]
    public static class StarterLoadoutHook
    {
        // Item ids that match the assets in Resources/Items/.
        static readonly string[] StarterItems =
        {
            "iron_sword",
            "iron_helm",
            "leather_chest",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            FoundationBootstrap.Ready += OnReady;
        }

        static void OnReady(FoundationBootstrap bootstrap)
        {
            var loadout = bootstrap.Equipment;
            if (loadout == null)
            {
                Debug.LogWarning("[StarterLoadout] No EquipmentLoadout on bootstrap — skipping.");
                return;
            }

            foreach (var id in StarterItems)
            {
                // Only equip if the slot is currently empty (don't clobber a loaded save).
                var def = bootstrap.Content?.Items?.Get(id);
                if (def == null)
                {
                    Debug.LogWarning($"[StarterLoadout] Item '{id}' not found in Content.Items. " +
                                     "Make sure the asset is in Resources/Items/ and its id field matches.");
                    continue;
                }
                if (loadout.IsEquipped(def.equipSlot))
                    continue;

                loadout.Equip(id, def.maxDurability);
                Debug.Log($"[StarterLoadout] Equipped {id} → slot {def.equipSlot}");
            }
        }
    }
}
