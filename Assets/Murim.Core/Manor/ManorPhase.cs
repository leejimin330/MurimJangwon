using System.Linq;
using Murim.Core.Simulation;
using Murim.Core.Story;

namespace Murim.Core.Manor
{
    /// <summary>장원 단계: 이번 달 처리로 완공되는 건물을 연대기에 남기고, 건물 수입을 세력 금고에 넣는다.</summary>
    public class ManorPhase : ITurnPhase
    {
        public void Run(TurnContext t)
        {
            var w = t.World;
            foreach (var f in w.Factions.Values.Where(f => !f.IsDissolved && f.Manor != null))
            {
                // 월 처리가 끝나면 다음 달이 되므로, 다음 달에 완공되는 건물을 이번에 알린다.
                foreach (var b in f.Manor.Buildings.Where(b => b.CompleteMonth == w.Month + 1))
                {
                    var def = ManorOps.Def(t.Data, b.DefId);
                    // 담장은 한 칸씩 지으므로 연대기에 남기지 않는다.
                    if (def != null && def.Id != "wall") Chronicle.BuildingCompleted(w, f, def.Name);
                }
                f.Silver += ManorOps.Bonuses(w, t.Data, f).IncomePerMonth;
            }
        }
    }
}
