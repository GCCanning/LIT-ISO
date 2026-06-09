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
        StorageSystem _storage;
        Camera _cam;
        FoundationInteractionOverlay _overlay;
        FoundationInstanceSystem _instances;
        FoundationDungeonPortalSystem _dungeonPortals;
        PlayerHeldTool _heldTool;
        FoundationCampingSystem _camping;
        ResourceNode _heldHarvestTarget;
        float _nextHeldPrimaryTime;

        const float HeldHarvestInterval = 0.30f;

        public event Action<ResourceNodeDefinition, IReadOnlyList<ItemStack>> ResourceHarvested;
        public event Action<StationType> CraftingRequested;
        public event Action<StorageContainer> ContainerOpened;
        public event Action<string, string> ContextActionUsed;

        public void Init(IsoFoundationPlayer player, IsoWorldController controller, FoundationContent content,
            FoundationConfig cfg, Inventory inv, Hotbar hotbar, PlacementSystem placement,
            FarmingSystem farming, StorageSystem storage = null,
            Camera cam = null, FoundationInteractionOverlay overlay = null,
            FoundationInstanceSystem instances = null, FoundationDungeonPortalSystem dungeonPortals = null,
            PlayerHeldTool heldTool = null, FoundationCampingSystem camping = null)
        {
            _player = player; _controller = controller; _content = content; _cfg = cfg;
            _inv = inv; _hotbar = hotbar; _placement = placement; _farming = farming;
            _storage = storage; _cam = cam; _overlay = overlay; _instances = instances; _dungeonPortals = dungeonPortals;
            _heldTool = heldTool;
            _camping = camping;

            var hi = new GameObject("TargetHighlight");
            hi.transform.SetParent(transform, false);
            _highlight = hi.AddComponent<TargetHighlight>();
            _highlight.Build();
        }

        TargetHighlight _highlight;

        void Update()
        {
            if (_player == null) return;
            bool blocked = WorldInputBlocked();
            if (!blocked)
                HandleHotbar();
            UpdateHighlight(blocked);

            if (blocked) return;
            if (Input.GetMouseButtonDown(0))
            {
                _heldHarvestTarget = null;
                _nextHeldPrimaryTime = Time.time + HeldHarvestInterval;
                HandlePrimaryClick();
            }
            else if (Input.GetMouseButton(0) && Time.time >= _nextHeldPrimaryTime)
            {
                _nextHeldPrimaryTime = Time.time + HeldHarvestInterval;
                TryHandleHeldHarvest();
            }
            else if (!Input.GetMouseButton(0))
            {
                _heldHarvestTarget = null;
            }
            if (Input.GetMouseButtonDown(1)) HandleSecondaryClick();
        }

        void UpdateHighlight(bool inputBlocked)
        {
            if (_highlight == null) return;
            if (inputBlocked)
            {
                _highlight.SetTarget(false, Vector3.zero);
                return;
            }

            if (TryDecorationUnderCursor(out var decoration) && InRange(decoration.HighlightPosition))
            {
                _highlight.SetTarget(true, decoration.HighlightPosition);
                return;
            }

            if (TryPortalUnderCursor(out var portal) && InRange(portal.HighlightPosition))
            {
                _highlight.SetTarget(true, portal.HighlightPosition);
                return;
            }

            if (TryPlaceableUnderCursor(out var hoveredPlaceable) && InRange(hoveredPlaceable.HighlightPosition))
            {
                _highlight.SetTarget(true, hoveredPlaceable.HighlightPosition);
                return;
            }

            if (TryNodeUnderCursor(out var hoveredNode) && InRange(hoveredNode.HighlightPosition))
            {
                _highlight.SetTarget(true, hoveredNode.HighlightPosition);
                return;
            }

            var c = CursorCell();
            var node = _controller.NodeAtCell(c.x, c.y);
            var placeable = _placement.PlaceableAtCell(c.x, c.y);
            bool showNode = node != null && InRange(node.transform.position);
            bool showPlaceable = !showNode && placeable != null && InRange(placeable.transform.position);
            Vector3 pos = showNode ? node.transform.position :
                showPlaceable ? placeable.transform.position : Vector3.zero;
            _highlight.SetTarget(showNode || showPlaceable, pos);
        }

        Camera ActiveCamera() => _cam != null ? _cam : Camera.main;

        bool TryDecorationUnderCursor(out FoundationInstanceDecoration decoration)
        {
            decoration = null;
            return _instances != null &&
                _instances.TryGetDecorationUnderCursor(ActiveCamera(), Input.mousePosition, out decoration);
        }

        bool TryNodeUnderCursor(out ResourceNode node)
        {
            node = null;
            return _controller != null &&
                _controller.TryGetNodeUnderCursor(ActiveCamera(), Input.mousePosition, out node);
        }

        bool TryPlaceableUnderCursor(out PlaceableInstance placeable)
        {
            placeable = null;
            return _placement != null &&
                _placement.TryGetPlaceableUnderCursor(ActiveCamera(), Input.mousePosition, out placeable);
        }

        bool TryPortalUnderCursor(out FoundationDungeonPortalInstance portal)
        {
            portal = null;
            return _dungeonPortals != null &&
                _dungeonPortals.TryGetPortalUnderCursor(ActiveCamera(), Input.mousePosition, out portal);
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
            (_overlay != null && _overlay.PointerOverUI);

        bool WorldInputBlocked() =>
            PointerOverUI() ||
            (FoundationUiCoordinator.Active != null && FoundationUiCoordinator.Active.BlocksWorldInput);

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

        string SelectedActionFailureMessage()
        {
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return "Select a tool, seed, block, or station first";

            var def = _content.Items.Get(stack.itemId);
            if (def == null) return "Selected item is unavailable";
            if (def.IsSeed) return "Seeds need tilled soil";
            if (def.category == ItemCategory.Tool && def.toolType == ToolType.Hoe) return "Cannot till this tile";
            if (def.IsPlaceable) return "Cannot place here";
            return "Nothing to use";
        }

        bool TryHandleHeldHarvest()
        {
            if (PointerOverUI()) return false;

            if (_heldHarvestTarget != null && InRange(_heldHarvestTarget.HighlightPosition))
            {
                HarvestNode(_heldHarvestTarget);
                return true;
            }

            if (TryNodeUnderCursor(out var hoveredNode) && InRange(hoveredNode.HighlightPosition))
            {
                _heldHarvestTarget = hoveredNode;
                HarvestNode(hoveredNode);
                return true;
            }

            var c = CursorCell();
            var node = _controller.NodeAtCell(c.x, c.y);
            if (node == null || !InRange(node.transform.position))
                return false;

            _heldHarvestTarget = node;
            HarvestNode(node);
            return true;
        }

        void HandlePrimaryClick()
        {
            if (PointerOverUI()) return;
            _overlay?.CloseContextMenu();

            if (SelectedIsPlacementAction())
            {
                string farmMsg = _farming.TryUseSelected();
                if (farmMsg != null) { if (!OnSelectedUseSucceeded(farmMsg)) Flash(farmMsg); return; }
                if (_placement.TryPlaceSelected()) { _heldTool?.Swing(); Flash("Placed"); SfxManager.Play("place", 0.8f); return; }
            }

            var c = CursorCell();
            if (_farming.TryHarvestCropAtCell(c.x, c.y, out bool cropFull))
            {
                Flash("Harvested crop");
                return;
            }
            if (cropFull) { Flash("Inventory full!"); return; }

            var node = TryNodeUnderCursor(out var hoveredNode) && InRange(hoveredNode.HighlightPosition)
                ? hoveredNode
                : _controller.NodeAtCell(c.x, c.y);
            if (node != null && InRange(node.transform.position))
            {
                _heldHarvestTarget = node;
                HarvestNode(node);
                return;
            }

            if (!SelectedIsPlacementAction())
            {
                string farmMsg = _farming.TryUseSelected();
                if (farmMsg != null) { if (!OnSelectedUseSucceeded(farmMsg)) Flash(farmMsg); return; }
                if (_placement.TryPlaceSelected()) { _heldTool?.Swing(); Flash("Placed"); SfxManager.Play("place", 0.8f); return; }
            }

            Flash(SelectedIsPlacementAction() ? SelectedActionFailureMessage() : "Nothing to use");
        }

        void HandleSecondaryClick()
        {
            if (PointerOverUI()) return;

            if (TryDecorationUnderCursor(out var decoration) && InRange(decoration.HighlightPosition))
            {
                if (decoration.IsDungeonExit && _dungeonPortals != null && _dungeonPortals.IsActiveDungeon)
                {
                    _overlay?.OpenContextMenu(decoration.DisplayName, Input.mousePosition,
                        new[]
                        {
                            new FoundationContextAction("complete_dungeon", "Complete and exit dungeon",
                                () =>
                                {
                                    ContextActionUsed?.Invoke("complete_dungeon", _dungeonPortals.PortalIdForActiveDungeon);
                                    if (!_dungeonPortals.CompleteAndExit()) Flash("No exit found");
                                })
                        });
                }
                else if (_instances.IsExitDecoration(decoration))
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
                }
                else
                {
                    _overlay?.OpenContextMenu(decoration.DisplayName, Input.mousePosition,
                        new[]
                        {
                            new FoundationContextAction("inspect", $"Inspect {decoration.DisplayName}",
                                () =>
                                {
                                    ContextActionUsed?.Invoke("inspect_decoration", decoration.DisplayName);
                                    Flash(decoration.DisplayName);
                                })
                        });
                }
                return;
            }

            var c = CursorCell();
            var portal = TryPortalUnderCursor(out var hoveredPortal) && InRange(hoveredPortal.HighlightPosition)
                ? hoveredPortal
                : (_dungeonPortals != null ? _dungeonPortals.PortalAtCell(c.x, c.y) : null);
            if (portal != null && InRange(portal.transform.position))
            {
                string label = portal.Completed
                    ? $"Re-enter cleared Tier {portal.Tier} dungeon"
                    : portal.RewardOpened
                        ? $"Enter claimed Tier {portal.Tier} dungeon"
                        : $"Enter Tier {portal.Tier} dungeon";
                _overlay?.OpenContextMenu(portal.DisplayName, Input.mousePosition,
                    new[]
                    {
                        new FoundationContextAction("enter_dungeon", label,
                            () =>
                            {
                                ContextActionUsed?.Invoke("enter_dungeon", portal.PortalId);
                                if (!_dungeonPortals.Enter(portal)) Flash("Dungeon failed to open");
                            })
                    });
                return;
            }

            var placeable = TryPlaceableUnderCursor(out var hoveredPlaceable) && InRange(hoveredPlaceable.HighlightPosition)
                ? hoveredPlaceable
                : _placement.PlaceableAtCell(c.x, c.y);
            if (placeable != null && InRange(placeable.transform.position))
            {
                OpenPlaceableContext(placeable);
                return;
            }

            var node = TryNodeUnderCursor(out var hoveredNode) && InRange(hoveredNode.HighlightPosition)
                ? hoveredNode
                : _controller.NodeAtCell(c.x, c.y);
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
            int beforeHits = node.RemainingHits;
            bool depleted = node.Harvest(_inv, tool, tier, out bool full, granted);
            if (full)
            {
                Flash("Inventory full!");
                return;
            }

            bool toolBroke = false;
            if (node.RemainingHits < beforeHits)
                toolBroke = OnHarvestHitSucceeded(node.Def);
            if (depleted) ResourceHarvested?.Invoke(node.Def, granted);
            if (depleted && _heldHarvestTarget == node)
                _heldHarvestTarget = null;
            if (!toolBroke)
                Flash(depleted ? $"Harvested {node.Def.Display}" : $"Breaking {node.Def.Display}...");
        }

        bool OnHarvestHitSucceeded(ResourceNodeDefinition nodeDef)
        {
            _heldTool?.Swing();

            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return false;
            var def = _content.Items.Get(stack.itemId);
            if (def == null || !def.HasDurability) return false;

            int loss = Mathf.Max(1, def.durabilityLossPerUse);
            if (nodeDef != null && nodeDef.requiredTool != ToolType.None &&
                def.toolType != nodeDef.requiredTool)
                loss += 1;

            if (_inv.DamageSlot(_hotbar.Selected, loss))
            {
                Flash($"{def.Display} broke");
                return true;
            }
            return false;
        }

        bool OnSelectedUseSucceeded(string message)
        {
            _heldTool?.Swing();
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return false;
            var def = _content.Items.Get(stack.itemId);
            if (def == null || !def.HasDurability) return false;

            if (message == "Tilled soil" && def.toolType == ToolType.Hoe &&
                _inv.DamageSlot(_hotbar.Selected, Mathf.Max(1, def.durabilityLossPerUse)))
            {
                Flash($"{def.Display} broke");
                return true;
            }
            return false;
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
            else if (def.interaction == InteractionKind.Construction)
            {
                var result = _content.Placeables.Get(def.constructionResultPlaceableId);
                string resultName = result != null ? result.Display : "building";
                bool canBuild = result != null && HasConstructionMaterials(def);
                string reason = result == null ? "missing result" : MissingConstructionMaterials(def);
                actions.Add(new FoundationContextAction("build", $"Build {resultName} ({ConstructionCostText(def)})",
                    () => CompleteConstruction(placeable, result), canBuild, reason));
            }
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
            else if (def.isCampsite)
            {
                AddCampsiteActions(actions, placeable);
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

        void AddCampsiteActions(List<FoundationContextAction> actions, PlaceableInstance placeable)
        {
            bool canRest = _camping != null && _camping.CanRestAt(placeable);
            actions.Add(new FoundationContextAction("rest", "Rest until dawn",
                () =>
                {
                    ContextActionUsed?.Invoke("rest_at_camp", placeable.Def.id);
                    if (_camping == null || !_camping.RestAt(placeable))
                        Flash("Stand closer to the fire");
                },
                canRest,
                "stand inside the firelight"));

            actions.Add(new FoundationContextAction("cook", "Cook at fire",
                () => RequestCrafting(StationType.CookingPot, placeable.Def.id)));

            actions.Add(new FoundationContextAction("inspect", "Inspect camp aura",
                () =>
                {
                    ContextActionUsed?.Invoke("inspect_camp", placeable.Def.id);
                    Flash(_camping != null ? _camping.DescribeCamp(placeable) : "A warm fire.");
                }));
        }

        bool HasConstructionMaterials(PlaceableDefinition plot)
        {
            if (plot == null || plot.constructionCost == null)
                return false;

            foreach (var cost in plot.constructionCost)
                if (cost.count > 0 && !_inv.Has(cost.itemId, cost.count))
                    return false;
            return true;
        }

        string MissingConstructionMaterials(PlaceableDefinition plot)
        {
            if (plot == null || plot.constructionCost == null || plot.constructionCost.Length == 0)
                return "no plan";

            var missing = new List<string>();
            foreach (var cost in plot.constructionCost)
            {
                if (cost.count <= 0 || string.IsNullOrWhiteSpace(cost.itemId))
                    continue;

                int have = _inv.Count(cost.itemId);
                if (have < cost.count)
                    missing.Add($"{cost.count - have} {DisplayItem(cost.itemId)}");
            }

            return missing.Count == 0 ? "" : "needs " + string.Join(", ", missing);
        }

        string ConstructionCostText(PlaceableDefinition plot)
        {
            if (plot == null || plot.constructionCost == null || plot.constructionCost.Length == 0)
                return "no materials";

            var parts = new List<string>();
            foreach (var cost in plot.constructionCost)
            {
                if (cost.count <= 0 || string.IsNullOrWhiteSpace(cost.itemId))
                    continue;
                parts.Add($"{cost.count} {DisplayItem(cost.itemId)}");
            }
            return parts.Count == 0 ? "no materials" : string.Join(", ", parts);
        }

        string DisplayItem(string itemId)
        {
            var item = _content != null ? _content.Items.Get(itemId) : null;
            return item != null ? item.Display : itemId;
        }

        void CompleteConstruction(PlaceableInstance plot, PlaceableDefinition result)
        {
            if (plot == null || plot.Def == null || result == null)
            {
                Flash("Construction plan is incomplete");
                return;
            }

            var def = plot.Def;
            if (!HasConstructionMaterials(def))
            {
                Flash(MissingConstructionMaterials(def));
                return;
            }

            if (!_placement.TryReplacePlaceableAtCell(plot.Wx, plot.Wy, result))
            {
                Flash("Cannot build here");
                return;
            }

            foreach (var cost in def.constructionCost)
                if (cost.count > 0)
                    _inv.Remove(cost.itemId, cost.count);

            ContextActionUsed?.Invoke("build_placeable", result.id);
            Flash($"Built {result.Display}");
            SfxManager.Play("place", 0.8f);
        }

        void RequestCrafting(StationType station, string targetId)
        {
            ContextActionUsed?.Invoke("craft", targetId);
            CraftingRequested?.Invoke(station);
            Flash($"{station} crafting");
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
            else Debug.Log(message);
        }
    }
}
