using System;
using System.Collections.Generic;
using Murim.Core.Data;
using Newtonsoft.Json;

namespace Murim.Core.World
{
    public enum RelationTag { Master, Disciple, Lover, Spouse, Rival, Nemesis, Benefactor, SwornSibling }

    public class Character
    {
        public string Id;
        public string FamilyName;
        public string GivenName;
        public Sex Sex;
        public int BirthMonth;
        public int? DeathMonth;
        public string DeathCause;

        /// <summary>null이면 무소속.</summary>
        public string FactionId;
        public Role Role = Role.None;

        public string FatherId;
        public string MotherId;
        public string SpouseId;
        public List<string> ChildIds = new List<string>();

        /// <summary>0 삼류 ~ 6 현경.</summary>
        public int Realm;
        public int Constitution;
        public int Insight;
        public int Cunning;
        public int Charisma;

        public List<string> TraitIds = new List<string>();
        public List<Memory> Memories = new List<Memory>();
        public SortedDictionary<string, List<RelationTag>> Relations = new SortedDictionary<string, List<RelationTag>>(StringComparer.Ordinal);

        public string LocationRegionId;

        /// <summary>개인 자원. 무소속이거나 객경 등 장문인이 아닐 때 쓴다.</summary>
        public int Silver;
        public int Fame;
        public int Chivalry;

        /// <summary>이 월 번호 전까지는 행동할 수 없다 (표국 호위 등).</summary>
        public int BusyUntilMonth;

        /// <summary>표식 id → 만료 월 (0이면 영구). 만료 월부터는 없는 것으로 본다.</summary>
        public SortedDictionary<string, int> Flags = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>익힌 무공 (최대 6개, 내공심법 1개 포함).</summary>
        public List<Martial.KnownArt> Arts = new List<Martial.KnownArt>();
        /// <summary>내공. 단위는 개월 (화면에는 "내공 n년").</summary>
        public int Qi;
        /// <summary>깨달음 0~100. 돌파하면 0.</summary>
        public int Enlightenment;
        /// <summary>주로 쓰는 외공. null이면 성취가 가장 높은 외공.</summary>
        public string MainArtId;
        /// <summary>폐관이 끝나는 월 (이 월부터 출관). 0이면 폐관 중이 아님.</summary>
        public int TrainingUntil;
        public int TrainingMonths;
        /// <summary>올해 이긴 결투 수. 1월 깨달음 계산 뒤 0.</summary>
        public int DuelWinsThisYear;

        [JsonIgnore] public string FullName => FamilyName + GivenName;
        [JsonIgnore] public bool IsAlive => DeathMonth == null;
        public int AgeYears(int month) => ((DeathMonth ?? month) - BirthMonth) / 12;
        public bool HasTrait(string id) => TraitIds.Contains(id);

        public void AddRelation(string otherId, RelationTag tag)
        {
            if (!Relations.TryGetValue(otherId, out var list)) Relations[otherId] = list = new List<RelationTag>();
            if (!list.Contains(tag)) list.Add(tag);
        }

        public bool HasFlag(string id, int month) => id != null && Flags.TryGetValue(id, out var until) && (until == 0 || month < until);

        /// <summary>months가 0이면 영구.</summary>
        public void SetFlag(string id, int month, int months) => Flags[id] = months <= 0 ? 0 : month + months;

        public void ClearFlag(string id) => Flags.Remove(id);

        public bool InTraining(int month) => month < TrainingUntil;

        public Martial.KnownArt KnownArt(string artId)
        {
            foreach (var a in Arts)
                if (a.ArtId == artId) return a;
            return null;
        }

        public bool HasRelation(string otherId, RelationTag tag) => Relations.TryGetValue(otherId, out var list) && list.Contains(tag);
    }
}
