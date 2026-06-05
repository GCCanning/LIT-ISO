using UnityEngine;

namespace IsoCore.Foundation
{
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Mob", fileName = "Mob")]
    public class MobDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = new Color(0.4f, 0.8f, 0.4f);
        public float sizeUnits = 0.55f;

        [Header("Behaviour")]
        public MobBehavior behaviour = MobBehavior.Passive;
        public float moveSpeed = 1.2f;
        public float wanderRadius = 4f;
        public float repathSeconds = 2.5f;

        [Header("Drops")]
        public ItemDrop[] drops;
    }

    public class MobDatabase : Database<MobDefinition> { }
}
