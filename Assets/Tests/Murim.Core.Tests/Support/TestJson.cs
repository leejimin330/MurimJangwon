using System.Collections.Generic;

namespace Murim.Core.Tests
{
    /// <summary>로더·검증기 테스트용 최소 JSON 데이터 묶음.</summary>
    public static class TestJson
    {
        public static Dictionary<string, string> MinimalFiles() => new Dictionary<string, string>
        {
            ["regions.json"] = @"[
                {""id"":""hebei"",""name"":""하북"",""neighbors"":[""shandong""],""x"":0.6,""y"":0.3},
                {""id"":""shandong"",""name"":""산동"",""neighbors"":[""hebei""],""x"":0.7,""y"":0.4}]",
            ["factions.json"] = @"[
                {""id"":""shaolin"",""name"":""소림사"",""type"":""Sect"",""alignment"":""Righteous"",""homeRegion"":""hebei"",
                 ""influence"":30,""namedMembers"":10,""unnamedDisciples"":100,""silver"":2000,""fame"":80,""leaderRealm"":5}]",
            ["traits.json"] = @"[
                {""id"":""bold"",""name"":""호탕"",""description"":""d"",""category"":""Personality"",""opposites"":[""sly""],
                 ""statMods"":{""charisma"":2},""yearlyChivalry"":0,""actionWeights"":{""accept_marriage"":5}},
                {""id"":""sly"",""name"":""음험"",""description"":""d"",""category"":""Personality"",""opposites"":[""bold""],
                 ""statMods"":{""cunning"":2},""yearlyChivalry"":0,""actionWeights"":{}}]",
            ["scenarios.json"] = @"[
                {""id"":""minor_sect"",""name"":""약소문파 장문인"",""description"":""d"",""hasFaction"":true,""factionType"":""Sect"",
                 ""allowedAlignments"":[""Righteous"",""Unorthodox""],""familyMin"":1,""familyMax"":3,""namedMembers"":4,
                 ""unnamedDisciples"":10,""silver"":500,""fame"":10,""influence"":5,""realm"":1,""leaderAgeMin"":30,""leaderAgeMax"":45}]",
            ["names.json"] = @"{""familyNames"":[""장"",""남궁""],""maleGivenNames"":[""삼""],""femaleGivenNames"":[""설""]}",
            ["faction_name_parts.json"] = @"{""prefixes"":[""청운""],""righteousSuffixes"":[""검문""],""unorthodoxSuffixes"":[""채""]}",
        };
    }
}
