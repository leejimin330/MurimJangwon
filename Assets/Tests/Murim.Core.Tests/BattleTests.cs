using System.Collections.Generic;
using System.Linq;
using Murim.Core.Battle;
using Murim.Core.Data;
using Murim.Core.Manor;
using Murim.Core.Martial;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class BattleMapTests
    {
        static GameData D => TestPaths.LoadShippedData();

        static BattleMap Open(int w = 10, int h = 10) => new BattleMap(w, h);

        [Test]
        public void FromManor_CompletedBuildingsBlock_TowerWalkable()
        {
            var w = TestWorlds.Empty(D);
            var m = ManorOps.CreateDefault(D);
            m.Buildings.Add(new BuildingInstance { Id = "t", DefId = "watchtower", X = 2, Y = 2, CompleteMonth = 0 });
            m.Buildings.Add(new BuildingInstance { Id = "u", DefId = "library", X = 5, Y = 0, CompleteMonth = w.Month + 5 });
            var map = BattleMap.FromManor(w, D, m);
            Assert.That(map.Walkable(10, 6), Is.False, "본당");
            Assert.That(map.Walkable(13, 8), Is.False);
            Assert.That(map.Walkable(14, 8), Is.True);
            Assert.That(map.Walkable(2, 2) && map.Tower[2, 2], "망루 위는 설 수 있다");
            Assert.That(map.Walkable(5, 0), Is.True, "미완공 건물은 막지 않는다");
        }

        [Test]
        public void Path_AroundWall()
        {
            var map = Open();
            for (int y = 0; y < 9; y++) map.Blocked[5, y] = true;
            var path = map.FindPath(2, 2, 8, 2);
            Assert.That(path.Last(), Is.EqualTo((8, 2)));
            Assert.That(path.All(p => map.Walkable(p.x, p.y)));
            Assert.That(path.Any(p => p.y == 9), "벽 끝을 돌아간다");
        }

        [Test]
        public void Path_NoCornerCutting()
        {
            var map = Open(3, 3);
            map.Blocked[1, 0] = true;
            map.Blocked[0, 1] = true;
            Assert.That(map.FindPath(0, 0, 1, 1), Is.Empty, "대각선 모서리를 지나갈 수 없다");
        }

        [Test]
        public void Path_FullyBlocked_GoesNearest()
        {
            var map = Open();
            for (int x = 0; x < 10; x++) map.Blocked[x, 5] = true;
            var path = map.FindPath(4, 1, 4, 8);
            Assert.That(path.Last(), Is.EqualTo((4, 4)), "막힌 벽 바로 앞까지");
        }
    }

    public class BattleSimTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;
        Faction f;

        [SetUp]
        public void Setup()
        {
            w = TestWorlds.Empty(D);
            f = TestWorlds.AddFaction(w, "home", Alignment.Righteous, "hubei", 10);
        }

        Character Fighter(string faction, int realm, string art = "three_talent_sword", int mastery = 5)
        {
            var c = TestWorlds.AddCharacter(w, faction, faction == null ? Role.None : Role.Disciple, Sex.Male, 30);
            c.Realm = realm;
            c.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = mastery });
            if (art != null) c.Arts.Add(new KnownArt { ArtId = art, Mastery = mastery });
            c.Qi = (realm == 0 ? 0 : Cultivation.ThresholdYears[realm - 1]) * 12;
            return c;
        }

        BattleState Make(ulong seed, IList<Character> defenders, Character boss, IList<Character> attackers, int defGrunts = 0, int atkGrunts = 0, ManorState manor = null) =>
            BattleSetup.Create(w, D, seed, manor ?? ManorOps.CreateDefault(D), f, defenders, defGrunts, boss, attackers, atkGrunts);

        [Test]
        public void SameSeed_SameResult()
        {
            var defs = new List<Character> { Fighter("home", 2), Fighter("home", 1) };
            var boss = Fighter(null, 2);
            var a = Make(7, defs, boss, new Character[0], 3, 5);
            var b = Make(7, defs, boss, new Character[0], 3, 5);
            BattleSim.RunToEnd(a);
            BattleSim.RunToEnd(b);
            Assert.That(a.Tick, Is.EqualTo(b.Tick));
            Assert.That(a.DefendersWon, Is.EqualTo(b.DefendersWon));
            Assert.That(a.Units.Select(u => u.Hp), Is.EqualTo(b.Units.Select(u => u.Hp)));
            Assert.That(a.Log, Is.EqualTo(b.Log));
        }

        [Test]
        public void Orders_ApplyOnNextTick()
        {
            var me = Fighter("home", 2);
            var s = Make(1, new[] { me }, Fighter(null, 0), new Character[0]);
            var unit = s.Units[0];
            float x = unit.X, y = unit.Y;
            BattleSim.Order(s, 0, new BattleOrder { X = 2, Y = 2 });
            Assert.That((unit.X, unit.Y), Is.EqualTo((x, y)), "명령만으로는 움직이지 않는다 (일시정지)");
            BattleSim.Step(s);
            Assert.That(BattleSim.Dist(unit.X, unit.Y, x, y), Is.GreaterThan(0.1f));
        }

        [Test]
        public void MoveOrder_Arrives()
        {
            var me = Fighter("home", 2);
            var s = Make(1, new[] { me }, Fighter(null, 0), new Character[0]);
            BattleSim.Order(s, 0, new BattleOrder { X = 2, Y = 3 });
            for (int i = 0; i < 200 && s.Units[0].Order != null; i++) BattleSim.Step(s);
            Assert.That(s.Units[0].CellX, Is.EqualTo(2));
            Assert.That(s.Units[0].CellY, Is.EqualTo(3));
            Assert.That(s.Units[0].Holding, "자리를 지킨다");
            for (int i = 0; i < 300; i++) BattleSim.Step(s);
            Assert.That((s.Units[0].CellX, s.Units[0].CellY), Is.EqualTo((2, 3)), "닿지 않는 적을 쫓아가지 않는다");
        }

        [Test]
        public void RealmAdvantage_Wins()
        {
            int wins = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var defs = new List<Character> { Fighter("home", 4), Fighter("home", 4), Fighter("home", 4) };
                var boss = Fighter(null, 2);
                var allies = new List<Character> { Fighter(null, 2), Fighter(null, 2) };
                var s = Make(seed, defs, boss, allies);
                BattleSim.RunToEnd(s);
                if (s.DefendersWon && !s.TimedOut) wins++;
            }
            Assert.That(wins / 200.0, Is.AtLeast(0.9));
        }

        [Test]
        public void Master_BeatsMob()
        {
            int wins = 0;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var master = Fighter("home", 5, "taiji_sword", 8);
                var s = Make(seed, new[] { master }, Fighter(null, 0), new Character[0], 0, 10);
                BattleSim.RunToEnd(s);
                if (s.DefendersWon) wins++;
            }
            Assert.That(wins, Is.AtLeast(40), "절정 넘은 고수 하나가 졸개 열하나를 이긴다");
        }

        [Test]
        public void Rout_WhenLeaderDown()
        {
            var s = Make(3, new[] { Fighter("home", 2) }, Fighter(null, 1), new Character[0], 0, 6);
            var boss = s.Units.First(u => u.Side == BattleSide.Attacker && u.IsLeader);
            boss.Hp = 1;
            for (int i = 0; i < 600 && !boss.Down; i++)
            {
                BattleSim.Order(s, 0, new BattleOrder { Attack = true, TargetId = boss.Id });
                BattleSim.Step(s);
            }
            Assert.That(boss.Down);
            BattleSim.Step(s);
            Assert.That(s.Routed);
            Assert.That(s.Units.Where(u => u.Side == BattleSide.Attacker && u.Active).All(u => u.Fleeing));
            BattleSim.RunToEnd(s);
            Assert.That(s.DefendersWon);
        }

        [Test]
        public void Timeout_DefendersWin()
        {
            // 서로 닿을 수 없는 지도: 습격자는 담 너머에 갇힌다.
            var manor = ManorOps.CreateDefault(D);
            int id = 100;
            for (int x = 0; x < 24; x++)
                if (!ManorOps.IsGate(x, 13)) manor.Buildings.Add(new BuildingInstance { Id = "w" + id++, DefId = "wall", X = x, Y = 13, CompleteMonth = 0 });
            foreach (var gx in ManorOps.GateX) manor.Buildings.Add(new BuildingInstance { Id = "w" + id++, DefId = "wall", X = gx, Y = 13, CompleteMonth = 0 });
            var s = Make(1, new[] { Fighter("home", 2) }, Fighter(null, 1), new Character[0], 0, 0, manor);
            BattleSim.RunToEnd(s);
            Assert.That(s.TimedOut && s.DefendersWon);
            Assert.That(s.Tick, Is.EqualTo(BattleState.MaxTicks));
        }

        [Test]
        public void Watchtower_RangeSix()
        {
            var manor = ManorOps.CreateDefault(D);
            manor.Buildings.Add(new BuildingInstance { Id = "t", DefId = "watchtower", X = 4, Y = 4, CompleteMonth = 0 });
            var archer = Fighter("home", 2, "flying_dagger");
            var s = Make(1, new[] { archer }, Fighter(null, 0), new Character[0], 0, 0, manor);
            var u = s.Units[0];
            float ground = BattleSim.Reach(s, u);
            u.X = 4.5f;
            u.Y = 4.5f;
            Assert.That(ground, Is.EqualTo(BattleSim.HiddenRange));
            Assert.That(BattleSim.Reach(s, u), Is.EqualTo(BattleSim.HiddenRange + BattleSim.TowerRangeBonus));
        }
    }
}
