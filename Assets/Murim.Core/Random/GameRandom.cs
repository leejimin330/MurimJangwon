using System.Collections.Generic;

namespace Murim.Core.Random
{
    /// <summary>시드 기반 결정론 난수. 게임 로직의 모든 난수는 여기서 나온다.</summary>
    public sealed class GameRandom
    {
        public ulong State { get; set; }

        public GameRandom(ulong seed)
        {
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            State = z == 0 ? 0x2545F4914F6CDD1DUL : z;
        }

        public ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 2685821657736338717UL;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)(NextULong() % range));
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        public bool Chance(double p)
        {
            if (p <= 0) return false;
            if (p >= 1) return true;
            return NextDouble() < p;
        }

        public T Pick<T>(IReadOnlyList<T> items) => items[Next(0, items.Count)];

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Next(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
