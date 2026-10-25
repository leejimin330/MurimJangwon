using System;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    /// <summary>행위자의 은자·명성·협의. 장문인이면 세력 금고, 아니면 개인 몫.</summary>
    public static class Wallet
    {
        public static Faction LedFaction(WorldState w, Character c)
        {
            var f = w.GetFaction(c.FactionId);
            return f != null && !f.IsDissolved && f.LeaderId == c.Id ? f : null;
        }

        public static int Silver(WorldState w, Character c) => LedFaction(w, c)?.Silver ?? c.Silver;

        public static void AddSilver(WorldState w, Character c, int delta)
        {
            var f = LedFaction(w, c);
            if (f != null) f.Silver = Math.Max(0, f.Silver + delta);
            else c.Silver = Math.Max(0, c.Silver + delta);
        }

        public static int Fame(WorldState w, Character c) => LedFaction(w, c)?.Fame ?? c.Fame;

        public static void AddFame(WorldState w, Character c, int delta)
        {
            var f = LedFaction(w, c);
            if (f != null) f.Fame = Math.Max(0, f.Fame + delta);
            else c.Fame = Math.Max(0, c.Fame + delta);
        }

        public static void AddChivalry(WorldState w, Character c, int delta)
        {
            var f = LedFaction(w, c);
            if (f != null) f.Chivalry = ClampChivalry(f.Chivalry + delta);
            else c.Chivalry = ClampChivalry(c.Chivalry + delta);
        }

        public static int ClampChivalry(int v) => Math.Max(SimConstants.ChivalryMin, Math.Min(SimConstants.ChivalryMax, v));
    }
}
