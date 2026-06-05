using UnityEngine;

/// <summary>
/// Ensures there is always one AudioListener in the scene.
/// Automatically added to the Main Camera if one doesn't exist.
///
/// This prevents the "There are no audio listeners in the scene" warning.
/// </summary>
public class AudioListenerEnsurer : MonoBehaviour
{
    private void Awake()
    {
        // Check if an AudioListener already exists
        AudioListener existingListener = FindFirstObjectByType<AudioListener>();

        if (existingListener == null)
        {
            // No listener found — add one to this camera
            if (GetComponent<Camera>() != null)
            {
                gameObject.AddComponent<AudioListener>();
                Debug.Log("[AudioListenerEnsurer] Added missing AudioListener to " + gameObject.name);
            }
            else if (gameObject.CompareTag("MainCamera"))
            {
                // This is the main camera, add listener
                gameObject.AddComponent<AudioListener>();
                Debug.Log("[AudioListenerEnsurer] Added missing AudioListener to Main Camera");
            }
        }
    }
}
