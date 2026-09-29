using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Pressão de GC CONTROLADA: aloca blocos de 64 KB a uma taxa fixa em MB/s
    /// (independente do FPS). Só os últimos 64 blocos (4 MB) ficam vivos; o resto vira
    /// lixo de vida curta, exercitando o coletor incremental sem risco de estourar memória.
    /// </summary>
    public class GcPressureModule : LoadModule
    {
        public override string Id => "gc";

        const int Chunk = 64 * 1024;
        const int RingSize = 64;
        readonly byte[][] ring = new byte[RingSize][];
        int index;
        double debtBytes;
        float rateMBps;

        public long BytesAllocated { get; private set; }

        public override void ApplyProfile(ScenarioProfile p)
        {
            rateMBps = Mathf.Max(0f, p.gcPressureMBps);
            if (rateMBps == 0f) { System.Array.Clear(ring, 0, RingSize); debtBytes = 0; }
        }

        void Update()
        {
            if (!Ready || rateMBps <= 0f) return;
            debtBytes += rateMBps * 1048576.0 * Time.unscaledDeltaTime;
            int guard = 0;
            while (debtBytes >= Chunk && guard++ < 256)
            {
                var block = new byte[Chunk];
                block[(index * 4099) & (Chunk - 1)] = 1;
                ring[index++ & (RingSize - 1)] = block;
                debtBytes -= Chunk;
                BytesAllocated += Chunk;
            }
        }
    }
}
