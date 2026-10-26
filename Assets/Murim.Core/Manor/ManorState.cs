using System.Collections.Generic;

namespace Murim.Core.Manor
{
    /// <summary>세력 본거지의 격자와 건물. 플레이어가 이끄는 세력만 가진다.</summary>
    public class ManorState
    {
        public int Width = 24;
        public int Height = 16;
        public List<BuildingInstance> Buildings = new List<BuildingInstance>();
        public int NextBuildingNumber = 1;
    }

    public class BuildingInstance
    {
        public string Id;
        public string DefId;
        /// <summary>왼쪽 위 칸.</summary>
        public int X;
        public int Y;
        /// <summary>이 월부터 완공.</summary>
        public int CompleteMonth;
    }
}
