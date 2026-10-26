using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Martial
{
    /// <summary>인물에게 무공을 주고 배우게 한다: 생성 시 부여, 저장 이관, 배우기 규칙.</summary>
    public static class ArtGranting
    {
        public const int MaxArts = 6;
        public const int MaxMastery = 10;

        public static ArtDef Def(GameData d, string artId) => artId != null && d.Arts.TryGetValue(artId, out var a) ? a : null;

        public static ArtDef Inner(GameData d, Character c) =>
            c.Arts.Select(k => Def(d, k.ArtId)).FirstOrDefault(a => a != null && a.IsInner);

        public static IEnumerable<(KnownArt known, ArtDef def)> Externals(GameData d, Character c) =>
            c.Arts.Select(k => (k, Def(d, k.ArtId))).Where(x => x.Item2 != null && !x.Item2.IsInner);

        /// <summary>주무공: 지정한 외공, 없으면 성취가 가장 높은 외공 (신법은 뒤로).</summary>
        public static ArtDef MainArt(GameData d, Character c)
        {
            var main = Def(d, c.MainArtId);
            if (main != null && !main.IsInner && c.KnownArt(main.Id) != null) return main;
            return Externals(d, c).OrderBy(x => x.def.IsMovement ? 1 : 0).ThenByDescending(x => x.known.Mastery)
                .ThenBy(x => x.def.Id, StringComparer.Ordinal).Select(x => x.def).FirstOrDefault();
        }

        /// <summary>이 인물의 길(정·사·마)로 익힐 수 있는 무공 성향.</summary>
        public static bool AlignmentAllows(Alignment who, Alignment art) => who switch
        {
            Alignment.Righteous => art == Alignment.Righteous,
            Alignment.Unorthodox => art != Alignment.Demonic,
            _ => true,
        };

        public static bool MeetsRequires(ArtDef a, Character c) =>
            a.Requires == null || (c.Realm >= a.Requires.RealmMin && c.Constitution >= a.Requires.ConstitutionMin && c.Insight >= a.Requires.InsightMin);

        /// <summary>아직 모르고, 조건(경지·근골·오성)과 길이 맞고, 비전이면 그 세력 사람인 무공.</summary>
        public static IEnumerable<ArtDef> Learnable(WorldState w, GameData d, Character c, Func<ArtDef, bool> filter = null)
        {
            var align = ConditionCheck.AlignmentOf(w, c);
            return d.Arts.Values.Where(a => c.KnownArt(a.Id) == null && MeetsRequires(a, c)
                && (a.Faction == null ? AlignmentAllows(align, a.Alignment) : a.Faction == c.FactionId)
                && (filter == null || filter(a)));
        }

        /// <summary>무공을 배운다. 이미 알거나 칸(6개)이 차면 false. 내공심법은 더 높은 등급일 때만 바꾼다.</summary>
        public static bool Learn(GameData d, Character c, string artId, int mastery)
        {
            var a = Def(d, artId);
            if (a == null || c.KnownArt(artId) != null) return false;
            mastery = Math.Max(1, Math.Min(MaxMastery, mastery));
            if (a.IsInner)
            {
                var old = Inner(d, c);
                if (old != null)
                {
                    if (old.Grade >= a.Grade) return false;
                    c.Arts.RemoveAll(k => k.ArtId == old.Id);
                }
                c.Arts.Insert(0, new KnownArt { ArtId = a.Id, Mastery = mastery });
                return true;
            }
            if (c.Arts.Count >= MaxArts) return false;
            c.Arts.Add(new KnownArt { ArtId = a.Id, Mastery = mastery });
            return true;
        }

        /// <summary>무공이 없을 때만 준다 (설계 4.4).</summary>
        public static void Grant(WorldState w, GameData d, GameRandom rng, Character c)
        {
            if (d.Arts.Count == 0 || c.Arts.Count > 0) return;
            int age = c.AgeYears(w.Month);
            var (gMin, gMax) = GradeRange(c.Realm);
            int Mastery() => age < 10 ? 1 : Math.Max(1, Math.Min(MaxMastery, (int)Math.Round(c.Realm * 1.5) + rng.Next(-1, 2)));

            // 내공심법: 비전 → 경지에 맞는 일반 → 가장 낮은 것
            var inner = Pick(rng, Learnable(w, d, c, a => a.IsInner && a.Faction != null).ToList())
                ?? Pick(rng, Learnable(w, d, c, a => a.IsInner && a.Faction == null && a.Grade >= gMin && a.Grade <= gMax).ToList())
                ?? d.Arts.Values.Where(a => a.IsInner && a.Faction == null).OrderBy(a => a.Grade).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            if (inner != null) Learn(d, c, inner.Id, Mastery());

            int externals = age < 10 ? 1 : rng.Next(1, 3);
            var secret = Learnable(w, d, c, a => !a.IsInner && a.Faction != null).ToList();
            rng.Shuffle(secret);
            foreach (var a in secret.Take(externals)) Learn(d, c, a.Id, Mastery());
            for (int lo = gMin; Externals(d, c).Count() < externals && lo >= 1; lo--)
            {
                var pool = Learnable(w, d, c, a => !a.IsInner && a.Faction == null && a.Grade >= lo && a.Grade <= gMax).ToList();
                while (pool.Count > 0 && Externals(d, c).Count() < externals)
                {
                    var a = Pick(rng, pool);
                    pool.Remove(a);
                    Learn(d, c, a.Id, Mastery());
                }
            }

            int lower = c.Realm == 0 ? 0 : Cultivation.ThresholdYears[c.Realm - 1];
            int upper = c.Realm < Cultivation.ThresholdYears.Length ? Cultivation.ThresholdYears[c.Realm] : lower + 60;
            c.Qi = age < 10 ? 0 : rng.Next(lower * 12, upper * 12);
            c.Enlightenment = age < 10 ? 0 : rng.Next(0, 61);
            c.MainArtId = MainArt(d, c)?.Id;
        }

        /// <summary>경지를 바꾼 뒤 무공을 다시 준다 (세계 생성 중 자식 경지 조정).</summary>
        public static void Regrant(WorldState w, GameData d, GameRandom rng, Character c)
        {
            c.Arts.Clear();
            c.Qi = 0;
            c.Enlightenment = 0;
            c.MainArtId = null;
            Grant(w, d, rng, c);
        }

        /// <summary>버전 1·2 저장: 살아 있는 인물 중 무공이 없는 사람 모두에게 준다 (Ordinal 순서).</summary>
        public static void Migrate(WorldState w, GameData d, GameRandom rng)
        {
            foreach (var c in w.Characters.Values)
                if (c.IsAlive) Grant(w, d, rng, c);
        }

        static (int min, int max) GradeRange(int realm) => realm <= 1 ? (1, 2) : realm <= 3 ? (2, 3) : (3, 4);

        static ArtDef Pick(GameRandom rng, List<ArtDef> pool) => pool.Count == 0 ? null : rng.Pick(pool);
    }
}
