// Shared tool navigator for the LIT-ISO worldgen web tools.
// Include with: <script src="nav.js"></script> just before </body>.
// Renders a small floating launcher (bottom-right) that opens a menu of all tools.
(function(){
  var TOOLS = [
    { f:'biome_world_overview.html', n:'Biome World Overview',   d:'All biomes side-by-side · prop density · guide' },
    { f:'biome_variations.html',     n:'Biome Variant Cards',    d:'21 sub-variants · tiles + props + mobs · 6 biomes' },
    { f:'variant_world_preview.html',n:'Variant World Preview',  d:'Isometric render of all 21 biome variants live' },
    { f:'biome_preview.html',        n:'Biome Preview + Editor', d:'Generate & tune each biome' },
    { f:'settlement_editor.html',    n:'Settlement Editor',      d:'Villages / towns / cities' },
    { f:'interior_editor.html',      n:'Interior Editor',        d:'Tavern / guild / library interiors' },
    { f:'character_party_builder.html', n:'Party Builder',       d:'Clean LPC-style party lineup for key art' },
    { f:'bridge_format_preview.html',n:'Bridge Preview',         d:'Bridge format comparison' },
    { f:'index.html',                n:'Biome Sketch (freehand)',d:'Hand-paint vignettes' },
    { f:'variant_lab.html',          n:'Tile Variant Lab',       d:'Tile variants + blends' },
    { f:'vfx_preview.html',          n:'VFX Preview + Tuner',    d:'Animate & tune smoke/magic VFX' },
    { f:'rules.html',                n:'Worldgen Rules',         d:'Placement rule vignettes' },
    { f:'previews.html',             n:'Biome Previews',         d:'Rendered biome samples' }
  ];
  var here = (location.pathname.split('/').pop() || 'index.html').toLowerCase();

  var css = ''
   + '#litnav-fab{position:fixed;bottom:14px;right:14px;z-index:2147483000;background:#5b4d20;color:#e8c468;'
   + 'border:1px solid #e8c468;border-radius:22px;padding:8px 14px;font:600 13px Segoe UI,Arial,sans-serif;'
   + 'cursor:pointer;box-shadow:0 3px 12px rgba(0,0,0,.45);user-select:none}'
   + '#litnav-fab:hover{background:#6d5c27}'
   + '#litnav-menu{position:fixed;bottom:58px;right:14px;z-index:2147483000;width:270px;background:#1d2027;'
   + 'border:1px solid #3a4150;border-radius:10px;padding:6px;box-shadow:0 8px 28px rgba(0,0,0,.55);display:none}'
   + '#litnav-menu.open{display:block}'
   + '#litnav-menu .hd{color:#e8c468;font:600 11px Segoe UI,Arial,sans-serif;padding:6px 8px 8px;border-bottom:1px solid #2a2f3a;margin-bottom:4px}'
   + '#litnav-menu a{display:block;text-decoration:none;color:#cdd;padding:7px 9px;border-radius:6px;font:13px Segoe UI,Arial,sans-serif}'
   + '#litnav-menu a small{display:block;color:#889;font-size:11px;margin-top:1px}'
   + '#litnav-menu a:hover{background:#262c38}'
   + '#litnav-menu a.cur{background:#33301f;color:#e8c468;cursor:default}'
   + '#litnav-menu a.cur small{color:#9a8a52}';
  var st = document.createElement('style'); st.textContent = css; document.head.appendChild(st);

  var fab = document.createElement('div'); fab.id = 'litnav-fab'; fab.textContent = '▦ Tools';
  var menu = document.createElement('div'); menu.id = 'litnav-menu';
  var html = '<div class="hd">LIT-ISO WORLDGEN TOOLS</div>';
  TOOLS.forEach(function(t){
    var cur = (t.f.toLowerCase() === here);
    html += '<a class="'+(cur?'cur':'')+'" '+(cur?'':'href="'+t.f+'"')+'>'+t.n
          + (cur?' &middot; <span style="font-size:11px">you are here</span>':'')
          + '<small>'+t.d+'</small></a>';
  });
  menu.innerHTML = html;

  function toggle(){ menu.classList.toggle('open'); }
  fab.addEventListener('click', function(e){ e.stopPropagation(); toggle(); });
  document.addEventListener('click', function(e){ if(!menu.contains(e.target) && e.target!==fab) menu.classList.remove('open'); });

  function mount(){ document.body.appendChild(fab); document.body.appendChild(menu); }
  if(document.body) mount(); else document.addEventListener('DOMContentLoaded', mount);
})();
