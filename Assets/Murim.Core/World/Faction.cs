using System.Collections.Generic;
using Murim.Core.Data;

namespace Murim.Core.World
{
    public class Faction
    {
        public string Id;
        public string Name;
        public FactionType Type;
        public Alignment Alignment;
        /// <summary>협의 점수 -100 ~ +100.</summary>
        public int Chivalry;
        public string HomeRegionId;
        public string LeaderId;
        public string HeirId;
        public List<string> MemberIds = new List<string>();
        public int UnnamedDisciples;
        public int Silver;
        public int Fame;
        public int Debt;
        public bool IsDissolved;
        public bool IsRandomMinor;
        /// <summary>AI가 유지하려는 이름 있는 인물 수.</summary>
        public int TargetNamedMembers;
    }
}
