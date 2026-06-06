using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Routes player input to the gameplay systems:
    /// 1-9 / scroll = hotbar, E = interact/harvest, LMB = place, RMB = remove,
    /// I = inventory, C = crafting.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        IsoFoundationPlayer _player;
        IsoWorldController _controller;
        FoundationContent _content;
        FoundationConfig _cfg;
        Inventory _inv;
        Hotbar _hotbar;
        PlacementSystem _placement;
        FarmingSystem _farming;
        FoundationHUD _hud;
        StorageSystem _storage;

        public event Action<ResourceNodeDefinition, IReadOnlyList<ItemStack>> ResourceHarvested;

        public void Init(IsoFoundationPlayer player, IsoWorldController controller, FoundationContent content,
            FoundationConfig cfg, Inventory inv, Hotbar hotbar, PlacementSystem placement,
            FarmingSystem farming, FoundationHUD hud, StorageSystem storage = null)
        {
            _player = player; _controller = controller; _content = content; _cfg = cfg;
            _inv = inv; _hotbar = hotbar; _placement = placement; _farming = farming; _hud = hud;
            _storage = storage;

            var hi = new GameObject("TargetHighlight");
            hi.transform.SetParent(transform, false);
            _highlight = hi.AddComponent<TargetHighlight>();
            _highlight.Build();
        }

        TargetHighlight _highlight;

        void Update()
        {
            if (_player == null) return;
            HandleHotbar();
            UpdateHighlight();

            if (Input.GetKeyDown(KeyCode.I)) _hud?.ToggleInventory();
            if (Input.GetKeyDown(KeyCode.C)) _hud?.ToggleCrafting(StationType.Hand);
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetMouseButtonDown(0)) TryPlace();
            if (Input.GetMouseButtonDown(1)) TryRemove();
        }

        void UpdateHighlight()
        {
            if (_highlight == null) return;
            var node = _controller.NearestNode(_player.transform.position, _cfg.interactRange);
            _highlight.SetTarget(node != null, node != null ? node.transform.position : Vector3.zero);
        }

        void HandleHotbar()
        {
            for (int i = 0; i < _hotbar.Size && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { _hotbar.Select(i); SfxManager.Play("ui_click", 0.6f); }

            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.01f) _hotbar.Step(-1);
            else if (scroll < -0.01f) _hotbar.Step(1);
        }

        (ToolType type, int tier) SelectedToolInfo()
        {
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return (ToolType.None, 0);
            var def = _content.Items.Get(stack.itemId);
            if (def != null && def.category == ItemCategory.Tool) return (def.toolType, def.toolTier);
            return (ToolType.None, 0);
        }

        void Interact()
        {
            Vector3 pos = _player.transform.position;
            float range = _cfg.interactRange;

            // Prefer a nearby crafting station.
            var inter = _placement.NearestInteractable(pos, range);
            if (inter != null && inter.Def.interaction == InteractionKind.CraftingStation)
            {
                _hud?.ToggleCrafting(inter.Def.stationType);
                return;
            }
            if (inter != null && inter.Def.interaction == InteractionKind.Container)
            {
                if (_storage != null && _storage.TryOpenContainer(inter, out var container))
                    _hud?.Flash($"{inter.Def.Display}: {container.UsedSlots}/{container.SlotCount} slots");
                else
                    _hud?.Flash($"Opened {inter.Def.Display}");
                return;
            }

            // Harvest a mature crop if one is in range.
            if (_farming.TryHarvestCrop(pos, range, out bool cropFull)) { _hud?.Flash("Harvested crop"); return; }
            if (cropFull) { _hud?.Flash("Inventory full!"); return; }

            // Otherwise harvest the nearest resource node.
            var node = _controller.NearestNode(pos, range);
            if (node != null)
            {
                var (tool, tier) = SelectedToolInfo();
                if (node.RequiresMissingTool(tool))
                {
                    _hud?.Flash($"Needs a {node.Def.requiredTool}");
                    return;
                }
                var granted = new List<ItemStack>();
                bool depleted = node.Harvest(_inv, tool, tier, out bool full, granted);
                if (full)
                {
                    _hud?.Flash("Inventory full!");
                    return;
                }
                if (depleted) ResourceHarvested?.Invoke(node.Def, granted);
                if (_hud == null) return;
                _hud.Flash(depleted ? $"Harvested {node.Def.Display}" : $"Hitting {node.Def.Display}...");
            }
            else _hud?.Flash("Nothing to interact with");
        }

        void TryPlace()
        {
            if (_hud != null && _hud.PointerOverUI) return;
            string farmMsg = _farming.TryUseSelected(); // hoe tills / seed plants
            if (farmMsg != null) { _hud?.Flash(farmMsg); return; }
            if (_placement.TryPlaceSelected()) { _hud?.Flash("Placed"); SfxManager.Play("place", 0.8f); }
        }

        void TryRemove()
        {
            if (_hud != null && _hud.PointerOverUI) return;
            if (_placement.TryRemoveAtCursor(out string blockedMessage)) _hud?.Flash("Removed");
            else if (!string.IsNullOrEmpty(blockedMessage)) _hud?.Flash(blockedMessage);
        }
    }
}
