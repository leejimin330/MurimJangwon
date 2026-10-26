using System;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Generation
{
    public static class WorldGenerator
    {
        public static int StartChivalry(Alignment a) => a switch
        {
            Alignment.Righteous => SimConstants.RighteousStartChivalry,
            Alignment.Unorthodox => SimConstants.UnorthodoxStartChivalry,
            _ => SimConstants.DemonicStartChivalry,
        };

        /// <param name="playerRegionId">null이 아니면 그 지역에 플레이어와 체급이 비슷한 정파·사파 경쟁 세력을 추가로 만든다.</param>
        public static WorldState Generate(GameData d, GameRandom rng, string playerRegionId, int playerScaleNamed)
        {
            var w = new WorldState();
            var gen = new CharacterGenerator(d, rng);
            foreach (var r in d.Regions.Values)
                w.Regions[r.Id] = new RegionState { Id = r.Id, Stability = rng.Next(50, 71) };

            foreach (var def in d.Factions.Values)
            {
                var f = new Faction
                {
                    Id = def.Id, Name = def.Name, Type = def.Type, Alignment = def.Alignment,
                    Chivalry = StartChivalry(def.Alignment), HomeRegionId = def.HomeRegion,
                    UnnamedDisciples = def.UnnamedDisciples, Silver = def.Silver, Fame = def.Fame,
                    TargetNamedMembers = def.NamedMembers,
                };
                w.Factions[f.Id] = f;
                if (def.Influence > 0) w.Regions[def.HomeRegion].Influence[f.Id] = def.Influence;
                Populate(w, gen, rng, f, def.NamedMembers, def.LeaderRealm, false);
            }

            foreach (var r in d.Regions.Keys)
            {
                int n = rng.Next(1, 4);
                for (int i = 0; i < n; i++)
                {
                    var align = rng.Chance(0.5) ? Alignment.Righteous : Alignment.Unorthodox;
                    CreateMinor(w, d, gen, rng, r, align, rng.Next(3, 7), rng.Next(5, 21), rng.Next(2, 9));
                }
            }

            if (playerRegionId != null)
            {
                foreach (var align in new[] { Alignment.Righteous, Alignment.Unorthodox })
                {
                    int named = Math.Max(3, playerScaleNamed + rng.Next(-1, 2));
                    CreateMinor(w, d, gen, rng, playerRegionId, align, named, rng.Next(8, 16), rng.Next(3, 7));
                }
            }
            return w;
        }

        public static Faction CreateMinor(WorldState w, GameData d, CharacterGenerator gen, GameRandom rng,
            string regionId, Alignment align, int named, int unnamed, int influence)
        {
            var f = new Faction
            {
                Id = w.NewFactionId(), Name = FactionNames.Random(w, d, rng, align), Type = FactionType.Sect,
                Alignment = align, Chivalry = StartChivalry(align), HomeRegionId = regionId,
                UnnamedDisciples = unnamed, Silver = 200 + influence * 50, Fame = influence * 2,
                IsRandomMinor = true, TargetNamedMembers = named,
            };
            w.Factions[f.Id] = f;
            Influence.Gain(w, regionId, f.Id, influence);
            Populate(w, gen, rng, f, named, rng.Next(1, 3), true);
            return f;
        }

        /// <summary>장문인·가족·장로·후계자·제자를 만든다. exact면 멤버 수를 named에 정확히 맞춘다.</summary>
        static void Populate(WorldState w, CharacterGenerator gen, GameRandom rng, Faction f, int named, int leaderRealm, bool exact)
        {
            bool clan = f.Type == FactionType.Clan;
            var leaderSex = rng.Chance(clan ? 0.15 : 0.2) ? Sex.Female : Sex.Male;
            int leaderAge = rng.Next(40, 71);
            var leader = gen.Create(w, leaderSex, leaderAge, f.Id, Role.Leader, leaderRealm);
            string surname = leader.FamilyName;
            int budget = named - 1;

            Character spouse = null;
            if (budget > 2 && rng.Chance(0.6))
            {
                var spouseAge = Math.Max(20, leaderAge + rng.Next(-10, 4));
                spouse = gen.Create(w, leaderSex == Sex.Male ? Sex.Female : Sex.Male, spouseAge, f.Id, Role.Family,
                    Math.Max(0, leaderRealm - 2));
                gen.Marry(w, leader, spouse);
                budget--;
                var mother = leaderSex == Sex.Female ? leader : spouse;
                var father = leaderSex == Sex.Male ? leader : spouse;
                // 어머니가 출산 가능 나이(FertileMinAge~FertileMaxAge)였을 때 태어난 자식만 만든다.
                int motherAge = mother.AgeYears(w.Month);
                int minChildAge = Math.Max(1, motherAge - SimConstants.FertileMaxAge);
                int maxChildAge = Math.Min(motherAge - SimConstants.FertileMinAge - 1, 40);
                int kids = rng.Next(0, 4);
                for (int i = 0; i < kids && budget > 1 && maxChildAge >= minChildAge; i++)
                {
                    var child = gen.CreateChild(w, mother, father);
                    int age = rng.Next(minChildAge, maxChildAge + 1);
                    child.BirthMonth = w.Month - age * 12 - rng.Next(0, 12);
                    if (age >= SimConstants.AdultAge) child.Realm = rng.Next(0, Math.Max(1, leaderRealm));
                    gen.Regrant(w, child);
                    budget--;
                }
            }

            int elders = named / 4;
            for (int i = 0; i < elders && budget > 1; i++)
            {
                var e = gen.Create(w, rng.Chance(0.2) ? Sex.Female : Sex.Male, rng.Next(45, 71), f.Id, Role.Elder,
                    Math.Max(0, leaderRealm - 1), clan ? surname : null);
                LinkDisciple(leader, e);
                budget--;
            }

            var eldestAdultChild = leader.ChildIds.Select(w.Get)
                .Where(c => c.AgeYears(w.Month) >= SimConstants.AdultAge)
                .OrderBy(c => c.BirthMonth).FirstOrDefault();
            if (clan && eldestAdultChild != null)
            {
                eldestAdultChild.Role = Role.Heir;
                f.HeirId = eldestAdultChild.Id;
            }
            else if (budget > 0 || !exact)
            {
                var heir = gen.Create(w, rng.Chance(0.2) ? Sex.Female : Sex.Male, rng.Next(25, 41), f.Id, Role.Heir,
                    Math.Max(0, leaderRealm - 1), clan ? surname : null);
                LinkDisciple(leader, heir);
                budget--;
            }

            while (budget > 0)
            {
                bool kin = clan && rng.Chance(0.7);
                var m = gen.Create(w, rng.Chance(0.25) ? Sex.Female : Sex.Male, rng.Next(16, 36), f.Id,
                    kin ? Role.Family : Role.Disciple, rng.Next(0, Math.Max(1, leaderRealm)), kin ? surname : null);
                if (!kin) LinkDisciple(leader, m);
                budget--;
            }
        }

        static void LinkDisciple(Character master, Character disciple)
        {
            disciple.AddRelation(master.Id, RelationTag.Master);
            master.AddRelation(disciple.Id, RelationTag.Disciple);
        }
    }

    public static class FactionNames
    {
        public static string Random(WorldState w, GameData d, GameRandom rng, Alignment align)
        {
            var parts = d.FactionNameParts;
            var suffixes = align == Alignment.Righteous ? parts.RighteousSuffixes : parts.UnorthodoxSuffixes;
            var taken = new System.Collections.Generic.HashSet<string>(w.Factions.Values.Select(f => f.Name));
            for (int i = 0; i < 50; i++)
            {
                var name = rng.Pick(parts.Prefixes) + rng.Pick(suffixes);
                if (!taken.Contains(name)) return name;
            }
            var baseName = rng.Pick(parts.Prefixes) + rng.Pick(suffixes);
            int n = 2;
            while (taken.Contains(baseName + n)) n++;
            return baseName + n;
        }
    }
}
