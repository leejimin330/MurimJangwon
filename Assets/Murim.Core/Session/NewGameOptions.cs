using System.Collections.Generic;
using Murim.Core.Data;

namespace Murim.Core.Session
{
    public class NewGameOptions
    {
        public string ScenarioId;
        public string RegionId;
        public ulong Seed;
        public Sex Sex = Sex.Male;
        /// <summary>null이면 무작위.</summary>
        public string FamilyName;
        public string GivenName;
        public string FactionName;
        /// <summary>시나리오 허용 목록에 없으면 첫 번째 허용 성향을 쓴다.</summary>
        public Alignment Alignment = Alignment.Righteous;
        /// <summary>최대 2개까지 적용된다.</summary>
        public List<string> ChosenTraitIds = new List<string>();
    }
}
