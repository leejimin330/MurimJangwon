using System;
using System.Collections.Generic;

namespace Murim.Core.Data
{
    /// <summary>게임 시작 시 읽는 정적 데이터. 저장 파일에는 들어가지 않는다.</summary>
    public class GameData
    {
        public SortedDictionary<string, RegionDef> Regions = new SortedDictionary<string, RegionDef>(StringComparer.Ordinal);
        public SortedDictionary<string, FactionDef> Factions = new SortedDictionary<string, FactionDef>(StringComparer.Ordinal);
        public SortedDictionary<string, TraitDef> Traits = new SortedDictionary<string, TraitDef>(StringComparer.Ordinal);
        public SortedDictionary<string, ScenarioDef> Scenarios = new SortedDictionary<string, ScenarioDef>(StringComparer.Ordinal);
        public NameData Names = new NameData();
        public FactionNameParts FactionNameParts = new FactionNameParts();
    }

    public class DataLoadResult
    {
        public GameData Data;
        public List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }
}
