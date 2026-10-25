using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.Text;
using Murim.Core.World;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    public interface INav
    {
        void ShowRegion(string regionId);
        void ShowFaction(string factionId);
        void ShowCharacter(string characterId);
        void ShowFamily(string characterId);
    }

    /// <summary>세력 정보, 멤버, 그리고 플레이어가 장문인일 때 할 수 있는 행동.</summary>
    public static class FactionPanel
    {
        public static void Build(RectTransform panel, Bootstrap app, INav nav, string factionId)
        {
            var s = app.Session;
            var w = s.World;
            var f = w.GetFaction(factionId);
            if (f == null) return;
            var leader = FactionOps.Leader(w, f);
            var player = s.Player;
            var own = player == null ? null : Wallet.LedFaction(w, player);

            var head = UiFactory.Row(panel, 14, 54);
            UiFactory.Panel(head, Theme.Of(f.Alignment), "Chip").Size(8, 44);
            UiFactory.Label(head, f.Name, Theme.HeadingSize + 4, Theme.Ink, true).Size(-1, 54, 1);

            var list = UiFactory.ScrollList(panel, 8);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;

            string kind = f.Type == FactionType.Clan ? "세가" : "문파";
            Info(list, $"{Theme.AlignmentName(f.Alignment)} {kind}, 협의 {f.Chivalry}" + (f.IsDissolved ? " (해산됨)" : ""));
            var region = s.Data.Regions[f.HomeRegionId];
            Link(list, "본거지 " + region.Name, () => nav.ShowRegion(region.Id));
            Info(list, $"은자 {f.Silver:N0}냥, 명성 {f.Fame}, 문도 {f.UnnamedDisciples}명, 영향력 합 {Influence.Total(w, f.Id)}"
                + (f.Debt > 0 ? $", 빚 {f.Debt:N0}냥" : ""));
            var allies = w.Factions.Values.Where(o => o.Id != f.Id && w.AreAllied(f.Id, o.Id)).ToList();
            if (allies.Count > 0) Info(list, "동맹: " + string.Join(", ", allies.Select(a => a.Name)));
            if (own != null && own.Id != f.Id) Info(list, $"우리와의 관계 점수 {w.AllianceScore(own.Id, f.Id)} (동맹에는 30 필요)");
            if (leader != null && player != null && leader.Id != player.Id)
                Info(list, $"{Titles.Role(f, Role.Leader)}의 우리에 대한 호감 {Opinion.Of(w, s.Data, leader.Id, player.Id)}");

            if (own != null && !f.IsDissolved) BuildActions(list, app, f, own, leader);

            Heading(list, $"사람들 ({f.MemberIds.Count})");
            var members = FactionOps.LivingMembers(w, f)
                .OrderBy(m => RoleOrder(m.Role)).ThenBy(m => m.BirthMonth).ToList();
            foreach (var m in members)
            {
                var id = m.Id;
                string mark = m.Id == player?.Id ? " (나)" : "";
                Link(list, $"{Titles.Role(f, m.Role)} {m.FullName}{mark}",
                    () => nav.ShowCharacter(id), $"{m.AgeYears(w.Month)}세, {Titles.Realm(m.Realm)}");
            }
        }

        static int RoleOrder(Role r) => r switch
        {
            Role.Leader => 0, Role.Heir => 1, Role.Elder => 2, Role.Family => 3, Role.Disciple => 4, Role.Guest => 5, _ => 6,
        };

        static void BuildActions(RectTransform list, Bootstrap app, Faction f, Faction own, Character leader)
        {
            var s = app.Session;
            var w = s.World;
            var me = s.Player;
            Heading(list, "할 수 있는 일");
            var grid = ActionMenu.Grid(list);
            if (f.Id == own.Id)
            {
                ActionMenu.Add(grid, app, "제자 받기 (20냥)", new RecruitNamed(me.Id));
                ActionMenu.Add(grid, app, "문도 5명 (25냥)", new RecruitUnnamed(me.Id, 5));
                ActionMenu.Add(grid, app, "문도 10명 (50냥)", new RecruitUnnamed(me.Id, 10));
                ActionMenu.Opener(grid, app, "영향력 확장 (100냥)", () => PickExpand(app));
                ActionMenu.Add(grid, app, "의뢰 수행", new DoQuest(me.Id, false));
                if (f.Alignment != Alignment.Righteous) ActionMenu.Add(grid, app, "약탈", new DoQuest(me.Id, true));
                if (f.Debt > 0)
                {
                    int pay = System.Math.Min(f.Debt, System.Math.Min(f.Silver, 200));
                    ActionMenu.Add(grid, app, $"빚 {pay}냥 갚기", new RepayDebt(me.Id, System.Math.Max(1, pay)));
                }
                return;
            }
            ActionMenu.Opener(grid, app, "혼담 넣기 (50냥)", () => PickMarriage(app, f));
            ActionMenu.Add(grid, app, "동맹 제안 (명성 5)", new ProposeAlliance(me.Id, f.Id));
            ActionMenu.Add(grid, app, "선물 30냥", new SendGift(me.Id, f.Id, 30));
            ActionMenu.Add(grid, app, "선물 100냥", new SendGift(me.Id, f.Id, 100));
            ActionMenu.Add(grid, app, "선물 200냥", new SendGift(me.Id, f.Id, 200));
            ActionMenu.Add(grid, app, "모욕하기", new Insult(me.Id, f.Id));
        }

        static void PickExpand(Bootstrap app)
        {
            var s = app.Session;
            var me = s.Player;
            var opts = new List<Picker.Option>();
            foreach (var r in s.Data.Regions.Values.OrderBy(r => r.Y))
            {
                var a = new ExpandInfluence(me.Id, r.Id);
                var c = a.CanExecute(s.World, s.Data);
                if (!ExpandInfluence.CanReach(s.World, s.Data, Wallet.LedFaction(s.World, me), r.Id)) continue;
                opts.Add(new Picker.Option(r.Name, () => ActionMenu.Queue(app, a),
                    $"우리 영향력 {Influence.Get(s.World, r.Id, me.FactionId)}, 남은 몫 {Influence.Free(s.World, r.Id)}", c.Ok, c.Reason));
            }
            Picker.Show(app, "어디로 세력을 넓힐까요", opts);
        }

        /// <summary>우리 쪽 인물 → 상대 인물 → 어느 쪽으로 갈지 순서로 고른다.</summary>
        static void PickMarriage(Bootstrap app, Faction target)
        {
            var s = app.Session;
            var w = s.World;
            var me = s.Player;
            var own = Wallet.LedFaction(w, me);
            var ours = FactionOps.LivingMembers(w, own).Where(c => c.AgeYears(w.Month) >= 16 && !MarriageRules.IsMarried(w, c)).ToList();
            var opts = ours.Select(o => new Picker.Option(o.FullName, () => PickTheirs(app, target, o),
                $"{Titles.Role(own, o.Role)}, {o.AgeYears(w.Month)}세")).ToList();
            Picker.Show(app, "우리 쪽에서 누구를 보낼까요", opts, "혼인할 수 있는 사람이 없습니다");
        }

        static void PickTheirs(Bootstrap app, Faction target, Character ours)
        {
            var s = app.Session;
            var w = s.World;
            var opts = new List<Picker.Option>();
            foreach (var t in FactionOps.LivingMembers(w, target))
            {
                if (!MarriageRules.Eligible(w, ours, t).Ok) continue;
                var theirs = t;
                opts.Add(new Picker.Option(t.FullName, () => PickDirection(app, ours, theirs),
                    $"{Titles.Role(target, t.Role)}, {t.AgeYears(w.Month)}세, {Titles.Realm(t.Realm)}"));
            }
            Picker.Show(app, $"{target.Name}의 누구와 맺어 줄까요", opts, "짝이 될 만한 사람이 없습니다");
        }

        static void PickDirection(Bootstrap app, Character ours, Character theirs)
        {
            var s = app.Session;
            var me = s.Player;
            var join = new ProposeMarriage(me.Id, ours.Id, theirs.Id, true);
            var leave = new ProposeMarriage(me.Id, ours.Id, theirs.Id, false);
            var cj = join.CanExecute(s.World, s.Data);
            var cl = leave.CanExecute(s.World, s.Data);
            Picker.Show(app, "혼인 후 어디서 살까요", new[]
            {
                new Picker.Option($"{Josa.IGa(theirs.FullName)} 우리에게 온다", () => ActionMenu.Queue(app, join), null, cj.Ok, cj.Reason),
                new Picker.Option($"{Josa.IGa(ours.FullName)} 저쪽으로 간다", () => ActionMenu.Queue(app, leave), null, cl.Ok, cl.Reason),
            });
        }

        public static void Heading(RectTransform list, string text) =>
            UiFactory.Label(list, text, Theme.SubheadingSize, Theme.Ink, true).Size(-1, 46);

        public static void Info(RectTransform list, string text, Color? color = null) =>
            UiFactory.Label(list, text, Theme.BodySize, color ?? Theme.InkDim);

        public static void Link(RectTransform list, string text, System.Action onClick, string detail = null)
        {
            var row = UiFactory.Panel(list, Theme.PanelRaised, "Link");
            row.raycastTarget = true;
            row.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onClick());
            row.Size(-1, 44);
            var inner = UiFactory.Rect(row.transform, "Inner").Fill(14, 0, 14, 0);
            UiFactory.HStack(inner, 10);
            UiFactory.Label(inner, text, Theme.BodySize, Theme.Ink).Size(-1, 44, 1);
            if (detail != null) UiFactory.Label(inner, detail, Theme.SmallSize, Theme.InkDim, align: TextAnchor.MiddleRight).Size(200);
        }
    }
}
