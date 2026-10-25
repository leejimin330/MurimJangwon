using System;
using System.Collections.Generic;

namespace Murim.Core.World
{
    /// <summary>특성 상성·기억 외에 행동으로 쌓인 호감도 변화량. 0이 아닌 쌍만 저장한다.</summary>
    public class OpinionStore
    {
        public SortedDictionary<string, int> Entries = new SortedDictionary<string, int>(StringComparer.Ordinal);

        static string Key(string from, string to) => from + "|" + to;

        public int GetStored(string from, string to) => Entries.TryGetValue(Key(from, to), out var v) ? v : 0;

        public void Add(string from, string to, int delta)
        {
            var key = Key(from, to);
            Entries.TryGetValue(key, out var v);
            v += delta;
            if (v == 0) Entries.Remove(key);
            else Entries[key] = v;
        }
    }
}
