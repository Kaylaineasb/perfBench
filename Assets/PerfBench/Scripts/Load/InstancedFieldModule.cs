using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerfBench
{
    /// <summary>
    /// Milhares de objetos COM GPU instancing (Graphics.RenderMeshInstanced),
    /// espalhados ao lado da pista e vindo em direção à câmera.
    /// As matrizes são calculadas por um job Burst paralelo (carga de CPU real).
    /// Posições são função de (seed, índice, distância, tempo): determinísticas.
    /// </summary>
    public class InstancedFieldModule : LoadModule
    {
        public override string Id => "instanced";

        [SerializeField] float fieldLength = 180f;
        [SerializeField] float fieldHalfWidth = 32f;
        [SerializeField] float trackClearance = 5f;
        [SerializeField] float maxHeight = 22f;

        const int BatchSize = 1023;

        NativeArray<Matrix4x4> matrices;
        JobHandle handle;
        bool scheduled;
        int count;
        int meshSegments = -1;
        Mesh mesh;
        Material material;
        RenderParams rp;

        public override int ActiveObjectCount => count;
        public int Triangles => mesh != null ? (int)(mesh.GetIndexCount(0) / 3) : 0;

        protected override void OnInitialize()
        {
            material = MakeMaterial(new Color(0.55f, 0.75f, 0.95f));
            material.enableInstancing = true;
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            handle.Complete();
            scheduled = false;

            count = Mathf.Max(0, p.instancedCount);
            if (count > 0 && (!matrices.IsCreated || matrices.Length < count))
            {
                if (matrices.IsCreated) matrices.Dispose();
                matrices = new NativeArray<Matrix4x4>(count, Allocator.Persistent);
            }
            if (p.instancedMeshSegments != meshSegments)
            {
                if (mesh != null) Destroy(mesh);
                meshSegments = p.instancedMeshSegments;
                mesh = MeshFactory.Sphere(meshSegments);
            }
            rp = new RenderParams(material)
            {
                shadowCastingMode = p.instancedCastShadows && !Off("shadows") ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(new Vector3(0f, maxHeight * 0.5f, fieldLength * 0.5f),
                                         new Vector3(fieldHalfWidth * 2f + 4f, maxHeight + 4f, fieldLength + 20f))
            };
        }

        void Update()
        {
            if (!Ready || count == 0) return;
            var job = new FieldJob
            {
                matrices = matrices.Reinterpret<float4x4>(),
                seed = (uint)Ctx.seed,
                distance = (float)(Ctx.runner.Distance % fieldLength),
                time = (float)Ctx.runner.Elapsed,
                fieldLength = fieldLength,
                halfWidth = fieldHalfWidth,
                clearance = trackClearance,
                maxHeight = maxHeight
            };
            handle = job.Schedule(count, 256);
            scheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        void LateUpdate()
        {
            if (!scheduled) return;
            handle.Complete();
            scheduled = false;
            for (int start = 0; start < count; start += BatchSize)
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, Mathf.Min(BatchSize, count - start), start);
        }

        void OnDestroy()
        {
            handle.Complete();
            if (matrices.IsCreated) matrices.Dispose();
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }

        [BurstCompile]
        struct FieldJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<float4x4> matrices;
            public uint seed;
            public float distance, time, fieldLength, halfWidth, clearance, maxHeight;

            public void Execute(int i)
            {
                uint h = DeterministicRandom.Hash((uint)i * 0x9E3779B9u ^ seed);
                float r0 = DeterministicRandom.Hash01(h);
                float r1 = DeterministicRandom.Hash01(h + 1u);
                float r2 = DeterministicRandom.Hash01(h + 2u);
                float r3 = DeterministicRandom.Hash01(h + 3u);
                float r4 = DeterministicRandom.Hash01(h + 4u);

                float side = r0 < 0.5f ? -1f : 1f;
                float x = side * math.lerp(clearance, halfWidth, r1);
                float y = math.lerp(0.5f, maxHeight, r2 * r2);
                float z = r3 * fieldLength - distance;
                if (z < -10f) z += fieldLength;
                float scale = math.lerp(0.35f, 1.6f, r4);

                float3 axis = math.normalize(new float3(r1 - 0.5f, 1f, r2 - 0.5f));
                quaternion q = quaternion.AxisAngle(axis, time * (0.5f + 2f * r0) + r3 * 6.2831853f);
                matrices[i] = float4x4.TRS(new float3(x, y, z), q, new float3(scale));
            }
        }
    }
}
