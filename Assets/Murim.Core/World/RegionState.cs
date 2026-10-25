using System;
using System.Collections.Generic;

namespace Murim.Core.World
{
    public class RegionState
    {
        public string Id;
        /// <summary>치안·민심 0~100.</summary>
        public int Stability;
        /// <summary>세력 id → 영향력. 합은 100을 넘지 않는다.</summary>
        public SortedDictionary<string, int> Influence = new SortedDictionary<string, int>(StringComparer.Ordinal);
    }
}
