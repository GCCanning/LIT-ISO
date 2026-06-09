# LPC Pack Asset Summary

Purpose: summarize what is available in the Universal LPC Spritesheet Character Generator pack for LIT-ISO planning. This is an inventory/research artifact only; it does not import LPC assets into Unity.

Sources:

- Generator: https://liberatedpixelcup.github.io/Universal-LPC-Spritesheet-Character-Generator/
- Repository: https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator
- Parsed from a temporary downloaded repo archive; archive removed after inventory generation to avoid repo bloat.
- Full parsed CSV: `Docs/handoff/LPC_Pack_Asset_Inventory.csv`

## Headline Counts

- Parsed unique non-meta sheet-definition asset families: **655**
- Raw sprite PNG layer files in `spritesheets/`: **145,452**
- Sheet definition JSON files total: **767**
- Non-meta sheet definition JSON files summarized here: **655**

Why PNG count is much higher than asset-family count: each unique item can have many repeated PNGs for body variants, animations, directions/poses, colors, layers, and equipment states.

## Category Counts

| Category | Unique asset families |
|---|---:|
| `arms` | 11 |
| `body` | 31 |
| `feet` | 17 |
| `hair` | 122 |
| `head` | 120 |
| `headwear` | 128 |
| `legs` | 23 |
| `tools` | 4 |
| `torso` | 102 |
| `weapons` | 97 |

## Functional Summary

### Body And Creature Bases

Available body-oriented families include normal humanoid body colors, child/teen/adult/muscular/pregnant support, skeleton, zombie, wheelchair, prostheses, wounds, tails, lizard appendages, and wings. This is where the pack supports plain starting bodies plus fantasy modifications.

### Hair And Facial Hair

Hair is the largest customization area after headwear. It includes short, long, extra-long, curly, afro/natural styles, braids, pigtails, ponytails/topknots, bob/lob styles, bald/buzzcut/hawk variants, hair extensions, beards, and mustaches. Most hair supports palette recolor materials.

### Head, Face, Eyes, And Expressions

Head assets include human heads/faces, expressions, eyes, eyebrows, cyclops eye, noses, ears, horns, fins, glasses, monocles, masks, patches, earrings, and facial accessories. These are the main pieces for face customization and fantasy ancestry overlays.

### Clothing And Armor

Torso, legs, feet, and arms cover shirts, sleeves, jackets, aprons, dresses, kimono/bodice/sash parts, pants, shorts, leggings, skirts, pantaloons, socks, shoes, boots, sandals, slippers, hoof feet, gloves, bracers, armour pieces, pauldrons, epaulets, and mantles.

### Headwear And Accessories

Headwear is broad: cloth hats, formal hats, holiday hats, pirate hats, magic hats, helmets, visors, headbands, crowns/ornaments where present, plus headwear accessories/trims. Neck and torso-adjacent accessories appear through definitions such as amulets, charms, necklaces, ties, cravats, jabots, scarves, cape ties/clips, belts, waist layers, and backpacks/capes in source paths.

### Weapons, Shields, And Tools

Weapon and tool definitions include swords, polearms, ranged weapons, magic weapons/effects, shields with many heater/kite/scutum/crusader-style patterns, quivers, and small tools. These are useful for equipped previews and combat animation testing.

## Animation Tags Found

`1h_backslash`, `1h_halfslash`, `1h_slash`, `backslash_128`, `climb`, `combat`, `emote`, `halfslash_128`, `hurt`, `idle`, `jump`, `run`, `shoot`, `sit`, `slash`, `slash_128`, `slash_oversize`, `slash_reverse_oversize`, `spellcast`, `thrust`, `thrust_oversize`, `tool_rod`, `tool_whip`, `walk`, `walk_128`, `watering`, `wheelchair`


## Body-Type Keys Found In Layer Definitions

`child`, `female`, `male`, `muscular`, `pregnant`, `teen`


## Top Type Names

| Type name | Count |
|---|---:|
| `hair` | 91 |
| `hat` | 52 |
| `shield_pattern` | 48 |
| `head` | 45 |
| `weapon` | 36 |
| `clothes` | 35 |
| `legs` | 22 |
| `charm` | 16 |
| `expression` | 15 |
| `facial_eyes` | 14 |
| `hat_trim` | 13 |
| `shoes` | 12 |
| `wings` | 11 |
| `ears` | 10 |
| `visor` | 9 |
| `shield` | 9 |
| `accessory` | 9 |
| `neck` | 8 |
| `mustache` | 8 |
| `belt` | 8 |
| `hairextl` | 7 |
| `hairextr` | 7 |
| `jacket` | 6 |
| `necklace` | 5 |
| `nose` | 5 |
| `earrings` | 5 |
| `beard` | 5 |
| `sleeves` | 5 |
| `tail` | 5 |
| `dress` | 4 |
| `bandana` | 4 |
| `backpack` | 4 |
| `apron` | 4 |
| `vest` | 4 |
| `sash` | 4 |
| `shoulders` | 4 |
| `hat_accessory` | 4 |
| `shield_trim` | 4 |
| `jacket_trim` | 3 |
| `body` | 3 |
| `hat_overlay` | 3 |
| `socks` | 3 |
| `ponytail` | 3 |
| `armour` | 3 |
| `headcover` | 3 |
| `ears_inner` | 3 |
| `cargo` | 3 |
| `overalls` | 2 |
| `shield_paint` | 2 |
| `cape` | 2 |
| `shoes_toe` | 2 |
| `sash_tie` | 2 |
| `eyebrows` | 2 |
| `fins` | 2 |
| `dress_sleeves_trim` | 2 |
| `horns` | 2 |
| `dress_trim` | 2 |
| `dress_sleeves` | 2 |
| `furry_ears_skin` | 2 |
| `furry_ears` | 2 |

## Full Unique Asset Family Index

This index lists each parsed non-meta sheet-definition asset family. For full path, animation, palette, body-type, license, and layer-path detail, use the CSV beside this file.

### arms

- `(root)` (4): Armour; Bauldron; Gloves; Stud Ring
- `shoulders` (4): Epaulets; Legion; Mantal; Pauldrons
- `wrists` (3): Bracers; Cuffs; Lace Cuffs

### body

- `(root)` (3): Body Color; Shadow; Wheelchair
- `lizard` (3): Batlike Lizard Wings; Lizard tail; Lizard Wings
- `prostheses` (2): Hook hand; Peg leg
- `special` (2): Skeleton; Zombie
- `tails` (4): Cat Tail; Fluffy Wolf Tail; Lizard Tail (Alt Colors); Wolf Tail
- `wings` (4): Bat Wings; Feathered Wings; Lizard Wings (Alt Colors); Lunar Wings
- `wings/dragonfly` (2): Dragonfly Wings; Transparent Dragonfly Wings
- `wings/monarch` (3): Monarch Wings; Monarch Wings Dots; Monarch Wings Edge
- `wings/pixie` (2): Pixie Wings; Transparent Pixie Wings
- `wounds` (6): Arm; Brain; Left Eye; Mouth; Ribs; Right Eye

### feet

- `(root)` (4): Armour; Hoofs; Sandals; Slippers
- `accessory` (2): Plated Toe; Thick Plated Toe
- `boots` (4): Basic Boots; Folded Rim Boots; Revised Boots; Rimmed Boots
- `shoes` (4): Basic Shoes; Ghillies; Revised Shoes; Sara Shoes
- `socks` (3): Ankle Socks; High Socks; Tabi Socks

### hair

- `afro` (9): Afro; Cornrows; Dreadlocks long; Dreadlocks short; Flat top fade; Flat top straight; Natural; Twists fade; Twists straight
- `bald` (5): Balding; Buzzcut; High and tight; Longhawk; Shorthawk
- `beards` (5): 5 O'clock Shadow; Basic Beard; Medium Beard; Trimmed Beard; Winter Beard
- `bob` (4): Bob; Bob side part; Lob; Relm Short
- `braids` (16): Bangs bun; Braid; Braid2; Half up; High ponytail; Long tied; Long Topknot; Long Topknot 2; Ponytail; Ponytail2; Relm w/Ponytail; Relm XLong; Short Topknot; Short Topknot 2; Shoulderl; Shoulderr
- `curly` (6): Curly long; Curly short; Curly short 2; Jewfro; Large Curls; Large Curls XLong
- `extensions/bangs` (14): Left Braid; Left Long Straight; Left Long Wavy; Left XLong Bang; Left XLong Braid; Left XLong Curly; Left XLong Wavy; Right Braid; Right Long Straight; Right Long Wavy; Right XLong Bang; Right XLong Braid; Right XLong Curly; Right XLong Wavy
- `extensions/ponytails` (3): Long Topknot; Relm Topknot; Short Topknot
- `extensions/ties` (1): High Bun
- `long` (11): Bangslong; Bangslong2; Child Wavy; Curtains long; Long; Long center part; Long messy; Long messy2; Long straight; Loose; Wavy
- `mustaches` (8): Big Mustache; Chevron Mustache; French Mustache; Handlebar Mustache; Horseshoe Mustache; Lampshade Mustache; Mustache; Walrus Mustache
- `pigtails` (3): Bunches; Pigtails; Pigtails bangs
- `short` (25): Bangs; Bangsshort; Bedhead; Cowlick; Cowlick tall; Curtains; Idol; Messy; Messy1; Messy2; Messy3; Mop; Page; Page2; Parted; Parted 2; Parted 3; Pixie; Plain; Side Parted w/Bangs; Side Parted w/Bangs 2; Side Swoop; Single; Swoop; Unkempt
- `spiky` (7): Halfmessy; Spiked; Spiked beehive; Spiked liberty; Spiked liberty2; Spiked porcupine; Spiked2
- `xlong` (5): Long band; Princess; Sara; Xlong; XLong Wavy

### head

- `(root)` (1): Wrinkles
- `appendages` (4): Backwards Horns; Curled Horns; Fin; Short fin
- `ears` (7): Big ears; Downward Elven Ears; Dragon Ears; Elven ears; Hanging Elven Ears; Long ears; Medium Elven Ears
- `eyebrows` (2): Thick Eyebrows; Thin Eyebrows
- `eyes` (1): Cyclops Eyes
- `faces` (16): Angry; Angry Alt; Blush; Closed Eyes; Closing Eyes; Happy; Happy Alt; Looking Left; Looking Right; Neutral; Rolling Eyes; Sad; Sad Alt; Shame; Shock; Tears
- `furry_ears/side` (6): Feather Ears; Feather Ears Skintone; Side Cat Ears; Side Cat Ears Skintone; Side Wolf Ears; Side Wolf Ears Skintone
- `furry_ears/top` (4): Cat Ears; Cat Ears Skintone; Wolf Ears; Wolf Ears Skintone
- `heads/beast` (9): Boarman; Boarman child; Minotaur; Minotaur child; Minotaur female; Wartotaur; Wolf child; Wolf female; Wolf male
- `heads/fantasy` (7): Goblin; Goblin child; Orc child; Orc female; Orc male; Troll; Troll child
- `heads/farm` (10): Mouse; Mouse child; Pig; Pig child; Rabbit; Rabbit child; Rat; Rat child; Sheep; Sheep child
- `heads/human` (10): Human Child; Human Elderly Small; Human Female; Human Female Elderly; Human Female Small; Human Male; Human Male Elderly; Human Male Gaunt; Human Male Plump; Human Male Small
- `heads/reptile` (4): Alien; Lizard child; Lizard female; Lizard male
- `heads/undead` (5): Frankenstein; Jack O Lantern; Skeleton; Vampire; Zombie
- `neck` (13): Bowtie; Bowtie 2; Capeclip; Capetie; Chain Necklace; Cravat; Jabot; Large Beaded Necklace; Necklace; Necktie; Scarf; Simple Necklace; Small Beaded Necklace
- `neck/charms` (16): Box Charm; Cross amulet; Dangling amulet; Emerald cut Gem; Marquise cut Gem; Natural cut Gem; Oval Charm; Pear cut Gem; Pearl Gem; Princess cut Gem; Ring Charm; Round cut Gem; Spider amulet; Star amulet; Star Charm; Trilliant cut Gem
- `nose` (5): Big nose; Button nose; Elderly nose; Large nose; Straight nose

### headwear

- `accessories` (1): Plain Mask
- `accessories/earrings` (7): Emerald earrings; Moon earrings; Pear earrings; Princess earrings; Simple Earring Left; Simple Earring Right; Stud earrings
- `accessories/eyepatch` (7): Eyepatch 2 Left; Eyepatch 2 Right; Eyepatch Ambidextrous; Eyepatch Left; Eyepatch Right; Small Eyepatch Left; Small Eyepatch Right
- `accessories/glasses` (7): Glasses; Halfmoon Glasses; Nerd Glasses; Round Glasses; Secretary Glasses; Shades; Sunglasses
- `accessories/monocle` (4): Left Monocle; Left Monocle Frame Color; Right Monocle; Right Monocle Frame Color
- `coverings/bandana` (4): Bandana; Bordered Bandana; Pirate Bandana; Skull Bandana Overlay
- `coverings/headbands` (6): Hair Tie; Hair Tie Rune; Kerchief; Thick Headband; Thick Headband Rune; Tied Headband
- `coverings/hoods` (3): Hijab; Hood; Sack Cloth Hood
- `hats/athwart` (9): Bicorne Athwart; Bicorne Athwart Admiral; Bicorne Athwart Admiral Cockade; Bicorne Athwart Admiral Trim; Bicorne Athwart Captain; Bicorne Athwart Captain Skull; Bicorne Athwart Commodore; Bicorne Athwart Commodore Trim; Bicorne Athwart Skull
- `hats/caps` (8): Bonnie; Bonnie Alt Tilt; Bonnie Center Trim; Bonnie feather; Cavalier; Cavalier feather; Leather Cap; Leather Cap Feather
- `hats/foreaft` (3): Bicorne foreaft; Bicorne Foreaft Commodore; Bicorne Foreaft Commodore Trim
- `hats/formal` (4): Crown; Formal Bowler Hat; Formal Tophat; Tiara
- `hats/holiday` (3): Christmas Hat; Elf Trim; Santa Trim
- `hats/magic` (8): Celestial Wizard Hat; Celestial Wizard Hat Second Color; Celestial Wizard Moon Hat; Celestial Wizard Moon Hat Second Color; Large Hat; Wizard Hat Base; Wizard Hat Belt; Wizard Hat Buckle
- `hats/tricorne` (8): Tricorne; Tricorne Captain; Tricorne Captain Skull; Tricorne Captain Trim; Tricorne Lieutenant; Tricorne Lieutenant Trim; Tricorne Stitching; Tricorne Thatching
- `helmets/accessories` (9): Centurion Crest; Centurion Plumage; Crest; Downward Horns; Helmet wings; Legion Plumage; Plumage; Short Horns; Upward Horns
- `helmets/helmets` (28): Armet; Barbarian; Barbarian nasal; Barbarian Viking; Barbuta; Bascinet; Close helm; Flattop; Greathelm; Horned helmet; Kettle helm; Legion; Mail; Maximus; Morion; Nasal helm; Norman helm; Pigface bascinet; Pigface bascinet raised; Pointed helm; Round bascinet; Simple Armet; Simple barbuta; Simple sugarloaf helm; Spangenhelm; Sugarloaf greathelm; Viking spangenhelm; Xeon helmet
- `helmets/visors` (9): Grated visor; Horned visor; Narrow grated visor; Narrow slit visor; Pigface visor; Pigface visor raised; Round visor; Round visor raised; Slit visor

### legs

- `(root)` (1): Armour
- `leggings` (3): Hose; Leggings; Leggings 2
- `pants` (10): Child pants; Cuffed Pants; Formal Pants; Fur Pants; Long Pants; Pantaloons; Pants; Pregnancy pants; Striped Formal Pants; Wide pants
- `shorts` (2): Short Shorts; Shorts
- `skirts` (7): Belle skirt; Child skirts; Legion skirt; Overskirt; Plain skirt; Slit skirt; Straight skirt

### tools

- `(root)` (4): Rod; Smash; Thrust; Whip

### torso

- `(root)` (2): Bandages; Chainmail
- `aprons` (5): Apron; Apron full; Apron half; Overalls; Suspenders
- `armour` (3): Leather; Legion; Plate
- `backpack` (7): Backpack; Basket; Jetpack; Jetpack fins; Quiver; Square pack; Straps
- `backpack/cargo` (2): Ore; Wood
- `cape` (3): Cape Trim; Solid; Tattered
- `dresses` (3): Bodice; Sash dress; Slit dress
- `dresses/kimono` (8): Kimono; Kimono Oversized Sleeves; Kimono Oversized Sleeves Trim; Kimono Sleeves; Kimono Sleeves Trim; Kimono Trim; Split Kimono; Split Kimono Trim
- `jacket` (6): Collared coat; Frock coat; Iverness cloak; Santa coat; Tabard; Trench coat
- `jacket/accessory` (5): Frock coat buttons; Frock coat lace; Frock coat lapel; Frock collar; Jacket pockets
- `shirts` (7): Blouse; Child shirts; Corset; Longsleeve blouse; Robe; Sara Tunic; Tunic
- `shirts/longsleeve` (12): Cardigan; Collared/Formal Longsleeve; Cuffed Longsleeves Overlay; Longsleeve; Longsleeve 2; Longsleeve 2 Buttoned; Longsleeve 2 Scoop; Longsleeve 2 VNeck; Longsleeve laced; Longsleeve Polo; Scoop; Striped Collared/Formal Longsleeve
- `shirts/shortsleeve` (7): Shortsleeve; Shortsleeve Cardigan; Shortsleeve Polo; TShirt; TShirt Buttoned; TShirt Scoop; TShirt VNeck
- `shirts/sleeveless` (11): Original Sleeveless; Sleeveless; Sleeveless 2; Sleeveless 2 Buttoned; Sleeveless 2 Cardigan; Sleeveless 2 Polo; Sleeveless 2 Scoop; Sleeveless 2 VNeck; Sleeveless laced; Sleeveless striped; Tanktop
- `shirts/sleeves` (4): Longsleeves 2 Overlay; Original Longsleeves Overlay; Original Shortsleeves Overlay; Shortsleeves 2 Overlay
- `vest` (2): Vest; Vest open
- `waist` (12): Belly belt; Buckles; Double Belt; Formal Belt; Leather Belt; Leather Belt Alt; Loose Belt; Mage Belt; Narrow sash; Robe Belt; Sash; Waistband
- `waist/obi` (3): Obi; Obi Knot Left; Obi Knot Right

### weapons

- `blunt` (4): Club; Flail; Mace; Waraxe
- `magic` (7): Crystal; Diamond staff; Gnarled staff; Loop staff; S staff; Simple staff; Wand
- `polearm` (7): Cane; Dragon spear; Halberd; Long spear; Scythe; Spear; Trident
- `ranged` (3): Boomerang; Crossbow; Slingshot
- `ranged/bow` (4): Ammo; Great; Normal; Recurve
- `shields` (3): Kite; Round Shield; Spartan Shield
- `shields/engrailed` (4): Crusader shield; Plus shield; Two engrailed shield; Two engrailed shield trim
- `shields/heater` (6): Heater Shield Base; Heater Shield Paint; Heater Shield Trim; Revised Heater Shield Base; Revised Heater Shield Paint; Revised Heater Shield Trim
- `shields/heater/pattern` (24): barry; bend; bend_sinister; bendy; bendy_sinister; bordure; chevron; chevron_inverted; chief; cross; fess; lozengy; pale; pall; paly; per_bend; per_bend_sinister; per_chevron; per_chevron_inverted; per_fess; per_pale; per_saltire; quarterly; saltire
- `shields/heater/revised_pattern` (24): revised_barry; revised_bend; revised_bend_sinister; revised_bendy; revised_bendy_sinister; revised_bordure; revised_chevron; revised_chevron_inverted; revised_chief; revised_cross; revised_fess; revised_lozengy; revised_pale; revised_pall; revised_paly; revised_per_bend; revised_per_bend_sinister; revised_per_chevron; revised_per_chevron_inverted; revised_per_fess; revised_per_pale; revised_per_saltire; revised_quarterly; revised_saltire
- `shields/scutum` (2): Scutum shield; Scutum shield trim
- `sword` (9): Arming Sword; Dagger; Glowsword; Katana; Longsword; Longsword alt; Rapier; Saber; Scimitar

## LIT-ISO Takeaways

- Great reference for modular character categories and naming.
- Great reference for JSON/credits/provenance discipline.
- Direct shipping use remains risky because the pack is mixed-license and not original LIT-ISO art.
- Best production approach: build an original LIT-ISO character generator with similar categories, not copied pixels.
