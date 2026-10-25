using Murim.Core.Data;
using Murim.Core.World;

namespace Murim.Core.Text
{
    public static class Titles
    {
        public static readonly string[] RealmNames = { "삼류", "이류", "일류", "절정", "초절정", "화경", "현경" };

        public static string Realm(int realm) => RealmNames[System.Math.Max(0, System.Math.Min(RealmNames.Length - 1, realm))];

        public static string Role(Faction f, Role role)
        {
            bool clan = f?.Type == FactionType.Clan;
            bool gang = f != null && f.Type == FactionType.Sect && f.Alignment != Alignment.Righteous;
            switch (role)
            {
                case Data.Role.Leader: return clan ? "가주" : gang ? "방주" : "장문인";
                case Data.Role.Heir: return clan ? "소가주" : "후계자";
                case Data.Role.Elder: return "장로";
                case Data.Role.Disciple: return gang ? "수하" : "제자";
                case Data.Role.Family: return "일가";
                case Data.Role.Guest: return "객경";
                default: return "무소속";
            }
        }

        /// <summary>"무당파 장문인 장삼" / "무소속 장삼".</summary>
        public static string Full(WorldState w, Character c)
        {
            var f = w.GetFaction(c.FactionId);
            return f == null ? "무소속 " + c.FullName : $"{f.Name} {Role(f, c.Role)} {c.FullName}";
        }
    }
}
