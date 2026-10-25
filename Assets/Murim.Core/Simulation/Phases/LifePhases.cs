using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Simulation
{
    public class BirthPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            var mothers = w.Characters.Values.Where(c => c.IsAlive && c.Sex == Sex.Female && c.SpouseId != null).ToList();
            foreach (var mother in mothers)
            {
                int age = mother.AgeYears(w.Month);
                if (age < SimConstants.FertileMinAge || age > SimConstants.FertileMaxAge) continue;
                var father = w.Get(mother.SpouseId);
                if (father == null || !father.IsAlive) continue;
                if (!ctx.Rng.Chance(BirthChance(w, mother))) continue;
                var child = ctx.Generator.CreateChild(w, mother, father);
                if (IsNotable(w, mother) || IsNotable(w, father)) Chronicle.Born(w, mother, father, child);
            }
        }

        /// <summary>월 출생 확률. 세력이 목표 규모보다 붐빌수록 줄고, 작을수록 조금 는다.</summary>
        public static double BirthChance(WorldState w, Character mother)
        {
            var f = w.GetFaction(mother.FactionId);
            if (f == null || f.IsDissolved || f.TargetNamedMembers <= 0) return SimConstants.MonthlyBirthChance;
            int living = FactionOps.LivingMembers(w, f).Count();
            double ratio = f.TargetNamedMembers * SimConstants.CrowdingFactor / System.Math.Max(1, living);
            ratio = System.Math.Max(SimConstants.MinBirthMultiplier, System.Math.Min(SimConstants.MaxBirthMultiplier, ratio));
            return SimConstants.MonthlyBirthChance * ratio;
        }

        /// <summary>연대기에 출생·사망을 남길 만한 인물.</summary>
        public static bool IsNotable(WorldState w, Character c)
        {
            if (c.Id == w.PlayerCharacterId) return true;
            var player = w.Player;
            if (player?.FactionId != null && c.FactionId == player.FactionId) return true;
            if (player != null && (c.Id == player.SpouseId || c.FatherId == player.Id || c.MotherId == player.Id)) return true;
            return c.Role == Role.Leader || c.Role == Role.Heir || c.Realm >= 4;
        }
    }

    public class DeathPhase : ITurnPhase
    {
        public static double MonthlyDeathChance(int ageYears, int realm, float traitMultiplier)
        {
            double e = ageYears - realm * SimConstants.RealmLifespanYears;
            double h = Math.Min(1.0, SimConstants.BaseHazard * Math.Exp(SimConstants.HazardGrowth * e));
            return Math.Min(1.0, h / 12 * traitMultiplier + SimConstants.MonthlyAccidentChance);
        }

        public static float TraitMultiplier(GameData d, IEnumerable<string> traits)
        {
            float m = 1f;
            foreach (var t in traits)
                if (d.Traits.TryGetValue(t, out var def)) m *= def.DeathRiskMultiplier;
            return m;
        }

        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            foreach (var c in w.Characters.Values.Where(c => c.IsAlive).ToList())
            {
                int age = c.AgeYears(w.Month);
                double p = MonthlyDeathChance(age, c.Realm, TraitMultiplier(ctx.Data, c.TraitIds));
                double roll = ctx.Rng.NextDouble();
                if (roll >= p) continue;
                string cause;
                if (roll < SimConstants.MonthlyAccidentChance) cause = "사고";
                else cause = age - c.Realm * SimConstants.RealmLifespanYears >= SimConstants.OldAgeThreshold ? "노환" : "병";
                Kill(w, c, cause);
            }
        }

        public static void Kill(WorldState w, Character c, string cause)
        {
            c.DeathMonth = w.Month;
            c.DeathCause = cause;
            var children = c.ChildIds.Select(w.Get).Where(x => x != null && x.IsAlive).ToList();
            Opinion.InheritMemories(c, children, w.Month);
            if (BirthPhase.IsNotable(w, c)) Chronicle.Died(w, c);
        }
    }

    public class SuccessionPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            var player = w.Player;
            string playerFactionBefore = player?.FactionId;
            bool playerDied = player != null && !player.IsAlive;

            foreach (var f in w.Factions.Values.Where(f => !f.IsDissolved).ToList())
            {
                var leader = w.Get(f.LeaderId);
                if (leader != null && leader.IsAlive) continue;
                var successor = PickSuccessor(w, f, leader);
                if (successor == null)
                {
                    FactionOps.Dissolve(w, f, "대를 이을 사람이 없어");
                    continue;
                }
                successor.Role = Role.Leader;
                f.LeaderId = successor.Id;
                f.HeirId = null;
                Chronicle.Succeeded(w, f, successor, leader);
                if (playerDied && leader != null && leader.Id == player.Id) w.PlayerCharacterId = successor.Id;

                foreach (var m in FactionOps.LivingMembers(w, f).ToList())
                {
                    if (m.Id == successor.Id || !m.HasTrait("ambitious")) continue;
                    if (Opinion.Of(w, ctx.Data, m.Id, successor.Id) > SimConstants.DesertionOpinion) continue;
                    if (!ctx.Rng.Chance(SimConstants.DesertionChance)) continue;
                    var location = f.HomeRegionId;
                    FactionOps.MoveToFaction(w, m, null, Role.None);
                    m.LocationRegionId = location;
                    Chronicle.Deserted(w, f, m, successor);
                }
            }

            if (playerDied && w.PlayerCharacterId == player.Id)
            {
                var heir = player.ChildIds.Select(w.Get).Where(c => c != null && c.IsAlive)
                    .OrderBy(c => c.BirthMonth).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (heir == null)
                {
                    w.GameOver = true;
                    Chronicle.GameOver(w, player);
                }
                else
                {
                    w.PlayerCharacterId = heir.Id;
                    if (heir.FactionId == null || playerFactionBefore == null)
                    {
                        heir.Silver += player.Silver;
                        heir.Fame += player.Fame / 2;
                        player.Silver = 0;
                    }
                }
            }
        }

        /// <summary>지정 후계자 → (세가) 최연장 자식 → 경지×3 + 심계 + 매력 최고점.</summary>
        public static Character PickSuccessor(WorldState w, Faction f, Character deadLeader)
        {
            var members = FactionOps.LivingMembers(w, f).Where(c => c.Role != Role.Guest && c.Id != deadLeader?.Id).ToList();
            var heir = w.Get(f.HeirId);
            if (heir != null && heir.IsAlive && heir.FactionId == f.Id) return heir;
            if (f.Type == FactionType.Clan && deadLeader != null)
            {
                var child = members.Where(c => c.FatherId == deadLeader.Id || c.MotherId == deadLeader.Id)
                    .OrderBy(c => c.BirthMonth).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (child != null) return child;
            }
            var adults = members.Where(c => c.AgeYears(w.Month) >= SimConstants.AdultAge).ToList();
            var pool = adults.Count > 0 ? adults : members;
            return pool.OrderByDescending(c => c.Realm * 3 + c.Cunning + c.Charisma)
                .ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        }
    }

    public class YearlyPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            if (w.Month % 12 != 0) return;
            var d = ctx.Data;

            w.Events.PruneExpired(w.Month);
            foreach (var c in w.Characters.Values.Where(c => c.IsAlive))
            {
                Opinion.DecayMemories(c);
                foreach (var flag in c.Flags.Where(kv => kv.Value != 0 && kv.Value <= w.Month).Select(kv => kv.Key).ToList())
                    c.Flags.Remove(flag);
                foreach (var flag in c.Flags.Keys)
                    if (d.Flags.TryGetValue(flag, out var fd) && fd.YearlyChivalry != 0) Actions.Wallet.AddChivalry(w, c, fd.YearlyChivalry);
                int age = c.AgeYears(w.Month);
                if (age >= SimConstants.RealmGrowthMinAge && age <= SimConstants.RealmGrowthMaxAge && c.Realm < SimConstants.MaxRealm)
                {
                    double p = (c.Constitution + c.Insight) / SimConstants.RealmGrowthDivisor * Math.Pow(0.5, c.Realm);
                    if (ctx.Rng.Chance(p)) c.Realm++;
                }
                if (c.FactionId == null) c.Chivalry = Actions.Wallet.ClampChivalry(c.Chivalry + YearlyChivalry(d, c));
            }

            foreach (var f in w.Factions.Values.Where(f => !f.IsDissolved))
            {
                var leader = FactionOps.Leader(w, f);
                if (leader != null && leader.IsAlive)
                {
                    f.Chivalry = Actions.Wallet.ClampChivalry(f.Chivalry + YearlyChivalry(d, leader));
                    if (f.Debt > 0)
                    {
                        f.Debt += f.Debt * SimConstants.DebtInterestPercent / 100;
                        if (f.Debt > SimConstants.DebtUnrestThreshold)
                            foreach (var m in FactionOps.LivingMembers(w, f).Where(m => m.Role == Role.Family || m.Role == Role.Heir))
                                w.Opinions.Add(m.Id, leader.Id, SimConstants.DebtUnrestOpinion);
                    }
                    foreach (var g in FactionOps.LivingMembers(w, f).Where(m => m.Role == Role.Guest).ToList())
                        if (Opinion.Of(w, d, leader.Id, g.Id) >= SimConstants.GuestToElderOpinion) g.Role = Role.Elder;
                }
                int target = Influence.Total(w, f.Id) * 2;
                f.Fame = Math.Max(0, f.Fame + Math.Sign(target - f.Fame));
            }
        }

        static int YearlyChivalry(GameData d, Character c) =>
            c.TraitIds.Sum(t => d.Traits.TryGetValue(t, out var def) ? def.YearlyChivalry : 0);
    }

    public class AlignmentPhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            foreach (var f in ctx.World.Factions.Values.Where(f => !f.IsDissolved))
            {
                if (f.Alignment == Alignment.Righteous && f.Chivalry < -SimConstants.AlignmentShiftThreshold)
                {
                    f.Alignment = Alignment.Unorthodox;
                    Chronicle.AlignmentShifted(ctx.World, f, Alignment.Righteous);
                }
                else if (f.Alignment == Alignment.Unorthodox && f.Chivalry > SimConstants.AlignmentShiftThreshold)
                {
                    f.Alignment = Alignment.Righteous;
                    Chronicle.AlignmentShifted(ctx.World, f, Alignment.Unorthodox);
                }
            }
        }
    }

    public class IncomePhase : ITurnPhase
    {
        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            foreach (var f in w.Factions.Values.Where(f => !f.IsDissolved))
            {
                f.Silver += Influence.Total(w, f.Id) * SimConstants.InfluenceIncome + f.UnnamedDisciples;
                foreach (var g in FactionOps.LivingMembers(w, f).Where(m => m.Role == Role.Guest))
                {
                    if (f.Silver < SimConstants.GuestStipend) break;
                    f.Silver -= SimConstants.GuestStipend;
                    g.Silver += SimConstants.GuestStipend;
                }
            }
        }
    }
}
