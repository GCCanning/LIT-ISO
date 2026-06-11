"""
Import PixelLab animation frames into the menu flipbook.

Reads PixelArt/MenuScene/frames/frame_*.png, nearest-neighbor x5 upscales,
center-crops to 1920x1080, writes Resources/UI/Menu/background_frames/bg_NN.png
with pixel-art .meta files. MenuBackgroundFlipbook auto-detects them on next run.

Run from anywhere:  py -3 import_menu_frames.py
"""

import os, re, uuid, glob, sys

try:
    from PIL import Image
except ImportError:
    sys.exit("pip install pillow")

SRC = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\MenuScene\frames"
DST = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\UI\Menu\background_frames"

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def main():
    frames = sorted(glob.glob(os.path.join(SRC, "frame_*.png")))
    if not frames:
        sys.exit(f"No frames at {SRC} — run animate_menu_scene first.")
    os.makedirs(DST, exist_ok=True)
    for i, f in enumerate(frames):
        im = Image.open(f).convert("RGBA")
        big = im.resize((im.width * 5, im.height * 5), Image.NEAREST)
        x = max(0, (big.width - 1920) // 2)
        y = max(0, (big.height - 1080) // 2)
        final = big.crop((x, y, x + 1920, y + 1080))
        out = os.path.join(DST, f"bg_{i:02d}.png")
        final.save(out)
        mp = out + ".meta"
        guid = uuid.uuid4().hex
        if os.path.exists(mp):
            m = re.search(r"guid: ([0-9a-f]{32})", open(mp).read())
            if m: guid = m.group(1)
        open(mp, "w").write(META.format(guid=guid))
        print("imported", out)
    print(f"\n{len(frames)} frames -> menu flipbook. Recompile/refocus Unity and the menu animates.")

if __name__ == "__main__":
    main()
