using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Manor
{
    /// <summary>장원 건물 놓기·허물기, 완공, 보너스 합산, 정원.</summary>
    public static class ManorOps
    {
        public const int GateY = 15;
        public static readonly int[] GateX = { 11, 12 };
        public const int MainHallX = 10;
        public const int MainHallY = 6;

        public static BuildingDef Def(GameData d, string defId) => defId != null && d.Buildings.TryGetValue(defId, out var b) ? b : null;

        public static ManorState CreateDefault(GameData d)
        {
            var m = new ManorState();
            if (d.Buildings.ContainsKey(BuildingValidator.MainHall))
                m.Buildings.Add(new BuildingInstance
                {
                    Id = "b" + (m.NextBuildingNumber++).ToString("D4"), DefId = BuildingValidator.MainHall, X = MainHallX, Y = MainHallY,
                    CompleteMonth = int.MinValue / 2,
                });
            return m;
        }

        /// <summary>플레이어가 이끄는 세력에 장원이 없으면 만든다 (시나리오 시작, 문파 창설, 후계, 저장 이관).</summary>
        public static void EnsurePlayerManor(WorldState w, GameData d)
        {
            if (d.Buildings.Count == 0) return;
            var p = w.Player;
            var f = p == null ? null : Wallet.LedFaction(w, p);
            if (f != null && f.Manor == null) f.Manor = CreateDefault(d);
        }

        public static bool IsComplete(WorldState w, BuildingInstance b) => b.CompleteMonth <= w.Month;

        public static bool IsGate(int x, int y) => y == GateY && Array.IndexOf(GateX, x) >= 0;

        public static BuildingInstance At(ManorState m, GameData d, int x, int y)
        {
            foreach (var b in m.Buildings)
            {
                var def = Def(d, b.DefId);
                if (def != null && x >= b.X && x < b.X + def.Width && y >= b.Y && y < b.Y + def.Height) return b;
            }
            return null;
        }

        public static ActionCheck CanPlace(WorldState w, GameData d, Faction f, string defId, int x, int y)
        {
            if (f == null || f.IsDissolved || f.Manor == null) return ActionCheck.Fail("세력을 이끌 때만 지을 수 있습니다");
            var def = Def(d, defId);
            if (def == null || def.Fixed) return ActionCheck.Fail("지을 수 없는 건물입니다");
            var m = f.Manor;
            if (x < 0 || y < 0 || x + def.Width > m.Width || y + def.Height > m.Height) return ActionCheck.Fail("장원 밖입니다");
            for (int i = x; i < x + def.Width; i++)
                for (int j = y; j < y + def.Height; j++)
                {
                    if (IsGate(i, j)) return ActionCheck.Fail("정문 자리는 비워 두어야 합니다");
                    if (At(m, d, i, j) != null) return ActionCheck.Fail("다른 건물과 겹칩니다");
                }
            if (m.Buildings.Count(b => b.DefId == defId) >= def.Max) return ActionCheck.Fail($"{Josa.EunNeun(def.Name)} {def.Max}개까지 지을 수 있습니다");
            if (f.Silver < def.Cost) return ActionCheck.Fail("은자가 부족합니다");
            return ActionCheck.Pass;
        }

        /// <summary>CanPlace가 통과했을 때만 부른다.</summary>
        public static BuildingInstance Place(WorldState w, GameData d, Faction f, string defId, int x, int y)
        {
            var def = Def(d, defId);
            f.Silver -= def.Cost;
            var b = new BuildingInstance
            {
                Id = "b" + (f.Manor.NextBuildingNumber++).ToString("D4"), DefId = defId, X = x, Y = y, CompleteMonth = w.Month + def.BuildMonths,
            };
            f.Manor.Buildings.Add(b);
            return b;
        }

        public static ActionCheck CanDemolish(GameData d, Faction f, string buildingId)
        {
            var b = f?.Manor?.Buildings.FirstOrDefault(x => x.Id == buildingId);
            if (b == null) return ActionCheck.Fail("없는 건물입니다");
            if (Def(d, b.DefId)?.Fixed == true) return ActionCheck.Fail("본당은 허물 수 없습니다");
            return ActionCheck.Pass;
        }

        /// <summary>환불액을 돌려준다. 미완공이면 전액, 완공이면 절반.</summary>
        public static int Demolish(WorldState w, GameData d, Faction f, string buildingId)
        {
            var b = f.Manor.Buildings.First(x => x.Id == buildingId);
            var def = Def(d, b.DefId);
            int refund = def == null ? 0 : IsComplete(w, b) ? def.Cost / 2 : def.Cost;
            f.Manor.Buildings.Remove(b);
            f.Silver += refund;
            return refund;
        }

        /// <summary>완공된 건물 보너스의 합. 장원이 없으면 빈 값.</summary>
        public static BuildingBonuses Bonuses(WorldState w, GameData d, Faction f)
        {
            var sum = new BuildingBonuses();
            if (f?.Manor == null) return sum;
            foreach (var b in f.Manor.Buildings)
                if (IsComplete(w, b)) sum.Add(Def(d, b.DefId)?.Bonuses);
            return sum;
        }

        static readonly BuildingBonuses None = new BuildingBonuses();

        public static BuildingBonuses BonusesFor(WorldState w, GameData d, Character c)
        {
            var f = w.GetFaction(c.FactionId);
            return f == null || f.IsDissolved || f.Manor == null ? None : Bonuses(w, d, f);
        }

        /// <summary>이름 있는 인물 정원. 장원이 없으면 제한 없음.</summary>
        public static int Capacity(WorldState w, GameData d, Faction f) =>
            f?.Manor == null ? int.MaxValue : Bonuses(w, d, f).Capacity;

        /// <summary>정원이 찼으면 사유, 아니면 null.</summary>
        public static string CapacityBlock(WorldState w, GameData d, Faction f)
        {
            if (f?.Manor == null) return null;
            return FactionOps.LivingMembers(w, f).Count() >= Capacity(w, d, f) ? "숙소가 모자랍니다" : null;
        }
    }
}
