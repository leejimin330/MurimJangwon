using System.Collections.Generic;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.World;
using Newtonsoft.Json;

namespace Murim.Core.Tests
{
    /// <summary>사건 테스트용: JSON 한 조각으로 사건을 만들고 묶는다.</summary>
    public static class EventFixtures
    {
        public static GameData Data()
        {
            var d = TestWorlds.MinimalData();
            d.Traits["hot_tempered"] = new TraitDef { Id = "hot_tempered", Name = "다혈질" };
            d.Traits["coward"] = new TraitDef { Id = "coward", Name = "겁쟁이" };
            foreach (var f in new[]
            {
                new FlagDef { Id = "injured", Name = "부상", DuelModifier = -15, Visible = true },
                new FlagDef { Id = "demonic_art", Name = "마공 수련", YearlyChivalry = -5, Visible = true },
                new FlagDef { Id = "secret", Name = "비밀" },
            }) d.Flags[f.Id] = f;
            d.Storytellers["balanced"] = new StorytellerDef
            {
                Id = "balanced", Name = "균형", TensionGain = 10, TensionMax = 100, BaseChance = 1, CrisisGapMonths = 10,
            };
            return d;
        }

        public static EventDef Add(GameData d, string json)
        {
            var e = JsonConvert.DeserializeObject<EventDef>(json, DataLoader.Settings);
            d.Events[e.Id] = e;
            EventValidator.Prepare(d);
            return e;
        }

        public static EventContext Bind(WorldState w, GameData d, EventDef e, Dictionary<string, string> fixedRoles = null, ulong seed = 1) =>
            RoleBinder.TryBind(w, d, new GameRandom(seed), e, fixedRoles, CandidateIndex.Build(w), false);

        /// <summary>역할을 직접 채운 문맥 (효과·본문 테스트용).</summary>
        public static EventContext Context(WorldState w, GameData d, EventDef e, params (string role, Character c)[] roles)
        {
            var ctx = new EventContext { W = w, D = d, Rng = new GameRandom(7), Def = e };
            foreach (var (role, c) in roles) ctx.Roles[role] = c.Id;
            return ctx;
        }

        /// <summary>역할 a(결정자), b와 선택지 하나짜리 빈 사건.</summary>
        public static EventDef Blank(GameData d, string id = "blank") => Add(d,
            "{\"id\":\"" + id + "\",\"name\":\"빈 사건\",\"category\":\"trait\",\"decider\":\"a\",\"text\":\"{a:이} {b:와} 만났다.\"," +
            "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}}," +
            "\"choices\":[{\"text\":\"좋다\",\"effects\":[]}]}");
    }
}
