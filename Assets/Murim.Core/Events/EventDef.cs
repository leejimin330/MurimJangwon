using System.Collections.Generic;
using Murim.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Events
{
    /// <summary>events.json의 사건 하나. 역할을 채우고, 결정자가 선택지를 고르면 효과를 실행한다.</summary>
    public class EventDef
    {
        public string Id;
        public string Name;
        public string Category;
        public float Weight = 10;
        /// <summary>플레이어에게 보낼 때 드는 긴장도. 20 이상이면 위기.</summary>
        public int Tension;
        public int CooldownMonths = 24;
        public int GlobalCooldownMonths;
        public string Decider;
        public bool FollowupOnly;
        public bool PlayerOnly;
        public bool WorldOnly;
        public int Importance = 1;
        /// <summary>1~12면 그 달에만 일어난다. 0이면 언제든.</summary>
        public int Month;
        public string Text;
        public Dictionary<string, RoleDef> Roles = new Dictionary<string, RoleDef>();
        public List<ChoiceDef> Choices = new List<ChoiceDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        /// <summary>검증기가 채운다: 뿌리 역할(Ordinal 순) → 의존 역할.</summary>
        [JsonIgnore] public List<string> BindOrder = new List<string>();

        public const int CrisisTension = 20;
        [JsonIgnore] public bool IsCrisis => Tension >= CrisisTension;
    }

    /// <summary>인물 하나에 대한 조건 묶음. 모두 만족해야 한다.</summary>
    public class Conditions
    {
        /// <summary>기본은 살아 있는 인물만. IncludeDead면 생사를 따지지 않는다.</summary>
        public bool IncludeDead;
        public int? AgeMin;
        public int? AgeMax;
        public Sex? Sex;
        public string[] TraitsAll;
        public string[] TraitsAny;
        public string[] TraitsNone;
        /// <summary>"None"은 무소속.</summary>
        public Role[] Roles;
        public Alignment[] Alignment;
        public bool? Married;
        public int? RealmMin;
        public int? RealmMax;
        public int? SilverMin;
        public int? FameMin;
        public int? ChivalryMin;
        public int? ChivalryMax;
        public int? FactionSizeMin;
        public int? RegionStabilityMin;
        public int? RegionStabilityMax;
        public string HasFlag;
        public string NotFlag;
        public MemoryCond Memory;
        public OpinionCond OpinionOf;
        public double? Chance;
        /// <summary>이 역할과 성별이 달라야 한다.</summary>
        public string OppositeSexOf;
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;
    }

    public class RoleDef : Conditions
    {
        public string Pick;
        public string Of;
        /// <summary>memoryTarget이 따라갈 기억 종류.</summary>
        public MemoryKind MemoryKind = MemoryKind.Grudge;
        public SpawnDef Spawn;
    }

    public class SpawnDef
    {
        public int AgeMin = 20;
        public int AgeMax = 45;
        public Sex? Sex;
        public int RealmMin;
        public int RealmMax;
        public int StatBonus;
        /// <summary>"산적 두목" 같은 호칭. {role.title}에 쓴다.</summary>
        public string Epithet;
    }

    public class MemoryCond
    {
        public MemoryKind Kind;
        public int MinStrength;
        /// <summary>기억 대상이 이 역할이어야 한다.</summary>
        public string Target;
        public bool? Inherited;
    }

    public class OpinionCond
    {
        public string Role;
        public int? Min;
        public int? Max;
    }

    public class ChoiceDef
    {
        public string Text;
        public string Result;
        public string Hint;
        /// <summary>역할 이름 → 조건. 만족하지 못하면 비활성.</summary>
        public Dictionary<string, Conditions> Requires;
        public float Ai;
        public Dictionary<string, float> AiTraits;
        public List<EffectDef> Effects = new List<EffectDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;
    }

    public class FlagDef
    {
        public string Id;
        public string Name;
        public string Description;
        public bool Visible;
        public int YearlyChivalry;
        public int DuelModifier;
    }

    public class StorytellerDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int TensionGain;
        public int TensionMax;
        public double BaseChance;
        public int CrisisGapMonths;
        /// <summary>플레이어가 결정자일 때 사건 쿨다운에 곱한다 (작을수록 같은 사건이 자주 온다).</summary>
        public float PlayerCooldownScale = 1f;
        public Dictionary<string, float> CategoryMultipliers = new Dictionary<string, float>();

        public float Multiplier(string category) =>
            category != null && CategoryMultipliers != null && CategoryMultipliers.TryGetValue(category, out var m) ? m : 1f;
    }

    /// <summary>events.json 최상위.</summary>
    public class EventFile
    {
        public List<FlagDef> Flags = new List<FlagDef>();
        public List<EventDef> Events = new List<EventDef>();
    }

    public static class EventCategories
    {
        public static readonly string[] All = { "trait", "grudge", "debt", "faction", "region", "demonic", "wanderer", "followup" };
    }

    public static class EventPicks
    {
        public const string AnyAdult = "anyAdult";
        public const string Leader = "leader";
        public const string GrudgeHolder = "grudgeHolder";
        public const string DebtHolder = "debtHolder";
        public const string MemoryTarget = "memoryTarget";
        public const string Spouse = "spouse";
        public const string Child = "child";
        public const string LeaderOf = "leaderOf";
        public const string SameFaction = "sameFaction";
        public const string SameRegion = "sameRegion";
        public const string RivalLeader = "rivalLeader";
        public const string DemonicNeighborLeader = "demonicNeighborLeader";
        public const string New = "new";

        public static readonly string[] Roots = { AnyAdult, Leader, GrudgeHolder, DebtHolder };
        public static readonly string[] Relative = { MemoryTarget, Spouse, Child, LeaderOf, SameFaction, SameRegion, RivalLeader, DemonicNeighborLeader };

        public static bool IsRoot(string pick) => System.Array.IndexOf(Roots, pick) >= 0;
        public static bool IsRelative(string pick) => System.Array.IndexOf(Relative, pick) >= 0;
    }
}
