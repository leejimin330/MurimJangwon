using System.Collections.Generic;
using System.Text.RegularExpressions;
using Murim.Core.Text;

namespace Murim.Core.Martial
{
    /// <summary>결투 문구 템플릿: {a} {b} {winner} {loser} {art} {card} {guard}, 뒤에 :이 :을 :은 :와 :으로.
    /// {card}·{guard}는 작은따옴표로 감싸고 조사는 이름 기준으로 단다.</summary>
    public static class DuelText
    {
        public static readonly string[] Keys = { "a", "b", "winner", "loser", "art", "card", "guard" };
        static readonly Regex Brace = new Regex(@"\{([^{}]*)\}");
        static readonly Regex Token = new Regex(@"^(a|b|winner|loser|art|card|guard)(?::(이|을|은|와|으로))?$");

        public static IEnumerable<string> BadTokens(string template)
        {
            if (string.IsNullOrEmpty(template)) yield break;
            foreach (Match m in Brace.Matches(template))
                if (!Token.IsMatch(m.Groups[1].Value)) yield return m.Value;
        }

        public static string Format(string template, IDictionary<string, string> words)
        {
            if (string.IsNullOrEmpty(template)) return "";
            return Brace.Replace(template, m =>
            {
                var t = Token.Match(m.Groups[1].Value);
                if (!t.Success) return m.Value;
                var key = t.Groups[1].Value;
                string word = words != null && words.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : "상대";
                bool quoted = key == "card" || key == "guard";
                string withJosa = t.Groups[2].Value switch
                {
                    "이" => Josa.IGa(word),
                    "을" => Josa.EulReul(word),
                    "은" => Josa.EunNeun(word),
                    "와" => Josa.WaGwa(word),
                    "으로" => Josa.EuroRo(word),
                    _ => word,
                };
                if (!quoted) return withJosa;
                return "'" + word + "'" + withJosa.Substring(word.Length);
            });
        }
    }
}
