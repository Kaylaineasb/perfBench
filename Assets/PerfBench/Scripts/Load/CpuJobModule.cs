using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Carga de CPU em TODAS as threads de worker, sem bloquear a thread principal:
    /// o job é agendado no início do frame e só é concluído no início do frame seguinte,
    /// rodando em paralelo com a renderização.
    ///
    /// O estado é realimentado a cada frame e um valor é lido periodicamente, então o
    /// compilador (Burst/IL2CPP) não consegue eliminar o cálculo como código morto,
    /// ao contrário do Parallel.For da versão anterior.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CpuJobModule : LoadModule
    {
        public override string Id => "cpu";

        NativeArray<float4> state;
        JobHandle handle;
        bool scheduled;
        int elements, iterations;

        public float Checksum { get; private set; }

        public override void ApplyProfile(ScenarioProfile p)
        {
            handle.Complete();
            scheduled = false;
            elements = Mathf.Max(0, p.cpuJobElements);
            iterations = Mathf.Max(1, p.cpuJobIterations);
            if (elements > 0 && (!state.IsCreated || state.Length < elements))
            {
                if (state.IsCreated) state.Dispose();
                state = new NativeArray<float4>(elements, Allocator.Persistent);
                for (int i = 0; i < elements; i++)
                {
                    uint h = DeterministicRandom.Hash((uint)i ^ (uint)Ctx.seed);
                    state[i] = new float4(DeterministicRandom.Hash01(h), DeterministicRandom.Hash01(h + 1u),
                                          DeterministicRandom.Hash01(h + 2u), DeterministicRandom.Hash01(h + 3u));
                }
            }
        }

        void Update()
        {
            if (!Ready) return;
            if (scheduled)
            {
                handle.Complete();
                scheduled = false;
                if ((Time.frameCount & 31) == 0 && elements > 0)
                    Checksum += state[Time.frameCount % elements].x;
            }
            if (elements == 0) return;
            handle = new ChaosJob { state = state, iterations = iterations }.Schedule(elements, 1024);
            scheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        void OnDestroy()
        {
            handle.Complete();
            if (state.IsCreated) state.Dispose();
        }

        [BurstCompile]
        struct ChaosJob : IJobParallelFor
        {
            public NativeArray<float4> state;
            public int iterations;

            public void Execute(int i)
            {
                float4 s = state[i];
                for (int k = 0; k < iterations; k++)
                {
                    s = math.frac(s * 1.6180339f + math.sin(s.yzwx * 6.2831853f) * 0.5f + 0.1234f);
                    s = s * 0.999f + math.sqrt(math.abs(s.zwxy)) * 0.001f;
                }
                state[i] = s;
            }
        }
    }
}
