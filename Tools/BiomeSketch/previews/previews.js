const RULE_PREVIEWS = {
 "grid": 256,
 "scale": 3,
 "items": [
  {
   "file": "previews/continent_overview.png",
   "title": "Continent overview (A2 macro cells + A4 gating + B-blends + D1 sites)",
   "note": "13 settlement sites (red) on flat mid-elevation meadow; ocean/beach ring; mountain->snowcap by elevation; biome regions are macro-cell coherent."
  },
  {
   "file": "previews/blend_meadow_forest.png",
   "title": "Forest -> meadow blend band (B1\u2013B4)",
   "note": "6-cell band: weighted base crossfade + accent ramp; grove density fades to lone trees (owner rule in forest.json)."
  },
  {
   "file": "previews/blend_meadow_snow.png",
   "title": "Snow -> meadow blend band (B1\u2013B3, A5)",
   "note": "cold/temperate pair is legal-adjacent; dithered crossfade replaces the hard palette switch."
  },
  {
   "file": "previews/blend_beach_water.png",
   "title": "Coastline: water -> wet sand -> dry sand (B6)",
   "note": "beach exists only against water; swell accents on the rim band (current rule kept, band art pending promotion)."
  },
  {
   "file": "previews/mountain_strata.png",
   "title": "Mountain stratification + lapse rate (A4, C6)",
   "note": "meadow -> scree -> cracked stone -> snowcap by elevation band; ore density rises with height. NOTE: stone_* ids are hand-colored \u2014 family exists in PixelArt but is not promoted yet."
  },
  {
   "file": "previews/props_compare.png",
   "title": "Prop scatter: current random (left) vs blue-noise contract (right) (C1\u2013C2)",
   "note": "same average density both sides; right side has no accidental clumps/voids and respects min spacing."
  },
  {
   "file": "previews/town_hamlet.png",
   "title": "Town preset: HAMLET (D1\u2013D3)",
   "note": "4 building lots, plaza-anchored, doors (gold) face the plaza; ring+cross roads; decor suppressed inside town core (C4/D7)."
  },
  {
   "file": "previews/town_village.png",
   "title": "Town preset: VILLAGE (D1\u2013D5)",
   "note": "9 building lots, plaza-anchored, doors (gold) face the plaza; ring+cross roads; farm plots outer ring; decor suppressed inside town core (C4/D7)."
  },
  {
   "file": "previews/town_large.png",
   "title": "Town preset: TOWN w/ farms + dock (D1\u2013D5)",
   "note": "16 building lots, plaza-anchored, doors (gold) face the plaza; ring+cross roads; farm plots outer ring; dock arm to water; decor suppressed inside town core (C4/D7)."
  }
 ]
};\n