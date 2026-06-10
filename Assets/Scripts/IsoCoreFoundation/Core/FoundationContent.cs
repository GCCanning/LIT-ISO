using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Holds every content database and builds a complete default content set in
    /// code, so the runtime never depends on hand-authored .asset files.
    /// The editor tool can additionally bake these to disk (see FoundationMenu).
    /// </summary>
    public class FoundationContent
    {
        public readonly BlockDatabase Blocks = new();
        public readonly BlockGroupDatabase BlockGroups = new();
        public readonly BiomeDatabase Biomes = new();
        public readonly ItemDatabase Items = new();
        public readonly PlaceableDatabase Placeables = new();
        public readonly RecipeDatabase Recipes = new();
        public readonly ResourceNodeDatabase Nodes = new();
        public readonly MobDatabase Mobs = new();
        public readonly CropDatabase Crops = new();
        public readonly FoundationCallingDatabase Callings = new();
        public readonly FoundationSkillDatabase Skills = new();
        public readonly FoundationQuestDatabase Quests = new();
        public readonly SystemMessageDatabase SystemMessages = new();
        public readonly EvidenceEventDatabase EvidenceEvents = new();
        public readonly XPChannelDatabase XPChannels = new();
        public readonly TitleDatabase Titles = new();
        public readonly AffinityDatabase Affinities = new();
        public readonly FoundationAbilityDatabase Abilities = new();
        public readonly ClassDatabase Classes = new();
        public readonly ProfessionDatabase Professions = new();
        public readonly DungeonDatabase Dungeons = new();
        public readonly ExpeditionTemplateDatabase Expeditions = new();
        public readonly DungeonResultDatabase DungeonResults = new();
        public readonly GuildBoardEntryDatabase GuildBoardEntries = new();
        public readonly WorldEventDatabase WorldEvents = new();

        static T New<T>(string id) where T : FoundationDefinition
        {
            var def = ScriptableObject.CreateInstance<T>();
            def.id = id;
            def.displayName = Prettify(id);
            def.name = id;
            return def;
        }

        static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var s = id.Replace('_', ' ');
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public static FoundationContent BuildDefault()
        {
            var c = new FoundationContent();

            // ---- Blocks ----
            BlockDefinition Block(string id, string group, Color col, CollisionMode mode)
            {
                var b = New<BlockDefinition>(id);
                b.groupId = group; b.color = col; b.collision = mode;
                c.Blocks.Add(b); return b;
            }

            var grass1 = Block("grass_1", "grass_blocks", new Color(0.30f, 0.55f, 0.22f), CollisionMode.Walkable);
            var grass2 = Block("grass_2", "grass_blocks", new Color(0.34f, 0.60f, 0.25f), CollisionMode.Walkable);
            var grass3 = Block("grass_3", "grass_blocks", new Color(0.27f, 0.49f, 0.20f), CollisionMode.Walkable);
            var sand1 = Block("sand_1", "sand_blocks", new Color(0.85f, 0.78f, 0.45f), CollisionMode.Walkable);
            var sand2 = Block("sand_2", "sand_blocks", new Color(0.82f, 0.74f, 0.40f), CollisionMode.Walkable);
            var snow1 = Block("snow_1", "snow_blocks", new Color(0.91f, 0.94f, 0.97f), CollisionMode.Walkable);
            var snow2 = Block("snow_2", "snow_blocks", new Color(0.85f, 0.88f, 0.93f), CollisionMode.Walkable);
            var badlands1 = Block("badlands_1", "badlands_blocks", new Color(0.36f, 0.24f, 0.16f), CollisionMode.Walkable);
            var badlands2 = Block("badlands_2", "badlands_blocks", new Color(0.32f, 0.21f, 0.14f), CollisionMode.Walkable);
            // Forest terrain per the pack's design: deep-green floor field + dense
            // hedge/canopy blocks that tile into forest mass (029 dominant, 027/028
            // striped accents). Canopy cells ARE the vegetation - no props on them.
            var forestFloor = Block("forest_floor", "forest_blocks", new Color(0.20f, 0.42f, 0.22f), CollisionMode.Walkable);
            var canopy1 = Block("canopy_1", "canopy_blocks", new Color(0.16f, 0.36f, 0.18f), CollisionMode.Walkable);
            var canopy2 = Block("canopy_2", "canopy_blocks", new Color(0.18f, 0.38f, 0.20f), CollisionMode.Walkable);
            var canopy3 = Block("canopy_3", "canopy_blocks", new Color(0.17f, 0.37f, 0.19f), CollisionMode.Walkable);
            Block("water", "water_blocks", new Color(0.20f, 0.45f, 0.75f), CollisionMode.Water);
            Block("water_deep", "water_blocks", new Color(0.10f, 0.22f, 0.45f), CollisionMode.Water);
            // Ocean texture per the rulebook: sparse speckled variants (<= 15% of cells)
            // and wave-swell blocks on the rim band facing the shore.
            Block("water_deep_2", "water_blocks", new Color(0.11f, 0.23f, 0.46f), CollisionMode.Water);
            Block("water_deep_3", "water_blocks", new Color(0.10f, 0.21f, 0.44f), CollisionMode.Water);
            Block("water_swell_1", "water_blocks", new Color(0.14f, 0.28f, 0.52f), CollisionMode.Water);
            Block("water_swell_2", "water_blocks", new Color(0.13f, 0.27f, 0.50f), CollisionMode.Water);
            Block("dirt", "dirt_blocks", new Color(0.45f, 0.32f, 0.20f), CollisionMode.Walkable);
            Block("stone_block", "stone_blocks", new Color(0.55f, 0.55f, 0.58f), CollisionMode.Solid);
            Block("stone_path", "path_blocks", new Color(0.62f, 0.62f, 0.64f), CollisionMode.Decorative);
            var dungeonFloor1 = Block("dungeon_floor_1", "dungeon_floor_blocks", new Color(0.30f, 0.34f, 0.40f), CollisionMode.Decorative);
            var dungeonFloor2 = Block("dungeon_floor_2", "dungeon_floor_blocks", new Color(0.32f, 0.36f, 0.42f), CollisionMode.Decorative);
            var dungeonFloor3 = Block("dungeon_floor_3", "dungeon_floor_blocks", new Color(0.28f, 0.32f, 0.38f), CollisionMode.Decorative);
            var dungeonFloor4 = Block("dungeon_floor_4", "dungeon_floor_blocks", new Color(0.34f, 0.38f, 0.44f), CollisionMode.Decorative);
            var dungeonFloor5 = Block("dungeon_floor_5", "dungeon_floor_blocks", new Color(0.24f, 0.28f, 0.34f), CollisionMode.Decorative);
            Block("wood_floor", "floor_blocks", new Color(0.66f, 0.50f, 0.30f), CollisionMode.Decorative);
            Block("soil", "soil_blocks", new Color(0.40f, 0.27f, 0.16f), CollisionMode.Walkable);

            // ---- Block groups ----
            BlockGroupDefinition Group(string id, params BlockDefinition[] variants)
            {
                var g = New<BlockGroupDefinition>(id);
                g.variants.AddRange(variants);
                c.BlockGroups.Add(g); return g;
            }
            var grassGroup = Group("grass_blocks", grass1, grass2, grass3);
            var sandGroup = Group("sand_blocks", sand1, sand2);
            var snowGroup = Group("snow_blocks", snow1, snow2);
            var badlandsGroup = Group("badlands_blocks", badlands1, badlands2);
            var forestGroup = Group("forest_blocks", forestFloor);
            Group("canopy_blocks", canopy1, canopy2, canopy3);
            var dungeonFloorGroup = Group("dungeon_floor_blocks", dungeonFloor1, dungeonFloor2, dungeonFloor3, dungeonFloor4, dungeonFloor5);

            // ---- Items ----
            ItemDefinition Item(string id, Color col, ItemCategory cat, int stack = 99)
            {
                var it = New<ItemDefinition>(id);
                it.color = col; it.category = cat; it.maxStack = stack;
                c.Items.Add(it); return it;
            }
            Item("wood", new Color(0.55f, 0.38f, 0.20f), ItemCategory.Resource);
            Item("stone", new Color(0.55f, 0.55f, 0.58f), ItemCategory.Resource);
            Item("fiber", new Color(0.50f, 0.70f, 0.35f), ItemCategory.Resource);
            Item("slime_goo", new Color(0.45f, 0.85f, 0.45f), ItemCategory.Resource);
            Item("hide", new Color(0.62f, 0.46f, 0.30f), ItemCategory.Resource);
            var apple = Item("apple", new Color(0.85f, 0.20f, 0.20f), ItemCategory.Food);
            apple.foodRestore = 15;
            var carrot = Item("carrot", new Color(0.90f, 0.45f, 0.15f), ItemCategory.Food);
            carrot.foodRestore = 12;
            Item("wheat", new Color(0.85f, 0.75f, 0.35f), ItemCategory.Resource);
            var roastedApple = Item("roasted_apple", new Color(0.95f, 0.36f, 0.18f), ItemCategory.Food);
            roastedApple.foodRestore = 24;
            var campStew = Item("camp_stew", new Color(0.76f, 0.50f, 0.25f), ItemCategory.Food);
            campStew.foodRestore = 40;

            ItemDefinition Seed(string id, Color col, string cropId)
            {
                var it = Item(id, col, ItemCategory.Misc);
                it.plantCropId = cropId; return it;
            }
            Seed("carrot_seeds", new Color(0.95f, 0.55f, 0.25f), "carrot_crop");
            Seed("wheat_seeds", new Color(0.80f, 0.72f, 0.40f), "wheat_crop");

            ItemDefinition BlockItem(string id, Color col, string placeBlockId)
            {
                var it = Item(id, col, ItemCategory.Block);
                it.placeBlockId = placeBlockId; return it;
            }
            BlockItem("stone_block_item", new Color(0.55f, 0.55f, 0.58f), "stone_block");
            BlockItem("stone_path_item", new Color(0.62f, 0.62f, 0.64f), "stone_path");
            BlockItem("wood_floor_item", new Color(0.66f, 0.50f, 0.30f), "wood_floor");

            Item("copper_ore", new Color(0.80f, 0.45f, 0.25f), ItemCategory.Resource);
            Item("copper_bar", new Color(0.85f, 0.55f, 0.35f), ItemCategory.Resource);

            ItemDefinition Tool(string id, Color col, ToolType tool, int tier, int durability)
            {
                var it = Item(id, col, ItemCategory.Tool, 1);
                it.toolType = tool;
                it.toolTier = tier;
                it.maxDurability = durability;
                it.durabilityLossPerUse = 1;
                return it;
            }
            Tool("wood_axe", new Color(0.60f, 0.42f, 0.24f), ToolType.Axe, 1, 80);
            Tool("wood_pickaxe", new Color(0.60f, 0.42f, 0.24f), ToolType.Pickaxe, 1, 80);
            Tool("wood_shovel", new Color(0.58f, 0.40f, 0.22f), ToolType.Shovel, 1, 70);
            Tool("wood_sword", new Color(0.62f, 0.44f, 0.26f), ToolType.Sword, 1, 85);
            Tool("stone_axe", new Color(0.55f, 0.55f, 0.58f), ToolType.Axe, 2, 150);
            Tool("stone_pickaxe", new Color(0.55f, 0.55f, 0.58f), ToolType.Pickaxe, 2, 150);
            Tool("stone_shovel", new Color(0.56f, 0.56f, 0.58f), ToolType.Shovel, 2, 135);
            Tool("stone_sword", new Color(0.60f, 0.60f, 0.62f), ToolType.Sword, 2, 165);
            Tool("copper_axe", new Color(0.85f, 0.55f, 0.35f), ToolType.Axe, 3, 260);
            Tool("copper_pickaxe", new Color(0.85f, 0.55f, 0.35f), ToolType.Pickaxe, 3, 260);
            Tool("copper_shovel", new Color(0.84f, 0.52f, 0.32f), ToolType.Shovel, 3, 230);
            Tool("copper_sword", new Color(0.90f, 0.58f, 0.36f), ToolType.Sword, 3, 280);
            Tool("hoe", new Color(0.55f, 0.42f, 0.28f), ToolType.Hoe, 1, 120);

            ItemDefinition PlaceItem(string id, Color col, string placeableId)
            {
                var it = Item(id, col, ItemCategory.Placeable, 99);
                it.placeableId = placeableId; return it;
            }
            PlaceItem("workbench_item", new Color(0.60f, 0.45f, 0.30f), "workbench");
            PlaceItem("chest_item", new Color(0.70f, 0.55f, 0.35f), "chest");
            PlaceItem("lantern_item", new Color(0.95f, 0.85f, 0.40f), "lantern");
            PlaceItem("furnace_item", new Color(0.45f, 0.42f, 0.45f), "furnace");
            PlaceItem("campfire_item", new Color(0.90f, 0.42f, 0.18f), "campfire");
            PlaceItem("fireplace_item", new Color(0.88f, 0.48f, 0.20f), "fireplace");
            PlaceItem("tavern_door_item", new Color(0.58f, 0.34f, 0.18f), "tavern_door");
            PlaceItem("tavern_plot_item", new Color(0.56f, 0.38f, 0.22f), "tavern_plot");
            PlaceItem("tavern_building_item", new Color(0.66f, 0.40f, 0.22f), "tavern_building");
            PlaceItem("library_plot_item", new Color(0.58f, 0.56f, 0.50f), "library_plot");
            PlaceItem("library_building_item", new Color(0.60f, 0.58f, 0.52f), "library_building");
            PlaceItem("rootcellar_portal_item", new Color(0.40f, 0.75f, 0.85f), "rootcellar_portal");

            // ---- Placeables ----
            PlaceableDefinition Placeable(string id, Color col, bool blocks, InteractionKind kind,
                StationType station, string reqItem, float h = 1f)
            {
                var p = New<PlaceableDefinition>(id);
                p.color = col; p.blocksMovement = blocks; p.interaction = kind;
                p.stationType = station; p.requiredItemId = reqItem; p.heightUnits = h;
                c.Placeables.Add(p); return p;
            }
            Placeable("workbench", new Color(0.60f, 0.45f, 0.30f), true,
                InteractionKind.CraftingStation, StationType.Workbench, "workbench_item", 0.9f);
            Placeable("chest", new Color(0.70f, 0.55f, 0.35f), true,
                InteractionKind.Container, StationType.None, "chest_item", 0.8f);
            var lantern = Placeable("lantern", new Color(0.95f, 0.85f, 0.40f), false,
                InteractionKind.Decoration, StationType.None, "lantern_item", 0.9f);
            lantern.emitsLight = true;
            lantern.lightColor = new Color(1f, 0.9f, 0.55f);
            lantern.lightRadius = 2.2f;
            Placeable("furnace", new Color(0.45f, 0.42f, 0.45f), true,
                InteractionKind.CraftingStation, StationType.Furnace, "furnace_item", 1.0f);
            var campfire = Placeable("campfire", new Color(0.5f, 0.32f, 0.2f), false,
                InteractionKind.Decoration, StationType.None, "campfire_item", 0.5f);
            campfire.stationType = StationType.CookingPot;
            campfire.emitsLight = true;
            campfire.lightColor = new Color(1f, 0.62f, 0.28f);
            campfire.lightRadius = 3.0f;
            campfire.isCampsite = true;
            campfire.campTier = 1;
            campfire.campWardRadius = 5.5f;
            campfire.campRecoveryMultiplier = 1.25f;
            var fireplace = Placeable("fireplace", new Color(0.50f, 0.28f, 0.18f), true,
                InteractionKind.Decoration, StationType.None, "fireplace_item", 0.8f);
            fireplace.displayName = "Fireplace";
            fireplace.widthUnits = 0.95f;
            fireplace.stationType = StationType.CookingPot;
            fireplace.emitsLight = true;
            fireplace.lightColor = new Color(1f, 0.58f, 0.24f);
            fireplace.lightRadius = 3.6f;
            fireplace.isCampsite = true;
            fireplace.campTier = 2;
            fireplace.campWardRadius = 7.0f;
            fireplace.campRecoveryMultiplier = 1.55f;
            var tavernDoor = Placeable("tavern_door", new Color(0.58f, 0.34f, 0.18f), true,
                InteractionKind.Entrance, StationType.None, "tavern_door_item", 1.2f);
            tavernDoor.entranceLabel = "Enter";
            tavernDoor.destinationId = "tavern_common_room";
            tavernDoor.destinationDisplayName = "Tavern";
            tavernDoor.widthUnits = 0.9f;
            var tavernPlot = Placeable("tavern_plot", new Color(0.56f, 0.38f, 0.22f), true,
                InteractionKind.Construction, StationType.None, "tavern_plot_item", 0.35f);
            tavernPlot.displayName = "Tavern Plot";
            tavernPlot.widthUnits = 2.45f;
            tavernPlot.footprintWidth = 3;
            tavernPlot.footprintHeight = 3;
            tavernPlot.footprintLabel = "Tavern";
            tavernPlot.constructionResultPlaceableId = "tavern_building";
            tavernPlot.constructionCost = new[]
            {
                new RecipeIngredient("wood", 24),
                new RecipeIngredient("stone", 10),
                new RecipeIngredient("fiber", 8)
            };
            var tavernBuilding = Placeable("tavern_building", new Color(0.66f, 0.40f, 0.22f), true,
                InteractionKind.Entrance, StationType.None, "tavern_building_item", 1.9f);
            tavernBuilding.entranceLabel = "Enter";
            tavernBuilding.destinationId = "tavern_common_room";
            tavernBuilding.destinationDisplayName = "Tavern";
            tavernBuilding.widthUnits = 2.65f;
            tavernBuilding.footprintWidth = 3;
            tavernBuilding.footprintHeight = 3;
            tavernBuilding.footprintLabel = "Tavern";
            var libraryPlot = Placeable("library_plot", new Color(0.58f, 0.56f, 0.50f), true,
                InteractionKind.Construction, StationType.None, "library_plot_item", 0.35f);
            libraryPlot.displayName = "Library Plot";
            libraryPlot.widthUnits = 1.2f;
            libraryPlot.constructionResultPlaceableId = "library_building";
            libraryPlot.constructionCost = new[]
            {
                new RecipeIngredient("stone", 28),
                new RecipeIngredient("wood", 18),
                new RecipeIngredient("fiber", 6)
            };
            var libraryBuilding = Placeable("library_building", new Color(0.60f, 0.58f, 0.52f), true,
                InteractionKind.Entrance, StationType.None, "library_building_item", 2.0f);
            libraryBuilding.entranceLabel = "Enter";
            libraryBuilding.destinationId = "library_archive";
            libraryBuilding.destinationDisplayName = "Library";
            libraryBuilding.widthUnits = 1.45f;
            var rootcellarPortal = Placeable("rootcellar_portal", new Color(0.40f, 0.75f, 0.85f), false,
                InteractionKind.Entrance, StationType.None, "rootcellar_portal_item", 1.1f);
            rootcellarPortal.entranceLabel = "Enter";
            rootcellarPortal.destinationId = "rootcellar_starter";
            rootcellarPortal.destinationDisplayName = "Mosswake Rootcellar";

            // ---- Resource nodes ----
            ResourceNodeDefinition Node(string id, Color col, ToolType tool, bool mandatory, int hits, float h, ItemDrop[] drops)
            {
                var n = New<ResourceNodeDefinition>(id);
                n.color = col; n.requiredTool = tool; n.toolMandatory = mandatory;
                n.hitsToHarvest = hits; n.heightUnits = h; n.drops = drops;
                c.Nodes.Add(n); return n;
            }
            // Axe/pickaxe preferred (faster) but hand-harvestable; ore veins REQUIRE a pickaxe.
            var tree = Node("tree", new Color(0.18f, 0.40f, 0.18f), ToolType.Axe, false, 7, 1.5f,
                new[] { new ItemDrop("wood", 2, 4) });
            var rock = Node("rock", new Color(0.50f, 0.50f, 0.53f), ToolType.Pickaxe, false, 6, 0.9f,
                new[] { new ItemDrop("stone", 2, 3) });
            var bush = Node("bush", new Color(0.25f, 0.50f, 0.25f), ToolType.None, false, 3, 0.7f,
                new[] { new ItemDrop("fiber", 1, 2), new ItemDrop("apple", 0, 1, 0.5f) });
            var copperVein = Node("copper_vein", new Color(0.80f, 0.45f, 0.25f), ToolType.Pickaxe, true, 9, 0.95f,
                new[] { new ItemDrop("copper_ore", 1, 2), new ItemDrop("stone", 0, 1, 0.5f) });
            // Extra flora for visual variety (art in Resources/Decorations). Pine = a second
            // tree species for groves; flower = ambient ground cover; stump/log = low woody bits.
            var pine = Node("pine", new Color(0.16f, 0.34f, 0.22f), ToolType.Axe, false, 7, 1.5f,
                new[] { new ItemDrop("wood", 2, 4) });
            var flower = Node("flower", new Color(0.70f, 0.55f, 0.20f), ToolType.None, false, 2, 0.3f,
                new[] { new ItemDrop("fiber", 1, 1) });
            var stump = Node("stump", new Color(0.45f, 0.32f, 0.18f), ToolType.Axe, false, 4, 0.5f,
                new[] { new ItemDrop("wood", 1, 2) });
            var log = Node("log", new Color(0.42f, 0.29f, 0.16f), ToolType.Axe, false, 4, 0.4f,
                new[] { new ItemDrop("wood", 2, 3) });
            // Pack props: foam-footed shore stone (water edges), grass tuft, and a pink
            // tulip cluster as a flower variant (art in Resources/Decorations).
            var shoreStone = Node("shore_stone", new Color(0.55f, 0.58f, 0.60f), ToolType.Pickaxe, false, 5, 0.7f,
                new[] { new ItemDrop("stone", 1, 2) });
            shoreStone.blocksMovement = true;
            var tuft = Node("tuft", new Color(0.30f, 0.55f, 0.28f), ToolType.None, false, 2, 0.3f,
                new[] { new ItemDrop("fiber", 1, 2) });
            var tulip = Node("flower_tulip", new Color(0.80f, 0.40f, 0.70f), ToolType.None, false, 2, 0.3f,
                new[] { new ItemDrop("fiber", 1, 1) });

            // ---- Mobs ----
            MobDefinition Mob(string id, Color col, MobBehavior beh, float speed, float wander, ItemDrop[] drops)
            {
                var m = New<MobDefinition>(id);
                m.color = col; m.behaviour = beh; m.moveSpeed = speed; m.wanderRadius = wander; m.drops = drops;
                c.Mobs.Add(m); return m;
            }
            var deer = Mob("deer", new Color(0.62f, 0.45f, 0.30f), MobBehavior.Skittish, 1.6f, 6f,
                new[] { new ItemDrop("hide", 1, 2) });
            deer.threatTier = 0;
            deer.campWardIgnoreChance = 0f;
            deer.contactDamage = 0f;
            var slime = Mob("slime", new Color(0.40f, 0.82f, 0.45f), MobBehavior.Passive, 1.0f, 3f,
                new[] { new ItemDrop("slime_goo", 1, 2) });
            slime.threatTier = 1;
            slime.campWardIgnoreChance = 0.12f;
            slime.contactDamage = 4f;
            var fox = Mob("fox", new Color(0.85f, 0.45f, 0.20f), MobBehavior.Skittish, 2.0f, 7f,
                new[] { new ItemDrop("hide", 1, 1) });
            fox.threatTier = 2;
            fox.campWardIgnoreChance = 0.28f;
            fox.contactDamage = 6f;

            // ---- Crops ----
            void Crop(string id, Color young, Color ripe, int stages, float secs, float matureH, ItemDrop[] harvest)
            {
                var cr = New<CropDefinition>(id);
                cr.youngColor = young; cr.ripeColor = ripe; cr.stages = stages;
                cr.secondsPerStage = secs; cr.matureHeightUnits = matureH; cr.harvest = harvest;
                c.Crops.Add(cr);
            }
            Crop("carrot_crop", new Color(0.40f, 0.65f, 0.30f), new Color(0.90f, 0.50f, 0.15f), 3, 8f, 0.5f,
                new[] { new ItemDrop("carrot", 2, 3), new ItemDrop("carrot_seeds", 1, 1, 0.5f) });
            Crop("wheat_crop", new Color(0.45f, 0.70f, 0.35f), new Color(0.88f, 0.78f, 0.30f), 4, 7f, 1.0f,
                new[] { new ItemDrop("wheat", 2, 3), new ItemDrop("wheat_seeds", 1, 1, 0.5f) });

            // ---- Biomes ----
            BiomeDefinition Biome(string id, float temp, float moist, BlockGroupDefinition group,
                int baseH, int variance, BiomeNodeSpawn[] nodes, BiomeMobSpawn[] mobs, Color tint)
            {
                var b = New<BiomeDefinition>(id);
                b.temperature = temp; b.moisture = moist; b.surfaceGroup = group;
                b.baseHeight = baseH; b.heightVariance = variance;
                b.nodes = nodes; b.mobs = mobs; b.debugTint = tint;
                c.Biomes.Add(b); return b;
            }
            BiomeNodeSpawn NS(ResourceNodeDefinition n, float chance) =>
                new BiomeNodeSpawn { node = n, chancePerCell = chance };
            BiomeMobSpawn MS(MobDefinition m, float w) => new BiomeMobSpawn { mob = m, weight = w };

            // Rulebook: meadow carries no ore - copper lives in forest cover and badlands.
            Biome("meadow", 0.55f, 0.55f, grassGroup, 1, 2,
                new[] { NS(tree, 0.05f), NS(rock, 0.02f), NS(bush, 0.05f),
                        NS(flower, 0.03f), NS(tulip, 0.012f), NS(tuft, 0.03f),
                        NS(log, 0.005f), NS(stump, 0.005f) },
                new[] { MS(deer, 1f), MS(slime, 1f) }, new Color(0.4f, 0.7f, 0.4f));
            Biome("forest", 0.45f, 0.85f, forestGroup, 1, 3,
                new[] { NS(tree, 0.14f), NS(bush, 0.06f), NS(rock, 0.02f), NS(copperVein, 0.01f),
                        NS(flower, 0.012f), NS(tuft, 0.02f), NS(log, 0.01f), NS(stump, 0.01f) },
                new[] { MS(deer, 1f), MS(fox, 1f), MS(slime, 0.5f) }, new Color(0.25f, 0.55f, 0.30f));
            // Desert renders as pack badlands (dark cracked floor) - the pack has no
            // sandy-desert family; sand stays reserved for beaches and river banks.
            Biome("desert", 0.88f, 0.15f, badlandsGroup, 1, 1,
                new[] { NS(rock, 0.05f), NS(copperVein, 0.015f) },
                new[] { MS(slime, 1f) }, new Color(0.85f, 0.78f, 0.45f));
            // Rulebook: beaches carry only sparse rock - no vegetation on sand.
            Biome("beach", 0.70f, 0.45f, sandGroup, 1, 1,
                new[] { NS(rock, 0.02f) },
                new[] { MS(fox, 0.5f), MS(slime, 1f) }, new Color(0.90f, 0.84f, 0.55f));
            // Cold biome is a TAIGA: pine-heavy forest on grass ground (Minecraft-style
            // rule - trees grow on grass, never on bare stone/snow plates). The pack has
            // no snow family, so snowGroup stays unused until real snow art exists.
            Biome("snow", 0.12f, 0.45f, grassGroup, 1, 2,
                new[] { NS(tree, 0.04f), NS(pine, 0.05f), NS(rock, 0.03f), NS(copperVein, 0.008f),
                        NS(tuft, 0.02f), NS(log, 0.008f), NS(stump, 0.008f) },
                new[] { MS(deer, 1f), MS(fox, 0.5f) }, new Color(0.9f, 0.93f, 0.97f));

            // ---- Recipes ----
            void Recipe(string id, StationType station, RecipeIngredient[] inputs, ItemStack[] outputs)
            {
                var r = New<RecipeDefinition>(id);
                r.station = station; r.inputs = inputs; r.outputs = outputs;
                c.Recipes.Add(r);
            }
            RecipeIngredient In(string id, int n) => new RecipeIngredient(id, n);
            ItemStack Out(string id, int n) => new ItemStack(id, n);

            Recipe("craft_workbench", StationType.Hand,
                new[] { In("wood", 5) }, new[] { Out("workbench_item", 1) });
            Recipe("craft_campfire", StationType.Hand,
                new[] { In("wood", 5), In("stone", 3) }, new[] { Out("campfire_item", 1) });
            Recipe("craft_fireplace", StationType.Workbench,
                new[] { In("wood", 6), In("stone", 8) }, new[] { Out("fireplace_item", 1) });
            Recipe("cook_roasted_apple", StationType.CookingPot,
                new[] { In("apple", 1), In("wood", 1) }, new[] { Out("roasted_apple", 1) });
            Recipe("cook_camp_stew", StationType.CookingPot,
                new[] { In("carrot", 1), In("wheat", 1), In("wood", 1) }, new[] { Out("camp_stew", 1) });
            Recipe("craft_tavern_door", StationType.Hand,
                new[] { In("wood", 8), In("stone", 2) }, new[] { Out("tavern_door_item", 1) });
            Recipe("craft_tavern_plot", StationType.Workbench,
                new[] { In("wood", 4), In("fiber", 2) }, new[] { Out("tavern_plot_item", 1) });
            Recipe("craft_tavern_building", StationType.Workbench,
                new[] { In("wood", 24), In("stone", 10), In("fiber", 8) }, new[] { Out("tavern_building_item", 1) });
            Recipe("craft_library_plot", StationType.Workbench,
                new[] { In("wood", 4), In("stone", 4), In("fiber", 2) }, new[] { Out("library_plot_item", 1) });
            Recipe("craft_rootcellar_portal", StationType.Workbench,
                new[] { In("stone", 6), In("fiber", 4) }, new[] { Out("rootcellar_portal_item", 1) });
            Recipe("craft_wood_axe", StationType.Hand,
                new[] { In("wood", 3), In("fiber", 2) }, new[] { Out("wood_axe", 1) });
            Recipe("craft_wood_pickaxe", StationType.Workbench,
                new[] { In("wood", 3), In("stone", 2) }, new[] { Out("wood_pickaxe", 1) });
            Recipe("craft_wood_shovel", StationType.Hand,
                new[] { In("wood", 3), In("fiber", 1) }, new[] { Out("wood_shovel", 1) });
            Recipe("craft_wood_sword", StationType.Hand,
                new[] { In("wood", 4), In("fiber", 2) }, new[] { Out("wood_sword", 1) });
            Recipe("craft_stone_path", StationType.Workbench,
                new[] { In("stone", 2) }, new[] { Out("stone_path_item", 4) });
            Recipe("craft_wood_floor", StationType.Workbench,
                new[] { In("wood", 2) }, new[] { Out("wood_floor_item", 4) });
            Recipe("craft_chest", StationType.Workbench,
                new[] { In("wood", 8) }, new[] { Out("chest_item", 1) });
            Recipe("craft_lantern", StationType.Workbench,
                new[] { In("wood", 2), In("stone", 1) }, new[] { Out("lantern_item", 1) });
            Recipe("craft_stone_block", StationType.Workbench,
                new[] { In("stone", 3) }, new[] { Out("stone_block_item", 1) });
            Recipe("craft_furnace", StationType.Workbench,
                new[] { In("stone", 8) }, new[] { Out("furnace_item", 1) });
            Recipe("smelt_copper", StationType.Furnace,
                new[] { In("copper_ore", 2) }, new[] { Out("copper_bar", 1) });
            Recipe("craft_stone_axe", StationType.Workbench,
                new[] { In("wood", 2), In("stone", 3) }, new[] { Out("stone_axe", 1) });
            Recipe("craft_stone_pickaxe", StationType.Workbench,
                new[] { In("wood", 2), In("stone", 3) }, new[] { Out("stone_pickaxe", 1) });
            Recipe("craft_stone_shovel", StationType.Workbench,
                new[] { In("wood", 2), In("stone", 2) }, new[] { Out("stone_shovel", 1) });
            Recipe("craft_stone_sword", StationType.Workbench,
                new[] { In("wood", 2), In("stone", 4) }, new[] { Out("stone_sword", 1) });
            Recipe("craft_copper_axe", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 3) }, new[] { Out("copper_axe", 1) });
            Recipe("craft_copper_pickaxe", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 3) }, new[] { Out("copper_pickaxe", 1) });
            Recipe("craft_copper_shovel", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 2) }, new[] { Out("copper_shovel", 1) });
            Recipe("craft_copper_sword", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 4) }, new[] { Out("copper_sword", 1) });

            // ---- LitRPG progression: skills, Callings, starter quests ----
            FoundationSkillDefinition Skill(string id, string name, FoundationProgressionActivity activity,
                FoundationSkillNodeKind kind, string description, params string[] unlocks)
            {
                var s = New<FoundationSkillDefinition>(id);
                s.displayName = name;
                s.activity = activity;
                s.primaryNodeKind = kind;
                s.description = description;
                s.unlocks = unlocks;
                c.Skills.Add(s);
                return s;
            }

            Skill("foraging", "Foraging", FoundationProgressionActivity.Harvest, FoundationSkillNodeKind.Insight,
                "Read herbs, mushrooms, berries, fibers, and wild nodes.", "rare sprouts", "preserve recipes", "node reading");
            Skill("woodcraft", "Woodcraft", FoundationProgressionActivity.Harvest, FoundationSkillNodeKind.Yield,
                "Shape wood into bridges, furniture, grove care, and warm structures.", "timber tiers", "bridges", "grove care");
            Skill("mining", "Mining", FoundationProgressionActivity.Harvest, FoundationSkillNodeKind.Yield,
                "Break stone, ore, crystal, and clay with better precision.", "reinforced tools", "cellar rooms", "furnace chains");
            Skill("farming", "Farming", FoundationProgressionActivity.Farm, FoundationSkillNodeKind.Harmony,
                "Care for soil, seeds, crop rotation, and living fields.", "crop traits", "seed memory", "greenhouses");
            Skill("cooking", "Cooking", FoundationProgressionActivity.Craft, FoundationSkillNodeKind.Ease,
                "Turn food into buffs, comfort, visitor favorites, and festival meals.", "buff meals", "comfort feasts", "NPC favorites");
            Skill("crafting", "Crafting", FoundationProgressionActivity.Craft, FoundationSkillNodeKind.Utility,
                "Create tools, components, clothing, and repair kits.", "quality grades", "mod slots", "repair kits");
            Skill("building", "Building", FoundationProgressionActivity.Build, FoundationSkillNodeKind.Expression,
                "Place floors, walls, paths, utilities, civic structures, and decor.", "room types", "civic structures", "defense layouts");
            Skill("exploration", "Exploration", FoundationProgressionActivity.Explore, FoundationSkillNodeKind.Insight,
                "Map routes, climb ridges, read landmarks, and find hidden resources.", "shortcuts", "landmarks", "hidden resources");
            Skill("creaturecraft", "Creaturecraft", FoundationProgressionActivity.Creature, FoundationSkillNodeKind.Harmony,
                "Calm, lure, tame, relocate, and convert dens.", "calming", "lures", "den conversion");
            Skill("combat", "Combat", FoundationProgressionActivity.Combat, FoundationSkillNodeKind.Utility,
                "Read attack windows, swing weapons cleanly, dodge pressure, and finish dungeon threats.", "weapon timing", "dungeon tactics", "finisher cues");
            Skill("warding", "Warding", FoundationProgressionActivity.Combat, FoundationSkillNodeKind.Utility,
                "Use lights, traps, patrol posts, and wards to shape threat.", "non-lethal defenses", "threat shaping", "patrol posts");
            Skill("spellcraft", "Spellcraft", FoundationProgressionActivity.Magic, FoundationSkillNodeKind.Insight,
                "Shape neutral mana and elemental affinities into clean, readable spell effects.", "mana forms", "element tuning", "spell circuits");
            Skill("trade", "Trade", FoundationProgressionActivity.Trade, FoundationSkillNodeKind.Ease,
                "Handle requests, vendors, caravans, and special orders.", "better prices", "visitor schedules", "special orders");
            Skill("lorekeeping", "Lorekeeping", FoundationProgressionActivity.Lore, FoundationSkillNodeKind.Insight,
                "Journal relics, plaques, dialogue, and hidden recipe clues.", "memory pages", "shrine upgrades", "ancient recipes");

            FoundationCallingDefinition Calling(string id, string name, string title, string description,
                string capstone, FoundationStatBonus[] bonuses, string[] branches, params string[] starterSkills)
            {
                var calling = New<FoundationCallingDefinition>(id);
                calling.displayName = name;
                calling.startingTitle = title;
                calling.description = description;
                calling.capstone = capstone;
                calling.statBonuses = bonuses;
                calling.branchIds = branches;
                calling.starterSkillIds = starterSkills;
                c.Callings.Add(calling);
                return calling;
            }

            FoundationStatBonus Bonus(FoundationStatType stat, int amount) => new FoundationStatBonus(stat, amount);

            Calling("hearthwarden", "Hearthwarden", "Keeper of First Fire",
                "Keeper of home, food, safe nights, rest, and settlement morale.",
                "Day Feast: one meal sets a whole-day theme for the settlement.",
                new[] { Bonus(FoundationStatType.VIT, 2), Bonus(FoundationStatType.LUCK, 1) },
                new[] { "cook", "caretaker", "festival_host" }, "cooking", "building", "trade");
            Calling("greenhand", "Greenhand", "Sprout-Tender",
                "Farmer, grower, soil reader, and animal friend.",
                "Remembering Fields: fields mutate crops based on care history.",
                new[] { Bonus(FoundationStatType.VIT, 1), Bonus(FoundationStatType.DEX, 1), Bonus(FoundationStatType.LUCK, 1) },
                new[] { "cropkeeper", "beastfriend", "orchard_sage" }, "farming", "foraging", "creaturecraft");
            Calling("stonewright", "Stonewright", "Road-Hand",
                "Builder, mason, path-maker, and civic structure planner.",
                "Civic Landmark: build one regional structure that permanently changes services.",
                new[] { Bonus(FoundationStatType.STR, 2), Bonus(FoundationStatType.DEF, 1) },
                new[] { "mason", "roadmaker", "hall_builder" }, "building", "mining", "warding");
            Calling("threadsmith", "Threadsmith", "Bench-Adept",
                "Crafter, tailor, tool tuner, and workstation chain specialist.",
                "Storied Masterwork: a crafted item gains a name, history, and evolving trait.",
                new[] { Bonus(FoundationStatType.DEX, 2), Bonus(FoundationStatType.INT, 1) },
                new[] { "toolwright", "weaver", "relic_tinker" }, "crafting", "woodcraft", "lorekeeping");
            Calling("pathlighter", "Pathlighter", "Lantern Scout",
                "Explorer, mapper, ruin-reader, route opener, and shrine finder.",
                "Safe Route: discovered paths become fast travel and safer NPC travel lines.",
                new[] { Bonus(FoundationStatType.DEX, 1), Bonus(FoundationStatType.INT, 1), Bonus(FoundationStatType.LUCK, 1) },
                new[] { "scout", "cartographer", "ruin_guide" }, "exploration", "lorekeeping", "warding");
            Calling("bramblebound", "Bramblebound", "Wildspeaker",
                "Herbalist, denkeeper, creature calmer, and gentle wild-magic survivor.",
                "Den Accord: pacified mob dens become resource biomes instead of hazards.",
                new[] { Bonus(FoundationStatType.INT, 2), Bonus(FoundationStatType.LUCK, 1) },
                new[] { "herbalist", "denkeeper", "wildspeaker" }, "creaturecraft", "foraging", "farming");
            Calling("lanternblade", "Lanternblade", "First Patrol",
                "Protector, patrol fighter, shieldhand, and gloom-clearing defender.",
                "Patrol Legend: patrol routes reduce threat and unlock heroic town events.",
                new[] { Bonus(FoundationStatType.STR, 1), Bonus(FoundationStatType.DEF, 2) },
                new[] { "patroller", "shieldhand", "gloombreaker" }, "combat", "warding", "exploration");

            FoundationQuestDefinition Quest(string id, string name, FoundationQuestType type, string act,
                string description, FoundationQuestObjective[] objectives, FoundationQuestReward[] rewards)
            {
                var q = New<FoundationQuestDefinition>(id);
                q.displayName = name;
                q.type = type;
                q.act = act;
                q.description = description;
                q.objectives = objectives;
                q.rewards = rewards;
                c.Quests.Add(q);
                return q;
            }

            FoundationQuestObjective Obj(string id, string text, int required = 1) =>
                new FoundationQuestObjective(id, text, required);
            FoundationQuestReward Reward(FoundationRewardType type, string id, int amount = 1) =>
                new FoundationQuestReward(type, id, amount);

            Quest("first_flame_first_field", "First Flame, First Field", FoundationQuestType.Hearth, "Act 1: First Fire",
                "Make the first camp feel like a place that might remember you.",
                new[] { Obj("gather_wood", "Gather wood", 5), Obj("craft_workbench", "Craft a workbench"), Obj("till_soil", "Till your first soil") },
                new[] { Reward(FoundationRewardType.Xp, "character", 40), Reward(FoundationRewardType.Recipe, "craft_campfire") });
            Quest("a_roof_before_rain", "A Roof Before Rain", FoundationQuestType.Civic, "Act 1: First Fire",
                "Prepare a real shelter before the first hard weather rolls through Mosswake.",
                new[] { Obj("place_floor", "Place wood floor tiles", 4), Obj("place_lantern", "Place a lantern"), Obj("place_chest", "Place a chest") },
                new[] { Reward(FoundationRewardType.Xp, "character", 60), Reward(FoundationRewardType.Pattern, "hearthplank_flooring") });
            Quest("thread_twig_and_tin", "Thread, Twig, and Tin", FoundationQuestType.Craft, "Act 1: First Fire",
                "Learn the first loop of gathering, refining, and improving a tool.",
                new[] { Obj("gather_fiber", "Gather fiber", 3), Obj("mine_stone", "Mine stone", 5), Obj("craft_tool", "Craft any tool") },
                new[] { Reward(FoundationRewardType.Xp, "character", 60), Reward(FoundationRewardType.TraitSeed, "sturdy") });
            Quest("fixing_the_south_path", "Fixing the South Path", FoundationQuestType.Path, "Act 2: Green Roads",
                "Reopen the old path so visitors can find the homestead without crossing brambles.",
                new[] { Obj("craft_path", "Craft stone path pieces", 4), Obj("place_path", "Place path pieces", 4), Obj("clear_node", "Clear one blocking resource node") },
                new[] { Reward(FoundationRewardType.Xp, "character", 80), Reward(FoundationRewardType.RegionShift, "mosswake_path_safe") });
            Quest("the_rootcellar_below", "The Rootcellar Below", FoundationQuestType.Exploration, "Act 2: Green Roads",
                "Find the first underground room and bring back proof that the old systems are still humming.",
                new[] { Obj("enter_cellar", "Enter the Rootcellar"), Obj("recover_relic", "Recover a Memory Amber"), Obj("return_home", "Return home safely") },
                new[] { Reward(FoundationRewardType.Xp, "character", 100), Reward(FoundationRewardType.MemoryPage, "old_lamps_01") });

            // ---- Seven-day trial data spine ----
            FoundationEvidenceWeight W(TrialEvidenceCategory category, int amount) => new FoundationEvidenceWeight(category, amount);
            FoundationXpGrant X(FoundationXpChannel channel, string id, int amount) => new FoundationXpGrant(channel, id, amount);
            FoundationTitleProgressGrant T(string id, int amount) => new FoundationTitleProgressGrant(id, amount);
            FoundationAffinityGrant A(string id, int amount) => new FoundationAffinityGrant(id, amount);

            SystemMessageDefinition SystemMessage(string id, SystemMessageChannel channel, string text, int priority = 1)
            {
                var m = New<SystemMessageDefinition>(id);
                m.channel = channel;
                m.text = text;
                m.priority = priority;
                c.SystemMessages.Add(m);
                return m;
            }

            XPChannelDefinition XpChannel(FoundationXpChannel channel, string id, string name, string description, int xpPerLevel = 100)
            {
                var x = New<XPChannelDefinition>(id);
                x.displayName = name;
                x.channel = channel;
                x.description = description;
                x.xpPerLevel = xpPerLevel;
                c.XPChannels.Add(x);
                return x;
            }

            TitleDefinition Title(string id, string name, int threshold, string effectPolicy, string message, bool mechanical = false, string hiddenClassKey = "")
            {
                var t = New<TitleDefinition>(id);
                t.displayName = name;
                t.threshold = threshold;
                t.effectPolicy = effectPolicy;
                t.unlockMessage = message;
                t.mechanical = mechanical;
                t.hiddenClassKey = hiddenClassKey;
                c.Titles.Add(t);
                return t;
            }

            AffinityDefinition Affinity(string id, string name, string family, int awakenThreshold, string description, params string[] rewards)
            {
                var a = New<AffinityDefinition>(id);
                a.displayName = name;
                a.family = family;
                a.awakenThreshold = awakenThreshold;
                a.description = description;
                a.thresholdRewards = rewards;
                c.Affinities.Add(a);
                return a;
            }

            EvidenceEventDefinition Evidence(string id, string name, string message,
                FoundationEvidenceWeight[] weights, FoundationXpGrant[] xp,
                FoundationTitleProgressGrant[] titles, FoundationAffinityGrant[] affinities,
                SystemMessageChannel channel = SystemMessageChannel.TrialEvidence)
            {
                var e = New<EvidenceEventDefinition>(id);
                e.displayName = name;
                e.message = message;
                e.messageChannel = channel;
                e.evidenceWeights = weights;
                e.xpGrants = xp;
                e.titleProgress = titles;
                e.affinityProgress = affinities;
                c.EvidenceEvents.Add(e);
                return e;
            }

            FoundationAbilityDefinition Ability(string id, string name, FoundationAbilityKind kind,
                FoundationAbilityResource resource, FoundationAbilityElement element,
                FoundationProgressionActivity activity, int cost, float cooldown, float power, float range,
                int activityXp, string evidenceId, string affinityId, string description, string message,
                params string[] skillIds)
            {
                var a = New<FoundationAbilityDefinition>(id);
                a.displayName = name;
                a.kind = kind;
                a.resource = resource;
                a.element = element;
                a.activity = activity;
                a.resourceCost = cost;
                a.cooldownSeconds = cooldown;
                a.basePower = power;
                a.range = range;
                a.activityXp = activityXp;
                a.evidenceId = evidenceId;
                a.affinityId = affinityId;
                a.description = description;
                a.systemMessage = message;
                a.skillIds = skillIds;
                c.Abilities.Add(a);
                return a;
            }

            ClassDefinition Class(string id, string name, FoundationClassRarity rarity, string description,
                FoundationEvidenceWeight[] weights, params string[] affinityIds)
            {
                var cls = New<ClassDefinition>(id);
                cls.displayName = name;
                cls.rarity = rarity;
                cls.description = description;
                cls.weights = weights;
                cls.preferredAffinityIds = affinityIds;
                c.Classes.Add(cls);
                return cls;
            }

            ProfessionDefinition Profession(string id, string name, FoundationProgressionActivity activity,
                string description, params string[] skillIds)
            {
                var p = New<ProfessionDefinition>(id);
                p.displayName = name;
                p.primaryActivity = activity;
                p.description = description;
                p.progressionSkillIds = skillIds;
                c.Professions.Add(p);
                return p;
            }

            DungeonResultDefinition DungeonResult(string id, string name, string dungeonId, string summary,
                FoundationXpGrant[] xp, FoundationTitleProgressGrant[] titles, FoundationAffinityGrant[] affinities,
                FoundationQuestReward[] rewards)
            {
                var r = New<DungeonResultDefinition>(id);
                r.displayName = name;
                r.dungeonId = dungeonId;
                r.summary = summary;
                r.xpRewards = xp;
                r.titleProgress = titles;
                r.affinityProgress = affinities;
                r.rewards = rewards;
                c.DungeonResults.Add(r);
                return r;
            }

            DungeonDefinition Dungeon(string id, string name, string family, int threat, int travelHours,
                string resultId, string description, params string[] supplies)
            {
                var d = New<DungeonDefinition>(id);
                d.displayName = name;
                d.family = family;
                d.threatRank = threat;
                d.travelHours = travelHours;
                d.resultId = resultId;
                d.description = description;
                d.recommendedSupplyItemIds = supplies;
                c.Dungeons.Add(d);
                return d;
            }

            ExpeditionTemplateDefinition Expedition(string id, string name, string dungeonId, int hours, int danger, params string[] supplies)
            {
                var e = New<ExpeditionTemplateDefinition>(id);
                e.displayName = name;
                e.dungeonId = dungeonId;
                e.expectedHours = hours;
                e.danger = danger;
                e.requiredSupplyItemIds = supplies;
                c.Expeditions.Add(e);
                return e;
            }

            GuildBoardEntryDefinition BoardEntry(string id, string name, string questId, string eventId, int rank, int days, string description)
            {
                var b = New<GuildBoardEntryDefinition>(id);
                b.displayName = name;
                b.questId = questId;
                b.worldEventId = eventId;
                b.rankRequirement = rank;
                b.expiresAfterDays = days;
                b.description = description;
                c.GuildBoardEntries.Add(b);
                return b;
            }

            WorldEventDefinition WorldEvent(string id, string name, int severity, string trigger, string consequence, string message)
            {
                var e = New<WorldEventDefinition>(id);
                e.displayName = name;
                e.severity = severity;
                e.triggerId = trigger;
                e.consequenceId = consequence;
                e.message = message;
                c.WorldEvents.Add(e);
                return e;
            }

            SystemMessage("foreign_soul_detected", SystemMessageChannel.Notice, "Foreign soul detected.", 3);
            SystemMessage("trial_protocol_started", SystemMessageChannel.Notice, "Trial Protocol initiated: survive, adapt, act.", 3);

            XpChannel(FoundationXpChannel.Character, "character", "Character Level", "Broad survivability from quests, discoveries, and major proof.");
            XpChannel(FoundationXpChannel.Class, "class", "Class Level", "Future class-aligned action growth after the Obelisk.");
            XpChannel(FoundationXpChannel.Profession, "profession", "Profession Level", "Production, contracts, station chains, and economic identity.");
            XpChannel(FoundationXpChannel.SkillMastery, "skill_mastery", "Skill Mastery", "Repeated use of tools, weapons, craft, routes, and rituals.");
            XpChannel(FoundationXpChannel.AdventurerRank, "adventurer_rank", "Adventurer Rank", "Guild-reviewed delves, bounties, rescues, and reliability.");
            XpChannel(FoundationXpChannel.GuildRank, "guild_rank", "Guild Rank", "Shared base, civic, and party-scale milestones.");
            XpChannel(FoundationXpChannel.RegionReputation, "mosswake_reputation", "Mosswake Reputation", "Local trust, permissions, shop standing, and civic help.");
            XpChannel(FoundationXpChannel.DungeonClearance, "rootcellar_clearance", "Rootcellar Clearance", "Performance records for the first dungeon family.");

            Title("first_night_survivor", "First Night Survivor", 5, "fatigue_resistance_minor",
                "Title acquired: First Night Survivor.", true, "survivalist");
            Title("village_shield", "Village Shield", 6, "town_defense_reputation",
                "Title acquired: Village Shield.", true, "guardian");
            Title("campfire_captain", "Campfire Captain", 4, "camp_recovery_minor",
                "Title acquired: Campfire Captain.", true, "hearthkeeper");
            Title("trail_cook", "Trail Cook", 4, "travel_meal_bonus",
                "Title acquired: Trail Cook.", true, "cook");
            Title("goblin_bane", "Goblin-Bane", 5, "goblin_threat_bias",
                "Title acquired: Goblin-Bane.", true, "warden");
            Title("returned_for_them", "Returned For Them", 3, "support_leadership_key",
                "Title acquired: Returned For Them.", true, "oathbearer");

            Affinity("ember", "Ember", "Fire, forging, courage, and camp warmth.", 10,
                "Warmth, flamecraft, bold combat, and firelit shelter.", "campfire focus", "forge rites");
            Affinity("tide", "Tide", "Water, healing, fishing, and weather.", 10,
                "Water work, recovery, fishing, weather survival, and mercy.", "rain reading", "healing springs");
            Affinity("root", "Root", "Growth, farming, binding, and wild care.", 10,
                "Plants, soil, forage, animals, and living terrain.", "crop memory", "den accord");
            Affinity("stone", "Stone", "Defense, mining, construction, and endurance.", 10,
                "Ore, paths, walls, shields, and hard choices held steady.", "mason signs", "ward anchors");
            Affinity("gale", "Gale", "Speed, scouting, sailing, and clean routes.", 10,
                "Travel, mapping, scouting, movement, and road sense.", "route whisper", "storm step");
            Affinity("glimmer", "Glimmer", "Light, illusion, secrets, and hidden knowledge.", 10,
                "Lanterns, shrines, lore, stealth, and revealed paths.", "hidden doors", "memory pages");
            Affinity("hearth", "Hearth", "Food, comfort, camp safety, and morale.", 10,
                "Meals, shelter, camp order, rest, and safe returns.", "shared supper", "rested camp");

            Evidence("harvest_wood", "Harvest Wood",
                "Entry recorded: wood gathered. Survival and craft tendencies rise.",
                new[] { W(TrialEvidenceCategory.Gathering, 2), W(TrialEvidenceCategory.Survival, 1), W(TrialEvidenceCategory.Building, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "woodcraft", 3), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("first_night_survivor", 1), T("campfire_captain", 1) },
                new[] { A("root", 1), A("hearth", 1) });
            Evidence("harvest_stone", "Mine Stone",
                "Entry recorded: stone broken. Building and Stone resonance rise.",
                new[] { W(TrialEvidenceCategory.Gathering, 2), W(TrialEvidenceCategory.Building, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "mining", 3), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("village_shield", 1) },
                new[] { A("stone", 2) });
            Evidence("harvest_forage", "Gather Forage",
                "Entry recorded: forage recovered. Survival and Root resonance rise.",
                new[] { W(TrialEvidenceCategory.Gathering, 2), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "foraging", 3), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("trail_cook", 1) },
                new[] { A("root", 2), A("hearth", 1) });
            Evidence("craft_workbench", "Craft Workbench",
                "Entry recorded: first station made. Crafting identity rises.",
                new[] { W(TrialEvidenceCategory.Crafting, 3), W(TrialEvidenceCategory.Building, 2) },
                new[] { X(FoundationXpChannel.SkillMastery, "crafting", 4), X(FoundationXpChannel.Profession, "builder", 2) },
                new[] { T("campfire_captain", 1) },
                new[] { A("stone", 1), A("hearth", 1) });
            Evidence("craft_campfire", "Craft Campfire",
                "Entry recorded: camp warmth secured. Hearth resonance rises.",
                new[] { W(TrialEvidenceCategory.Survival, 3), W(TrialEvidenceCategory.Crafting, 1), W(TrialEvidenceCategory.Support, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "cooking", 2), X(FoundationXpChannel.Character, "character", 2) },
                new[] { T("first_night_survivor", 2), T("campfire_captain", 2) },
                new[] { A("ember", 2), A("hearth", 3) });
            Evidence("place_path", "Place Path",
                "Entry recorded: route improved. Building and Gale tendencies rise.",
                new[] { W(TrialEvidenceCategory.Building, 2), W(TrialEvidenceCategory.Exploration, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "building", 3), X(FoundationXpChannel.RegionReputation, "mosswake_reputation", 1) },
                new[] { T("village_shield", 1) },
                new[] { A("stone", 1), A("gale", 1) });
            Evidence("till_soil", "Till Soil",
                "Entry recorded: soil prepared. Root resonance rises.",
                new[] { W(TrialEvidenceCategory.Gathering, 1), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "farming", 3), X(FoundationXpChannel.Profession, "farmer", 2) },
                new[] { T("trail_cook", 1) },
                new[] { A("root", 3), A("hearth", 1) });
            Evidence("crop_harvest", "Harvest Crop",
                "Entry recorded: food brought home. Hearth and Root progress rise.",
                new[] { W(TrialEvidenceCategory.Survival, 2), W(TrialEvidenceCategory.Gathering, 1), W(TrialEvidenceCategory.Support, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "farming", 4), X(FoundationXpChannel.Profession, "farmer", 2) },
                new[] { T("trail_cook", 2), T("campfire_captain", 1) },
                new[] { A("root", 2), A("hearth", 2) });
            Evidence("cook_fire_meal", "Cook Fire Meal",
                "Entry recorded: a cooked meal steadies the camp. Hearth progress rises.",
                new[] { W(TrialEvidenceCategory.Survival, 2), W(TrialEvidenceCategory.Crafting, 1), W(TrialEvidenceCategory.Support, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "cooking", 4), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("trail_cook", 2), T("campfire_captain", 1) },
                new[] { A("hearth", 3), A("ember", 1) });
            Evidence("rest_at_camp", "Rest at Camp",
                "Entry recorded: you slept inside firelight. Survival and Hearth evidence rise.",
                new[] { W(TrialEvidenceCategory.Survival, 3), W(TrialEvidenceCategory.Support, 1) },
                new[] { X(FoundationXpChannel.Character, "character", 2), X(FoundationXpChannel.SkillMastery, "cooking", 1) },
                new[] { T("first_night_survivor", 2), T("campfire_captain", 2) },
                new[] { A("hearth", 3), A("ember", 1) });
            Evidence("mob_defeated", "Mob Defeated",
                "Entry recorded: threat resolved by force. Combat evidence rises.",
                new[] { W(TrialEvidenceCategory.Combat, 3), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "warding", 4), X(FoundationXpChannel.AdventurerRank, "adventurer_rank", 2) },
                new[] { T("village_shield", 1), T("goblin_bane", 1) },
                new[] { A("ember", 1), A("stone", 1) });
            Evidence("mob_calmed", "Mob Calmed",
                "Entry recorded: threat softened without bloodshed. Support and Root evidence rise.",
                new[] { W(TrialEvidenceCategory.Support, 2), W(TrialEvidenceCategory.Social, 1), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "creaturecraft", 4), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("returned_for_them", 1) },
                new[] { A("root", 2), A("tide", 1) });
            Evidence("use_steady_strike", "Use Steady Strike",
                "Entry recorded: stamina shaped into a clean martial skill.",
                new[] { W(TrialEvidenceCategory.Combat, 2), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "combat", 3), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("village_shield", 1) },
                new[] { A("stone", 1) });
            Evidence("use_guard_step", "Use Guard Step",
                "Entry recorded: stamina carried through footwork and threat spacing.",
                new[] { W(TrialEvidenceCategory.Combat, 1), W(TrialEvidenceCategory.Exploration, 1), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "combat", 2), X(FoundationXpChannel.SkillMastery, "warding", 2) },
                new[] { T("first_night_survivor", 1) },
                new[] { A("gale", 1), A("stone", 1) });
            Evidence("cast_mana_bolt", "Cast Mana Bolt",
                "Entry recorded: neutral mana obeyed without elemental affinity.",
                new[] { W(TrialEvidenceCategory.Magic, 2), W(TrialEvidenceCategory.Combat, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "spellcraft", 4), X(FoundationXpChannel.Character, "character", 1) },
                new[] { T("village_shield", 1) },
                System.Array.Empty<FoundationAffinityGrant>());
            Evidence("cast_ember_spark", "Cast Ember Spark",
                "Entry recorded: Ember mana flared through combat intent.",
                new[] { W(TrialEvidenceCategory.Magic, 3), W(TrialEvidenceCategory.Combat, 1), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "spellcraft", 4), X(FoundationXpChannel.Class, "class", 2) },
                new[] { T("goblin_bane", 1), T("campfire_captain", 1) },
                new[] { A("ember", 3) });
            Evidence("cast_root_snare", "Cast Root Snare",
                "Entry recorded: Root mana answered with restraint instead of waste.",
                new[] { W(TrialEvidenceCategory.Magic, 2), W(TrialEvidenceCategory.Support, 1), W(TrialEvidenceCategory.Survival, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "spellcraft", 3), X(FoundationXpChannel.SkillMastery, "creaturecraft", 2) },
                new[] { T("returned_for_them", 1) },
                new[] { A("root", 3) });
            Evidence("cast_stone_skin", "Cast Stone Skin",
                "Entry recorded: Stone mana settled into defense.",
                new[] { W(TrialEvidenceCategory.Magic, 2), W(TrialEvidenceCategory.Combat, 1), W(TrialEvidenceCategory.Building, 1) },
                new[] { X(FoundationXpChannel.SkillMastery, "spellcraft", 3), X(FoundationXpChannel.SkillMastery, "warding", 2) },
                new[] { T("village_shield", 1) },
                new[] { A("stone", 3) });

            Ability("steady_strike", "Steady Strike", FoundationAbilityKind.Skill,
                FoundationAbilityResource.Stamina, FoundationAbilityElement.None,
                FoundationProgressionActivity.Combat, 10, 0.7f, 1.15f, 1.35f, 6,
                "use_steady_strike", "",
                "A clean weapon skill. Spends stamina and grows combat proof without elemental scaling.",
                "Steady Strike lands on practiced breath.", "combat");
            Ability("guard_step", "Guard Step", FoundationAbilityKind.Skill,
                FoundationAbilityResource.Stamina, FoundationAbilityElement.None,
                FoundationProgressionActivity.Combat, 8, 1.2f, 0.75f, 1.0f, 5,
                "use_guard_step", "",
                "A defensive footwork skill. Spends stamina and supports future dodge/block timing.",
                "Guard Step resets your footing.", "combat", "warding");
            Ability("mana_bolt", "Mana Bolt", FoundationAbilityKind.Spell,
                FoundationAbilityResource.Mana, FoundationAbilityElement.Neutral,
                FoundationProgressionActivity.Magic, 9, 0.9f, 1.0f, 4.5f, 7,
                "cast_mana_bolt", "",
                "Plain non-affinity magic. Reliable force, no elemental resonance required.",
                "Mana Bolt snaps forward in plain light.", "spellcraft");
            Ability("ember_spark", "Ember Spark", FoundationAbilityKind.Spell,
                FoundationAbilityResource.Mana, FoundationAbilityElement.Ember,
                FoundationProgressionActivity.Magic, 12, 1.1f, 1.25f, 4.0f, 8,
                "cast_ember_spark", "ember",
                "A starter fire spell. Ember affinity increases its output and future burn effects.",
                "Ember Spark catches and remembers the heat.", "spellcraft", "combat");
            Ability("root_snare", "Root Snare", FoundationAbilityKind.Spell,
                FoundationAbilityResource.Mana, FoundationAbilityElement.Root,
                FoundationProgressionActivity.Magic, 11, 1.4f, 0.85f, 3.75f, 8,
                "cast_root_snare", "root",
                "A restraint spell. Root affinity increases hold strength and calm/tame synergy.",
                "Root Snare curls through the ground.", "spellcraft", "creaturecraft");
            Ability("stone_skin", "Stone Skin", FoundationAbilityKind.Spell,
                FoundationAbilityResource.Mana, FoundationAbilityElement.Stone,
                FoundationProgressionActivity.Magic, 13, 2.0f, 0.95f, 0f, 8,
                "cast_stone_skin", "stone",
                "A defensive spell. Stone affinity increases mitigation and ward duration.",
                "Stone Skin settles over your guard.", "spellcraft", "warding");

            Class("trailblade", "Trailblade", FoundationClassRarity.Uncommon,
                "A practical scout-fighter shaped by routes, tools, and first danger.",
                new[] { W(TrialEvidenceCategory.Exploration, 3), W(TrialEvidenceCategory.Combat, 2), W(TrialEvidenceCategory.Survival, 2) }, "gale", "stone");
            Class("iron_warden", "Iron Warden", FoundationClassRarity.Rare,
                "A defender whose proof is shelter, shields, paths, and pressure held.",
                new[] { W(TrialEvidenceCategory.Combat, 3), W(TrialEvidenceCategory.Building, 3), W(TrialEvidenceCategory.Survival, 2) }, "stone", "ember");
            Class("hearthbound_acolyte", "Hearthbound Acolyte", FoundationClassRarity.Rare,
                "A camp-centered support path built from food, warmth, and safe returns.",
                new[] { W(TrialEvidenceCategory.Support, 3), W(TrialEvidenceCategory.Survival, 3), W(TrialEvidenceCategory.Crafting, 1) }, "hearth", "ember");
            Class("stonehand_delver", "Stonehand Delver", FoundationClassRarity.Uncommon,
                "A miner-builder suited to stone, ore, tunnels, and steady tool work.",
                new[] { W(TrialEvidenceCategory.Gathering, 3), W(TrialEvidenceCategory.Building, 2), W(TrialEvidenceCategory.Exploration, 1) }, "stone");
            Class("wildsign_ranger", "Wildsign Ranger", FoundationClassRarity.Rare,
                "A wilderness reader whose trial favors forage, beasts, and route sense.",
                new[] { W(TrialEvidenceCategory.Exploration, 3), W(TrialEvidenceCategory.Gathering, 2), W(TrialEvidenceCategory.Survival, 2) }, "root", "gale");
            Class("ashvein_pyromancer", "Ashvein Pyromancer", FoundationClassRarity.Epic,
                "A flame path opened by danger, campfire discipline, and Ember resonance.",
                new[] { W(TrialEvidenceCategory.Magic, 3), W(TrialEvidenceCategory.Combat, 2), W(TrialEvidenceCategory.Survival, 1) }, "ember");
            Class("wayfarer", "Wayfarer", FoundationClassRarity.CommonPlus,
                "A reliable traveler shaped by roads, resource sense, and adaptable work.",
                new[] { W(TrialEvidenceCategory.Exploration, 2), W(TrialEvidenceCategory.Gathering, 2), W(TrialEvidenceCategory.Trade, 1) }, "gale", "hearth");
            Class("oathbearer", "Oathbearer", FoundationClassRarity.Epic,
                "A support-defender path for players who repeatedly return for others.",
                new[] { W(TrialEvidenceCategory.Support, 3), W(TrialEvidenceCategory.Social, 2), W(TrialEvidenceCategory.Combat, 2) }, "hearth", "stone");

            Profession("blacksmith", "Blacksmith", FoundationProgressionActivity.Craft,
                "Smelting, forging, repair, metal tools, and durable gear.", "crafting", "mining");
            Profession("alchemist", "Alchemist", FoundationProgressionActivity.Craft,
                "Potions, reagents, refining, and risky recipe discovery.", "foraging", "lorekeeping");
            Profession("cook", "Cook", FoundationProgressionActivity.Craft,
                "Meals, expedition food, comfort buffs, and visitor favorites.", "cooking", "trade");
            Profession("builder", "Builder", FoundationProgressionActivity.Build,
                "Floors, paths, civic footprints, shelter scoring, and expansion.", "building", "woodcraft");
            Profession("trader", "Trader", FoundationProgressionActivity.Trade,
                "Orders, prices, routes, reputation, and economic quests.", "trade", "lorekeeping");
            Profession("farmer", "Farmer", FoundationProgressionActivity.Farm,
                "Soil, seed memory, crop traits, and reliable food.", "farming", "foraging");
            Profession("miner", "Miner", FoundationProgressionActivity.Harvest,
                "Ore, stone, tunnels, durability, and delver preparation.", "mining", "warding");
            Profession("fisher", "Fisher", FoundationProgressionActivity.Harvest,
                "Water routes, fish, weather, and Tide-aligned food work.", "foraging", "cooking");

            DungeonResult("rootcellar_first_return", "Rootcellar First Return", "rootcellar_starter",
                "Returned with proof from the old food stores beneath Mosswake.",
                new[] { X(FoundationXpChannel.DungeonClearance, "rootcellar_clearance", 20), X(FoundationXpChannel.AdventurerRank, "adventurer_rank", 8) },
                new[] { T("first_night_survivor", 1), T("returned_for_them", 1) },
                new[] { A("root", 2), A("glimmer", 1) },
                new[] { Reward(FoundationRewardType.MemoryPage, "old_lamps_01"), Reward(FoundationRewardType.Xp, "character", 50) });
            Dungeon("rootcellar_starter", "Mosswake Rootcellar", "Root Cellar", 1, 2, "rootcellar_first_return",
                "An old cellar where roots, pests, and stale System hums gather under the first fields.",
                "apple", "carrot", "wood_axe");
            Expedition("rootcellar_day_two_probe", "Day Two Rootcellar Probe", "rootcellar_starter", 4, 2,
                "apple", "wood_axe");

            WorldEvent("goblin_raid_chain", "Goblin Raid Chain", 2, "ignored_road_threat", "settlement_pressure",
                "World event: tracks suggest a raid chain forming beyond the safe road.");
            WorldEvent("dangerous_mob_sighting", "Dangerous Mob Sighting", 1, "night_noise", "local_warning",
                "World event: something large crossed the outer meadow after dusk.");
            WorldEvent("resource_bloom", "Resource Bloom", 1, "rain_after_clear", "rare_nodes",
                "World event: fresh rain has woken rare sprouts and exposed stone seams.");
            WorldEvent("rival_npc_party", "Rival NPC Party", 1, "guild_board_day_two", "social_pressure",
                "World event: another Unwritten party accepted a nearby contract.");

            BoardEntry("board_rootcellar_probe", "Rootcellar Probe", "the_rootcellar_below", "resource_bloom", 0, 3,
                "Bring back proof from the old cellar before pests chew through the stores.");
            BoardEntry("board_south_path", "South Path Repair", "fixing_the_south_path", "dangerous_mob_sighting", 0, 4,
                "Clear and mark the south path so visitors stop losing half a day in brambles.");

            return c;
        }
    }
}
