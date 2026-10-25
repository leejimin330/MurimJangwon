using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class EventTextTests
    {
        GameData d;
        WorldState w;
        EventDef e;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
            e = EventFixtures.Blank(d);
        }

        Character Named(string family, string given, string faction = null, Role role = Role.None)
        {
            var c = TestWorlds.AddCharacter(w, faction, role, Sex.Male, 30);
            c.FamilyName = family;
            c.GivenName = given;
            return c;
        }

        [Test]
        public void Name_WithJosa()
        {
            var ctx = EventFixtures.Context(w, d, e, ("a", Named("장", "삼")), ("b", Named("장", "사")));
            Assert.That(EventText.Format("{a:이} {b:을} 보았다. {b:이} {a:와} {b:으로}", ctx), Is.EqualTo("장삼이 장사를 보았다. 장사가 장삼과 장사로"));
            Assert.That(EventText.Format("{a:은} {b:은}", ctx), Is.EqualTo("장삼은 장사는"));
        }

        [Test]
        public void Title_FactionMember_UsesTitles()
        {
            TestWorlds.AddFaction(w, "wd", Alignment.Righteous, "a", 10);
            var ctx = EventFixtures.Context(w, d, e, ("a", Named("장", "삼", "wd", Role.Leader)));
            Assert.That(EventText.Format("{a.title:이} 왔다. {a.faction} {a.region}", ctx), Is.EqualTo("wd파 장문인 장삼이 왔다. wd파 갑"));
        }

        [Test]
        public void Title_Spawned_UsesEpithet()
        {
            var s = EventFixtures.Add(d, "{\"id\":\"sp\",\"name\":\"x\",\"category\":\"wanderer\",\"decider\":\"a\",\"text\":\"t\"," +
                "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"bandit\":{\"pick\":\"new\",\"spawn\":{\"epithet\":\"산적 두목\"}}}," +
                "\"choices\":[{\"text\":\"x\",\"effects\":[]}]}");
            var bandit = new Character { Id = "c999999", FamilyName = "흑", GivenName = "호", BirthMonth = 0 };
            var ctx = EventFixtures.Context(w, d, s, ("a", Named("장", "삼")));
            ctx.Spawned.Add(bandit);
            ctx.Roles["bandit"] = bandit.Id;
            Assert.That(EventText.Format("{bandit.title:이} 나타났다.", ctx), Is.EqualTo("산적 두목 흑호가 나타났다."));
        }

        [Test]
        public void Faction_None_Gangho()
        {
            var ctx = EventFixtures.Context(w, d, e, ("a", Named("장", "삼")));
            Assert.That(EventText.Format("{a.faction}", ctx), Is.EqualTo("강호"));
        }

        [Test]
        public void UnknownToken_LeftForValidator()
        {
            Assert.That(EventText.IsKnownToken("a.foo"), Is.False);
            Assert.That(EventText.IsKnownToken("a.title:으로"), Is.True);
            Assert.That(EventText.BadTokens("{a} {a:가}"), Is.EqualTo(new[] { "{a:가}" }));
        }
    }
}
