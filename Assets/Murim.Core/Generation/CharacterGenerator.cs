using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Generation
{
    public class CharacterGenerator
    {
        readonly GameData d;
        readonly GameRandom rng;
        readonly List<string> traitIds;

        public CharacterGenerator(GameData data, GameRandom random)
        {
            d = data;
            rng = random;
            traitIds = d.Traits.Keys.ToList();
        }

        public string RandomFamilyName() => rng.Pick(d.Names.FamilyNames);

        public string RandomGivenName(Sex sex) =>
            rng.Pick(sex == Sex.Male ? d.Names.MaleGivenNames : d.Names.FemaleGivenNames);

        public Character Create(WorldState w, Sex sex, int ageYears, string factionId, Role role, int realm, string familyName = null)
        {
            var c = new Character
            {
                Id = w.NewCharacterId(),
                FamilyName = familyName ?? RandomFamilyName(),
                GivenName = RandomGivenName(sex),
                Sex = sex,
                BirthMonth = w.Month - ageYears * 12 - rng.Next(0, 12),
                Realm = Math.Min(SimConstants.MaxRealm, Math.Max(0, realm)),
                Constitution = rng.Next(1, 15),
                Insight = rng.Next(1, 15),
                Cunning = rng.Next(1, 15),
                Charisma = rng.Next(1, 15),
            };
            FillTraits(c, rng.Next(SimConstants.MinTraits, SimConstants.MaxTraits + 1), null);
            ApplyTraitMods(c, c.TraitIds, +1);
            w.Characters[c.Id] = c;
            if (factionId != null) FactionOps.MoveToFaction(w, c, factionId, role);
            else c.Role = role;
            return c;
        }

        public Character CreateChild(WorldState w, Character mother, Character father)
        {
            var c = new Character
            {
                Id = w.NewCharacterId(),
                FamilyName = father.FamilyName,
                Sex = rng.Chance(0.5) ? Sex.Male : Sex.Female,
                BirthMonth = w.Month,
                FatherId = father.Id,
                MotherId = mother.Id,
                Constitution = Inherit(mother.Constitution, father.Constitution),
                Insight = Inherit(mother.Insight, father.Insight),
                Cunning = Inherit(mother.Cunning, father.Cunning),
                Charisma = Inherit(mother.Charisma, father.Charisma),
            };
            c.GivenName = RandomGivenName(c.Sex);
            var parentTraits = mother.TraitIds.Concat(father.TraitIds).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (parentTraits.Count > 0 && rng.Chance(SimConstants.TraitInheritChance)) c.TraitIds.Add(rng.Pick(parentTraits));
            FillTraits(c, rng.Next(SimConstants.MinTraits, 4), null);
            ApplyTraitMods(c, c.TraitIds, +1);
            w.Characters[c.Id] = c;
            mother.ChildIds.Add(c.Id);
            father.ChildIds.Add(c.Id);
            var factionId = mother.FactionId ?? father.FactionId;
            if (factionId != null) FactionOps.MoveToFaction(w, c, factionId, Role.Family);
            c.LocationRegionId = mother.LocationRegionId ?? father.LocationRegionId;
            return c;
        }

        public void Marry(WorldState w, Character a, Character b)
        {
            a.SpouseId = b.Id;
            b.SpouseId = a.Id;
            a.AddRelation(b.Id, RelationTag.Spouse);
            b.AddRelation(a.Id, RelationTag.Spouse);
        }

        /// <summary>플레이어가 고른 특성으로 교체한다. 나머지 칸은 excluded를 피해 무작위로 채운다.</summary>
        public void SetTraits(Character c, IList<string> chosen, ICollection<string> excluded)
        {
            ApplyTraitMods(c, c.TraitIds, -1);
            int count = Math.Max(c.TraitIds.Count, chosen.Count);
            c.TraitIds.Clear();
            c.TraitIds.AddRange(chosen);
            FillTraits(c, count, excluded);
            ApplyTraitMods(c, c.TraitIds, +1);
        }

        int Inherit(int a, int b) => Clamp((a + b) / 2 + rng.Next(-4, 5));

        static int Clamp(int v) => Math.Max(SimConstants.StatMin, Math.Min(SimConstants.StatMax, v));

        bool Conflicts(Character c, string traitId) =>
            c.TraitIds.Contains(traitId) || c.TraitIds.Any(t => d.Traits[t].Opposites.Contains(traitId));

        void FillTraits(Character c, int count, ICollection<string> excluded)
        {
            var pool = traitIds.Where(t => excluded == null || !excluded.Contains(t)).ToList();
            rng.Shuffle(pool);
            foreach (var t in pool)
            {
                if (c.TraitIds.Count >= count) break;
                if (!Conflicts(c, t)) c.TraitIds.Add(t);
            }
        }

        void ApplyTraitMods(Character c, IEnumerable<string> traits, int sign)
        {
            foreach (var t in traits)
            {
                var m = d.Traits[t].StatMods;
                if (m == null) continue;
                c.Constitution = Clamp(c.Constitution + sign * m.Constitution);
                c.Insight = Clamp(c.Insight + sign * m.Insight);
                c.Cunning = Clamp(c.Cunning + sign * m.Cunning);
                c.Charisma = Clamp(c.Charisma + sign * m.Charisma);
            }
        }
    }
}
