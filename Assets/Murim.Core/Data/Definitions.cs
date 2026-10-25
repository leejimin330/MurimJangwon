using System.Collections.Generic;

namespace Murim.Core.Data
{
    public class StatBlock
    {
        public int Constitution;
        public int Insight;
        public int Cunning;
        public int Charisma;
    }

    public class RegionDef
    {
        public string Id;
        public string Name;
        public string[] Neighbors = new string[0];
        public float X;
        public float Y;
    }

    public class FactionDef
    {
        public string Id;
        public string Name;
        public FactionType Type;
        public Alignment Alignment;
        public string HomeRegion;
        public int Influence;
        public int NamedMembers;
        public int UnnamedDisciples;
        public int Silver;
        public int Fame;
        public int LeaderRealm;
    }

    public class TraitDef
    {
        public string Id;
        public string Name;
        public string Description;
        public TraitCategory Category;
        public string[] Opposites = new string[0];
        public StatBlock StatMods = new StatBlock();
        public int YearlyChivalry;
        public float DeathRiskMultiplier = 1f;
        public Dictionary<string, float> ActionWeights = new Dictionary<string, float>();

        public float Weight(string key) => ActionWeights != null && ActionWeights.TryGetValue(key, out var w) ? w : 0f;
    }

    public class ScenarioDef
    {
        public string Id;
        public string Name;
        public string Description;
        public bool HasFaction;
        public FactionType FactionType;
        public Alignment[] AllowedAlignments = new Alignment[0];
        public int FamilyMin;
        public int FamilyMax;
        public int NamedMembers;
        public int UnnamedDisciples;
        public int Silver;
        public int Debt;
        public int Fame;
        public int Chivalry;
        public int Realm;
        public int Influence;
        public int LeaderAgeMin = 25;
        public int LeaderAgeMax = 40;
        public bool StartWithGrudge;
        public bool StartWithRighteousFriend;
        public bool StartWithNeighborGrudge;
    }

    public class NameData
    {
        public string[] FamilyNames = new string[0];
        public string[] MaleGivenNames = new string[0];
        public string[] FemaleGivenNames = new string[0];
    }

    public class FactionNameParts
    {
        public string[] Prefixes = new string[0];
        public string[] RighteousSuffixes = new string[0];
        public string[] UnorthodoxSuffixes = new string[0];
    }
}
