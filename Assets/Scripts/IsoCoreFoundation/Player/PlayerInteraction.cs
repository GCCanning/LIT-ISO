using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Routes player input to the gameplay systems:
    /// 1-9 / scroll = hotbar, LMB = primary use/place/break, RMB = context options,
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
        Camera _cam;
        FoundationInteractionOverlay _overlay;
        FoundationInstanceSystem _instances;

        public event Action<ResourceNodeDefinition, IReadOnlyList<ItemStack>> ResourceHarvested;
        public event Action<StationType> CraftingRequested;
        public event Action<StorageContainer> ContainerOpened;
        public event Action<string, string> ContextActionUsed;

        public void Init(IsoFoundationPlayer player, IsoWorldController controller, FoundationContent content,
            FoundationConfig cfg, Inventory inv, Hotbar hotbar, PlacementSystem placement,
            FarmingSystem farming, FoundationHUD hud, StorageSystem storage = null,
            Camera cam = null, FoundationInteractionOverlay overlay = null,
            FoundationInstanceSystem instances = null)
        {
            _player = player; _controller = controller; _content = content; _cfg = cfg;
            _inv = inv; _hotbar = hotbar; _placement = placement; _farming = farming; _hud = hud;
            _storage = storage; _cam = cam; _overlay = overlay; _instances = instances;

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
            if (Input.GetMouseButtonDown(0)) HandlePrimaryClick();
            if (Input.GetMouseButtonDown(1)) HandleSecondaryClick();
        }

        void UpdateHighlight()
        {
            if (_highlight == null) return;
            var c = CursorCell();
            var node = _controller.NodeAtCell(c.x, c.y);
            var placeable = _placement.PlaceableAtCell(c.x, c.y);
            bool showNode = node != null && InRange(node.transform.position);
            bool showPlaceable = !showNode && placeable != null && InRange(placeable.transform.position);
            Vector3 pos = showNode ? node.transform.position :
                showPlaceable ? placeable.transform.position : Vector3.zero;
            _highlight.SetTarget(showNode || showPlaceable, pos);
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

        Vector2Int CursorCell()
        {
            var cam = _cam != null ? _cam : Camera.main;
            if (cam == null || _controller?.World == null)
                return _player.CurrentCell;

            var wp = cam.ScreenToWorldPoint(Input.mousePosition);
            wp.z = 0f;
            var c = IsoGrid.WorldToCell(wp);
            int h = _controller.World.GetHeight(c.x, c.y);
            if (h != 0)
                c = IsoGrid.WorldToCell(new Vector3(wp.x, wp.y - h * IsoGrid.HeightStep, 0f));
            return c;
        }

        bool PointerOverUI() =>
            (_hud != null && _hud.PointerOverUI) ||
            (_overlay != null && _overlay.PointerOverUI);

        bool InRange(Vector3 target) =>
            ((Vector2)(target - _player.transform.position)).sqrMagnitude <=
            _cfg.interactRange * _cfg.interactRange;

        bool SelectedIsPlacementAction()
        {
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return false;
            var def = _content.Items.Get(stack.itemId);
            return def != null &&
                (def.IsPlaceable || def.IsSeed ||
                 (def.category == ItemCategory.Tool && def.toolType == ToolType.Hoe));
        }

        void HandlePrimaryClick()
        {
            if (PointerOverUI()) return;
            _overlay?.CloseContextMenu();

            if (SelectedIsPlacementAction())
            {
                string farmMsg = _farming.TryUseSelected();
                if (farmMsg != null) { Flash(farmMsg); return; }
                if (_placement.TryPlaceSelected()) { Flash("Placed"); SfxManager.Play("place", 0.8f); return; }
            }

            var c = CursorCell();
            if (_farming.TryHarvestCropAtCell(c.x, c.y, out bool cropFull))
            {
                Flash("Harvested crop");
                return;
            }
            if (cropFull) { Flash("Inventory full!"); return; }

            var node = _controller.NodeAtCell(c.x, c.y);
            if (node != null && InRange(node.transform.position))
            {
                HarvestNode(node);
                return;
            }

            if (!SelectedIsPlacementAction())
            {
                string farmMsg = _farming.TryUseSelected();
                if (farmMsg != null) { Flash(farmMsg); return; }
                if (_placement.TryPlaceSelected()) { Flash("Placed"); SfxManager.Play("place", 0.8f); return; }
            }

            Flash("Nothing to use");
        }

        void HandleSecondaryClick()
        {
            if (PointerOverUI()) return;

            var c = CursorCell();
            var placeable = _placement.PlaceableAtCell(c.x, c.y);
            if (placeable != null && InRange(placeable.transform.position))
            {
                OpenPlaceableContext(placeable);
                return;
            }

            var node = _controller.NodeAtCell(c.x, c.y);
            if (node != null && InRange(node.transform.position))
            {
                _overlay?.OpenContextMenu(node.Def.Display, Input.mousePosition,
                    new[]
                    {
                        new FoundationContextAction("break", $"Break {node.Def.Display}",
                            () => { ContextActionUsed?.Invoke("break", node.Def.id); HarvestNode(node); })
                    });
                return;
            }

            if (_controller.World.GetCell(c.x, c.y).SolidBlock)
            {
                _overlay?.OpenContextMenu("Block", Input.mousePosition,
                    new[]
                    {
                        new FoundationContextAction("remove", "Remove block",
                            () => { ContextActionUsed?.Invoke("remove_block", "solid_block"); RemoveAt(c.x, c.y); })
                    });
                return;
            }

            if (_instances != null && _instances.IsInsideInstance)
            {
                _overlay?.OpenContextMenu(_instances.ActiveDisplayName, Input.mousePosition,
                    new[]
                    {
                        new FoundationContextAction("exit", $"Exit {_instances.ActiveDisplayName}",
                            () =>
                            {
                                ContextActionUsed?.Invoke("exit_instance", _instances.ActiveInstanceId);
                                if (!_instances.Exit()) Flash("No exit found");
                            })
                    });
                return;
            }

            Flash("No options here");
        }

        void HarvestNode(ResourceNode node)
        {
            if (node == null) return;
            var (tool, tier) = SelectedToolInfo();
            if (node.RequiresMissingTool(tool))
            {
                Flash($"Needs a {node.Def.requiredTool}");
                return;
            }

            var granted = new List<ItemStack>();
            bool depleted = node.Harvest(_inv, tool, tier, out bool full, granted);
            if (full)
            {
                Flash("Inventory full!");
                return;
            }

            if (depleted) ResourceHarvested?.Invoke(node.Def, granted);
            Flash(depleted ? $"Harvested {node.Def.Display}" : $"Breaking {node.Def.Display}...");
        }

        void OpenPlaceableContext(PlaceableInstance placeable)
        {
            var actions = new List<FoundationContextAction>();
            var def = placeable.Def;

            if (def.interaction == InteractionKind.CraftingStation)
                actions.Add(new FoundationContextAction("use", $"Use {def.Display}",
                    () => RequestCrafting(def.stationType, def.id)));
            else if (def.interaction == InteractionKind.Container)
                actions.Add(new FoundationContextAction("open", $"Open {def.Display}",
                    () => OpenContainer(placeable)));
            else if (def.interaction == InteractionKind.Entrance)
            {
                bool hasDestination = !string.IsNullOrWhiteSpace(def.destinationId);
                string label = string.IsNullOrWhiteSpace(def.entranceLabel) ? "Enter" : def.entranceLabel;
                string destination = string.IsNullOrWhiteSpace(def.destinationDisplayName)
                    ? def.Display
                    : def.destinationDisplayName;
                actions.Add(new FoundationContextAction("enter", $"{label} {destination}",
                    () => RequestEntrance(def, destination), hasDestination, "not connected yet"));
            }
            else
            {
                actions.Add(new FoundationContextAction("inspect", $"Inspect {def.Display}",
                    () => { ContextActionUsed?.Invoke("inspect", def.id); Flash(def.Display); }));
            }

            actions.Add(new FoundationContextAction("remove", $"Remove {def.Display}",
                () => { ContextActionUsed?.Invoke("remove_placeable", def.id); RemoveAt(placeable.Wx, placeable.Wy); }));

            _overlay?.OpenContextMenu(def.Display, Input.mousePosition, actions.ToArray());
        }

        void RequestCrafting(StationType station, string targetId)
        {
            ContextActionUsed?.Invoke("craft", targetId);
            CraftingRequested?.Invoke(station);
            if (_hud != null) _hud.ToggleCrafting(station);
            else Flash($"{station} crafting");
        }

        void OpenContainer(PlaceableInstance placeable)
        {
            if (_storage != null && _storage.TryOpenContainer(placeable, out var container))
            {
                ContextActionUsed?.Invoke("open_container", placeable.Def.id);
                ContainerOpened?.Invoke(container);
                Flash($"{placeable.Def.Display}: {container.UsedSlots}/{container.SlotCount} slots");
            }
            else
            {
                ContextActionUsed?.Invoke("open_container", placeable.Def.id);
                Flash($"Opened {placeable.Def.Display}");
            }
        }

        void RequestEntrance(PlaceableDefinition def, string destination)
        {
            ContextActionUsed?.Invoke("enter", def.id);
            var c = CursorCell();
            if (_instances != null && _instances.Enter(def, c))
                return;
            Flash($"Entering {destination}...");
        }

        void RemoveAt(int wx, int wy)
        {
            if (_placement.TryRemoveAtCell(wx, wy, out string blockedMessage)) Flash("Removed");
            else if (!string.IsNullOrEmpty(blockedMessage)) Flash(blockedMessage);
            else Flash("Nothing to remove");
        }

        void Flash(string message)
        {
            if (_overlay != null) _overlay.Flash(message);
            else _hud?.Flash(message);
        }
    }
}
