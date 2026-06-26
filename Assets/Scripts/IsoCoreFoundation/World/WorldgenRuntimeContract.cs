using System;
using System.IO;
using UnityEngine;

namespace IsoCore.Foundation
{
#pragma warning disable 0649 // JsonUtility populates these DTO fields by reflection.
    /// <summary>
    /// Typed bridge for the authored StreamingAssets worldgen datapack contract.
    /// The sampler does not consume this yet; this class makes the contract loadable
    /// and validator-covered before terrain behavior changes.
    /// </summary>
    [Serializable]
    public class WorldgenRuntimeContract
    {
        public const string SchemaId = "litiso.worldgen.runtime_contract.v1";

        public string schema;
        public WorldgenContractRules rules;
        public WorldgenTileEntry[] tiles;
        public WorldgenFeatureEntry[] features;
        public WorldgenVariantGroupEntry[] variantGroups;
        public WorldgenBlendPairEntry[] blendPairs;

        public bool IsSchemaValid => schema == SchemaId;

        public static string DefaultProjectPath =>
            Path.Combine("Assets", "StreamingAssets", "worldgen", "runtime_contract.json");

        public static string DefaultStreamingAssetsPath =>
            Path.Combine(Application.streamingAssetsPath, "worldgen", "runtime_contract.json");

        public static bool TryLoadProjectContract(out WorldgenRuntimeContract contract, out string error)
        {
            return TryLoad(DefaultProjectPath, out contract, out error);
        }

        public static bool TryLoadStreamingAssetsContract(out WorldgenRuntimeContract contract, out string error)
        {
            return TryLoad(DefaultStreamingAssetsPath, out contract, out error);
        }

        public static bool TryLoadDefaultContract(out WorldgenRuntimeContract contract, out string error)
        {
            if (TryLoadStreamingAssetsContract(out contract, out error))
                return true;

            string streamingError = error;
            if (TryLoadProjectContract(out contract, out error))
                return true;

            error = $"{streamingError}; {error}";
            return false;
        }

        public static bool TryLoad(string path, out WorldgenRuntimeContract contract, out string error)
        {
            contract = null;
            error = "";

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Path is empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"File does not exist: {path}";
                return false;
            }

            try
            {
                contract = JsonUtility.FromJson<WorldgenRuntimeContract>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (contract == null)
            {
                error = "JsonUtility returned null contract.";
                return false;
            }

            if (!contract.IsSchemaValid)
            {
                error = $"Unexpected schema: {contract.schema}";
                return false;
            }

            return true;
        }
    }

    [Serializable]
    public class WorldgenContractRules
    {
        public string[] materialTagsAreNotTileIds;
    }

    [Serializable]
    public class WorldgenTileEntry
    {
        public string tileId;
        public string status;
        public WorldgenPromotion promotion;
    }

    [Serializable]
    public class WorldgenFeatureEntry
    {
        public string featureId;
        public string role;
        public string visualId;
        public string gameplayPrototype;
        public string archetype;
        public string footprint;
        public float dropMultiplier;
        public WorldgenPromotion promotion;
    }

    [Serializable]
    public class WorldgenPromotion
    {
        public string status;
        public string source;
        public string destination;
        public string aliasOf;
    }

    [Serializable]
    public class WorldgenVariantGroupEntry
    {
        public string sourceTileId;
        public string sourceGroup;
        public string group;
        public string variantKind;
        public string mode;
        public string status;
        public WorldgenVariantEntry[] variants;
    }

    [Serializable]
    public class WorldgenVariantEntry
    {
        public string tileId;
        public string path;
        public int index;
        public string seed;
        public string status;
    }

    [Serializable]
    public class WorldgenBlendPairEntry
    {
        public string fromTileId;
        public string toTileId;
        public string pattern;
        public float softness;
        public string status;
        public WorldgenBlendVariantEntry[] variants;
    }

    [Serializable]
    public class WorldgenBlendVariantEntry
    {
        public string tileId;
        public string path;
        public string direction;
        public int index;
        public string seed;
        public string status;
    }
#pragma warning restore 0649
}
