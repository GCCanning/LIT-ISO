using System;
using System.Collections.Generic;

namespace IsoCore.Foundation
{
    public static class FoundationCharacterAppearanceCatalog
    {
        public const string DefaultAppearanceId = "adventurer";
        public const string DefaultSpriteResource = "Characters/Player/Generated/LitIsoCreator_Adventurer_512x1024";

        static readonly FoundationCharacterAppearanceDefinition[] s_Definitions =
        {
            new()
            {
                id = "adventurer",
                displayName = "Adventurer",
                bodyType = "adult",
                spriteResource = "Characters/Player/Generated/LitIsoCreator_Adventurer_512x1024",
                previewResource = "Characters/Player/Generated/LitIsoCreator_Adventurer_512x1024",
                directionCount = 8,
                framesPerRow = 4,
                provenanceId = "litiso_creator_adventurer",
            },
            new()
            {
                id = "mystic",
                displayName = "Mystic",
                bodyType = "adult",
                spriteResource = "Characters/Player/Generated/LitIsoCreator_Mystic_512x1024",
                previewResource = "Characters/Player/Generated/LitIsoCreator_Mystic_512x1024",
                directionCount = 8,
                framesPerRow = 4,
                provenanceId = "litiso_creator_mystic",
            },
            new()
            {
                id = "ranger",
                displayName = "Ranger",
                bodyType = "adult",
                spriteResource = "Characters/Player/Generated/LitIsoCreator_Ranger_512x1024",
                previewResource = "Characters/Player/Generated/LitIsoCreator_Ranger_512x1024",
                directionCount = 8,
                framesPerRow = 4,
                provenanceId = "litiso_creator_ranger",
            },
            new()
            {
                id = "smith",
                displayName = "Smith",
                bodyType = "adult",
                spriteResource = "Characters/Player/Generated/LitIsoCreator_Smith_512x1024",
                previewResource = "Characters/Player/Generated/LitIsoCreator_Smith_512x1024",
                directionCount = 8,
                framesPerRow = 4,
                provenanceId = "litiso_creator_smith",
            },
        };

        static readonly Dictionary<string, FoundationCharacterAppearanceDefinition> s_ById = BuildIndex();

        public static IReadOnlyList<FoundationCharacterAppearanceDefinition> Definitions => s_Definitions;

        public static FoundationCharacterAppearanceDefinition Default => s_ById[DefaultAppearanceId];

        public static bool TryGet(string id, out FoundationCharacterAppearanceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(id) && s_ById.TryGetValue(id.Trim(), out definition))
                return true;

            definition = Default;
            return false;
        }

        public static FoundationCharacterAppearanceSaveData ResolveSaveData(FoundationCharacterAppearanceSaveData saveData)
        {
            if (saveData == null)
                return Default.ToSaveData();

            if (!string.IsNullOrWhiteSpace(saveData.appearanceId) && TryGet(saveData.appearanceId, out var definition))
            {
                var resolved = definition.ToSaveData();
                resolved.skinPaletteId = string.IsNullOrWhiteSpace(saveData.skinPaletteId) ? resolved.skinPaletteId : saveData.skinPaletteId;
                resolved.hairId = string.IsNullOrWhiteSpace(saveData.hairId) ? resolved.hairId : saveData.hairId;
                resolved.hairPaletteId = string.IsNullOrWhiteSpace(saveData.hairPaletteId) ? resolved.hairPaletteId : saveData.hairPaletteId;
                resolved.outfitId = string.IsNullOrWhiteSpace(saveData.outfitId) ? resolved.outfitId : saveData.outfitId;
                resolved.outfitPaletteId = string.IsNullOrWhiteSpace(saveData.outfitPaletteId) ? resolved.outfitPaletteId : saveData.outfitPaletteId;
                return resolved;
            }

            if (saveData.HasSpriteResource)
            {
                var clone = saveData.Clone();
                clone.appearanceId = string.IsNullOrWhiteSpace(clone.appearanceId) ? "custom" : clone.appearanceId.Trim();
                clone.directionCount = Math.Max(1, clone.directionCount);
                clone.framesPerRow = Math.Max(1, clone.framesPerRow);
                return clone;
            }

            return Default.ToSaveData();
        }

        static Dictionary<string, FoundationCharacterAppearanceDefinition> BuildIndex()
        {
            var result = new Dictionary<string, FoundationCharacterAppearanceDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in s_Definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    continue;
                result[definition.id.Trim()] = definition;
            }
            return result;
        }
    }
}
