using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Events
{
    /// <summary>효과 하나. JSON에서는 키 하나짜리 객체다: { "fame": { "who": "a", "amount": 3 } }.</summary>
    public class EffectDef
    {
        public OpinionFx Opinion;
        public MemoryFx Memory;
        public MemoryRefFx WeakenMemory;
        public MemoryRefFx RemoveMemory;
        public WhoAmountFx Silver;
        public WhoAmountFx Fame;
        public WhoAmountFx Chivalry;
        public FactionAmountFx Influence;
        public RegionAmountFx Stability;
        public FactionAmountFx Unnamed;
        public StatFx Stat;
        public KillFx Kill;
        public FlagFx Flag;
        public FlagFx ClearFlag;
        public RelationFx Relation;
        public SetAlignmentFx SetAlignment;
        public RecruitFx Recruit;
        public WhoFx Leave;
        public WhoFx Move;
        public WhoFx Usurp;
        public PairAmountFx AllianceScore;
        public DuelFx Duel;
        public ContestFx Contest;
        public ChanceFx Chance;
        public ScheduleFx Schedule;
        public ChronicleFx Chronicle;
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        IEnumerable<(string name, FxArgs args)> All()
        {
            yield return ("opinion", Opinion);
            yield return ("memory", Memory);
            yield return ("weakenMemory", WeakenMemory);
            yield return ("removeMemory", RemoveMemory);
            yield return ("silver", Silver);
            yield return ("fame", Fame);
            yield return ("chivalry", Chivalry);
            yield return ("influence", Influence);
            yield return ("stability", Stability);
            yield return ("unnamed", Unnamed);
            yield return ("stat", Stat);
            yield return ("kill", Kill);
            yield return ("flag", Flag);
            yield return ("clearFlag", ClearFlag);
            yield return ("relation", Relation);
            yield return ("setAlignment", SetAlignment);
            yield return ("recruit", Recruit);
            yield return ("leave", Leave);
            yield return ("move", Move);
            yield return ("usurp", Usurp);
            yield return ("allianceScore", AllianceScore);
            yield return ("duel", Duel);
            yield return ("contest", Contest);
            yield return ("chance", Chance);
            yield return ("schedule", Schedule);
            yield return ("chronicle", Chronicle);
        }

        /// <summary>값이 있는 효과들 (정상이면 정확히 하나).</summary>
        [JsonIgnore] public List<(string name, FxArgs args)> Present => All().Where(x => x.args != null).ToList();
        [JsonIgnore] public string Kind => Present.Count == 1 ? Present[0].name : null;
        [JsonIgnore] public FxArgs Args => Present.Count == 1 ? Present[0].args : null;
    }

    public abstract class FxArgs
    {
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        /// <summary>이 효과가 가리키는 역할 이름들 (검증용).</summary>
        public abstract IEnumerable<string> RoleRefs();

        /// <summary>안에 든 하위 효과 목록 (검증용).</summary>
        public virtual IEnumerable<List<EffectDef>> Branches() { yield break; }
    }

    public class WhoFx : FxArgs
    {
        public string Who;
        public override IEnumerable<string> RoleRefs() { yield return Who; }
    }

    public class WhoAmountFx : WhoFx
    {
        public int Amount;
    }

    public class OpinionFx : FxArgs
    {
        public string Who;
        public string Of;
        public int Amount;
        public override IEnumerable<string> RoleRefs() { yield return Who; yield return Of; }
    }

    public class MemoryFx : FxArgs
    {
        public string Holder;
        public MemoryKind Kind;
        public string Target;
        public string Reason;
        public int Strength;
        public override IEnumerable<string> RoleRefs() { yield return Holder; yield return Target; }
    }

    public class MemoryRefFx : FxArgs
    {
        public string Holder;
        public string Target;
        public MemoryKind Kind = MemoryKind.Grudge;
        public int Amount;
        public override IEnumerable<string> RoleRefs() { yield return Holder; yield return Target; }
    }

    public class FactionAmountFx : FxArgs
    {
        /// <summary>역할 이름. 그 인물의 세력을 가리킨다.</summary>
        public string Faction;
        public int Amount;
        public override IEnumerable<string> RoleRefs() { yield return Faction; }
    }

    public class RegionAmountFx : FxArgs
    {
        /// <summary>역할 이름. 그 인물이 있는 지역을 가리킨다.</summary>
        public string Region;
        public int Amount;
        public override IEnumerable<string> RoleRefs() { yield return Region; }
    }

    public class StatFx : WhoFx
    {
        /// <summary>constitution / insight / cunning / charisma.</summary>
        public string Stat;
        public int Amount;
    }

    public class KillFx : WhoFx
    {
        public string Cause;
    }

    public class FlagFx : WhoFx
    {
        public string Id;
        public int Months;
    }

    public class RelationFx : FxArgs
    {
        public string A;
        public string B;
        public RelationTag Tag;
        public override IEnumerable<string> RoleRefs() { yield return A; yield return B; }
    }

    public class SetAlignmentFx : FxArgs
    {
        public string Faction;
        public Alignment Alignment;
        public override IEnumerable<string> RoleRefs() { yield return Faction; }
    }

    public class RecruitFx : WhoFx
    {
        /// <summary>역할 이름. 그 인물의 세력으로 들인다.</summary>
        public string Faction;
        public Role Role = Data.Role.Disciple;
        public override IEnumerable<string> RoleRefs() { yield return Who; yield return Faction; }
    }

    public class PairAmountFx : FxArgs
    {
        public string A;
        public string B;
        public int Amount;
        public override IEnumerable<string> RoleRefs() { yield return A; yield return B; }
    }

    public class DuelFx : FxArgs
    {
        public string A;
        public string B;
        public double DeathChance;
        /// <summary>A가 이겼을 때.</summary>
        public List<EffectDef> Win = new List<EffectDef>();
        public List<EffectDef> Lose = new List<EffectDef>();
        public override IEnumerable<string> RoleRefs() { yield return A; yield return B; }
        public override IEnumerable<List<EffectDef>> Branches() { yield return Win; yield return Lose; }
    }

    public class ContestFx : WhoFx
    {
        public string Stat;
        /// <summary>고정 난이도. AgainstRole이 있으면 무시한다.</summary>
        public int Against = 20;
        public string AgainstRole;
        public string AgainstStat;
        public List<EffectDef> Win = new List<EffectDef>();
        public List<EffectDef> Lose = new List<EffectDef>();

        public override IEnumerable<string> RoleRefs()
        {
            yield return Who;
            if (AgainstRole != null) yield return AgainstRole;
        }

        public override IEnumerable<List<EffectDef>> Branches() { yield return Win; yield return Lose; }
    }

    public class ChanceFx : FxArgs
    {
        public double P;
        public List<EffectDef> Then = new List<EffectDef>();
        public List<EffectDef> Else = new List<EffectDef>();
        public override IEnumerable<string> RoleRefs() { yield break; }
        public override IEnumerable<List<EffectDef>> Branches() { yield return Then; yield return Else; }
    }

    public class ScheduleFx : FxArgs
    {
        public string Event;
        public int[] AfterMonths = { 6, 12 };
        /// <summary>후속 사건의 역할 이름 → 이 사건의 역할 이름.</summary>
        public Dictionary<string, string> Roles = new Dictionary<string, string>();
        public override IEnumerable<string> RoleRefs() => Roles.Values;
    }

    public class ChronicleFx : FxArgs
    {
        public string Text;
        public override IEnumerable<string> RoleRefs() => EventText.RoleRefs(Text);
    }

    public static class Stats
    {
        public static readonly string[] Names = { "constitution", "insight", "cunning", "charisma" };
        public static readonly string[] Korean = { "근골", "오성", "심계", "매력" };

        public static int Get(Character c, string stat)
        {
            switch (stat)
            {
                case "constitution": return c.Constitution;
                case "insight": return c.Insight;
                case "cunning": return c.Cunning;
                case "charisma": return c.Charisma;
                default: return 0;
            }
        }

        public static void Set(Character c, string stat, int v)
        {
            switch (stat)
            {
                case "constitution": c.Constitution = v; break;
                case "insight": c.Insight = v; break;
                case "cunning": c.Cunning = v; break;
                case "charisma": c.Charisma = v; break;
            }
        }
    }
}
