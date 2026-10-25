using System;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public static class PersonalRules
    {
        /// <summary>세력 수장이 아니면 개인 행동을 쓴다 (무소속, 객경, 장로, 제자 등).</summary>
        public static bool UsesPersonalActions(WorldState w, Character c) => Wallet.LedFaction(w, c) == null;

        /// <summary>0보다 크면 a 승리.</summary>
        public static int SparScore(Character a, Character b, int roll) =>
            (a.Realm - b.Realm) * 25 + (a.Constitution + a.Insight - b.Constitution - b.Insight) + roll;

        public static bool IsNeighbor(GameData d, string from, string to) =>
            from != null && d.Regions.TryGetValue(from, out var r) && r.Neighbors.Contains(to);

        /// <summary>같은 지역에 있는 살아 있는 성인 (자기 자신 제외).</summary>
        public static bool IsNearbyAdult(WorldState w, Character actor, Character target) =>
            target != null && target.IsAlive && target.Id != actor.Id && target.LocationRegionId == actor.LocationRegionId
            && target.AgeYears(w.Month) >= SimConstants.AdultAge;
    }

    public abstract class PersonalAction : IGameAction
    {
        public string ActorId { get; }
        public abstract string Key { get; }

        protected PersonalAction(string actorId) => ActorId = actorId;

        public ActionCheck CanExecute(WorldState w, GameData d)
        {
            var actor = w.Get(ActorId);
            if (actor == null || !actor.IsAlive) return ActionCheck.Fail("행동할 수 없는 인물입니다");
            if (!PersonalRules.UsesPersonalActions(w, actor)) return ActionCheck.Fail("세력의 수장은 세력 행동을 씁니다");
            return Check(w, d, actor);
        }

        public void Execute(WorldState w, GameData d, GameRandom rng) => Run(w, d, rng, w.Get(ActorId));

        public abstract string Describe(WorldState w);
        protected abstract ActionCheck Check(WorldState w, GameData d, Character actor);
        protected abstract void Run(WorldState w, GameData d, GameRandom rng, Character actor);
    }

    public class Travel : PersonalAction, IWholeMonthAction
    {
        public readonly string ToRegionId;

        public Travel(string actorId, string toRegionId) : base(actorId) => ToRegionId = toRegionId;

        public override string Key => ActionKeys.Travel;
        public override string Describe(WorldState w) => $"{ToRegionId}(으)로 이동";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor) =>
            PersonalRules.IsNeighbor(d, actor.LocationRegionId, ToRegionId) ? ActionCheck.Pass : ActionCheck.Fail("인접한 지역이 아닙니다");

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor) => actor.LocationRegionId = ToRegionId;
    }

    public class Escort : PersonalAction, IWholeMonthAction
    {
        public const int Pay = 80;
        public readonly string ToRegionId;

        public Escort(string actorId, string toRegionId) : base(actorId) => ToRegionId = toRegionId;

        public override string Key => ActionKeys.Escort;
        public override string Describe(WorldState w) => $"{ToRegionId}까지 표국 호위";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor) =>
            PersonalRules.IsNeighbor(d, actor.LocationRegionId, ToRegionId) ? ActionCheck.Pass : ActionCheck.Fail("인접한 지역이 아닙니다");

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            actor.Silver += Pay;
            actor.LocationRegionId = ToRegionId;
            actor.BusyUntilMonth = w.Month + 2;
        }
    }

    public class PersonalQuest : PersonalAction
    {
        /// <summary>true면 청부(사파 의뢰), false면 협행.</summary>
        public readonly bool Contract;

        public PersonalQuest(string actorId, bool contract) : base(actorId) => Contract = contract;

        public override string Key => Contract ? ActionKeys.QuestContract : ActionKeys.QuestChivalrous;
        public override string Describe(WorldState w) => Contract ? "청부" : "협행";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor) => ActionCheck.Pass;

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            int silver = rng.Next(20, 61);
            if (Contract)
            {
                actor.Silver += silver * 3 / 2;
                actor.Chivalry = Wallet.ClampChivalry(actor.Chivalry - 4);
            }
            else
            {
                actor.Silver += silver;
                actor.Chivalry = Wallet.ClampChivalry(actor.Chivalry + 3);
                actor.Fame += 1;
            }
        }
    }

    public class Spar : PersonalAction
    {
        public readonly string TargetId;

        public Spar(string actorId, string targetId) : base(actorId) => TargetId = targetId;

        public override string Key => ActionKeys.Spar;
        public override string Describe(WorldState w) => $"{w.Get(TargetId)?.FullName}에게 비무 신청";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor) =>
            PersonalRules.IsNearbyAdult(w, actor, w.Get(TargetId)) ? ActionCheck.Pass : ActionCheck.Fail("같은 지역의 상대가 아닙니다");

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            var target = w.Get(TargetId);
            bool win = PersonalRules.SparScore(actor, target, rng.Next(-20, 21)) > 0;
            if (win)
            {
                actor.Fame += rng.Next(2, 6);
                w.Opinions.Add(target.Id, actor.Id, 10);
            }
            else
            {
                actor.Fame = Math.Max(0, actor.Fame - 1);
                w.Opinions.Add(target.Id, actor.Id, -10);
            }
            Chronicle.Sparred(w, actor, target, win);
        }
    }

    public class Befriend : PersonalAction
    {
        public const int Cost = 10;
        public readonly string TargetId;

        public Befriend(string actorId, string targetId) : base(actorId) => TargetId = targetId;

        public override string Key => ActionKeys.Befriend;
        public override string Describe(WorldState w) => $"{w.Get(TargetId)?.FullName}과(와) 교분 쌓기";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor)
        {
            if (!PersonalRules.IsNearbyAdult(w, actor, w.Get(TargetId))) return ActionCheck.Fail("같은 지역의 상대가 아닙니다");
            return actor.Silver >= Cost ? ActionCheck.Pass : ActionCheck.Fail("은자가 부족합니다");
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            actor.Silver -= Cost;
            w.Opinions.Add(TargetId, actor.Id, Math.Min(20, 10 + actor.Charisma / 2));
        }
    }

    public class JoinAsGuest : PersonalAction
    {
        public readonly string FactionId;

        public JoinAsGuest(string actorId, string factionId) : base(actorId) => FactionId = factionId;

        public override string Key => ActionKeys.JoinAsGuest;
        public override string Describe(WorldState w) => $"{w.GetFaction(FactionId)?.Name}에 객경으로 의탁";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor)
        {
            if (actor.FactionId != null) return ActionCheck.Fail("이미 세력에 속해 있습니다");
            var f = w.GetFaction(FactionId);
            if (f == null || f.IsDissolved || FactionOps.Leader(w, f) == null) return ActionCheck.Fail("존재하지 않는 세력입니다");
            return f.HomeRegionId == actor.LocationRegionId ? ActionCheck.Pass : ActionCheck.Fail("그 세력의 본거지에 있어야 합니다");
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            var f = w.GetFaction(FactionId);
            var leader = FactionOps.Leader(w, f);
            bool accepted = Opinion.Of(w, d, leader.Id, actor.Id) >= 20 || actor.Fame >= 20;
            if (accepted) FactionOps.MoveToFaction(w, actor, f.Id, Role.Guest);
            Chronicle.GuestRequest(w, actor, f, accepted);
        }
    }

    public class FoundSect : PersonalAction
    {
        public const int Cost = 1000;
        public const int FameRequired = 30;
        public const int FollowerOpinion = 40;
        public const int FollowersRequired = 2;
        public const int StartInfluence = 3;
        public readonly string Name;

        public FoundSect(string actorId, string name) : base(actorId) => Name = name?.Trim();

        public override string Key => ActionKeys.FoundSect;
        public override string Describe(WorldState w) => $"{Name} 창설";

        public static System.Collections.Generic.List<Character> Followers(WorldState w, GameData d, Character actor) =>
            w.Characters.Values.Where(c => c.IsAlive && c.FactionId == null && c.Id != actor.Id
                && c.LocationRegionId == actor.LocationRegionId && c.AgeYears(w.Month) >= SimConstants.AdultAge
                && Opinion.Of(w, d, c.Id, actor.Id) >= FollowerOpinion).ToList();

        protected override ActionCheck Check(WorldState w, GameData d, Character actor)
        {
            if (actor.FactionId != null) return ActionCheck.Fail("세력을 떠나야 문파를 세울 수 있습니다");
            if (string.IsNullOrEmpty(Name)) return ActionCheck.Fail("문파 이름을 정해야 합니다");
            if (w.Factions.Values.Any(f => !f.IsDissolved && f.Name == Name)) return ActionCheck.Fail("이미 있는 이름입니다");
            if (actor.Silver < Cost) return ActionCheck.Fail($"은자 {Cost}냥이 필요합니다");
            if (actor.Fame < FameRequired) return ActionCheck.Fail($"명성 {FameRequired}이(가) 필요합니다");
            if (Followers(w, d, actor).Count < FollowersRequired) return ActionCheck.Fail($"따르는 사람이 {FollowersRequired}명 이상 필요합니다");
            return ActionCheck.Pass;
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            var followers = Followers(w, d, actor);
            var f = new Faction
            {
                Id = w.NewFactionId(), Name = Name, Type = FactionType.Sect,
                Alignment = actor.Chivalry >= 0 ? Alignment.Righteous : Alignment.Unorthodox,
                Chivalry = actor.Chivalry, HomeRegionId = actor.LocationRegionId,
                Silver = actor.Silver - Cost, Fame = actor.Fame, TargetNamedMembers = followers.Count + 3,
            };
            w.Factions[f.Id] = f;
            actor.Silver = 0;
            actor.Fame = 0;
            actor.Chivalry = 0;
            FactionOps.MoveToFaction(w, actor, f.Id, Role.Leader);
            foreach (var c in followers)
            {
                FactionOps.MoveToFaction(w, c, f.Id, Role.Disciple);
                c.AddRelation(actor.Id, RelationTag.Master);
                actor.AddRelation(c.Id, RelationTag.Disciple);
            }
            Influence.Gain(w, f.HomeRegionId, f.Id, StartInfluence);
            Chronicle.SectFounded(w, actor, f);
        }
    }

    public class LeaveFaction : PersonalAction
    {
        public LeaveFaction(string actorId) : base(actorId) { }

        public override string Key => ActionKeys.LeaveFaction;
        public override string Describe(WorldState w) => "세력 떠나기";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor) =>
            actor.FactionId == null ? ActionCheck.Fail("소속된 세력이 없습니다") : ActionCheck.Pass;

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor)
        {
            var f = w.GetFaction(actor.FactionId);
            var leader = FactionOps.Leader(w, f);
            var location = actor.LocationRegionId;
            FactionOps.MoveToFaction(w, actor, null, Role.None);
            actor.LocationRegionId = location;
            if (leader != null) w.Opinions.Add(leader.Id, actor.Id, -20);
            Chronicle.LeftFaction(w, actor, f);
        }
    }
}
