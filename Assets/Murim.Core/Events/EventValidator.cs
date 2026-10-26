using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Events
{
    /// <summary>events.json·storytellers.json 검증. 통과하면 각 사건의 BindOrder를 채운다.</summary>
    public static class EventValidator
    {
        const string File = "events.json";

        /// <summary>각 사건의 역할 묶는 순서를 정한다. 불러오기 직후 한 번 부른다.</summary>
        public static void Prepare(GameData d)
        {
            foreach (var e in d.Events.Values) e.BindOrder = BindOrder(e.Roles ?? new Dictionary<string, RoleDef>(), out _);
        }

        public static List<string> Validate(GameData d)
        {
            var errors = new List<string>();
            var scheduled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in d.Events.Values) ValidateEvent(d, e, errors, scheduled);
            foreach (var e in d.Events.Values)
                if (e.FollowupOnly && !e.System && !scheduled.Contains(e.Id))
                    errors.Add($"{File}: 사건 '{e.Id}'은(는) 후속 전용인데 어떤 사건도 예약하지 않습니다");

            if (!d.Storytellers.ContainsKey("balanced")) errors.Add("storytellers.json: 기본 이야기꾼 'balanced'가 없습니다");
            foreach (var s in d.Storytellers.Values)
            {
                if (s.TensionGain <= 0 || s.TensionMax <= 0 || s.BaseChance <= 0 || s.BaseChance > 1)
                    errors.Add($"storytellers.json: 이야기꾼 '{s.Id}'의 수치가 올바르지 않습니다");
                foreach (var k in s.CategoryMultipliers?.Keys ?? Enumerable.Empty<string>())
                    if (!EventCategories.All.Contains(k)) errors.Add($"storytellers.json: 이야기꾼 '{s.Id}'의 범주 '{k}'이(가) 없습니다");
            }
            return errors;
        }

        static void Unknown(IDictionary<string, JToken> unknown, string where, string what, List<string> errors)
        {
            if (unknown == null) return;
            foreach (var k in unknown.Keys) errors.Add($"{where}: 알 수 없는 {what} '{k}'");
        }

        static void ValidateEvent(GameData d, EventDef e, List<string> errors, HashSet<string> scheduled)
        {
            string at = $"{File}: 사건 '{e.Id}'";
            Unknown(e.Unknown, at, "항목", errors);
            if (string.IsNullOrEmpty(e.Name) || string.IsNullOrEmpty(e.Text)) errors.Add($"{at}: 이름이나 본문이 없습니다");
            if (!EventCategories.All.Contains(e.Category)) errors.Add($"{at}: 범주 '{e.Category}'이(가) 없습니다");
            if (e.Month < 0 || e.Month > 12) errors.Add($"{at}: 달 {e.Month}이(가) 0~12 밖입니다");
            var roles = e.Roles ?? new Dictionary<string, RoleDef>();
            if (e.Decider == null || !roles.ContainsKey(e.Decider)) errors.Add($"{at}: 결정자 역할 '{e.Decider}'이(가) 없습니다");

            foreach (var kv in roles)
            {
                string ra = $"{at} 역할 '{kv.Key}'";
                var r = kv.Value;
                ValidateConditions(d, r, roles, ra, errors);
                if (r.Pick == EventPicks.New)
                {
                    if (r.Spawn == null) errors.Add($"{ra}: 새 인물인데 spawn이 없습니다");
                    if (r.Of != null && !roles.ContainsKey(r.Of)) errors.Add($"{ra}: of '{r.Of}' 역할이 없습니다");
                }
                else if (EventPicks.IsRelative(r.Pick))
                {
                    if (r.Of == null || !roles.ContainsKey(r.Of)) errors.Add($"{ra}: of '{r.Of}' 역할이 없습니다");
                }
                else if (!EventPicks.IsRoot(r.Pick)) errors.Add($"{ra}: 알 수 없는 pick '{r.Pick}'");
            }

            e.BindOrder = BindOrder(roles, out bool cyclic);
            if (cyclic) errors.Add($"{at}: 역할의 of가 순환합니다");
            if (!roles.Values.Any(r => EventPicks.IsRoot(r.Pick))) errors.Add($"{at}: 뿌리 역할(anyAdult·leader·grudgeHolder·debtHolder)이 없습니다");

            CheckText(e.Text, roles, $"{at} 본문", errors);
            if (e.Choices == null || e.Choices.Count == 0)
            {
                errors.Add($"{at}: 선택지가 없습니다");
                return;
            }
            if (e.Choices.All(c => c.Requires != null && c.Requires.Count > 0)) errors.Add($"{at}: 항상 고를 수 있는 선택지가 없습니다");
            for (int i = 0; i < e.Choices.Count; i++)
            {
                var c = e.Choices[i];
                string ca = $"{at} 선택지 {i + 1}";
                Unknown(c.Unknown, ca, "항목", errors);
                if (string.IsNullOrEmpty(c.Text)) errors.Add($"{ca}: 문구가 없습니다");
                CheckText(c.Text, roles, ca, errors);
                CheckText(c.Result, roles, ca + " 결과", errors);
                foreach (var req in c.Requires ?? new Dictionary<string, Conditions>())
                {
                    if (!roles.ContainsKey(req.Key)) errors.Add($"{ca}: requires의 역할 '{req.Key}'이(가) 없습니다");
                    if (req.Value?.Chance != null) errors.Add($"{ca}: requires에는 chance를 쓸 수 없습니다");
                    ValidateConditions(d, req.Value, roles, ca, errors);
                }
                foreach (var t in c.AiTraits?.Keys ?? Enumerable.Empty<string>())
                    if (!d.Traits.ContainsKey(t)) errors.Add($"{ca}: 알 수 없는 특성 '{t}'");
                ValidateEffects(d, c.Effects, roles, ca, errors, scheduled);
            }
        }

        static List<string> BindOrder(Dictionary<string, RoleDef> roles, out bool cyclic)
        {
            var order = roles.Where(kv => EventPicks.IsRoot(kv.Value.Pick)).Select(kv => kv.Key)
                .OrderBy(k => k, StringComparer.Ordinal).ToList();
            var rest = roles.Keys.Where(k => !order.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            bool progress = true;
            while (rest.Count > 0 && progress)
            {
                progress = false;
                foreach (var k in rest.ToList())
                {
                    var r = roles[k];
                    // 새 인물은 다른 역할을 다 채운 뒤에 만든다.
                    bool ready = r.Pick == EventPicks.New
                        ? rest.All(o => o == k || roles[o].Pick == EventPicks.New) && (r.Of == null || order.Contains(r.Of))
                        : r.Of != null && order.Contains(r.Of);
                    if (!ready) continue;
                    order.Add(k);
                    rest.Remove(k);
                    progress = true;
                }
            }
            cyclic = rest.Any(k => roles[k].Of != null && roles.ContainsKey(roles[k].Of));
            order.AddRange(rest);
            return order;
        }

        static void CheckText(string text, Dictionary<string, RoleDef> roles, string at, List<string> errors)
        {
            foreach (var bad in EventText.BadTokens(text)) errors.Add($"{at}: 알 수 없는 표기 '{bad}'");
            foreach (var r in EventText.RoleRefs(text))
                if (!roles.ContainsKey(r)) errors.Add($"{at}: 본문의 '{{{r}}}' 역할이 없습니다");
        }

        static void ValidateConditions(GameData d, Conditions c, Dictionary<string, RoleDef> roles, string at, List<string> errors)
        {
            if (c == null) return;
            Unknown(c.Unknown, at, "조건", errors);
            foreach (var t in (c.TraitsAll ?? new string[0]).Concat(c.TraitsAny ?? new string[0]).Concat(c.TraitsNone ?? new string[0]))
                if (!d.Traits.ContainsKey(t)) errors.Add($"{at}: 알 수 없는 특성 '{t}'");
            foreach (var f in new[] { c.HasFlag, c.NotFlag })
                if (f != null && !d.Flags.ContainsKey(f)) errors.Add($"{at}: 알 수 없는 표식 '{f}'");
            if (c.Memory?.Target != null && !roles.ContainsKey(c.Memory.Target)) errors.Add($"{at}: 기억 대상 역할 '{c.Memory.Target}'이(가) 없습니다");
            if (c.OppositeSexOf != null && !roles.ContainsKey(c.OppositeSexOf))
                errors.Add($"{at}: 성별 비교 역할 '{c.OppositeSexOf}'이(가) 없습니다");
            foreach (var a in new[] { c.KnowsArt, c.NotKnowsArt })
                if (a != null && !d.Arts.ContainsKey(a)) errors.Add($"{at}: 알 수 없는 무공 '{a}'");
            if (c.OpinionOf != null && (c.OpinionOf.Role == null || !roles.ContainsKey(c.OpinionOf.Role)))
                errors.Add($"{at}: 호감 대상 역할 '{c.OpinionOf.Role}'이(가) 없습니다");
        }

        static void ValidateEffects(GameData d, List<EffectDef> effects, Dictionary<string, RoleDef> roles, string at,
            List<string> errors, HashSet<string> scheduled)
        {
            foreach (var fx in effects ?? new List<EffectDef>())
            {
                if (fx == null) continue;
                if (fx.Unknown != null)
                    foreach (var k in fx.Unknown.Keys) errors.Add($"{at}: 알 수 없는 효과 '{k}'");
                var present = fx.Present;
                if (present.Count != 1)
                {
                    if (present.Count > 1 || fx.Unknown == null || fx.Unknown.Count == 0)
                        errors.Add($"{at}: 효과 하나에는 키가 하나만 있어야 합니다");
                    continue;
                }
                var (name, args) = present[0];
                Unknown(args.Unknown, $"{at} {name}", "항목", errors);
                foreach (var r in args.RoleRefs())
                    if (r == null || !roles.ContainsKey(r)) errors.Add($"{at} {name}: 역할 '{r}'이(가) 없습니다");
                switch (args)
                {
                    case StatFx s when !Stats.Names.Contains(s.Stat):
                        errors.Add($"{at} {name}: 알 수 없는 능력치 '{s.Stat}'");
                        break;
                    case ContestFx k when !Stats.Names.Contains(k.Stat) || (k.AgainstRole != null && !Stats.Names.Contains(k.AgainstStat)):
                        errors.Add($"{at} {name}: 알 수 없는 능력치");
                        break;
                    case FlagFx f when !d.Flags.ContainsKey(f.Id ?? ""):
                        errors.Add($"{at} {name}: 알 수 없는 표식 '{f.Id}'");
                        break;
                    case LearnArtFx la when la.Art != null && !d.Arts.ContainsKey(la.Art):
                        errors.Add($"{at} {name}: 알 수 없는 무공 '{la.Art}'");
                        break;
                    case MasteryFx mf when mf.Art != null && !d.Arts.ContainsKey(mf.Art):
                        errors.Add($"{at} {name}: 알 수 없는 무공 '{mf.Art}'");
                        break;
                    case ClosedTrainingFx ct when ct.Months != 3 && ct.Months != 6 && ct.Months != 12:
                        errors.Add($"{at} {name}: 폐관은 3·6·12개월만 됩니다");
                        break;
                    case ChronicleFx ch:
                        CheckText(ch.Text, roles, $"{at} {name}", errors);
                        break;
                    case ScheduleFx sc:
                        if (sc.Event == null || !d.Events.TryGetValue(sc.Event, out var next))
                            errors.Add($"{at} {name}: 예약할 사건 '{sc.Event}'이(가) 없습니다");
                        else
                        {
                            scheduled.Add(sc.Event);
                            foreach (var k in sc.Roles.Keys)
                                if (!next.Roles.ContainsKey(k)) errors.Add($"{at} {name}: 사건 '{sc.Event}'에 역할 '{k}'이(가) 없습니다");
                        }
                        if (sc.AfterMonths == null || sc.AfterMonths.Length != 2 || sc.AfterMonths[0] < 1 || sc.AfterMonths[1] < sc.AfterMonths[0])
                            errors.Add($"{at} {name}: afterMonths는 [최소, 최대] (1 이상)여야 합니다");
                        break;
                }
                foreach (var branch in args.Branches()) ValidateEffects(d, branch, roles, at, errors, scheduled);
            }
        }
    }
}
