// Rule-focused BiomeSketch presets.
//
// These are visual vignettes for validating placement rules in the browser
// sketch tool. They are not runtime worldgen data and should not be treated as
// approved art promotion.

(() => {
  if (typeof PRESETS === 'undefined') return;

  const cell = (t, d = null, h = 0) => ({ t, d, h, f: false, df: false });
  const make = (n, tile, h = 0) => Array.from({ length: n }, () =>
    Array.from({ length: n }, () => cell(tile, null, h)));

  const put = (grid, r, c, t, d = null, h = null) => {
    if (!grid[r] || !grid[r][c]) return;
    grid[r][c].t = t;
    grid[r][c].d = d;
    if (h !== null) grid[r][c].h = h;
  };

  const prop = (grid, r, c, d) => {
    if (!grid[r] || !grid[r][c]) return;
    grid[r][c].d = d;
  };

  const heightPatch = (grid, coords, h) => {
    coords.forEach(([r, c]) => {
      if (grid[r] && grid[r][c]) grid[r][c].h = h;
    });
  };

  const pushPreset = (name, grid) => PRESETS.push({ name, size: grid.length, grid });

  function meadowForestTransition() {
    const n = 17;
    const g = make(n, 'plains2_00');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if (c > 10) g[r][c].t = (r + c) % 3 === 0 ? 'forest_leaf_litter' : 'forest_floor';
        else if (c > 7) g[r][c].t = (r % 2 === 0) ? 'forest_moss_grass' : 'forest_grass_base';
        else if ((r + c) % 7 === 0) g[r][c].t = 'plains2_02';
        else if ((r * 3 + c) % 11 === 0) g[r][c].t = 'plains2_03';
      }
    }

    for (let r = 1; r < n - 1; r++) {
      g[r][7].t = r % 2 === 0
        ? 'blend_plains2_00_to_plains_bare_dirt_e_v01'
        : 'blend_plains2_00_to_plains_bare_dirt_e_v02';
      g[r][8].t = r % 2 === 0 ? 'forest_grass_base' : 'forest_moss_grass';
      g[r][9].t = r % 3 === 0 ? 'forest_leaf_litter' : 'forest_floor';
    }

    [[2, 12], [3, 14], [5, 11], [8, 13], [10, 15], [13, 12], [14, 14]].forEach(([r, c]) => {
      prop(g, r, c, (r + c) % 2 ? 'forest_oak_tree' : 'forest_deep_oak_tree');
    });
    [[4, 9], [6, 10], [11, 9], [12, 11], [15, 10]].forEach(([r, c]) => prop(g, r, c, 'forest_bush'));
    [[2, 3], [5, 5], [11, 4], [14, 6]].forEach(([r, c]) => prop(g, r, c, 'plains_bush_v2_0'));
    [[7, 7], [8, 8], [9, 7]].forEach(([r, c]) => put(g, r, c, 'plains2_00', null, 0));

    pushPreset('RULE_meadow_to_forest_transition', g);
  }

  function forestGroveClearing() {
    const n = 17;
    const g = make(n, 'forest_floor');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if ((r - 8) * (r - 8) + (c - 8) * (c - 8) < 15) g[r][c].t = 'forest_grass_base';
        else if ((r + c) % 4 === 0) g[r][c].t = 'forest_leaf_litter';
        else if ((r * 2 + c) % 5 === 0) g[r][c].t = 'forest_moss_grass';
      }
    }

    [
      [1, 5], [1, 10], [2, 3], [2, 13], [4, 2], [4, 14],
      [6, 1], [8, 15], [10, 1], [12, 3], [12, 14], [14, 5],
      [14, 11], [15, 8]
    ].forEach(([r, c], i) => prop(g, r, c, i % 3 === 0 ? 'forest_deep_oak_tree' : 'forest_oak_tree'));
    [[5, 5], [5, 11], [8, 4], [9, 12], [11, 6], [11, 10]].forEach(([r, c]) => prop(g, r, c, 'forest_bush'));
    [[7, 7], [7, 9], [9, 7], [9, 9]].forEach(([r, c]) => prop(g, r, c, 'stump'));
    [[8, 8], [6, 8], [10, 8]].forEach(([r, c]) => put(g, r, c, 'forest_grass_base', null, 0));

    pushPreset('RULE_forest_grove_clearing', g);
  }

  function mountainHeightBands() {
    const n = 13;
    const g = make(n, 'plains2_02');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        const d = Math.abs(r - 6) + Math.abs(c - 6);
        if (d < 3) put(g, r, c, 'mountain_stone_05', null, 4);
        else if (d < 5) put(g, r, c, 'mountain_stone_03', null, 3);
        else if (d < 7) put(g, r, c, 'blend_plains2_02_to_mountain_stone_05_n_v01', null, 2);
        else if (d < 8) put(g, r, c, 'plains_bare_dirt', null, 1);
      }
    }

    [[4, 5], [5, 8], [7, 4], [8, 7], [6, 6]].forEach(([r, c]) => prop(g, r, c, 'shared_gray_rock'));
    [[3, 6], [9, 6]].forEach(([r, c]) => prop(g, r, c, 'plains_rock_v2_0'));

    pushPreset('RULE_mountain_height_bands', g);
  }

  function snowMountainTransition() {
    const n = 13;
    const g = make(n, 'snow2_00');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if (r + c > 15) put(g, r, c, 'mountain_stone_03', null, 2);
        else if (r + c > 11) put(g, r, c, 'blend_snow2_00_to_mountain_stone_03_se_v01', null, 1);
      }
    }
    [[8, 9], [9, 7], [10, 10], [11, 8]].forEach(([r, c]) => prop(g, r, c, 'shared_gray_rock'));
    [[4, 4], [5, 6], [7, 3]].forEach(([r, c]) => prop(g, r, c, 'plains_bush_v2_1'));

    pushPreset('RULE_snow_to_mountain_transition', g);
  }

  function coastShoreline() {
    const n = 13;
    const g = make(n, 'water_deep');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if (r + c > 14) put(g, r, c, 'plains2_00', null, 1);
        else if (r + c > 11) put(g, r, c, 'sand_1', null, 0);
        else if ((r + c) % 3 === 0) g[r][c].t = 'water_swell_1';
      }
    }
    [[8, 6], [9, 5], [10, 4], [11, 3]].forEach(([r, c]) => put(g, r, c, 'sand_2', null, 0));
    [[9, 9], [10, 8], [11, 10]].forEach(([r, c]) => prop(g, r, c, 'plains_bush_v2_0'));
    [[7, 6], [8, 5], [10, 3]].forEach(([r, c]) => prop(g, r, c, 'shared_gray_rock'));

    pushPreset('RULE_coast_sand_water_edge', g);
  }

  function tavernRankFootprints() {
    const n = 17;
    const g = make(n, 'plains2_00');

    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if ((r > 2 && r < 14 && c > 2 && c < 14) && (r + c) % 2 === 0) g[r][c].t = 'plains_bare_dirt';
      }
    }

    // Planner-selected rank buildings.
    prop(g, 4, 3, 'tavern_r1');
    prop(g, 5, 8, 'tavern_r2');
    prop(g, 6, 13, 'tavern_r3');

    // Raw candidate alternatives for direct visual comparison.
    prop(g, 11, 2, 'tavern_r1_frame_0');
    prop(g, 11, 5, 'tavern_r1_frame_1');
    prop(g, 11, 8, 'tavern_r1_frame_2');
    prop(g, 11, 11, 'tavern_r1_frame_3');

    [[1, 1], [2, 15], [14, 3], [15, 14]].forEach(([r, c]) => prop(g, r, c, 'plains_tree_v2_0'));
    [[8, 3], [9, 5], [9, 12], [13, 9]].forEach(([r, c]) => prop(g, r, c, 'plains_bush_v2_0'));

    pushPreset('RULE_tavern_rank_footprint_review', g);
  }

  function propDensityLanes() {
    const n = 17;
    const g = make(n, 'plains2_00');

    for (let i = 0; i < n; i++) {
      put(g, i, 8, 'plains_bare_dirt', null, 0);
      put(g, 8, i, 'plains_bare_dirt', null, 0);
    }

    [
      [1, 2, 'plains_tree_v2_0'], [2, 4, 'plains_tree_v2_1'], [3, 13, 'plains_tree_v2_0'],
      [5, 3, 'plains_bush_v2_0'], [5, 12, 'plains_bush_v2_1'], [6, 14, 'plains_rock_v2_0'],
      [10, 2, 'plains_bush_v2_0'], [11, 4, 'plains_rock_v2_0'], [12, 13, 'plains_tree_v2_1'],
      [14, 5, 'plains_bush_v2_1'], [15, 12, 'plains_rock_v2_0']
    ].forEach(([r, c, d]) => prop(g, r, c, d));

    heightPatch(g, [[0, 0], [0, 1], [1, 0], [15, 15], [16, 15], [15, 16], [16, 16]], 1);

    pushPreset('RULE_prop_density_with_clear_lanes', g);
  }

  meadowForestTransition();
  forestGroveClearing();
  mountainHeightBands();
  snowMountainTransition();
  coastShoreline();
  tavernRankFootprints();
  propDensityLanes();
})();
