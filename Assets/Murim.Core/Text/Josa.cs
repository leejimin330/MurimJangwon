namespace Murim.Core.Text
{
    /// <summary>받침에 따라 조사를 붙인다.</summary>
    public static class Josa
    {
        /// <summary>null = 한글 아님, true = 받침 있음.</summary>
        static bool? HasFinal(string word, out bool finalIsRieul)
        {
            finalIsRieul = false;
            if (string.IsNullOrEmpty(word)) return null;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return null;
            int final = (c - 0xAC00) % 28;
            finalIsRieul = final == 8;
            return final != 0;
        }

        static string Attach(string word, string withFinal, string withoutFinal)
        {
            var has = HasFinal(word, out _);
            if (has == null) return word + withFinal + "(" + withoutFinal + ")";
            return word + (has.Value ? withFinal : withoutFinal);
        }

        public static string IGa(string word) => Attach(word, "이", "가");
        public static string EunNeun(string word) => Attach(word, "은", "는");
        public static string EulReul(string word) => Attach(word, "을", "를");
        public static string WaGwa(string word) => Attach(word, "과", "와");

        /// <summary>ㄹ 받침이나 받침 없음 = "로", 그 밖의 받침 = "으로".</summary>
        public static string EuroRo(string word)
        {
            var has = HasFinal(word, out bool rieul);
            if (has == null) return word + "(으)로";
            return word + (has.Value && !rieul ? "으로" : "로");
        }
    }
}
