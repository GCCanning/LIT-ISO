using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A deterministic, non-save showroom world used to review systems and assets in
    /// one place before promoting them into normal world generation.
    /// </summary>
    public static class FoundationCreationInstanceShowroom
    {
        public const string WorldName = "Creation Instance";
        public static readonly int Seed = FoundationBootstrap.SeedStringToInt("creation-instance-showroom");

        const int MinX = -32;
        const int MaxX = 54;
        const int MinY = -28;
        const int MaxY = 30;

        // ---- "review everything" additions (owner request 2026-06): a chest bank
        //      stocked with every registered item, plus FULL procedural settlements
        //      (one of each tier) generated north of the showroom. ----
        const int AllChestBaseX = 10;
        const int AllChestY = 10;
        const int AllChestStepX = 3;
        const int AllChestPerChest = 60;   // headroom under the 64-slot cap
        const int AllChestMax = 12;

        // Settlement showcase row: hand-authored compact towns (one per tier), laid
        // west->east and centred on this Y, north of the showroom (see BuildTown).
        const int SettlementRowY = 85;

        static int AllItemsChestCount(FoundationContent content)
        {
            int n = content?.Items?.All?.Count ?? 0;
            return Mathf.Clamp(Mathf.CeilToInt(n / (float)AllChestPerChest), 0, AllChestMax);
        }

        public static void ApplyConfig(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null)
                return;

            if (bootstrap.config == null)
                bootstrap.config = new FoundationConfig();

            var config = bootstrap.config;
            config.flatWorld = true;
            config.flatSurfaceBlockId = "grass_1";
            config.flatWorldMaxHeight = 0;
            config.flatWorldUseVariants = false;
            config.flatWorldDecorations = false;
            config.spawnClearingRadius = 96;
            config.spawnHeight = 0;
            config.viewRadiusChunks = Mathf.Max(config.viewRadiusChunks, 4);
            config.mobCap = 0;

            bootstrap.inventorySlots = Mathf.Max(bootstrap.inventorySlots, 48);
            bootstrap.storageSlots = Mathf.Max(bootstrap.storageSlots, 64);
            bootstrap.cameraSize = 7.2f;
            bootstrap.cameraMaxSize = Mathf.Max(bootstrap.cameraMaxSize, 11f);
        }

        public static void PrepareWorld(IsoWorld world, FoundationContent content)
        {
            if (world == null || content == null)
                return;

            var cells = new List<FoundationSavedCell>();

            AddRect(cells, MinX, MinY, MaxX - MinX + 1, MaxY - MinY + 1, "grass_1", content);

            AddRect(cells, -30, -2, 82, 5, "stone_path", content);
            AddRect(cells, -2, -26, 5, 54, "stone_path", content);
            AddRect(cells, -24, -10, 20, 12, "wood_floor", content);
            AddRect(cells, -26, 8, 28, 18, "stone_path", content);
            AddRect(cells, 8, 6, 44, 22, "stone_path", content);
            AddRect(cells, 10, -20, 34, 14, "grass_1", content);
            AddRect(cells, -26, -25, 28, 9, "soil", content);
            AddRect(cells, 34, -25, 17, 12, "grass_1", content);

            AddRect(cells, -28, 26, 78, 2, "stone_path", content);
            AddRect(cells, -28, -27, 78, 2, "stone_path", content);
            AddRect(cells, -30, -25, 2, 51, "stone_path", content);
            AddRect(cells, 50, -25, 2, 51, "stone_path", content);

            AddTilePalette(cells, content);
            AddResourceSamples(cells, content);
            AddLowWalls(cells, content);

            // Stone-path "highway" running north from the showroom up to the
            // settlement showcase row, so the player has a clear route to it.
            AddRect(cells, -1, 28, 3, SettlementRowY - 27, "stone_path", content);

            // Compact hand-authored towns (one per tier) north of the showroom.
            StampSettlements(cells, content);

            world.RestoreModifiedCells(cells.ToArray());
        }

        // ----------------------------------------------------------------------
        // Hand-authored compact towns (one per tier). The overworld sampler produces
        // a sparse, farm-heavy sprawl that doesn't read as a town up close, so the
        // showroom lays out dense, tidy settlements that showcase the real tiered
        // building art around a paved plaza, scaling up Hamlet -> City.
        // ----------------------------------------------------------------------

        const int TownSpacing = 6;   // cells between building anchors (room for 4-wide + gap)
        const int TownGap = 16;      // gap between adjacent towns

        static readonly (string label, string[] buildings, int stalls)[] TownDefs =
        {
            ("HAMLET",  new[] { "tavern_r1", "shop_r1", "library_r1" }, 0),
            ("VILLAGE", new[] { "tavern_r1", "shop_r1", "library_r1", "guild_hall_r1", "shop_r2" }, 1),
            ("TOWN",    new[] { "tavern_r2", "shop_r2", "library_r2", "guild_hall_r2", "shop_r2", "tavern_r2" }, 2),
            ("CITY",    new[] { "tavern_r3", "shop_r3", "library_r3", "guild_hall_r3", "shop_r3", "library_r2", "guild_hall_r2", "tavern_r2" }, 3),
        };

        struct TownLayout { public string label; public int originX; public int originY; public string[] buildings; public int stalls; public int gw; public int gh; }

        static List<TownLayout> SettlementLayout()
        {
            var list = new List<TownLayout>();
            int cursorX = -70;
            foreach (var t in TownDefs)
            {
                int n = t.buildings.Length;
                int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
                int rows = Mathf.CeilToInt(n / (float)cols);
                int gw = (cols - 1) * TownSpacing + 4;
                int gh = (rows - 1) * TownSpacing + 4;
                list.Add(new TownLayout { label = t.label, originX = cursorX, originY = SettlementRowY, buildings = t.buildings, stalls = t.stalls, gw = gw, gh = gh });
                cursorX += gw + 8 + TownGap;
            }
            return list;
        }

        static void StampSettlements(List<FoundationSavedCell> cells, FoundationContent content)
        {
            var towns = SettlementLayout();
            // A main road linking every town's south edge (meets the north highway near x=0).
            int west = towns[0].originX - 6;
            var last = towns[towns.Count - 1];
            int span = (last.originX + last.gw + 6) - west;
            AddRect(cells, west, SettlementRowY - 7, span, 2, "stone_path", content);

            foreach (var t in towns)
                BuildTown(cells, content, t);
        }

        static void BuildTown(List<FoundationSavedCell> cells, FoundationContent content, TownLayout t)
        {
            // Lay the paved town block + ambient props only. Buildings, market stalls and
            // the plaza campfire are placed as PLACEABLES in SpawnShowroomPlaceables (the
            // buildings are enterable; their art/size come from the placeable registry).
            AddRect(cells, t.originX - 4, t.originY - 6, t.gw + 8, t.gh + 12, "stone_path", content);

            int plazaY = t.originY - 4;
            int cx = t.originX + t.gw / 2;
            cells.Add(GroundWithNode(t.originX - 1, plazaY, "stone_path", "campsite_lantern", content));
            cells.Add(GroundWithNode(t.originX + t.gw + 1, plazaY, "stone_path", "campsite_lantern", content));

            // A little greenery on the north edge for a lived-in feel.
            cells.Add(GroundWithNode(t.originX - 2, t.originY + t.gh + 3, "grass_1", "tree", content));
            cells.Add(GroundWithNode(t.originX + t.gw + 2, t.originY + t.gh + 3, "grass_1", "pine", content));
            cells.Add(GroundWithNode(cx, t.originY + t.gh + 3, "grass_1", "flower", content));
        }

        /// <summary>A flat ground cell that optionally carries a (building) decoration node.</summary>
        static FoundationSavedCell GroundWithNode(int x, int y, string surface, string nodeId, FoundationContent content)
        {
            var node = !string.IsNullOrEmpty(nodeId) ? content.Nodes.Get(nodeId) : null;
            return new FoundationSavedCell
            {
                x = x, y = y, height = 0, biomeIndex = 0,
                surfaceBlockId = surface, occupantId = null,
                nodeId = node != null ? node.id : null,
                solidBlock = false, water = false, occupantBlocks = false,
                nodeBlocks = node != null && node.blocksMovement,
                underBlockId = null, underHeight = 0,
            };
        }

        public static void BuildShowroom(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null || bootstrap.World == null || bootstrap.Content == null)
                return;

            SpawnShowroomPlaceables(bootstrap);
            StockResourceChest(bootstrap);
            StockAllItemsChests(bootstrap);
            SpawnCropSamples(bootstrap);
            SpawnLabels(bootstrap);

            bootstrap.InteractionOverlay?.Flash(
                "Creation Instance: settlement tiers north, ALL-ITEMS chests beside them, plus crafting, resources & interiors.",
                4.5f);
        }

        static void SpawnShowroomPlaceables(FoundationBootstrap bootstrap)
        {
            if (bootstrap.Placement == null)
                return;

            var placeables = new List<FoundationSavedPlaceable>
            {
                SavedPlaceable("chest", -20, -2),
                SavedPlaceable("workbench", -15, -2),
                SavedPlaceable("furnace", -11, -2),
                SavedPlaceable("campfire", -22, -7),
                SavedPlaceable("fireplace", -12, -7),
                SavedPlaceable("lantern", -5, -6),
                SavedPlaceable("tavern_building", -21, 15),
                SavedPlaceable("library_building", -10, 15),
                SavedPlaceable("tavern_plot", -21, 24),
                SavedPlaceable("library_plot", -10, 24),
                SavedPlaceable("rootcellar_portal", 4, 9),
            };

            // All-items review chest bank (filled in StockAllItemsChests).
            int chestCount = AllItemsChestCount(bootstrap.Content);
            for (int i = 0; i < chestCount; i++)
                placeables.Add(SavedPlaceable("chest", AllChestBaseX + i * AllChestStepX, AllChestY));

            // Settlement buildings (enterable placeables) + market stalls + plaza campfire,
            // placed on the paved town blocks laid by BuildTown.
            foreach (var t in SettlementLayout())
            {
                int n = t.buildings.Length;
                int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
                for (int i = 0; i < n; i++)
                {
                    int col = i % cols, row = i / cols;
                    placeables.Add(SavedPlaceable(t.buildings[i],
                        t.originX + col * TownSpacing, t.originY + row * TownSpacing));
                }
                int plazaY = t.originY - 4;
                placeables.Add(SavedPlaceable("campfire", t.originX + t.gw / 2, plazaY));
                for (int s = 0; s < t.stalls; s++)
                    placeables.Add(SavedPlaceable(s % 2 == 0 ? "market_stall_red" : "market_stall_blue",
                        t.originX + 2 + s * 3, plazaY));
            }

            bootstrap.Placement.RestorePlaceables(placeables.ToArray());
        }

        /// <summary>Fills the all-items chest bank with every registered item so the
        /// full catalogue (materials, tools, weapons, armour, placeables, seeds) can be
        /// pulled and reviewed. Equippables stock 1; stackables stock a small handful.</summary>
        static void StockAllItemsChests(FoundationBootstrap bootstrap)
        {
            if (bootstrap.Storage == null || bootstrap.Content?.Items?.All == null)
                return;

            var items = bootstrap.Content.Items.All;
            int chestCount = AllItemsChestCount(bootstrap.Content);
            for (int i = 0; i < chestCount; i++)
            {
                int cx = AllChestBaseX + i * AllChestStepX;
                var chest = bootstrap.Storage.EnsureContainer("chest", cx, AllChestY, 64);
                if (chest == null)
                    continue;

                int start = i * AllChestPerChest;
                int end = Mathf.Min(start + AllChestPerChest, items.Count);
                for (int k = start; k < end; k++)
                {
                    var it = items[k];
                    if (it == null || string.IsNullOrEmpty(it.id))
                        continue;
                    int count = it.IsEquippable ? 1 : Mathf.Clamp(it.maxStack, 1, 20);
                    chest.Add(it.id, count);
                }
            }
        }

        static FoundationSavedPlaceable SavedPlaceable(string id, int x, int y) =>
            new FoundationSavedPlaceable { placeableId = id, x = x, y = y };

        static void StockResourceChest(FoundationBootstrap bootstrap)
        {
            if (bootstrap.Storage == null)
                return;

            var chest = bootstrap.Storage.EnsureContainer("chest", -20, -2, 64);
            if (chest == null)
                return;

            Add(chest, "wood", 300);
            Add(chest, "stone", 300);
            Add(chest, "fiber", 200);
            Add(chest, "copper_ore", 120);
            Add(chest, "copper_bar", 80);
            Add(chest, "slime_goo", 80);
            Add(chest, "hide", 80);
            Add(chest, "apple", 60);
            Add(chest, "carrot", 60);
            Add(chest, "wheat", 60);
            Add(chest, "carrot_seeds", 80);
            Add(chest, "wheat_seeds", 80);
            Add(chest, "stone_path_item", 120);
            Add(chest, "wood_floor_item", 120);
            Add(chest, "stone_block_item", 80);
            Add(chest, "workbench_item", 8);
            Add(chest, "chest_item", 8);
            Add(chest, "campfire_item", 12);
            Add(chest, "fireplace_item", 6);
            Add(chest, "lantern_item", 24);
            Add(chest, "furnace_item", 6);
            Add(chest, "tavern_door_item", 4);
            Add(chest, "tavern_plot_item", 4);
            Add(chest, "tavern_building_item", 4);
            Add(chest, "library_plot_item", 4);
            Add(chest, "library_building_item", 4);
            Add(chest, "rootcellar_portal_item", 4);
            Add(chest, "hoe", 1);
            Add(chest, "wood_axe", 1);
            Add(chest, "wood_pickaxe", 1);
            Add(chest, "wood_shovel", 1);
            Add(chest, "wood_sword", 1);
            Add(chest, "stone_axe", 1);
            Add(chest, "stone_pickaxe", 1);
            Add(chest, "stone_shovel", 1);
            Add(chest, "stone_sword", 1);
            Add(chest, "copper_axe", 1);
            Add(chest, "copper_pickaxe", 1);
            Add(chest, "copper_shovel", 1);
            Add(chest, "copper_sword", 1);
        }

        static void Add(StorageContainer chest, string itemId, int count)
        {
            int leftover = chest.Add(itemId, count);
            if (leftover > 0)
                Debug.LogWarning($"[CreationInstance] Chest overflow: {itemId} x{leftover}");
        }

        static void SpawnCropSamples(FoundationBootstrap bootstrap)
        {
            if (bootstrap.Farming == null || bootstrap.World == null)
                return;

            var crops = new List<FoundationSavedCrop>();
            for (int i = 0; i < 6; i++)
            {
                crops.Add(new FoundationSavedCrop
                {
                    cropId = i % 2 == 0 ? "carrot_crop" : "wheat_crop",
                    x = -24 + i * 3,
                    y = -21,
                    stage = i % 4,
                    stageTimer = 0f,
                });
            }

            bootstrap.Farming.RestoreCrops(crops.ToArray());
        }

        static void SpawnLabels(FoundationBootstrap bootstrap)
        {
            var root = new GameObject("CreationInstanceShowroom").transform;
            root.SetParent(bootstrap.transform, false);

            Label(root, bootstrap.World, -20, -11, "START + RESOURCE CHEST\nRMB chest for materials");
            Label(root, bootstrap.World, -15, -9, "CRAFTING + CAMP\nWorkbench, furnace, fire");
            Label(root, bootstrap.World, -19, 6, "BUILDINGS + PLOTS\nEnter tavern/library or test plots");
            Label(root, bootstrap.World, 30, 4, "DUNGEON LAB\nT1-T6 baseline row + reroll row\nPress M inside to inspect layout");
            Label(root, bootstrap.World, 25, -22, "RESOURCE NODES\nTrees, rocks, ore, forage");
            Label(root, bootstrap.World, -13, -27, "FARMING STRIP\nSoil and crop growth samples");
            Label(root, bootstrap.World, 42, -27, "TILE PALETTE\nGround/path/block samples");
            Label(root, bootstrap.World, 4, 6, "INSTANCE PORTAL\nRootcellar pocket entrance");

            Label(root, bootstrap.World, AllChestBaseX, AllChestY + 2,
                "ALL ITEMS\nEvery item / weapon / armour — pull & review");
            foreach (var s in SettlementLayout())
                Label(root, bootstrap.World, s.originX + s.gw / 2, s.originY - 8, s.label);
        }

        static void Label(Transform root, IsoWorld world, int x, int y, string text)
        {
            var go = new GameObject($"Label_{x}_{y}");
            go.transform.SetParent(root, false);
            int h = world != null ? world.GetHeight(x, y) : 0;
            go.transform.position = IsoGrid.CellToWorld(x, y, h) + new Vector3(0f, 0.55f, 0f);

            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.045f;
            label.lineSpacing = 0.88f;
            label.color = new Color(1f, 0.92f, 0.62f, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = IsoGrid.SortingOrder(x, y, h, IsoGrid.LayerProp) + 200;
        }

        static void AddTilePalette(List<FoundationSavedCell> cells, FoundationContent content)
        {
            string[] blocks =
            {
                "grass_1", "grass_2", "grass_3",
                "dirt", "stone_path", "wood_floor",
                "soil", "sand_1", "snow_1",
                "stone_block"
            };

            int i = 0;
            for (int y = -23; y <= -15; y += 3)
            for (int x = 36; x <= 48; x += 4)
            {
                if (i >= blocks.Length)
                    return;

                string id = blocks[i++];
                bool solid = id == "stone_block";
                AddCell(cells, x, y, id, null, content, solid);
            }
        }

        static void AddResourceSamples(List<FoundationSavedCell> cells, FoundationContent content)
        {
            string[] nodes = { "tree", "pine", "rock", "copper_vein", "bush", "flower", "stump", "log" };
            int i = 0;
            for (int y = -17; y <= -10; y += 4)
            for (int x = 14; x <= 40; x += 4)
            {
                if (i >= nodes.Length)
                    return;
                AddCell(cells, x, y, "grass_1", nodes[i++], content, false);
            }
        }

        static void AddLowWalls(List<FoundationSavedCell> cells, FoundationContent content)
        {
            for (int i = 0; i < 9; i++)
                AddCell(cells, 38 + i, -13, "stone_block", null, content, true);
        }

        static void AddRect(List<FoundationSavedCell> cells, int x, int y, int w, int h,
            string surfaceBlockId, FoundationContent content)
        {
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                AddCell(cells, xx, yy, surfaceBlockId, null, content, false);
        }

        static void AddCell(List<FoundationSavedCell> cells, int x, int y, string surfaceBlockId,
            string nodeId, FoundationContent content, bool solidBlock)
        {
            var node = !string.IsNullOrWhiteSpace(nodeId) ? content.Nodes.Get(nodeId) : null;
            cells.Add(new FoundationSavedCell
            {
                x = x,
                y = y,
                height = solidBlock ? (byte)1 : (byte)0,
                biomeIndex = 0,
                surfaceBlockId = surfaceBlockId,
                occupantId = null,
                nodeId = node != null ? node.id : null,
                solidBlock = solidBlock,
                water = false,
                occupantBlocks = false,
                nodeBlocks = node != null && node.blocksMovement,
                underBlockId = solidBlock ? "grass_1" : null,
                underHeight = 0,
            });
        }
    }
}
