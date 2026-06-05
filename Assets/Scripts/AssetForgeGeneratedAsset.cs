using UnityEngine;

public sealed class AssetForgeGeneratedAsset : MonoBehaviour
{
    public string assetName;
    public string assetMode;
    public string productionPreset;
    public string loraName;
    public string loraCheckpoint;
    public float loraStrength;
    public int acceptedFrameCount;
    public int rejectedFrameCount;
    public int qaWarnCount;
    public int qaFailCount;
    public string manifestPath;

    public bool IsPromotionReady => rejectedFrameCount == 0 && qaFailCount == 0;
}
