using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Simulation
{
    /// <summary>규칙 기반 세력 AI. 후보 행동에 점수를 매겨 상위 1~2개를 고른다.</summary>
    public static class FactionAI
    {
        const int MarriageSearchFactions = 8;

        public static List<IGameAction> Choose(WorldState w, GameData d, GameRandom rng, Faction f)
        {
            var leader = FactionOps.Leader(w, f);
            if (leader == null || !leader.IsAlive || f.IsDissolved) return new List<IGameAction>();

            var scored = new List<(IGameAction action, float score)>();
            void Consider(IGameAction a, float baseScore, string extraWeightKey = null)
            {
                if (a == null || !a.CanExecute(w, d).Ok) return;
                float s = baseScore + TraitWeight(d, leader, a.Key) + rng.Next(0, 6);
                if (extraWeightKey != null) s += TraitWeight(d, leader, extraWeightKey);
                scored.Add((a, s));
            }

            bool clan = f.Type == FactionType.Clan;
            var members = FactionOps.LivingMembers(w, f).ToList();
            var adults = members.Where(m => m.AgeYears(w.Month) >= SimConstants.AdultAge).ToList();
            var single = adults.Where(m => !MarriageRules.IsMarried(w, m) && m.Role != Role.Guest).ToList();

            Consider(InternalMatch(w, f, leader, single), clan ? 35 : 20);
            Consider(ExternalMatch(w, d, rng, f, leader, single), clan ? 35 : 15);

            int deficit = f.TargetNamedMembers - adults.Count;
            if (deficit > 0 && f.Silver >= 100) Consider(new RecruitNamed(leader.Id), 10 + deficit * 5);

            int room = RecruitUnnamed.Cap(w, f) - f.UnnamedDisciples;
            if (f.Silver >= 200 && room > 0) Consider(new RecruitUnnamed(leader.Id, Math.Min(10, room)), 5);

            if (f.Silver >= 300)
            {
                var region = ExpandTarget(w, d, f);
                if (region != null) Consider(new ExpandInfluence(leader.Id, region), 8);
            }

            Consider(new DoQuest(leader.Id, false), f.Silver < 200 ? 16 : 6);
            if (f.Alignment != Alignment.Righteous) Consider(new DoQuest(leader.Id, true), 4);

            if (f.Silver >= 400)
            {
                var friend = Friends(w, f).OrderByDescending(o => w.AllianceScore(f.Id, o.Id))
                    .ThenBy(o => o.Id, StringComparer.Ordinal).FirstOrDefault();
                if (friend != null) Consider(new SendGift(leader.Id, friend.Id, 100), 3);
            }

            var ally = w.Factions.Values.FirstOrDefault(o => o.Id != f.Id && !o.IsDissolved && !w.AreAllied(f.Id, o.Id)
                && w.AllianceScore(f.Id, o.Id) >= SimConstants.AllianceThreshold);
            if (ally != null) Consider(new ProposeAlliance(leader.Id, ally.Id), 12);

            var enemies = w.Factions.Values.Where(o => o.Id != f.Id && !o.IsDissolved && o.HomeRegionId == f.HomeRegionId
                && Opposed(f.Alignment, o.Alignment)).ToList();
            if (enemies.Count > 0)
            {
                var target = rng.Pick(enemies);
                bool righteousVsEvil = f.Alignment == Alignment.Righteous;
                Consider(new Insult(leader.Id, target.Id), 2, righteousVsEvil ? ActionKeys.InsultUnorthodox : null);
            }

            if (f.HeirId == null && leader.AgeYears(w.Month) >= 50)
            {
                var pick = SuccessionPhase.PickSuccessor(w, f, leader);
                if (pick != null && pick.Id != leader.Id) Consider(new DesignateHeir(leader.Id, pick.Id), 25);
            }

            int count = leader.Cunning >= 12 ? 2 : 1;
            return scored.Where(s => s.score > 0).OrderByDescending(s => s.score).Take(count).Select(s => s.action).ToList();
        }

        static float TraitWeight(GameData d, Character leader, string key) =>
            leader.TraitIds.Sum(t => d.Traits.TryGetValue(t, out var def) ? def.Weight(key) : 0f);

        static bool Opposed(Alignment a, Alignment b) =>
            (a == Alignment.Righteous) != (b == Alignment.Righteous);

        static IEnumerable<Faction> Friends(WorldState w, Faction f) =>
            w.Factions.Values.Where(o => o.Id != f.Id && !o.IsDissolved && (o.Alignment == f.Alignment || w.AreAllied(f.Id, o.Id)));

        static bool IsPlayerFaction(WorldState w, Faction f) => w.Player != null && w.Player.FactionId == f.Id;

        static IGameAction InternalMatch(WorldState w, Faction f, Character leader, List<Character> single)
        {
            (Character a, Character b) best = (null, null);
            int bestGap = int.MaxValue;
            foreach (var a in single.Where(c => c.Sex == Sex.Male))
                foreach (var b in single.Where(c => c.Sex == Sex.Female))
                {
                    if (!MarriageRules.Eligible(w, a, b).Ok) continue;
                    int gap = Math.Abs(a.BirthMonth - b.BirthMonth);
                    if (gap < bestGap) { bestGap = gap; best = (a, b); }
                }
            return best.a == null ? null : new ArrangeInternalMarriage(leader.Id, best.a.Id, best.b.Id);
        }

        static IGameAction ExternalMatch(WorldState w, GameData d, GameRandom rng, Faction f, Character leader, List<Character> single)
        {
            if (single.Count == 0 || f.Silver < ProposeMarriage.Cost) return null;
            var ours = rng.Pick(single);
            var candidates = Friends(w, f).Where(o => !IsPlayerFaction(w, o)).ToList();
            rng.Shuffle(candidates);
            foreach (var o in candidates.Take(MarriageSearchFactions))
            {
                var head = FactionOps.Leader(w, o);
                if (head == null || !head.IsAlive) continue;
                int score = MarriageRules.AcceptanceScore(w, d, leader, head, f.Fame, o.Fame, f.Alignment, o.Alignment,
                    o.Type == FactionType.Clan);
                if (score <= 0) continue;
                var theirs = FactionOps.LivingMembers(w, o).FirstOrDefault(c => c.Id != o.LeaderId
                    && MarriageRules.Eligible(w, ours, c).Ok);
                if (theirs == null) continue;
                bool joinUs = ours.Id == f.LeaderId || ours.Id == f.HeirId || (theirs.Id != o.HeirId && rng.Chance(0.5));
                return new ProposeMarriage(leader.Id, ours.Id, theirs.Id, joinUs);
            }
            return null;
        }

        static string ExpandTarget(WorldState w, GameData d, Faction f)
        {
            var options = new List<string> { f.HomeRegionId };
            options.AddRange(d.Regions[f.HomeRegionId].Neighbors);
            return options.Where(r => ExpandInfluence.CanReach(w, d, f, r))
                .OrderBy(r => Influence.Get(w, r, f.Id) == 0 ? 1 : 0)
                .ThenBy(r => r == f.HomeRegionId ? 0 : 1)
                .ThenBy(r => Influence.Get(w, r, f.Id))
                .FirstOrDefault();
        }
    }

    public class AiPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            foreach (var f in w.Factions.Values.Where(f => !f.IsDissolved).ToList())
            {
                if (f.IsDissolved || (w.Player != null && f.LeaderId == w.PlayerCharacterId)) continue;
                foreach (var a in FactionAI.Choose(w, ctx.Data, ctx.Rng, f))
                    if (a.CanExecute(w, ctx.Data).Ok) a.Execute(w, ctx.Data, ctx.Rng);
            }
        }
    }

    /// <summary>무소속 인물: 매년 1월 성향 맞는 지역 세력에 들어가거나 떠돈다.</summary>
    public class UnaffiliatedPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            if (w.Month % 12 != 0) return;
            var playerFaction = w.Player?.FactionId;
            foreach (var c in w.Characters.Values.Where(c => c.IsAlive && c.FactionId == null && c.Id != w.PlayerCharacterId
                && c.AgeYears(w.Month) >= SimConstants.AdultAge).ToList())
            {
                bool good = c.Chivalry >= 0;
                var options = w.Factions.Values.Where(f => !f.IsDissolved && f.HomeRegionId == c.LocationRegionId
                    && f.Id != playerFaction && f.LeaderId != null
                    && (f.Alignment == Alignment.Righteous) == good).ToList();
                if (options.Count > 0)
                {
                    if (ctx.Rng.Chance(0.3)) FactionOps.MoveToFaction(w, c, ctx.Rng.Pick(options).Id, Role.Disciple);
                    continue;
                }
                if (c.LocationRegionId != null && ctx.Rng.Chance(0.1) && ctx.Data.Regions.TryGetValue(c.LocationRegionId, out var r)
                    && r.Neighbors.Length > 0)
                    c.LocationRegionId = ctx.Rng.Pick(r.Neighbors);
            }
        }
    }
}
