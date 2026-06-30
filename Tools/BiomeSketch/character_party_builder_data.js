window.LITISO_CHARACTER_PARTY_DATA = {
  frameSize: 64,
  rows: { N: 0, W: 1, S: 2, E: 3 },
  frames: 9,
  assetRoot: "../../Assets/Resources/Characters/Layers",
  slots: [
    {
      id: "body",
      label: "Body",
      required: true,
      options: [
        { id: "lpc/body", label: "Human Body" }
      ]
    },
    {
      id: "head",
      label: "Head",
      required: true,
      options: [
        { id: "lpc/heads_human_male", label: "Human Male" },
        { id: "lpc/heads_human_female", label: "Human Female" }
      ]
    },
    {
      id: "eyes",
      label: "Eyes",
      options: [
        { id: "lpc/eyes_human", label: "Human Eyes" }
      ]
    },
    {
      id: "hair",
      label: "Hair",
      options: [
        { id: "", label: "None" },
        { id: "lpc/hair_bedhead", label: "Bedhead" },
        { id: "lpc/hair_curtains", label: "Curtains" },
        { id: "lpc/hair_braid", label: "Braid" },
        { id: "lpc/hair_long", label: "Long" },
        { id: "lpc/hair_cowlick", label: "Cowlick" },
        { id: "lpc/hair_buzzcut", label: "Buzzcut" },
        { id: "lpc/beards_beard", label: "Beard" }
      ]
    },
    {
      id: "shirt",
      label: "Torso",
      options: [
        { id: "", label: "None" },
        { id: "lpc/torso_armour_leather", label: "Leather" },
        { id: "lpc/torso_chainmail", label: "Chainmail" },
        { id: "lpc/torso_armour_plate", label: "Plate" },
        { id: "lpc/torso_clothes_robe", label: "Robe" },
        { id: "lpc/torso_clothes_tunic", label: "Tunic" },
        { id: "lpc/torso_jacket_trench", label: "Trench" },
        { id: "lpc/torso_jacket_tabard", label: "Tabard" }
      ]
    },
    {
      id: "pants",
      label: "Legs",
      options: [
        { id: "", label: "None" },
        { id: "lpc/legs_pants", label: "Pants" },
        { id: "lpc/legs_armour", label: "Armour" }
      ]
    },
    {
      id: "shoes",
      label: "Feet",
      options: [
        { id: "", label: "None" },
        { id: "lpc/feet_boots_basic", label: "Boots" },
        { id: "lpc/feet_armour", label: "Armour" },
        { id: "lpc/feet_sandals", label: "Sandals" }
      ]
    },
    {
      id: "hands",
      label: "Arms",
      options: [
        { id: "", label: "None" },
        { id: "lpc/arms_armour", label: "Armour" },
        { id: "lpc/arms_bracers", label: "Bracers" },
        { id: "lpc/arms_gloves", label: "Gloves" },
        { id: "lpc/shoulders_pauldrons", label: "Pauldrons" }
      ]
    },
    {
      id: "belt",
      label: "Belt",
      options: [
        { id: "", label: "None" },
        { id: "lpc/belt_leather", label: "Leather" },
        { id: "lpc/belt_sash", label: "Sash" },
        { id: "lpc/belt_robe", label: "Robe Belt" }
      ]
    },
    {
      id: "back",
      label: "Back",
      options: [
        { id: "", label: "None" },
        { id: "lpc/cape_solid", label: "Cape" },
        { id: "lpc/cape_tattered", label: "Tattered Cape" },
        { id: "lpc/backpack", label: "Backpack" },
        { id: "lpc/quiver", label: "Quiver" }
      ]
    },
    {
      id: "hat",
      label: "Headgear",
      options: [
        { id: "", label: "None" },
        { id: "lpc/hat_hood_cloth", label: "Hood" },
        { id: "lpc/hat_magic_wizard", label: "Wizard" },
        { id: "lpc/hat_helmet_barbuta", label: "Barbuta" },
        { id: "lpc/hat_cap_leather", label: "Leather Cap" },
        { id: "lpc/hat_bandana", label: "Bandana" }
      ]
    },
    {
      id: "weapon",
      label: "Weapon",
      options: [
        { id: "", label: "None" },
        { id: "lpc/weapon_sword_longsword", label: "Longsword" },
        { id: "lpc/weapon_sword_dagger", label: "Dagger" },
        { id: "lpc/weapon_magic_simple", label: "Simple Staff" },
        { id: "lpc/weapon_magic_gnarled", label: "Gnarled Staff" },
        { id: "lpc/weapon_polearm_spear", label: "Spear" },
        { id: "lpc/weapon_ranged_bow_normal", label: "Bow" },
        { id: "lpc/weapon_blunt_waraxe", label: "Waraxe" }
      ]
    },
    {
      id: "offhand",
      label: "Offhand",
      options: [
        { id: "", label: "None" },
        { id: "lpc/shield_round", label: "Round Shield" },
        { id: "lpc/shield_kite", label: "Kite Shield" },
        { id: "lpc/shield_heater_wood", label: "Heater Shield" }
      ]
    }
  ],
  drawOrder: [
    "back",
    "body",
    "head",
    "eyes",
    "shoes",
    "pants",
    "shirt",
    "hands",
    "belt",
    "weapon",
    "offhand",
    "hair",
    "hat"
  ],
  party: [
    {
      id: "lead",
      label: "Lead",
      name: "Adventurer",
      sex: "male",
      direction: "S",
      selections: {
        body: "lpc/body",
        head: "lpc/heads_human_male",
        eyes: "lpc/eyes_human",
        hair: "lpc/hair_bedhead",
        shirt: "lpc/torso_armour_leather",
        pants: "lpc/legs_pants",
        shoes: "lpc/feet_boots_basic",
        hands: "lpc/arms_bracers",
        belt: "lpc/belt_leather",
        back: "lpc/cape_solid",
        hat: "",
        weapon: "lpc/weapon_sword_longsword",
        offhand: ""
      }
    },
    {
      id: "tank",
      label: "Tank",
      name: "Tank",
      sex: "male",
      direction: "S",
      selections: {
        body: "lpc/body",
        head: "lpc/heads_human_male",
        eyes: "lpc/eyes_human",
        hair: "",
        shirt: "lpc/torso_chainmail",
        pants: "lpc/legs_armour",
        shoes: "lpc/feet_armour",
        hands: "lpc/arms_armour",
        belt: "lpc/belt_leather",
        back: "",
        hat: "lpc/hat_helmet_barbuta",
        weapon: "lpc/weapon_blunt_waraxe",
        offhand: "lpc/shield_kite"
      }
    },
    {
      id: "mage",
      label: "Mage",
      name: "Healer",
      sex: "female",
      direction: "S",
      selections: {
        body: "lpc/body",
        head: "lpc/heads_human_female",
        eyes: "lpc/eyes_human",
        hair: "lpc/hair_long",
        shirt: "lpc/torso_clothes_robe",
        pants: "lpc/legs_pants",
        shoes: "lpc/feet_boots_basic",
        hands: "",
        belt: "lpc/belt_robe",
        back: "lpc/cape_solid",
        hat: "lpc/hat_magic_wizard",
        weapon: "lpc/weapon_magic_simple",
        offhand: ""
      }
    },
    {
      id: "rogue",
      label: "Rogue",
      name: "Rogue",
      sex: "male",
      direction: "S",
      selections: {
        body: "lpc/body",
        head: "lpc/heads_human_male",
        eyes: "lpc/eyes_human",
        hair: "lpc/hair_curtains",
        shirt: "lpc/torso_jacket_trench",
        pants: "lpc/legs_pants",
        shoes: "lpc/feet_boots_basic",
        hands: "lpc/arms_gloves",
        belt: "lpc/belt_leather",
        back: "lpc/quiver",
        hat: "lpc/hat_hood_cloth",
        weapon: "lpc/weapon_sword_dagger",
        offhand: ""
      }
    }
  ]
};
