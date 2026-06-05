using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A palette of interchangeable visual variants for one surface
    /// (e.g. GrassBlocks -&gt; Grass1..3). Mid-tier of the Biome -&gt; Group -&gt; Block
    /// hierarchy (ISO-CORE three-tier model, Reference Study §4).
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Block Group", fileName = "BlockGroup")]
    public class BlockGroupDefinition : FoundationDefinition
    {
        public List<BlockDefinition> variants = new();

        /// <summary>Deterministic variant pick from a per-cell hash.</summary>
        public BlockDefinition GetVariant(int hash)
        {
            if (variants == null || variants.Count == 0) return null;
            int i = (hash & 0x7fffffff) % variants.Count;
            return variants[i];
        }
    }
}
