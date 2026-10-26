using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class TrainingActionTests
    {
        static GameData D => TestPaths.LoadShippedData();

        /// <summary>소식은 확인하고, 답할 사건은 고를 수 있는 첫 선택지로 답한다. 남길 사건 id는 그대로 둔다.</summary>
        static void ClearPending(GameSession s, string keep = null)
        {
            for (int guard = 0; guard < 20; guard++)
            {
                int i = s.PendingEvents.ToList().FindIndex(p => p.EventId != keep || keep == null);
                if (i < 0 || s.PendingEvents.Count == 0) return;
                if (keep != null && s.PendingEvents[i].EventId == keep) return;
                int choice = Enumerable.Range(0, 5).FirstOrDefault(c => s.CanChoose(i, c).Ok);
                s.Choose(i, choice);
            }
        }

        [Test]
        public void ClosedTraining_BlocksAndEndsWithEvent()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 44 });
            var me = s.Player;
            ClearPending(s);
            int enlightenment = me.Enlightenment;
            Assert.That(s.Queue(new ClosedTraining(me.Id, 3)).Ok);
            Assert.That(s.RemainingActions, Is.Zero, "폐관은 이번 달 행동을 모두 쓴다");

            s.EndTurn();
            Assert.That(me.InTraining(s.World.Month), Is.True);
            Assert.That(s.RemainingActions, Is.Zero);
            Assert.That(s.PendingEvents.Where(p => !p.IsNotice), Is.Empty, "폐관 중에는 이야기꾼 사건이 없다");
            ClearPending(s);
            s.EndTurn();
            Assert.That(s.RemainingActions, Is.Zero);
            Assert.That(s.PendingEvents.Where(p => !p.IsNotice), Is.Empty);
            ClearPending(s);
            s.EndTurn();

            Assert.That(s.PendingEvents.Count(p => p.EventId == CultivationPhase.TrainingEndEvent), Is.EqualTo(1), "출관 사건");
            Assert.That(me.InTraining(s.World.Month), Is.False);
            Assert.That(me.Enlightenment, Is.GreaterThanOrEqualTo(System.Math.Min(100, enlightenment + 15)));
            Assert.That(s.RemainingActions, Is.GreaterThan(0));
        }

        [Test]
        public void ClosedTraining_InvalidMonths_Fail()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 2 });
            var check = new ClosedTraining(s.Player.Id, 4).CanExecute(s.World, D);
            Assert.That(check.Ok, Is.False);
            Assert.That(check.Reason, Does.Contain("3·6·12개월"));
        }

        [Test]
        public void TeachArt_CooldownAndRequires()
        {
            var w = TestWorlds.Empty(D);
            TestWorlds.AddFaction(w, "f", Alignment.Righteous, "hubei", 10);
            var leader = TestWorlds.AddCharacter(w, "f", Role.Leader, Sex.Male, 50);
            leader.Arts.Add(new KnownArt { ArtId = "taiji_sword", Mastery = 8 });
            leader.Arts.Add(new KnownArt { ArtId = "taiji_fist", Mastery = 8 });
            var student = TestWorlds.AddCharacter(w, "f", Role.Disciple, Sex.Female, 20);

            var teach = new TeachArt(leader.Id, student.Id, "taiji_sword");
            Assert.That(teach.CanExecute(w, D).Reason, Is.EqualTo("아직 익힐 그릇이 못 됩니다"));
            student.Realm = 2;
            student.Insight = 12;
            Assert.That(teach.CanExecute(w, D).Ok);
            teach.Execute(w, D, new GameRandom(1));
            Assert.That(student.KnownArt("taiji_sword").Mastery, Is.EqualTo(1));
            Assert.That(w.Opinions.GetStored(student.Id, leader.Id), Is.EqualTo(10));
            Assert.That(teach.CanExecute(w, D).Reason, Is.EqualTo("이미 익힌 무공입니다"));
            Assert.That(new TeachArt(leader.Id, student.Id, "taiji_fist").CanExecute(w, D).Reason, Is.EqualTo("올해는 이미 가르쳤습니다"));
            w.Month += 12;
            Assert.That(new TeachArt(leader.Id, student.Id, "taiji_fist").CanExecute(w, D).Ok);
            Assert.That(new TeachArt(leader.Id, student.Id, "plum_sword").CanExecute(w, D).Reason, Is.EqualTo("내가 익힌 무공이 아닙니다"));
        }

        [Test]
        public void TeachArt_NotLeader_Fail()
        {
            var w = TestWorlds.Empty(D);
            TestWorlds.AddFaction(w, "f", Alignment.Righteous, "hubei", 10);
            TestWorlds.AddCharacter(w, "f", Role.Leader, Sex.Male, 50);
            var elder = TestWorlds.AddCharacter(w, "f", Role.Elder, Sex.Male, 50);
            elder.Arts.Add(new KnownArt { ArtId = "breeze_sword", Mastery = 8 });
            var student = TestWorlds.AddCharacter(w, "f", Role.Disciple, Sex.Female, 20);
            Assert.That(new TeachArt(elder.Id, student.Id, "breeze_sword").CanExecute(w, D).Reason, Is.EqualTo("세력의 수장만 할 수 있습니다"));
        }

        [Test]
        public void SetMainArt_OnlyKnownExternal()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 2 });
            var me = s.Player;
            ArtGranting.Learn(D, me, "breeze_sword", 2);
            Assert.That(s.SetMainArt("breeze_sword").Ok);
            Assert.That(me.MainArtId, Is.EqualTo("breeze_sword"));
            Assert.That(s.SetMainArt("plum_sword").Reason, Is.EqualTo("익히지 않은 무공입니다"));
            var inner = ArtGranting.Inner(D, me);
            Assert.That(s.SetMainArt(inner.Id).Reason, Is.EqualTo("내공심법은 주무공이 될 수 없습니다"));
        }
    }
}
