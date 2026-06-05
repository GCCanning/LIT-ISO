using UnityEngine;

[CreateAssetMenu(fileName = "IsoLightingProfile", menuName = "LIT-ISO/Lighting Profile")]
public class IsoLightingProfile : ScriptableObject
{
    public string profileName = "Day";
    public Color cameraBackground = new Color(0.18f, 0.24f, 0.28f, 1f);
    public Color ambientLight = new Color(0.82f, 0.86f, 0.8f, 1f);
    public Color directionalLightColor = Color.white;
    public float directionalLightIntensity = 1f;
    public Color tilemapTint = Color.white;
    public Color spriteTint = Color.white;
}
