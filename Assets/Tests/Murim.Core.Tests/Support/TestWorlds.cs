using System.Collections.Generic;
using Murim.Core.Data;
using Murim.Core.World;

namespace Murim.Core.Tests
{
    /// <summary>규칙 테스트용 작은 세계. 지역 a↔b↔c, 특성 bold↔sly, ambitious, sickly.</summary>
    public static class TestWorlds
    {
        public static GameData MinimalData()
        {
            var d = new GameData();
            d.Regions["a"] = new RegionDef { Id = "a", Name = "갑", Neighbors = new[] { "b" } };
            d.Regions["b"] = new RegionDef { Id = "b", Name = "을", Neighbors = new[] { "a", "c" } };
            d.Regions["c"] = new RegionDef { Id = "c", Name = "병", Neighbors = new[] { "b" } };
            d.Traits["bold"] = new TraitDef { Id = "bold", Name = "호탕", Opposites = new[] { "sly" } };
            d.Traits["sly"] = new TraitDef { Id = "sly", Name = "음험", Opposites = new[] { "bold" } };
            d.Traits["ambitious"] = new TraitDef { Id = "ambitious", Name = "야심" };
            d.Traits["sickly"] = new TraitDef { Id = "sickly", Name = "병약", DeathRiskMultiplier = 2f };
            d.Names = new NameData
            {
                FamilyNames = new[] { "장", "이", "왕" },
                MaleGivenNames = new[] { "삼", "사", "오" },
                FemaleGivenNames = new[] { "설", "화", "월" },
            };
            d.FactionNameParts = new FactionNameParts
            {
                Prefixes = new[] { "청운", "흑풍", "철혈" },
                RighteousSuffixes = new[] { "검문", "파" },
                UnorthodoxSuffixes = new[] { "채", "방" },
            };
            return d;
        }

        public static WorldState Empty(GameData d)
        {
            var w = new WorldState { Month = 12 * 100 };
            foreach (var r in d.Regions.Values) w.Regions[r.Id] = new RegionState { Id = r.Id, Stability = 60 };
            return w;
        }

        public static Faction AddFaction(WorldState w, string id, Alignment alignment, string region, int influence,
            FactionType type = FactionType.Sect)
        {
            var f = new Faction
            {
                Id = id, Name = id + "파", Type = type, Alignment = alignment, HomeRegionId = region,
                Chivalry = alignment == Alignment.Righteous ? 50 : -50, Silver = 1000, Fame = 20,
            };
            w.Factions[id] = f;
            if (influence > 0) w.Regions[region].Influence[id] = influence;
            return f;
        }

        static int counter;

        public static Character AddCharacter(WorldState w, string factionId, Role role, Sex sex, int age, params string[] traits)
        {
            var c = new Character
            {
                Id = w.NewCharacterId(), FamilyName = "장", GivenName = "인물" + (++counter), Sex = sex,
                BirthMonth = w.Month - age * 12, FactionId = factionId, Role = role,
                Constitution = 10, Insight = 10, Cunning = 10, Charisma = 10,
                TraitIds = new List<string>(traits),
            };
            w.Characters[c.Id] = c;
            if (factionId != null)
            {
                var f = w.Factions[factionId];
                f.MemberIds.Add(c.Id);
                c.LocationRegionId = f.HomeRegionId;
                if (role == Role.Leader) f.LeaderId = c.Id;
                if (role == Role.Heir) f.HeirId = c.Id;
            }
            else c.LocationRegionId = "a";
            return c;
        }
    }
}
