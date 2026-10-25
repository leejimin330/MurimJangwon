using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.World;

namespace Murim.Core.Rules
{
    public static class FactionOps
    {
        public static Character Leader(WorldState w, Faction f) => f == null ? null : w.Get(f.LeaderId);

        public static IEnumerable<Character> LivingMembers(WorldState w, Faction f) =>
            f.MemberIds.Select(w.Get).Where(c => c != null && c.IsAlive);

        /// <summary>인물을 다른 세력(또는 null = 무소속)으로 옮긴다.</summary>
        public static void MoveToFaction(WorldState w, Character c, string factionIdOrNull, Role role)
        {
            var old = w.GetFaction(c.FactionId);
            if (old != null)
            {
                old.MemberIds.Remove(c.Id);
                if (old.HeirId == c.Id) old.HeirId = null;
                if (old.LeaderId == c.Id) old.LeaderId = null;
            }
            c.FactionId = factionIdOrNull;
            c.Role = role;
            var next = w.GetFaction(factionIdOrNull);
            if (next != null)
            {
                if (!next.MemberIds.Contains(c.Id)) next.MemberIds.Add(c.Id);
                c.LocationRegionId = next.HomeRegionId;
                if (role == Role.Leader) next.LeaderId = c.Id;
                if (role == Role.Heir) next.HeirId = c.Id;
            }
            else if (old != null && c.LocationRegionId == null) c.LocationRegionId = old.HomeRegionId;
        }

        /// <summary>세력을 해산한다. 멤버는 무소속, 영향력과 동맹은 사라진다.</summary>
        public static void Dissolve(WorldState w, Faction f, string reason)
        {
            foreach (var m in LivingMembers(w, f).ToList())
            {
                m.LocationRegionId = f.HomeRegionId;
                MoveToFaction(w, m, null, Role.None);
            }
            f.MemberIds.Clear();
            f.LeaderId = null;
            f.HeirId = null;
            foreach (var r in w.Regions.Values) r.Influence.Remove(f.Id);
            w.Alliances.RemoveWhere(k => k.Split('|').Contains(f.Id));
            f.IsDissolved = true;
            Story.Chronicle.Dissolved(w, f, reason);
        }
    }
}
