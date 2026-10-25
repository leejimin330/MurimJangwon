using System.Collections.Generic;
using System.Text.RegularExpressions;
using Murim.Core.Text;

namespace Murim.Core.Events
{
    /// <summary>사건 본문 템플릿: {role}, {role.title}, {role.faction}, {role.region}, 뒤에 :이 :을 :은 :와 :으로.</summary>
    public static class EventText
    {
        static readonly Regex Brace = new Regex(@"\{([^{}]*)\}");
        static readonly Regex Token = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)(?:\.(title|faction|region))?(?::(이|을|은|와|으로))?$");

        public static bool IsKnownToken(string inner) => Token.IsMatch(inner ?? "");

        /// <summary>{…} 안의 역할 이름들. 형식이 틀린 표기도 앞부분을 돌려준다 (검증기가 역할 존재를 따진다).</summary>
        public static IEnumerable<string> RoleRefs(string template)
        {
            if (string.IsNullOrEmpty(template)) yield break;
            foreach (Match m in Brace.Matches(template))
            {
                var inner = m.Groups[1].Value;
                int cut = inner.IndexOfAny(new[] { '.', ':' });
                yield return cut < 0 ? inner : inner.Substring(0, cut);
            }
        }

        /// <summary>형식이 틀린 표기들 (검증용).</summary>
        public static IEnumerable<string> BadTokens(string template)
        {
            if (string.IsNullOrEmpty(template)) yield break;
            foreach (Match m in Brace.Matches(template))
                if (!IsKnownToken(m.Groups[1].Value)) yield return m.Value;
        }

        public static string Format(string template, EventContext ctx)
        {
            if (string.IsNullOrEmpty(template)) return "";
            return Brace.Replace(template, m =>
            {
                var t = Token.Match(m.Groups[1].Value);
                if (!t.Success) return m.Value;
                var role = t.Groups[1].Value;
                var c = ctx.Get(role);
                string word;
                if (c == null) word = "누군가";
                else
                {
                    switch (t.Groups[2].Value)
                    {
                        case "title":
                            var epithet = ctx.EpithetOf(role);
                            word = epithet != null ? epithet + " " + c.FullName : Titles.Full(ctx.W, c);
                            break;
                        case "faction":
                            word = ctx.W.GetFaction(c.FactionId)?.Name ?? "강호";
                            break;
                        case "region":
                            var r = ConditionCheck.RegionOf(ctx.W, c);
                            word = r != null && ctx.D.Regions.TryGetValue(r, out var def) ? def.Name : "어딘가";
                            break;
                        default:
                            word = c.FullName;
                            break;
                    }
                }
                switch (t.Groups[3].Value)
                {
                    case "이": return Josa.IGa(word);
                    case "을": return Josa.EulReul(word);
                    case "은": return Josa.EunNeun(word);
                    case "와": return Josa.WaGwa(word);
                    case "으로": return Josa.EuroRo(word);
                    default: return word;
                }
            });
        }
    }
}
