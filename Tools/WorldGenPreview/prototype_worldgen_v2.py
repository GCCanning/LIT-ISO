#!/usr/bin/env python3
"""
LIT-ISO worldgen PROTOTYPE v2 - macro provinces + coherent climate + downhill rivers.
Renders a comparison PNG: OLD (per-cell independent climate noise, the patchwork) vs
NEW (province-coherent). Pure prototype to validate the approach before porting to
IsoTerrainSampler.cs. Run: python3 Tools/WorldGenPreview/prototype_worldgen_v2.py
"""
import numpy as np
from PIL import Image
import os

N = 320
SEED = 240611
rng = np.random.default_rng(SEED)

# biome colors derived from asset_catalog.json mean colors
COL = dict(ocean_deep=(34,54,82), ocean=(46,74,104), beach=(151,128,104),
           meadow=(101,143,65), forest=(71,98,53), snow=(196,210,214),
           mountain=(110,116,112), badlands=(168,120,78), river=(74,118,150))

def fractal(n, octaves=5, persist=0.55, seed=0):
    r = np.random.default_rng(seed)
    out = np.zeros((n,n), np.float32); amp=1.0; tot=0.0; size=4
    for _ in range(octaves):
        g = r.random((size+1,size+1)).astype(np.float32)
        img = np.array(Image.fromarray((g*255).astype(np.uint8)).resize((n,n), Image.BICUBIC),np.float32)/255.0
        out += img*amp; tot+=amp; amp*=persist; size*=2
    out/=tot
    return (out-out.min())/(out.max()-out.min()+1e-6)

# ---- elevation: fractal shaped into an island (ocean rim) per owner's continent vision
elev = fractal(N, 6, 0.5, SEED)
yy,xx = np.mgrid[0:N,0:N]
cx=cy=N/2
rad = np.sqrt((xx-cx)**2+(yy-cy)**2)/(N*0.52)
elev = np.clip(elev*1.15 - rad**2*0.9, 0, 1)
sea = 0.34
land = elev>sea

# ---- coherent climate fields (low frequency) ----
latitude = yy/ N                                  # 0 north .. 1 south
tnoise = fractal(N,3,0.5,SEED+7)
mnoise = fractal(N,3,0.5,SEED+13)
# temperature: warm south, cold north + noise - elevation lapse
temp = (1-latitude)*0.62 + tnoise*0.38 - np.clip((elev-sea),0,1)*0.55
temp = np.clip(temp,0,1)
# moisture: noise + coastal bonus (near sea = wetter)
# cheap coastal proximity without scipy: blur of land mask
lm = land.astype(np.float32)
coast = lm.copy()
for _ in range(6):
    coast = (coast + np.roll(coast,1,0)+np.roll(coast,-1,0)+np.roll(coast,1,1)+np.roll(coast,-1,1))/5
moist = np.clip(mnoise*0.7 + (1-coast)*0.3, 0,1)
# high-frequency INDEPENDENT fields to emulate the live game's per-cell climate
htemp = fractal(N,7,0.62,SEED+101)
hmoist= fractal(N,7,0.62,SEED+202)

def whittaker(t,m,e):
    if e>0.66: return 'mountain'
    if t>0.60 and m<0.40: return 'badlands'
    if t<0.30: return 'snow'
    if m>0.55: return 'forest'
    return 'meadow'

# ---- macro provinces: jittered-grid Voronoi ----
PROV=26  # province cell size in world cells
gx = N//PROV + 2
sites=[]
for j in range(gx):
    for i in range(gx):
        jitter = rng.random(2)
        sites.append(((i+jitter[0])*PROV, (j+jitter[1])*PROV))
sites=np.array(sites)
# nearest site per cell
pid = np.zeros((N,N),np.int32)
pts = np.stack([xx.ravel(),yy.ravel()],1)
# chunked nearest to save memory
for s in range(0,len(pts),20000):
    d = ((pts[s:s+20000,None,0]-sites[None,:,0])**2 + (pts[s:s+20000,None,1]-sites[None,:,1])**2)
    pid.ravel()[s:s+20000]=d.argmin(1)

# province biome = whittaker at province centroid climate (coherent!)
prov_biome={}
for p in np.unique(pid):
    mask = pid==p
    if not land[mask].any(): continue
    t=float(np.mean(temp[mask])); m=float(np.mean(moist[mask])); e=float(np.mean(elev[mask]))
    prov_biome[p]=whittaker(t,m,e)

# ---- NEW map: cell biome = its province biome (coherent regions) ----
def render(method):
    img=np.zeros((N,N,3),np.uint8)
    for y in range(N):
        for x in range(N):
            e=elev[y,x]
            if e<=sea:
                img[y,x]=COL['ocean_deep'] if e<sea-0.08 else COL['ocean']; continue
            if e<sea+0.03:
                img[y,x]=COL['beach']; continue
            if method=='old':
                b=whittaker(htemp[y,x],hmoist[y,x],e)    # per-cell HF -> patchwork
            else:
                b=prov_biome.get(pid[y,x],'meadow')        # province -> coherent
                if e>0.66: b='mountain'                     # elevation gate still local
            c=np.array(COL[b],np.float32)*(0.75+0.45*e)
            img[y,x]=np.clip(c,0,255)
    return img

old=render('old'); new=render('new')

# ---- downhill rivers on the NEW map: trace steepest descent from high cells to sea ----
def carve_rivers(img,count=10):
    img=img.copy()
    highs=np.argwhere(elev>0.7)
    rng.shuffle(highs)
    for hy,hx in highs[:count]:
        y,x=int(hy),int(hx); path=[]
        for _ in range(N):
            path.append((y,x))
            if elev[y,x]<=sea: break
            best=None;bv=elev[y,x]
            for dy,dx in [(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,1),(-1,1),(1,-1)]:
                ny,nx=y+dy,x+dx
                if 0<=ny<N and 0<=nx<N and elev[ny,nx]<bv:
                    bv=elev[ny,nx];best=(ny,nx)
            if best is None: break
            y,x=best
        if len(path)>8 and elev[path[-1][0],path[-1][1]]<=sea+0.02:
            for (py,px) in path:
                img[py,px]=COL['river']
    return img
new_r=carve_rivers(new)

def label(arr,txt):
    im=Image.fromarray(arr)
    return im

gap=8
H=N; W=N*2+gap
canvas=Image.new('RGB',(W,H+22),(12,12,14))
canvas.paste(Image.fromarray(old),(0,22))
canvas.paste(Image.fromarray(new_r),(N+gap,22))
from PIL import ImageDraw
d=ImageDraw.Draw(canvas)
d.text((6,6),"OLD: per-cell climate noise (patchwork)",fill=(220,180,120))
d.text((N+gap+6,6),"NEW: macro provinces + coherent climate + downhill rivers",fill=(150,210,150))
out='/sessions/exciting-gifted-ritchie/mnt/outputs/worldgen_v2_compare.png'
canvas.resize((W*2,(H+22)*2),Image.NEAREST).save(out)
print("provinces:",len(prov_biome),"-> ",out)
from collections import Counter
print("province biome mix:",dict(Counter(prov_biome.values())))
