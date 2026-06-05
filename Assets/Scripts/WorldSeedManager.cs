using System;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Manages world generation seeds for deterministic procedural generation.
    /// Provides seeded RNG for reproducible worlds (like Minecraft seed format).
    /// </summary>
    public class WorldSeedManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private long currentSeed = 0;

        private System.Random seededRandom;

        // ============= SINGLETON =============
        private static WorldSeedManager instance;
        public static WorldSeedManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<WorldSeedManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("WorldSeedManager");
                        instance = go.AddComponent<WorldSeedManager>();
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ============= SEED MANAGEMENT =============

        /// <summary>
        /// Generate a new random seed.
        /// </summary>
        public long GenerateNewSeed()
        {
            currentSeed = (long)(UnityEngine.Random.value * long.MaxValue);
            seededRandom = new System.Random((int)(currentSeed % int.MaxValue));
            Debug.Log($"[WorldSeedManager] Generated new seed: {currentSeed}");
            return currentSeed;
        }

        /// <summary>
        /// Set a specific seed for deterministic world generation.
        /// </summary>
        public void SetSeed(long seed)
        {
            currentSeed = seed;
            seededRandom = new System.Random((int)(seed % int.MaxValue));
            Debug.Log($"[WorldSeedManager] Set seed to: {currentSeed}");
        }

        /// <summary>
        /// Get the current seed.
        /// </summary>
        public long GetCurrentSeed()
        {
            return currentSeed;
        }

        /// <summary>
        /// Get a System.Random instance seeded with the current seed.
        /// Used for reproducible procedural generation.
        /// </summary>
        public System.Random GetSeededRandom()
        {
            if (seededRandom == null)
            {
                seededRandom = new System.Random((int)(currentSeed % int.MaxValue));
            }
            return seededRandom;
        }

        // ============= NOISE GENERATION =============

        /// <summary>
        /// Get deterministic noise value for grid-based biome assignment.
        /// </summary>
        public float GetDeterministicNoise(int gridX, int gridY)
        {
            // Hash grid coordinates to get noise
            long hash = currentSeed;
            hash = hash * 73856093 ^ gridX;
            hash = hash * 19349663 ^ gridY;
            hash = hash * 83492791 ^ (hash >> 13);

            System.Random r = new System.Random((int)(hash % int.MaxValue));
            return (float)r.NextDouble();
        }

        /// <summary>
        /// Encode seed to a player-friendly format (numeric string).
        /// </summary>
        public string EncodeSeedToCode(long seed)
        {
            return seed.ToString("X16");  // Hex format, 16 characters
        }

        /// <summary>
        /// Decode seed from player-friendly format.
        /// </summary>
        public long DecodeSeedFromCode(string seedCode)
        {
            if (long.TryParse(seedCode, System.Globalization.NumberStyles.HexNumber, null, out long seed))
            {
                return seed;
            }
            return GenerateNewSeed();  // Fallback to new seed if decode fails
        }
    }
}
