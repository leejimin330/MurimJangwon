using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.World;

namespace Murim.Core.Generation
{
    public static class ScenarioSetup
    {
        /// <summary>플레이어 인물(과 세력)을 만들고 w.PlayerCharacterId를 설정한다.</summary>
        public static Character Apply(WorldState w, GameData d, GameRandom rng, NewGameOptions o)
        {
            var s = d.Scenarios[o.ScenarioId];
            var gen = new CharacterGenerator(d, rng);
            int age = rng.Next(s.LeaderAgeMin, s.LeaderAgeMax + 1);
            Character player;

            if (s.HasFaction)
            {
                var align = s.AllowedAlignments.Contains(o.Alignment) ? o.Alignment : s.AllowedAlignments[0];
                var f = new Faction
                {
                    Id = w.NewFactionId(),
                    Name = string.IsNullOrWhiteSpace(o.FactionName) ? FactionNames.Random(w, d, rng, align) : o.FactionName.Trim(),
                    Type = s.FactionType, Alignment = align,
                    Chivalry = s.Chivalry != 0 ? s.Chivalry : WorldGenerator.StartChivalry(align),
                    HomeRegionId = o.RegionId, Silver = s.Silver, Fame = s.Fame, Debt = s.Debt,
                    UnnamedDisciples = s.UnnamedDisciples, TargetNamedMembers = s.NamedMembers + s.FamilyMax,
                };
                w.Factions[f.Id] = f;
                player = gen.Create(w, o.Sex, age, f.Id, Role.Leader, s.Realm, Clean(o.FamilyName));
                AddFamily(w, gen, rng, player, rng.Next(s.FamilyMin, s.FamilyMax + 1));
                for (int i = 0; i < s.NamedMembers; i++)
                {
                    var m = gen.Create(w, rng.Chance(0.25) ? Sex.Female : Sex.Male, rng.Next(15, 30), f.Id, Role.Disciple,
                        rng.Next(0, 2));
                    m.AddRelation(player.Id, RelationTag.Master);
                    player.AddRelation(m.Id, RelationTag.Disciple);
                }
                Influence.Gain(w, o.RegionId, f.Id, s.Influence);
            }
            else
            {
                player = gen.Create(w, o.Sex, age, null, Role.None, s.Realm, Clean(o.FamilyName));
                player.Silver = s.Silver;
                player.Fame = s.Fame;
                player.Chivalry = s.Chivalry;
                player.LocationRegionId = o.RegionId;
            }

            if (Clean(o.GivenName) != null) player.GivenName = Clean(o.GivenName);
            ApplyChosenTraits(d, gen, player, o.ChosenTraitIds ?? new List<string>());
            w.PlayerCharacterId = player.Id;

            if (s.StartWithNeighborGrudge) AddNeighborGrudge(w, d, rng, player, o.RegionId);
            if (s.StartWithRighteousFriend) AddRighteousFriend(w, rng, player, o.RegionId);
            if (s.StartWithGrudge) AddPastGrudge(w, rng, player);
            return player;
        }

        static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        static void AddFamily(WorldState w, CharacterGenerator gen, GameRandom rng, Character leader, int count)
        {
            int age = leader.AgeYears(w.Month);
            if (count <= 0) return;
            Character spouse = null;
            if (age >= 25)
            {
                spouse = gen.Create(w, leader.Sex == Sex.Male ? Sex.Female : Sex.Male, Math.Max(20, age + rng.Next(-8, 3)),
                    leader.FactionId, Role.Family, 0);
                gen.Marry(w, leader, spouse);
                count--;
            }
            var mother = leader.Sex == Sex.Female ? leader : spouse;
            var father = leader.Sex == Sex.Male ? leader : spouse;
            // 어머니가 출산 가능 나이였을 때 태어난 자식만 만든다.
            int motherAge = mother == null ? 0 : mother.AgeYears(w.Month);
            int minChildAge = Math.Max(1, motherAge - SimConstants.FertileMaxAge);
            int maxChildAge = mother == null ? 0 : Math.Min(motherAge - SimConstants.FertileMinAge - 1, 30);
            for (int i = 0; i < count; i++)
            {
                if (spouse != null && maxChildAge >= minChildAge)
                {
                    var child = gen.CreateChild(w, mother, father);
                    child.BirthMonth = w.Month - rng.Next(minChildAge, maxChildAge + 1) * 12 - rng.Next(0, 12);
                    gen.Regrant(w, child);
                }
                else
                {
                    gen.Create(w, rng.Chance(0.5) ? Sex.Male : Sex.Female, Math.Max(16, age + rng.Next(-6, 7)),
                        leader.FactionId, Role.Family, 0, leader.FamilyName);
                }
            }
        }

        static void ApplyChosenTraits(GameData d, CharacterGenerator gen, Character c, List<string> requested)
        {
            if (requested.Count == 0) return;
            var chosen = new List<string>();
            foreach (var t in requested.Take(2))
            {
                if (!d.Traits.ContainsKey(t) || chosen.Contains(t)) continue;
                if (chosen.Any(x => d.Traits[x].Opposites.Contains(t))) continue;
                chosen.Add(t);
            }
            gen.SetTraits(c, chosen, requested);
        }

        static void AddNeighborGrudge(WorldState w, GameData d, GameRandom rng, Character player, string regionId)
        {
            Character enemy = PickLeader(w, regionId, player.FactionId);
            if (enemy == null)
                foreach (var n in d.Regions[regionId].Neighbors)
                    if ((enemy = PickLeader(w, n, player.FactionId)) != null) break;
            if (enemy == null) return;
            Opinion.AddMemory(player, MemoryKind.Grudge, enemy.Id, "선대의 원한", w.Month, 60);
            Opinion.AddMemory(enemy, MemoryKind.Grudge, player.Id, "선대의 원한", w.Month, 30);

            Character PickLeader(WorldState ws, string region, string exceptFaction)
            {
                var leaders = ws.Factions.Values
                    .Where(f => f.HomeRegionId == region && f.Id != exceptFaction && !f.IsDissolved && f.LeaderId != null)
                    .Select(f => ws.Get(f.LeaderId)).ToList();
                return leaders.Count == 0 ? null : rng.Pick(leaders);
            }
        }

        static void AddRighteousFriend(WorldState w, GameRandom rng, Character player, string regionId)
        {
            var candidates = w.Characters.Values.Where(c => c.Id != player.Id && c.IsAlive
                && c.AgeYears(w.Month) >= SimConstants.AdultAge
                && w.GetFaction(c.FactionId) is Faction f && f.Alignment == Alignment.Righteous && f.HomeRegionId == regionId).ToList();
            if (candidates.Count == 0) return;
            var friend = rng.Pick(candidates);
            w.Opinions.Add(friend.Id, player.Id, 40);
            w.Opinions.Add(player.Id, friend.Id, 40);
        }

        static void AddPastGrudge(WorldState w, GameRandom rng, Character player)
        {
            var candidates = w.Characters.Values.Where(c => c.Id != player.Id && c.IsAlive
                && c.AgeYears(w.Month) >= 25 && c.FactionId != null).ToList();
            if (candidates.Count == 0) return;
            Opinion.AddMemory(player, MemoryKind.Grudge, rng.Pick(candidates).Id, "과거의 원한", w.Month, 60);
        }
    }

    public static class SimSetup
    {
        public static WorldState NewWorld(GameData d, NewGameOptions o, out GameRandom rng)
        {
            rng = new GameRandom(o.Seed);
            var s = d.Scenarios[o.ScenarioId];
            int scale = Math.Max(4, s.NamedMembers + s.FamilyMax);
            var w = WorldGenerator.Generate(d, rng, o.RegionId, scale);
            ScenarioSetup.Apply(w, d, rng, o);
            w.Events.StorytellerId = d.Storytellers.ContainsKey(o.StorytellerId ?? "") ? o.StorytellerId : "balanced";
            return w;
        }
    }
}
