using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class EventDefTests
    {
        /// <summary>events.json의 사건 배열만 바꾼 최소 데이터로 검증한다.</summary>
        static List<string> ValidateEvents(string eventsArray, string flags = "[]")
        {
            var files = TestJson.MinimalFiles();
            files["events.json"] = "{\"flags\":" + flags + ",\"events\":" + eventsArray + "}";
            var r = DataLoader.LoadFromStrings(files);
            Assert.That(r.Ok, Is.True, string.Join("\n", r.Errors));
            return EventValidator.Validate(r.Data);
        }

        const string Roles = "\"roles\":{\"hero\":{\"pick\":\"anyAdult\"},\"rival\":{\"pick\":\"sameRegion\",\"of\":\"hero\"}}";

        static string Event(string id = "e", string text = "{hero:이} 길을 간다.", string choices = null, string extra = "",
            string roles = Roles) =>
            "{\"id\":\"" + id + "\",\"name\":\"사건\",\"category\":\"trait\",\"decider\":\"hero\"," + roles + ",\"text\":\"" + text + "\"," +
            "\"choices\":" + (choices ?? "[{\"text\":\"간다\",\"effects\":[{\"fame\":{\"who\":\"hero\",\"amount\":1}}]}]") + extra + "}";

        [Test]
        public void Load_ShippedEvents_Ok()
        {
            var d = TestPaths.LoadShippedData();
            Assert.That(EventValidator.Validate(d), Is.Empty);
            Assert.That(d.Events.Count, Is.GreaterThan(0));
            Assert.That(d.Storytellers.Keys, Is.EquivalentTo(new[] { "calm", "balanced", "turbulent" }));
        }

        [Test]
        public void Minimal_Ok() => Assert.That(ValidateEvents("[" + Event() + "]"), Is.Empty);

        [Test]
        public void UnknownEffectKey_Error()
        {
            var errors = ValidateEvents("[" + Event(choices: "[{\"text\":\"간다\",\"effects\":[{\"explode\":{\"who\":\"hero\"}}]}]") + "]");
            Assert.That(errors, Has.Some.Contains("알 수 없는 효과 'explode'"));
        }

        [Test]
        public void UnknownCondition_Error()
        {
            var errors = ValidateEvents("[" + Event(roles: "\"roles\":{\"hero\":{\"pick\":\"anyAdult\",\"ageMinn\":3}}") + "]");
            Assert.That(errors, Has.Some.Contains("알 수 없는 조건 'ageMinn'"));
        }

        [Test]
        public void UnknownRoleInText_Error()
        {
            var errors = ValidateEvents("[" + Event(text: "{ghost}이 나타났다") + "]");
            Assert.That(errors, Has.Some.Contains("본문의 '{ghost}'"));
        }

        [Test]
        public void BadToken_Error()
        {
            var errors = ValidateEvents("[" + Event(text: "{hero.foo}") + "]");
            Assert.That(errors, Has.Some.Contains("알 수 없는 표기 '{hero.foo}'"));
        }

        [Test]
        public void AllChoicesRequire_Error()
        {
            var errors = ValidateEvents("[" + Event(choices: "[{\"text\":\"간다\",\"requires\":{\"hero\":{\"silverMin\":10}},\"effects\":[]}]") + "]");
            Assert.That(errors, Has.Some.Contains("항상 고를 수 있는 선택지가 없습니다"));
        }

        [Test]
        public void CyclicOf_Error()
        {
            var roles = "\"roles\":{\"hero\":{\"pick\":\"anyAdult\"},\"a\":{\"pick\":\"spouse\",\"of\":\"b\"},\"b\":{\"pick\":\"spouse\",\"of\":\"a\"}}";
            Assert.That(ValidateEvents("[" + Event(roles: roles) + "]"), Has.Some.Contains("순환"));
        }

        [Test]
        public void MissingDecider_Error()
        {
            var roles = "\"roles\":{\"someone\":{\"pick\":\"anyAdult\"}}";
            Assert.That(ValidateEvents("[" + Event(roles: roles, text: "x") + "]"), Has.Some.Contains("결정자 역할 'hero'"));
        }

        [Test]
        public void UnknownTrait_Error()
        {
            var roles = "\"roles\":{\"hero\":{\"pick\":\"anyAdult\",\"traitsAll\":[\"flying\"]}}";
            Assert.That(ValidateEvents("[" + Event(roles: roles) + "]"), Has.Some.Contains("알 수 없는 특성 'flying'"));
        }

        [Test]
        public void ScheduleUnknownEvent_Error()
        {
            var choices = "[{\"text\":\"간다\",\"effects\":[{\"schedule\":{\"event\":\"nope\",\"afterMonths\":[1,2],\"roles\":{}}}]}]";
            Assert.That(ValidateEvents("[" + Event(choices: choices) + "]"), Has.Some.Contains("예약할 사건 'nope'"));
        }

        [Test]
        public void FollowupNeverScheduled_Error()
        {
            var errors = ValidateEvents("[" + Event() + "," + Event(id: "f", extra: ",\"followupOnly\":true") + "]");
            Assert.That(errors, Has.Some.Contains("'f'은(는) 후속 전용"));
        }

        [Test]
        public void FollowupScheduled_Ok()
        {
            var choices = "[{\"text\":\"간다\",\"effects\":[{\"schedule\":{\"event\":\"f\",\"afterMonths\":[1,2],\"roles\":{\"hero\":\"hero\"}}}]}]";
            var errors = ValidateEvents("[" + Event(choices: choices) + "," + Event(id: "f", extra: ",\"followupOnly\":true") + "]");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void TwoEffectKeys_Error()
        {
            var choices = "[{\"text\":\"간다\",\"effects\":[{\"fame\":{\"who\":\"hero\",\"amount\":1},\"silver\":{\"who\":\"hero\",\"amount\":1}}]}]";
            Assert.That(ValidateEvents("[" + Event(choices: choices) + "]"), Has.Some.Contains("키가 하나만"));
        }

        [Test]
        public void UnknownFlag_Error()
        {
            var choices = "[{\"text\":\"간다\",\"effects\":[{\"flag\":{\"who\":\"hero\",\"id\":\"cursed\",\"months\":3}}]}]";
            Assert.That(ValidateEvents("[" + Event(choices: choices) + "]"), Has.Some.Contains("알 수 없는 표식 'cursed'"));
        }

        [Test]
        public void BindOrder_RootsFirst()
        {
            var roles = "\"roles\":{\"z\":{\"pick\":\"spouse\",\"of\":\"hero\"},\"bandit\":{\"pick\":\"new\",\"spawn\":{\"epithet\":\"산적\"}}," +
                        "\"hero\":{\"pick\":\"anyAdult\"},\"kid\":{\"pick\":\"child\",\"of\":\"z\"}}";
            var files = TestJson.MinimalFiles();
            files["events.json"] = "{\"flags\":[],\"events\":[" + Event(roles: roles) + "]}";
            var d = DataLoader.LoadFromStrings(files).Data;
            Assert.That(d.Events["e"].BindOrder, Is.EqualTo(new[] { "hero", "z", "kid", "bandit" }));
        }

        [Test]
        public void EffectKind_Parsed()
        {
            var d = DataLoader.LoadFromStrings(TestJson.MinimalFiles()).Data;
            var fx = d.Events["quarrel"].Choices[0].Effects[0];
            Assert.That(fx.Kind, Is.EqualTo("fame"));
            Assert.That(((WhoAmountFx)fx.Args).Amount, Is.EqualTo(1));
            Assert.That(d.Flags["injured"].DuelModifier, Is.EqualTo(-15));
        }
    }
}
