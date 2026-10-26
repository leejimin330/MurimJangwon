using System.Collections.Generic;

namespace Murim.Core.Tests
{
    /// <summary>로더·검증기 테스트용 최소 JSON 데이터 묶음.</summary>
    public static class TestJson
    {
        /// <summary>결투 문구 13개 키를 한 줄씩 채운 최소 묶음.</summary>
        public const string MinimalNarration = @"{""open"":[""{a:와} {b:이} 마주 섰다.""],""hit"":[""{card:이} {b:을} 맞혔다.""],
            ""graze"":[""{card:이} {b:을} 스쳤다.""],""block"":[""{b:은} {guard:으로} 막았다.""],""evade"":[""{b:은} {card:을} 피했다.""],
            ""clash"":[""초식이 부딪쳤다.""],""probe"":[""거리를 쟀다.""],""counter"":[""{card:이} {guard:을} 깨뜨렸다.""],
            ""finisher"":[""{a:이} 절초 {card:을} 펼쳤다!""],""exhausted"":[""{a:은} 내력이 다했다.""],""win"":[""{winner:이} 이겼다.""],
            ""death"":[""{loser:은} 숨을 거두었다.""],""timeout"":[""판정은 {winner}의 우세였다.""]}";

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
            ["events.json"] = @"{""flags"":[{""id"":""injured"",""name"":""부상"",""description"":""d"",""visible"":true,""duelModifier"":-15}],
                ""events"":[{""id"":""quarrel"",""name"":""시비"",""category"":""wanderer"",""decider"":""hero"",
                 ""roles"":{""hero"":{""pick"":""anyAdult""},""rival"":{""pick"":""sameRegion"",""of"":""hero""}},
                 ""text"":""{hero:이} {rival}의 시비에 휘말렸다."",
                 ""choices"":[{""text"":""겨룬다"",""effects"":[{""fame"":{""who"":""hero"",""amount"":1}}]}]}]}",
            ["storytellers.json"] = @"[{""id"":""balanced"",""name"":""균형"",""description"":""d"",""tensionGain"":4,""tensionMax"":100,
                ""baseChance"":0.35,""crisisGapMonths"":10}]",
        };
    }
}
