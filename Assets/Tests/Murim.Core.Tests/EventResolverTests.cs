using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class EventResolverTests
    {
        GameData d;
        WorldState w;
        Character a;
        Character b;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
            a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            a.FamilyName = "장";
            a.GivenName = "삼";
            b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            b.FamilyName = "이";
            b.GivenName = "사";
        }

        EventDef Challenge() => EventFixtures.Add(d,
            "{\"id\":\"ch\",\"name\":\"도전\",\"category\":\"grudge\",\"decider\":\"a\",\"cooldownMonths\":24,\"globalCooldownMonths\":6,\"text\":\"{b:이} {a}에게 도전했다.\"," +
            "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}}," +
            "\"choices\":[" +
            "{\"text\":\"칼을 뽑는다\",\"ai\":0,\"aiTraits\":{\"hot_tempered\":50},\"effects\":[{\"fame\":{\"who\":\"a\",\"amount\":1}}]}," +
            "{\"text\":\"달랜다\",\"result\":\"{a:은} 은자로 달랬다.\",\"ai\":10,\"requires\":{\"a\":{\"silverMin\":200}},\"effects\":[{\"silver\":{\"who\":\"a\",\"amount\":-200}}]}," +
            "{\"text\":\"[피한다]\",\"ai\":5,\"requires\":{\"a\":{\"traitsAll\":[\"coward\"]}},\"effects\":[]}]}");

        [Test]
        public void CanChoose_RequiresReason()
        {
            var ctx = EventFixtures.Context(w, d, Challenge(), ("a", a), ("b", b));
            Assert.That(EventResolver.CanChoose(ctx, 0), Is.Null);
            Assert.That(EventResolver.CanChoose(ctx, 1), Is.EqualTo("은자 200냥이 필요합니다"));
            Assert.That(EventResolver.CanChoose(ctx, 2), Is.EqualTo("겁쟁이만 고를 수 있습니다"));
            Assert.That(EventResolver.ChoiceLabel(ctx, 2), Does.StartWith("[겁쟁이] "));
        }

        [Test]
        public void AI_PrefersTraitWeighted()
        {
            a.Silver = 500;
            var e = Challenge();
            var calm = EventFixtures.Context(w, d, e, ("a", a), ("b", b));
            Assert.That(EventAI.Choose(calm), Is.EqualTo(1), "특성이 없으면 은자로 달랜다 (ai 10)");
            a.TraitIds.Add("hot_tempered");
            var hot = EventFixtures.Context(w, d, e, ("a", a), ("b", b));
            Assert.That(EventAI.Choose(hot), Is.EqualTo(0));
        }

        [Test]
        public void AI_SkipsDisabled()
        {
            var ctx = EventFixtures.Context(w, d, Challenge(), ("a", a), ("b", b));
            Assert.That(EventAI.Choose(ctx), Is.EqualTo(0), "은자가 없으면 달래기를 고를 수 없다");
        }

        [Test]
        public void Resolve_WritesChronicleWithResult()
        {
            a.Silver = 300;
            var ctx = EventFixtures.Context(w, d, Challenge(), ("a", a), ("b", b));
            var result = EventResolver.Resolve(ctx, 1);
            Assert.That(result, Is.EqualTo("장삼은 은자로 달랬다."));
            var line = w.Chronicle.Last();
            Assert.That(line.Kind, Is.EqualTo("Event:ch"));
            Assert.That(line.Text, Is.EqualTo("이사가 장삼에게 도전했다. [달랜다] 장삼은 은자로 달랬다."));
            Assert.That(line.InvolvedIds, Is.EquivalentTo(new[] { a.Id, b.Id }));
            Assert.That(a.Silver, Is.EqualTo(100));
        }

        [Test]
        public void Resolve_SetsCooldown()
        {
            var e = Challenge();
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var ctx = EventFixtures.Bind(w, d, e, new System.Collections.Generic.Dictionary<string, string> { ["a"] = a.Id });
            EventResolver.MarkFired(ctx);
            Assert.That(w.Events.IsCooling("ch|" + a.Id, w.Month + 23), Is.True);
            Assert.That(w.Events.IsCooling("ch|*", w.Month + 5), Is.True);
            Assert.That(EventFixtures.Bind(w, d, e), Is.Null, "전역 쿨다운 중에는 성립하지 않는다");
            w.Month += 6;
            var again = EventFixtures.Bind(w, d, e);
            Assert.That(again, Is.Not.Null);
            Assert.That(again.Roles["a"], Is.Not.EqualTo(a.Id), "결정자 쿨다운 중인 인물은 결정자가 될 수 없다");
        }

        [Test]
        public void Spawned_NotRecruited_Removed()
        {
            var e = EventFixtures.Add(d, "{\"id\":\"sp\",\"name\":\"x\",\"category\":\"faction\",\"decider\":\"a\",\"text\":\"{kid:이} 찾아왔다.\"," +
                "\"roles\":{\"a\":{\"pick\":\"leader\"},\"kid\":{\"pick\":\"new\",\"of\":\"a\",\"spawn\":{\"ageMin\":14,\"ageMax\":14}}}," +
                "\"choices\":[{\"text\":\"받는다\",\"effects\":[{\"recruit\":{\"who\":\"kid\",\"faction\":\"a\",\"role\":\"Disciple\"}}]}," +
                "{\"text\":\"돌려보낸다\",\"effects\":[]}]}");
            var f = TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "f", Role.Leader, Sex.Male, 40);
            int before = w.Characters.Count;

            var no = RoleBinder.TryBind(w, d, new GameRandom(1), e, null, CandidateIndex.Build(w), false);
            EventResolver.Resolve(no, 1);
            Assert.That(w.Characters.Count, Is.EqualTo(before));

            var yes = RoleBinder.TryBind(w, d, new GameRandom(2), e, null, CandidateIndex.Build(w), false);
            var kid = yes.Get("kid");
            EventResolver.Resolve(yes, 0);
            Assert.That(w.Characters.ContainsKey(kid.Id), Is.True);
            Assert.That(f.MemberIds, Does.Contain(kid.Id));
            Assert.That(kid.Role, Is.EqualTo(Role.Disciple));
        }
    }
}
