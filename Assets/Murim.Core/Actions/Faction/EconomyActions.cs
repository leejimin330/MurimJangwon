using System;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public class ExpandInfluence : FactionAction
    {
        public const int Cost = 100;
        public readonly string RegionId;

        public ExpandInfluence(string actorId, string regionId) : base(actorId) => RegionId = regionId;

        public override string Key => ActionKeys.Expand;
        public override string Describe(WorldState w) => $"{RegionId} 영향력 확장";

        public static int GainFor(Character leader) => 3 + leader.Cunning * 5 / 20;

        /// <summary>본거지, 이미 영향력이 있는 지역, 그 인접 지역.</summary>
        public static bool CanReach(WorldState w, GameData d, Faction f, string regionId)
        {
            if (!d.Regions.ContainsKey(regionId)) return false;
            if (regionId == f.HomeRegionId || Influence.Get(w, regionId, f.Id) > 0) return true;
            return d.Regions[regionId].Neighbors.Any(n => n == f.HomeRegionId || Influence.Get(w, n, f.Id) > 0);
        }

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            if (!CanReach(w, d, own, RegionId)) return ActionCheck.Fail("세력이 닿지 않는 지역입니다");
            return NeedSilver(own, Cost);
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            own.Silver -= Cost;
            var rivals = w.Regions[RegionId].Influence.Keys.Where(k => k != own.Id).ToList();
            Influence.Gain(w, RegionId, own.Id, GainFor(actor));
            foreach (var id in rivals)
            {
                var leader = FactionOps.Leader(w, w.GetFaction(id));
                if (leader != null && leader.IsAlive) w.Opinions.Add(leader.Id, actor.Id, -5);
            }
        }
    }

    public class DoQuest : FactionAction
    {
        public readonly bool Plunder;

        public DoQuest(string actorId, bool plunder) : base(actorId) => Plunder = plunder;

        public override string Key => Plunder ? ActionKeys.Plunder : ActionKeys.Quest;
        public override string Describe(WorldState w) => Plunder ? "약탈" : "의뢰 수행";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own) => ActionCheck.Pass;

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            int silver = rng.Next(30, 81);
            if (Plunder)
            {
                own.Silver += silver * 2;
                own.Chivalry = Wallet.ClampChivalry(own.Chivalry - 5);
                var region = w.Regions[own.HomeRegionId];
                region.Stability = Math.Max(0, region.Stability - 5);
                Chronicle.Plundered(w, own, d.Regions.TryGetValue(own.HomeRegionId, out var r) ? r.Name : own.HomeRegionId);
            }
            else
            {
                own.Silver += silver;
                if (own.Alignment == Alignment.Righteous) own.Chivalry = Wallet.ClampChivalry(own.Chivalry + 2);
            }
        }
    }

    public class RepayDebt : FactionAction
    {
        public readonly int Amount;

        public RepayDebt(string actorId, int amount) : base(actorId) => Amount = amount;

        public override string Key => ActionKeys.RepayDebt;
        public override string Describe(WorldState w) => $"빚 {Amount}냥 갚기";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            if (Amount < 1 || Amount > own.Debt) return ActionCheck.Fail("갚을 빚보다 많습니다");
            return NeedSilver(own, Amount);
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            own.Silver -= Amount;
            own.Debt -= Amount;
        }
    }
}
