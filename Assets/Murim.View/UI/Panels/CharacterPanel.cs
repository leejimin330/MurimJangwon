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
    /// <summary>초상화, 능력치, 특성, 호감도와 근거, 관계, 기억, 그리고 이 인물에게 할 수 있는 일.</summary>
    public static class CharacterPanel
    {
        // 어두운 바탕에서 읽히도록 성향 색보다 밝게.
        static readonly Color Good = Theme.Hex("#8DB0D8");
        static readonly Color Bad = Theme.Hex("#D27A70");

        public static void Build(RectTransform panel, Bootstrap app, INav nav, string characterId)
        {
            var s = app.Session;
            var w = s.World;
            var d = s.Data;
            var c = w.Get(characterId);
            if (c == null) return;
            var player = s.Player;
            var faction = w.GetFaction(c.FactionId);

            var head = UiFactory.Row(panel, 18, 128);
            Portrait.Build(head, c, w.Month, 120);
            var col = UiFactory.Rect(head, "Col");
            col.Size(-1, 120, 1);
            UiFactory.VStack(col, 4).childAlignment = TextAnchor.MiddleLeft;
            string mark = c.Id == player?.Id ? " (나)" : "";
            UiFactory.Label(col, c.FullName + mark, Theme.HeadingSize + 4, c.IsAlive ? Theme.Ink : Theme.InkFaint, true).Size(-1, 50);
            string life = c.IsAlive
                ? $"{c.AgeYears(w.Month)}세"
                : $"향년 {c.AgeYears(w.Month)}세 ({GameDate.Year(c.BirthMonth)}년 ~ {GameDate.Year(c.DeathMonth.Value)}년, {c.DeathCause})";
            UiFactory.Label(col, $"{(c.Sex == Sex.Male ? "남" : "여")}, {life}, {Titles.Realm(c.Realm)}", Theme.BodySize, Theme.InkDim)
                .Size(-1, 30);
            UiFactory.Label(col, faction == null ? "무소속" : $"{faction.Name} {Titles.Role(faction, c.Role)}", Theme.BodySize, Theme.InkDim)
                .Size(-1, 30);

            var list = UiFactory.ScrollList(panel, 8);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;

            if (faction != null) FactionPanel.Link(list, faction.Name + " 보기", () => nav.ShowFaction(faction.Id));
            if (c.LocationRegionId != null && d.Regions.TryGetValue(c.LocationRegionId, out var region))
                FactionPanel.Link(list, "지금 " + region.Name + "에 있음", () => nav.ShowRegion(region.Id));
            FactionPanel.Info(list, $"근골 {c.Constitution}   오성 {c.Insight}   심계 {c.Cunning}   매력 {c.Charisma}", Theme.Ink);
            if (faction == null || c.Role == Role.Leader)
                FactionPanel.Info(list, $"은자 {Wallet.Silver(w, c):N0}냥, 명성 {Wallet.Fame(w, c)}");

            Traits(list, d, c);
            if (player != null && player.Id != c.Id && c.IsAlive) OpinionLine(list, s, c, player);
            if (c.IsAlive) BuildActions(list, app, nav, c);
            Relations(list, w, nav, c);
            Memories(list, w, c);
        }

        static void Traits(RectTransform list, GameData d, Character c)
        {
            FactionPanel.Heading(list, "특성");
            if (c.TraitIds.Count == 0)
            {
                FactionPanel.Info(list, "눈에 띄는 특성이 없다");
                return;
            }
            var row = ActionMenu.Grid(list, 150);
            foreach (var id in c.TraitIds)
            {
                d.Traits.TryGetValue(id, out var def);
                var chip = UiFactory.Panel(row, Theme.PanelRaised, "Trait");
                UiFactory.Label(chip.transform, def?.Name ?? id, Theme.BodySize, Theme.Gold, align: TextAnchor.MiddleCenter)
                    .rectTransform.Fill(8, 0, 8, 0);
                if (def != null && !string.IsNullOrEmpty(def.Description)) Tooltip.Attach(chip.gameObject, def.Description);
            }
        }

        static void OpinionLine(RectTransform list, Murim.Core.Session.GameSession s, Character c, Character player)
        {
            var w = s.World;
            int total = Opinion.Of(w, s.Data, c.Id, player.Id);
            int traits = Opinion.TraitAffinity(s.Data, c, player);
            int stored = w.Opinions.GetStored(c.Id, player.Id);
            int memory = Opinion.MemoryTotal(c, player.Id);
            var color = total > 0 ? Good : total < 0 ? Bad : Theme.InkDim;
            FactionPanel.Info(list, $"나에 대한 호감 {Signed(total)}", color);
            FactionPanel.Info(list, $"   특성 상성 {Signed(traits)}, 쌓인 감정 {Signed(stored)}, 기억 {Signed(memory)}");
        }

        static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        static void BuildActions(RectTransform list, Bootstrap app, INav nav, Character c)
        {
            var s = app.Session;
            var w = s.World;
            var me = s.Player;
            var grid = ActionMenu.Grid(list);
            UiFactory.Button(grid, "가계도", () => nav.ShowFamily(c.Id));
            if (me == null || me.Id == c.Id) return;

            var own = Wallet.LedFaction(w, me);
            if (own != null && c.FactionId == own.Id)
            {
                if (own.HeirId != c.Id) ActionMenu.Add(grid, app, "후계자로 정하기", new DesignateHeir(me.Id, c.Id));
                ActionMenu.Add(grid, app, "파문하기", new Dismiss(me.Id, c.Id));
                if (!MarriageRules.IsMarried(w, c)) ActionMenu.Opener(grid, app, "짝 지어 주기", () => PickInternalSpouse(app, own, c));
            }
            if (PersonalRules.UsesPersonalActions(w, me) && PersonalRules.IsNearbyAdult(w, me, c))
            {
                ActionMenu.Add(grid, app, "비무 신청", new Spar(me.Id, c.Id));
                ActionMenu.Add(grid, app, $"교분 ({Befriend.Cost}냥)", new Befriend(me.Id, c.Id));
            }
        }

        static void PickInternalSpouse(Bootstrap app, Faction own, Character c)
        {
            var s = app.Session;
            var w = s.World;
            var opts = new List<Picker.Option>();
            foreach (var o in FactionOps.LivingMembers(w, own))
            {
                if (o.Id == c.Id || !MarriageRules.Eligible(w, c, o).Ok) continue;
                opts.Add(ActionMenu.Option(app, o.FullName, new ArrangeInternalMarriage(s.Player.Id, c.Id, o.Id),
                    $"{Titles.Role(own, o.Role)}, {o.AgeYears(w.Month)}세"));
            }
            Picker.Show(app, $"{c.FullName}의 짝", opts, "세력 안에 짝이 될 만한 사람이 없습니다");
        }

        static void Relations(RectTransform list, WorldState w, INav nav, Character c)
        {
            var rows = new List<(string label, Character who)>();
            void Add(string label, string id)
            {
                var who = w.Get(id);
                if (who != null) rows.Add((label, who));
            }
            Add(c.Sex == Sex.Male ? "아내" : "남편", c.SpouseId);
            Add("아버지", c.FatherId);
            Add("어머니", c.MotherId);
            foreach (var id in c.ChildIds)
            {
                var child = w.Get(id);
                if (child != null) rows.Add((child.Sex == Sex.Male ? "아들" : "딸", child));
            }
            foreach (var pair in c.Relations)
            {
                if (pair.Key == c.SpouseId) continue;
                var who = w.Get(pair.Key);
                if (who == null) continue;
                var tags = pair.Value.Where(t => t != RelationTag.Spouse).Select(TagName).ToList();
                if (tags.Count > 0) rows.Add((string.Join("·", tags), who));
            }

            FactionPanel.Heading(list, "관계");
            if (rows.Count == 0)
            {
                FactionPanel.Info(list, "알려진 관계가 없다");
                return;
            }
            foreach (var (label, who) in rows)
            {
                var id = who.Id;
                string detail = who.IsAlive ? $"{who.AgeYears(w.Month)}세" : "사망";
                FactionPanel.Link(list, $"{label}  {who.FullName}", () => nav.ShowCharacter(id), detail);
            }
        }

        static string TagName(RelationTag t) => t switch
        {
            RelationTag.Master => "사부",
            RelationTag.Disciple => "제자",
            RelationTag.Lover => "연인",
            RelationTag.Spouse => "배우자",
            RelationTag.Rival => "맞수",
            RelationTag.Nemesis => "숙적",
            RelationTag.Benefactor => "은인",
            _ => "의형제",
        };

        static void Memories(RectTransform list, WorldState w, Character c)
        {
            FactionPanel.Heading(list, "기억");
            if (c.Memories.Count == 0)
            {
                FactionPanel.Info(list, "마음에 남은 원한도 은혜도 없다");
                return;
            }
            foreach (var m in c.Memories.OrderByDescending(m => m.Strength))
            {
                bool grudge = m.Kind == MemoryKind.Grudge;
                string target = w.Get(m.TargetId)?.FullName ?? w.GetFaction(m.TargetId)?.Name ?? "누군가";
                FactionPanel.Info(list, $"{(grudge ? "원한" : "은혜")}  {target}: {m.Reason} (강도 {m.Strength}, {GameDate.Format(m.Month)})",
                    grudge ? Bad : Good);
            }
        }
    }
}
