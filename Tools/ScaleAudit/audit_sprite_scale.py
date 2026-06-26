#!/usr/bin/env python3
"""Bulk sprite-scale auditor: measures transparent padding per prop PNG so
NormalizeHeight can be made trim-aware. Run: python3 Tools/ScaleAudit/audit_sprite_scale.py
Outputs out/*.csv|json + Assets/Resources/sprite_content_metrics.json. Usage: script.py [repo_root]"""
import csv, glob, json, os, re, sys
from PIL import Image
RULES = [
 (r"guild_hall|great_hall|\bhall\b|keep|tower|cathedral","monument",5.5),
 (r"tavern|library|forge|smithy|store|shop|inn|church|temple","building",3.8),
 (r"cottage|house|cabin|hut|home|barn|stable|building","cottage",2.6),
 (r"\bpine\b|\boak\b|\btree\b|dead_tree|conifer|spruce","tree",3.0),
 (r"sapling|young_tree|sprout","sapling",1.8),
 (r"scarecrow|tavern_sign|\bsign\b|dock_post|\bpost\b|totem","tallmarker",1.4),
 (r"market_stall|stall|tent|campfire|brazier|lantern|well|cart|wagon","campobject",1.1),
 (r"villager|merchant|person|npc|human|figure","person",1.1),
 (r"pillar|column|statue|obelisk","pillar",1.6),
 (r"barrel|crate|chest|anvil|keg|sack|stool|bench|table|counter|cauldron|book_stack|boat","furniture",0.85),
 (r"stump|\blog\b|fallen","stump",0.5),
 (r"\bbush\b|shrub|fern|cactus|boulder","bush",0.6),
 (r"\brock\b|stone|pebble|ore|vein|mushroom|crystal","smallrock",0.5),
 (r"flower|tulip|tuft|grass|sprig|clover|weed|reed","groundcover",0.28),
 (r"fence|wall|gate|rail|palisade","fence",0.9),
]
def classify(n):
    n=n.lower()
    for p,c,h in RULES:
        if re.search(p,n): return c,h
    return "unclassified",0.7
root=os.path.abspath(sys.argv[1] if len(sys.argv)>1 else ".")
deco=os.path.join(root,"Assets","Resources","Decorations")
files=sorted(glob.glob(os.path.join(deco,"**","*.png"),recursive=True))
outdir=os.path.join(root,"Tools","ScaleAudit","out"); os.makedirs(outdir,exist_ok=True)
rows={}; metrics={}; heights={}; pad={"0-10":0,"10-25":0,"25-40":0,"40+":0}; cats={}; off=[]
csvrows=[]
for f in files:
    name=os.path.splitext(os.path.basename(f))[0]
    try: im=Image.open(f).convert("RGBA")
    except: continue
    W,H=im.size; bb=im.split()[3].getbbox()
    if not bb: continue
    ch=bb[3]-bb[1]; cw=bb[2]-bb[0]; frac=ch/H; bpad=H-bb[3]; p=1-frac
    cat,rh=classify(name); cats[cat]=cats.get(cat,0)+1
    k="40+" if p>=.40 else "25-40" if p>=.25 else "10-25" if p>=.10 else "0-10"
    pad[k]+=1
    if p>=.25 and ch>16: off.append((round(p*100),name,"%dx%d"%(W,H)))
    csvrows.append([name,cat,rh,W,H,cw,ch,round(frac,3),bb[1],bpad,round(p,3)])
    metrics[name]={"contentFracH":round(frac,4),"bottomPadFrac":round(bpad/H,4)}
    heights[name]={"category":cat,"heightUnits":rh}
with open(os.path.join(outdir,"sprite_metrics.csv"),"w",newline="") as fh:
    w=csv.writer(fh); w.writerow(["name","category","recHeightUnits","canvasW","canvasH","contentW","contentH","contentFracH","topPad","bottomPad","padPct"]); w.writerows(sorted(csvrows))
json.dump({"sprites":metrics},open(os.path.join(outdir,"sprite_content_metrics.json"),"w"),indent=1)
json.dump({"sprites":heights},open(os.path.join(outdir,"sprite_heights.recommended.json"),"w"),indent=1)
resdir=os.path.join(root,"Assets","Resources"); os.makedirs(resdir,exist_ok=True)
nm=sorted(metrics.keys())
flat={"names":nm,"contentFracH":[metrics[n]["contentFracH"] for n in nm],"bottomPadFrac":[metrics[n]["bottomPadFrac"] for n in nm]}
json.dump(flat,open(os.path.join(resdir,"sprite_content_metrics.json"),"w"))
print("Scanned",len(csvrows),"prop PNGs")
print("padding:",pad)
print("cats:",dict(sorted(cats.items(),key=lambda x:-x[1])))
print("worst:")
for a,b,c in sorted(off,reverse=True)[:12]: print("  %3d%% %s %s"%(a,b[:42].ljust(42),c))
print("runtime json entries:",len(nm))
