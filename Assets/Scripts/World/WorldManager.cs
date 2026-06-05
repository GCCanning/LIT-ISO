using UnityEngine;

/// <summary>
/// Singleton that holds the current world configuration (name, seed, difficulty) and persists
/// across scene loads. Initialized by WelcomeScreenManager, read by IsoWorldChunkManager.
///
/// Usage:
///   WorldManager.Instance.SetWorld(worldName, seed, difficulty);  // Called by WelcomeScreenManager
///   int difficulty = WorldManager.Instance.Difficulty;  // Read by IsoWorldChunkManager
///   string seed = WorldManager.Instance.Seed;
/// </summary>
public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    public string WorldName { get; private set; } = "Untitled World";
    public string Seed { get; private set; } = "12345";
    public int Difficulty { get; private set; } = 1;  // 0=easy, 1=normal, 2=hard

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Set the current world configuration. Called by WelcomeScreenManager before loading the game scene.
    /// </summary>
    public void SetWorld(string worldName, string seed, int difficulty)
    {
        WorldName = worldName ?? "Untitled World";
        Seed = seed ?? Random.Range(0, 999999).ToString();
        Difficulty = Mathf.Clamp(difficulty, 0, 2);

        Debug.Log($"World configured: {WorldName} (Seed: {Seed}, Difficulty: {Difficulty})");
    }

    /// <summary>
    /// Reset to defaults (for testing or new game).
    /// </summary>
    public void ResetToDefaults()
    {
        WorldName = "Untitled World";
        Seed = Random.Range(0, 999999).ToString();
        Difficulty = 1;
    }
}
