namespace PerfBench
{
    /// <summary>
    /// Gerador pseudoaleatório próprio (SplitMix64). Não usa UnityEngine.Random nem
    /// System.Random, cujas implementações podem mudar entre versões. Mesma seed =
    /// mesma sequência em qualquer aparelho. Também oferece hashes sem estado,
    /// usáveis dentro de jobs Burst.
    /// </summary>
    public struct DeterministicRandom
    {
        ulong state;

        public DeterministicRandom(ulong seed)
        {
            state = seed ^ 0x9E3779B97F4A7C15UL;
            if (state == 0) state = 1;
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = (state += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat() => (NextULong() >> 40) * (1.0f / 16777216f);

        /// <summary>[min, maxExclusive)</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }

        public static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352dU;
                x ^= x >> 15; x *= 0x846ca68bU;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>[0, 1) sem estado.</summary>
        public static float Hash01(uint x) => (Hash(x) >> 8) * (1.0f / 16777216f);
    }
}
