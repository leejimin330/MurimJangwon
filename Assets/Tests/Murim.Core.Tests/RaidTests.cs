using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Battle;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class RaidTests
    {
        static GameData D => TestPaths.LoadShippedData();

        /// <summary>플레이어 장원에 산적떼 습격을 걸고 "맞서 싸운다"를 고른 세션.</summary>
        static GameSession PendingRaidSession(ulong seed, Action<GameSession> before = null)
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = seed });
            s.World.Events.Pending.Clear();
            before?.Invoke(s);
            var own = Wallet.LedFaction(s.World, s.Player);
            s.World.Regions[own.HomeRegionId].Stability = 30;
            Assert.That(s.ForceEvent("raid_bandits").Ok, "산적떼 사건");
            var outcome = s.Choose(s.PendingEvents.Count - 1, 0);
            Assert.That(outcome.Ok, outcome.Reason);
            return s;
        }

        [Test]
        public void PlayerTarget_CreatesPendingRaid_NoBranchYet()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 5 });
            s.World.Events.Pending.Clear();
            var own = Wallet.LedFaction(s.World, s.Player);
            s.World.Regions[own.HomeRegionId].Stability = 30;
            int fame = own.Fame;
            Assert.That(s.ForceEvent("raid_bandits").Ok);
            var outcome = s.Choose(0, 0);
            Assert.That(outcome.Text, Does.Contain("장원으로 몰려온다"));
            Assert.That(s.PendingRaid, Is.Not.Null);
            Assert.That(s.CanEndTurn, Is.False);
            Assert.That(own.Fame, Is.EqualTo(fame), "갈래는 전투 뒤에");
            Assert.That(s.World.Get(s.PendingRaid.AttackerLeaderId), Is.Not.Null, "산적 두목이 세계에 남는다");
        }

        [Test]
        public void FinishRaid_AppliesBranchCasualtiesChronicle()
        {
            var s = PendingRaidSession(5);
            var own = Wallet.LedFaction(s.World, s.Player);
            int fame = own.Fame, silver = own.Silver, unnamed = own.UnnamedDisciples;
            var battle = s.StartBattle();
            Assert.That(battle.Units.Count(u => u.Side == BattleSide.Attacker), Is.EqualTo(1 + 8));
            BattleSim.RunToEnd(battle);
            var outcome = s.FinishRaid(battle);
            Assert.That(s.PendingRaid, Is.Null);
            Assert.That(s.CanEndTurn, Is.True);
            var line = s.World.Chronicle.Last(e => e.Kind == "Raid");
            Assert.That(line.Text, Does.Contain("습격"));
            Assert.That(line.Detail, Is.Not.Null.And.Not.Empty);
            if (outcome.DefendersWon) Assert.That(own.Fame, Is.EqualTo(fame + 5));
            else Assert.That(own.Silver, Is.LessThan(silver));
            Assert.That(own.UnnamedDisciples, Is.EqualTo(unnamed - outcome.DefenderGruntsLost));
            foreach (var u in battle.Units.Where(u => u.Down && u.Named))
            {
                var c = s.World.Get(u.CharacterId);
                Assert.That(!c.IsAlive || c.HasFlag("injured", s.World.Month), c.FullName);
            }
        }

        [Test]
        public void AiTarget_ResolvesImmediately()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 8 });
            var other = s.World.Factions.Values.First(f => !f.IsDissolved && f.LeaderId != null && f.Id != s.Player.FactionId && f.HomeRegionId == "hubei");
            s.World.Regions["hubei"].Stability = 30;
            int before = s.World.Chronicle.Count;
            Assert.That(s.ForceEvent("raid_bandits", new Dictionary<string, string> { ["head"] = other.LeaderId }).Ok);
            Assert.That(s.PendingRaid, Is.Null);
            var line = s.World.Chronicle.Skip(before).Last(e => e.Kind == "Event:raid_bandits");
            Assert.That(line.Text, Does.Contain("습격").Or.Contain("재물을 내주고"), "AI가 고른 쪽의 결과가 바로 남는다");
        }

        [Test]
        public void Validator_RaidMustBeTopLevel()
        {
            var d = DataLoader.LoadFromDirectory(TestPaths.DataDir).Data;
            EventFixtures.Add(d, "{\"id\":\"nested\",\"name\":\"중첩\",\"category\":\"faction\",\"decider\":\"a\",\"text\":\"{a}\"," +
                "\"roles\":{\"a\":{\"pick\":\"leader\"},\"b\":{\"pick\":\"anyAdult\"}},\"choices\":[{\"text\":\"싸운다\",\"effects\":[" +
                "{\"chance\":{\"p\":0.5,\"then\":[{\"raid\":{\"attacker\":\"b\",\"target\":\"a\"}}]}}]}]}");
            Assert.That(EventValidator.Validate(d), Has.Some.Contains("raid는 선택지 효과의 맨 위에만"));
        }

        [Test]
        public void PendingRaid_SaveLoad_SameOutcome()
        {
            var dir = Path.Combine(Path.GetTempPath(), "murim-raid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = PendingRaidSession(9);
                var path = Path.Combine(dir, "raid.json");
                Assert.That(s.Save(path).Ok);
                var a = GameSession.Load(D, path).Session;
                var b = GameSession.Load(D, path).Session;
                Assert.That(a.PendingRaid, Is.Not.Null);
                a.AutoResolveRaid();
                b.AutoResolveRaid();
                Assert.That(a.Serialize(), Is.EqualTo(b.Serialize()));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Raid_PlayerKilled_SuccessionRuns()
        {
            int deaths = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = PendingRaidSession(seed);
                var me = s.Player;
                var battle = s.StartBattle();
                var mine = battle.Units.First(u => u.CharacterId == me.Id);
                mine.Down = true;
                mine.Hp = 0;
                BattleSim.RunToEnd(battle);
                s.FinishRaid(battle);
                if (me.IsAlive) continue;
                deaths++;
                Assert.That(s.World.PlayerCharacterId, Is.Not.EqualTo(me.Id), "후계가 바로 잇는다");
                Assert.That(s.World.GameOver, Is.False);
                Assert.That(s.PendingEvents, Is.Empty);
            }
            Assert.That(deaths, Is.GreaterThan(0));
        }

        [Test]
        public void Raid_LoneLeader()
        {
            var s = PendingRaidSession(12, x =>
            {
                var own = Wallet.LedFaction(x.World, x.Player);
                foreach (var m in FactionOps.LivingMembers(x.World, own).Where(m => m.Id != x.Player.Id).ToList())
                    FactionOps.MoveToFaction(x.World, m, null, Role.None);
                own.UnnamedDisciples = 0;
            });
            var battle = s.StartBattle();
            Assert.That(battle.Units.Count(u => u.Side == BattleSide.Defender), Is.EqualTo(1));
            BattleSim.RunToEnd(battle);
            Assert.That(s.FinishRaid(battle), Is.Not.Null);
        }
    }
}
