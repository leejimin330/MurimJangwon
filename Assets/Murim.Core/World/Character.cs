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

        [JsonIgnore] public string FullName => FamilyName + GivenName;
        [JsonIgnore] public bool IsAlive => DeathMonth == null;
        public int AgeYears(int month) => ((DeathMonth ?? month) - BirthMonth) / 12;
        public bool HasTrait(string id) => TraitIds.Contains(id);

        public void AddRelation(string otherId, RelationTag tag)
        {
            if (!Relations.TryGetValue(otherId, out var list)) Relations[otherId] = list = new List<RelationTag>();
            if (!list.Contains(tag)) list.Add(tag);
        }

        public bool HasRelation(string otherId, RelationTag tag) => Relations.TryGetValue(otherId, out var list) && list.Contains(tag);
    }
}
