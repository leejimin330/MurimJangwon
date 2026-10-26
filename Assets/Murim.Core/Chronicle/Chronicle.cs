using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Story
{
    /// <summary>연대기 기록과 문장 템플릿. 날짜 접두는 화면에서 붙인다.</summary>
    public static class Chronicle
    {
        public static void Add(WorldState w, string kind, int importance, string text, params string[] involvedIds) =>
            AddWithDetail(w, kind, importance, text, null, involvedIds);

        /// <summary>detail은 결투 경과처럼 펼쳐 볼 줄. 비었으면 남기지 않는다.</summary>
        public static void AddWithDetail(WorldState w, string kind, int importance, string text, string[] detail, params string[] involvedIds)
        {
            w.Chronicle.Add(new ChronicleEntry
            {
                Month = w.Month, Kind = kind, Importance = importance, Text = text, InvolvedIds = involvedIds,
                Detail = detail != null && detail.Length > 0 ? detail : null,
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

        public static void Sparred(WorldState w, Character actor, Character target, bool won, string[] detail = null) =>
            AddWithDetail(w, nameof(Sparred), actor.Id == w.PlayerCharacterId ? 1 : 0, won
                ? $"{Josa.IGa(actor.FullName)} 비무에서 {Josa.EulReul(T(w, target))} 꺾었다."
                : $"{Josa.IGa(actor.FullName)} 비무에서 {T(w, target)}에게 패하였다.", detail, actor.Id, target.Id);

        public static void GuestRequest(WorldState w, Character actor, Faction f, bool accepted) =>
            Add(w, nameof(GuestRequest), accepted ? 1 : 0, accepted
                ? $"{Josa.IGa(actor.FullName)} {f.Name}의 객경이 되었다."
                : $"{Josa.EunNeun(f.Name)} {actor.FullName}의 의탁을 받아들이지 않았다.", actor.Id, f.Id);

        public static void SectFounded(WorldState w, Character founder, Faction f) =>
            Add(w, nameof(SectFounded), 2, $"{Josa.IGa(founder.FullName)} {Josa.EulReul(f.Name)} 세웠다.", founder.Id, f.Id);

        public static void LeftFaction(WorldState w, Character c, Faction f) =>
            Add(w, nameof(LeftFaction), 1, $"{Josa.IGa(c.FullName)} {Josa.EulReul(f?.Name ?? "세력")} 떠났다.", c.Id);

        public static void Born(WorldState w, Character mother, Character father, Character child) =>
            Add(w, nameof(Born), child.FactionId != null && child.FactionId == w.Player?.FactionId ? 1 : 0,
                $"{Josa.WaGwa(father.FullName)} {mother.FullName} 사이에서 {Josa.IGa(child.FullName)} 태어났다.",
                child.Id, father.Id, mother.Id);

        public static void Died(WorldState w, Character c)
        {
            string how = c.DeathCause switch
            {
                "노환" => "노환으로", "사고" => "뜻밖의 사고로", "병" => "병으로", null => "병으로", _ => Josa.EuroRo(c.DeathCause),
            };
            int importance = c.Role == Data.Role.Leader || c.Id == w.PlayerCharacterId ? 2 : 1;
            Add(w, nameof(Died), importance, $"{Josa.IGa(T(w, c))} {how} 세상을 떠났다. (향년 {c.AgeYears(w.Month)}세)", c.Id);
        }

        public static void Succeeded(WorldState w, Faction f, Character successor, Character previous) =>
            Add(w, nameof(Succeeded), 2, previous == null
                ? $"{Josa.IGa(successor.FullName)} {f.Name}의 새 {Josa.IGa(Titles.Role(f, Data.Role.Leader))} 되었다."
                : $"{Josa.IGa(successor.FullName)} {previous.FullName}의 뒤를 이어 {f.Name}의 {Josa.IGa(Titles.Role(f, Data.Role.Leader))} 되었다.",
                f.Id, successor.Id);

        public static void Deserted(WorldState w, Faction f, Character deserter, Character newLeader) =>
            Add(w, nameof(Deserted), 1, $"{Josa.IGa(deserter.FullName)} 새 {Titles.Role(f, Data.Role.Leader)} {Josa.EulReul(newLeader.FullName)} 따를 수 없다며 {Josa.EulReul(f.Name)} 떠났다.",
                f.Id, deserter.Id);

        public static void AlignmentShifted(WorldState w, Faction f, Data.Alignment from)
        {
            string text;
            if (f.Alignment == Data.Alignment.Demonic) text = $"{Josa.IGa(f.Name)} 마도에 몸을 담갔다.";
            else if (from == Data.Alignment.Demonic) text = $"{Josa.IGa(f.Name)} 마도를 버리고 {(f.Alignment == Data.Alignment.Righteous ? "정파" : "사파")}로 돌아섰다.";
            else if (f.Alignment == Data.Alignment.Unorthodox) text = $"{Josa.IGa(f.Name)} 정도를 버리고 사파의 길로 들어섰다.";
            else text = $"{Josa.IGa(f.Name)} 과거를 씻고 정파로 거듭났다.";
            Add(w, nameof(AlignmentShifted), 2, text, f.Id);
        }

        public static void Usurped(WorldState w, Faction f, Character usurper, Character previous) =>
            Add(w, nameof(Usurped), 2, $"{Josa.IGa(usurper.FullName)} {Josa.EulReul(previous.FullName)} 몰아내고 {f.Name}의 {Josa.IGa(Titles.Role(f, Data.Role.Leader))} 되었다.",
                f.Id, usurper.Id, previous.Id);

        /// <summary>사건 한 건의 기록. kind는 "Event:{사건 id}".</summary>
        public static void EventLine(WorldState w, string kind, int importance, string text, string[] detail, params string[] involvedIds) =>
            AddWithDetail(w, kind, importance, text, detail, involvedIds);

        public static void Breakthrough(WorldState w, Character c) =>
            Add(w, nameof(Breakthrough), c.Realm >= 4 || c.Id == w.PlayerCharacterId ? 2 : 1,
                $"{Josa.IGa(c.FullName)} {Titles.Realm(c.Realm)}의 경지에 올랐다.", c.Id);

        public static void QiDeviation(WorldState w, Character c, bool died) =>
            Add(w, nameof(QiDeviation), c.Id == w.PlayerCharacterId ? 2 : 1, died
                ? $"{Josa.IGa(c.FullName)} 돌파를 꾀하다 주화입마에 빠져 숨을 거두었다."
                : $"{Josa.IGa(c.FullName)} 돌파를 꾀하다 주화입마에 빠졌다.", c.Id);

        public static void TrainingStarted(WorldState w, Character c, int months) =>
            Add(w, nameof(TrainingStarted), 1, $"{Josa.IGa(c.FullName)} {months}개월의 폐관에 들어갔다.", c.Id);

        public static void ArtTaught(WorldState w, Character master, Character student, string artName) =>
            Add(w, nameof(ArtTaught), master.Id == w.PlayerCharacterId ? 1 : 0,
                $"{Josa.IGa(master.FullName)} {student.FullName}에게 {Josa.EulReul(artName)} 전수하였다.", master.Id, student.Id);

        public static void TrainingEnded(WorldState w, Character c, int months) =>
            Add(w, nameof(TrainingEnded), 1, $"{Josa.IGa(c.FullName)} {months}개월의 폐관을 마치고 세상에 나왔다.", c.Id);

        public static void BuildingCompleted(WorldState w, Faction f, string buildingName) =>
            Add(w, nameof(BuildingCompleted), 1, $"{f.Name}의 장원에 {Josa.IGa(buildingName)} 들어섰다.", f.Id);

        public static void RaidResolved(WorldState w, string text, string[] detail, params string[] involvedIds) =>
            AddWithDetail(w, "Raid", 2, text, detail, involvedIds);

        public static void GameOver(WorldState w, Character last) =>
            Add(w, nameof(GameOver), 2, $"{Josa.EulReul(last.FullName)} 끝으로 가문의 대가 끊겼다.", last.Id);

        public static void ActionSkipped(WorldState w, Character actor, string description, string reason) =>
            Add(w, nameof(ActionSkipped), 1, $"{description} — 하지 못했다: {reason}", actor.Id);
    }
}
