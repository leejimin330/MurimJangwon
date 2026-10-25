using Murim.Core.Text;
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

        static string T(WorldState w, Character c) => Titles.Full(w, c);

        public static void Dissolved(WorldState w, Faction f, string reason) =>
            Add(w, nameof(Dissolved), 2, $"{Josa.IGa(f.Name)} 문을 닫았다. ({reason})", f.Id);

        public static void Married(WorldState w, Character a, Character b) =>
            Add(w, nameof(Married), 1, $"{Josa.WaGwa(T(w, a))} {Josa.IGa(T(w, b))} 혼인하였다.", a.Id, b.Id);

        public static void MarriageRejected(WorldState w, Character proposer, Character target) =>
            Add(w, nameof(MarriageRejected), 1, $"{Josa.EunNeun(T(w, target))} {T(w, proposer)}의 혼담을 거절하였다.",
                proposer.Id, target.Id);

        public static void Allied(WorldState w, Faction a, Faction b) =>
            Add(w, nameof(Allied), 2, $"{Josa.WaGwa(a.Name)} {Josa.IGa(b.Name)} 동맹을 맺었다.", a.Id, b.Id);

        public static void AllianceRejected(WorldState w, Faction a, Faction b) =>
            Add(w, nameof(AllianceRejected), 0, $"{Josa.EunNeun(b.Name)} {a.Name}의 동맹 제안을 거절하였다.", a.Id, b.Id);

        public static void Insulted(WorldState w, Character actor, Character target) =>
            Add(w, nameof(Insulted), 1, $"{Josa.IGa(T(w, actor))} {Josa.EulReul(T(w, target))} 공개적으로 모욕하였다.",
                actor.Id, target.Id);

        public static void Dismissed(WorldState w, Faction f, Character c) =>
            Add(w, nameof(Dismissed), 1, $"{Josa.IGa(f.Name)} {Josa.EulReul(c.FullName)} 파문하였다.", f.Id, c.Id);

        public static void HeirDesignated(WorldState w, Faction f, Character heir) =>
            Add(w, nameof(HeirDesignated), 1, $"{Josa.IGa(f.Name)} {Josa.EulReul(heir.FullName)} 후계자로 정하였다.", f.Id, heir.Id);

        public static void Plundered(WorldState w, Faction f, string regionName) =>
            Add(w, nameof(Plundered), 1, $"{Josa.IGa(f.Name)} {regionName} 일대를 약탈하였다.", f.Id);

        public static void ActionSkipped(WorldState w, Character actor, string description, string reason) =>
            Add(w, nameof(ActionSkipped), 1, $"{description} — 하지 못했다: {reason}", actor.Id);
    }
}
