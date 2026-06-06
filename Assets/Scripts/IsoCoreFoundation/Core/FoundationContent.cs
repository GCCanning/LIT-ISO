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
            Block("water", "water_blocks", new Color(0.20f, 0.45f, 0.75f), CollisionMode.Water);
            Block("dirt", "dirt_blocks", new Color(0.45f, 0.32f, 0.20f), CollisionMode.Walkable);
            Block("stone_block", "stone_blocks", new Color(0.55f, 0.55f, 0.58f), CollisionMode.Solid);
            Block("stone_path", "path_blocks", new Color(0.62f, 0.62f, 0.64f), CollisionMode.Decorative);
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

            ItemDefinition Tool(string id, Color col, ToolType tool, int tier)
            {
                var it = Item(id, col, ItemCategory.Tool, 1);
                it.toolType = tool; it.toolTier = tier; return it;
            }
            Tool("wood_axe", new Color(0.60f, 0.42f, 0.24f), ToolType.Axe, 1);
            Tool("wood_pickaxe", new Color(0.60f, 0.42f, 0.24f), ToolType.Pickaxe, 1);
            Tool("stone_axe", new Color(0.55f, 0.55f, 0.58f), ToolType.Axe, 2);
            Tool("stone_pickaxe", new Color(0.55f, 0.55f, 0.58f), ToolType.Pickaxe, 2);
            Tool("copper_axe", new Color(0.85f, 0.55f, 0.35f), ToolType.Axe, 3);
            Tool("copper_pickaxe", new Color(0.85f, 0.55f, 0.35f), ToolType.Pickaxe, 3);
            Tool("hoe", new Color(0.55f, 0.42f, 0.28f), ToolType.Hoe, 1);

            ItemDefinition PlaceItem(string id, Color col, string placeableId)
            {
                var it = Item(id, col, ItemCategory.Placeable, 99);
                it.placeableId = placeableId; return it;
            }
            PlaceItem("workbench_item", new Color(0.60f, 0.45f, 0.30f), "workbench");
            PlaceItem("chest_item", new Color(0.70f, 0.55f, 0.35f), "chest");
            PlaceItem("lantern_item", new Color(0.95f, 0.85f, 0.40f), "lantern");
            PlaceItem("furnace_item", new Color(0.45f, 0.42f, 0.45f), "furnace");

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
            campfire.emitsLight = true;
            campfire.lightColor = new Color(1f, 0.62f, 0.28f);
            campfire.lightRadius = 3.0f;

            // ---- Resource nodes ----
            ResourceNodeDefinition Node(string id, Color col, ToolType tool, bool mandatory, int hits, float h, ItemDrop[] drops)
            {
                var n = New<ResourceNodeDefinition>(id);
                n.color = col; n.requiredTool = tool; n.toolMandatory = mandatory;
                n.hitsToHarvest = hits; n.heightUnits = h; n.drops = drops;
                c.Nodes.Add(n); return n;
            }
            // Axe/pickaxe preferred (faster) but hand-harvestable; ore veins REQUIRE a pickaxe.
            var tree = Node("tree", new Color(0.18f, 0.40f, 0.18f), ToolType.Axe, false, 3, 1.5f,
                new[] { new ItemDrop("wood", 2, 4) });
            var rock = Node("rock", new Color(0.50f, 0.50f, 0.53f), ToolType.Pickaxe, false, 3, 0.9f,
                new[] { new ItemDrop("stone", 2, 3) });
            var bush = Node("bush", new Color(0.25f, 0.50f, 0.25f), ToolType.None, false, 1, 0.7f,
                new[] { new ItemDrop("fiber", 1, 2), new ItemDrop("apple", 0, 1, 0.5f) });
            var copperVein = Node("copper_vein", new Color(0.80f, 0.45f, 0.25f), ToolType.Pickaxe, true, 4, 0.95f,
                new[] { new ItemDrop("copper_ore", 1, 2), new ItemDrop("stone", 0, 1, 0.5f) });
            // Extra flora for visual variety (art in Resources/Decorations). Pine = a second
            // tree species for groves; flower = ambient ground cover; stump/log = low woody bits.
            var pine = Node("pine", new Color(0.16f, 0.34f, 0.22f), ToolType.Axe, false, 3, 1.5f,
                new[] { new ItemDrop("wood", 2, 4) });
            var flower = Node("flower", new Color(0.70f, 0.55f, 0.20f), ToolType.None, false, 1, 0.3f,
                new[] { new ItemDrop("fiber", 1, 1) });
            var stump = Node("stump", new Color(0.45f, 0.32f, 0.18f), ToolType.Axe, false, 2, 0.5f,
                new[] { new ItemDrop("wood", 1, 2) });
            var log = Node("log", new Color(0.42f, 0.29f, 0.16f), ToolType.Axe, false, 2, 0.4f,
                new[] { new ItemDrop("wood", 2, 3) });

            // ---- Mobs ----
            MobDefinition Mob(string id, Color col, MobBehavior beh, float speed, float wander, ItemDrop[] drops)
            {
                var m = New<MobDefinition>(id);
                m.color = col; m.behaviour = beh; m.moveSpeed = speed; m.wanderRadius = wander; m.drops = drops;
                c.Mobs.Add(m); return m;
            }
            var deer = Mob("deer", new Color(0.62f, 0.45f, 0.30f), MobBehavior.Skittish, 1.6f, 6f,
                new[] { new ItemDrop("hide", 1, 2) });
            var slime = Mob("slime", new Color(0.40f, 0.82f, 0.45f), MobBehavior.Passive, 1.0f, 3f,
                new[] { new ItemDrop("slime_goo", 1, 2) });
            var fox = Mob("fox", new Color(0.85f, 0.45f, 0.20f), MobBehavior.Skittish, 2.0f, 7f,
                new[] { new ItemDrop("hide", 1, 1) });

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

            Biome("meadow", 0.55f, 0.55f, grassGroup, 1, 2,
                new[] { NS(tree, 0.05f), NS(rock, 0.02f), NS(bush, 0.05f), NS(copperVein, 0.008f) },
                new[] { MS(deer, 1f), MS(slime, 1f) }, new Color(0.4f, 0.7f, 0.4f));
            Biome("forest", 0.45f, 0.85f, grassGroup, 1, 3,
                new[] { NS(tree, 0.14f), NS(bush, 0.06f), NS(rock, 0.02f), NS(copperVein, 0.01f) },
                new[] { MS(deer, 1f), MS(fox, 1f), MS(slime, 0.5f) }, new Color(0.25f, 0.55f, 0.30f));
            Biome("desert", 0.88f, 0.15f, sandGroup, 1, 1,
                new[] { NS(rock, 0.05f), NS(copperVein, 0.015f) },
                new[] { MS(slime, 1f) }, new Color(0.85f, 0.78f, 0.45f));
            Biome("beach", 0.70f, 0.45f, sandGroup, 1, 1,
                new[] { NS(rock, 0.02f), NS(bush, 0.02f) },
                new[] { MS(fox, 0.5f), MS(slime, 1f) }, new Color(0.90f, 0.84f, 0.55f));
            Biome("snow", 0.12f, 0.45f, snowGroup, 1, 2,
                new[] { NS(tree, 0.04f), NS(pine, 0.05f), NS(rock, 0.03f), NS(copperVein, 0.008f) },
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
            Recipe("craft_wood_axe", StationType.Hand,
                new[] { In("wood", 3), In("fiber", 2) }, new[] { Out("wood_axe", 1) });
            Recipe("craft_wood_pickaxe", StationType.Workbench,
                new[] { In("wood", 3), In("stone", 2) }, new[] { Out("wood_pickaxe", 1) });
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
            Recipe("craft_copper_axe", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 3) }, new[] { Out("copper_axe", 1) });
            Recipe("craft_copper_pickaxe", StationType.Workbench,
                new[] { In("wood", 2), In("copper_bar", 3) }, new[] { Out("copper_pickaxe", 1) });

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
            Skill("warding", "Warding", FoundationProgressionActivity.Combat, FoundationSkillNodeKind.Utility,
                "Use lights, traps, patrol posts, and wards to shape threat.", "non-lethal defenses", "threat shaping", "patrol posts");
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
                new[] { "patroller", "shieldhand", "gloombreaker" }, "warding", "exploration", "mining");

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
                new[] { Obj("place_floor", "Place wood floor tiles", 4), Obj("place_lantern", "Place a lantern"), Obj("store_food", "Store any food item", 3) },
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

            return c;
        }
    }
}
