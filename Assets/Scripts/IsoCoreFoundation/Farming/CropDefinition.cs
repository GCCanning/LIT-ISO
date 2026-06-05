using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A plantable crop: grows through stages over time, then is harvestable.</summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Crop", fileName = "Crop")]
    public class CropDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color youngColor = new Color(0.45f, 0.70f, 0.35f);
        public Color ripeColor = new Color(0.85f, 0.75f, 0.25f);
        public float matureHeightUnits = 0.9f;

        [Header("Growth")]
        public int stages = 3;            // visual/growth stages (mature at the last)
        public float secondsPerStage = 8f;

        [Header("Harvest yield")]
        public ItemDrop[] harvest;        // produce (+ optional seed returns)
    }

    public class CropDatabase : Database<CropDefinition> { }
}
