using System;

namespace IsoCore.Foundation
{
    [Serializable]
    public class FoundationCharacterAppearanceSaveData
    {
        public string appearanceId;
        public string bodyId;
        public string bodyType;
        public string skinPaletteId;
        public string hairId;
        public string hairPaletteId;
        public string outfitId;
        public string outfitPaletteId;
        public int directionCount = 8;
        public int framesPerRow = 4;
        public string spriteResource;
        public string provenanceId;

        public bool HasSpriteResource => !string.IsNullOrWhiteSpace(spriteResource);

        public FoundationCharacterAppearanceSaveData Clone()
        {
            return new FoundationCharacterAppearanceSaveData
            {
                appearanceId = appearanceId,
                bodyId = bodyId,
                bodyType = bodyType,
                skinPaletteId = skinPaletteId,
                hairId = hairId,
                hairPaletteId = hairPaletteId,
                outfitId = outfitId,
                outfitPaletteId = outfitPaletteId,
                directionCount = directionCount,
                framesPerRow = framesPerRow,
                spriteResource = spriteResource,
                provenanceId = provenanceId,
            };
        }
    }

    [Serializable]
    public class FoundationCharacterAppearanceDefinition
    {
        public string id;
        public string displayName;
        public string bodyType;
        public string spriteResource;
        public string previewResource;
        public int directionCount = 8;
        public int framesPerRow = 4;
        public string provenanceId;
        public bool prototypeOnly;

        public FoundationCharacterAppearanceSaveData ToSaveData()
        {
            return new FoundationCharacterAppearanceSaveData
            {
                appearanceId = id,
                bodyId = bodyType,
                bodyType = bodyType,
                skinPaletteId = "default",
                hairId = id,
                hairPaletteId = "default",
                outfitId = id,
                outfitPaletteId = "default",
                directionCount = directionCount,
                framesPerRow = framesPerRow,
                spriteResource = spriteResource,
                provenanceId = provenanceId,
            };
        }
    }
}
