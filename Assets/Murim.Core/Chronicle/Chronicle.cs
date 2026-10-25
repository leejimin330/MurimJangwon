using Murim.Core.World;

namespace Murim.Core.Story
{
    /// <summary>연대기 기록과 문장 템플릿. 날짜 접두는 화면에서 붙인다.</summary>
    public static class Chronicle
    {
        public static void Add(WorldState w, string kind, int importance, string text, params string[] involvedIds)
        {
            w.Chronicle.Add(new ChronicleEntry
            {
                Month = w.Month, Kind = kind, Importance = importance, Text = text, InvolvedIds = involvedIds,
            });
        }

        public static void Dissolved(WorldState w, Faction f, string reason) =>
            Add(w, nameof(Dissolved), 2, $"{Text.Josa.IGa(f.Name)} 문을 닫았다. ({reason})", f.Id);
    }
}
