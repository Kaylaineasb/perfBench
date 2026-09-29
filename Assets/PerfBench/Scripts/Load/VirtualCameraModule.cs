using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PerfBench
{
    /// <summary>
    /// Simula o pipeline de uma câmera: uma segunda câmera renderiza a cena num
    /// RenderTexture quadrado ("sensor"), o frame é lido de volta de forma ASSÍNCRONA
    /// (AsyncGPUReadback, sem travar CPU e GPU como ReadPixels) e processado por um
    /// job Burst paralelo (tons de cinza + filtro de borda Sobel).
    /// Custo: render extra na GPU + banda de memória + CPU em todas as threads.
    /// </summary>
    public class VirtualCameraModule : LoadModule
    {
        public override string Id => "vcam";

        const int TransparentFxLayer = 1;
        const int MaxInFlight = 3;

        Camera vcam;
        RenderTexture rt;
        int res, interval = 1;
        NativeArray<byte> input, edges;
        NativeArray<int> rowSums;
        JobHandle handle;
        bool jobRunning, disposed;
        int inFlight;

        public int FramesProcessed { get; private set; }
        public long Checksum { get; private set; }
        public override int ActiveObjectCount => res > 0 ? 1 : 0;

        protected override void OnInitialize()
        {
            var go = new GameObject("VirtualCameraSensor");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(new Vector3(0f, 12f, -4f), Quaternion.Euler(35f, 0f, 0f));
            vcam = go.AddComponent<Camera>();
            vcam.enabled = false;
            vcam.fieldOfView = 60f;
            vcam.farClipPlane = 200f;
            vcam.depth = -10f;
            vcam.cullingMask = ~(1 << TransparentFxLayer);
            vcam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            int newRes = SystemInfo.supportsAsyncGPUReadback ? Mathf.Max(0, p.virtualCameraResolution) : 0;
            interval = Mathf.Max(1, p.virtualCameraInterval);
            if (newRes == res) return;

            handle.Complete();
            jobRunning = false;
            FreeBuffers();
            res = newRes;
            if (res == 0) { vcam.enabled = false; return; }

            rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { name = "PB_VirtualSensor" };
            rt.Create();
            vcam.targetTexture = rt;
            input = new NativeArray<byte>(res * res * 4, Allocator.Persistent);
            edges = new NativeArray<byte>(res * res, Allocator.Persistent);
            rowSums = new NativeArray<int>(res, Allocator.Persistent);
        }

        void Update()
        {
            if (!Ready || res == 0) return;

            if (jobRunning && handle.IsCompleted)
            {
                handle.Complete();
                jobRunning = false;
                FramesProcessed++;
                Checksum += rowSums[res / 2];
            }

            // A câmera ficou habilitada no frame anterior => o RT tem um frame novo.
            if (vcam.enabled && inFlight < MaxInFlight)
            {
                inFlight++;
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, OnReadback);
            }
            vcam.enabled = Time.frameCount % interval == 0;
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            inFlight = Mathf.Max(0, inFlight - 1);
            if (disposed || req.hasError || jobRunning || !input.IsCreated) return;
            var data = req.GetData<byte>();
            if (data.Length != input.Length) return;

            NativeArray<byte>.Copy(data, input, input.Length);
            handle = new SobelJob { rgba = input, edges = edges, rowSums = rowSums, width = res }.Schedule(res, 16);
            jobRunning = true;
            JobHandle.ScheduleBatchedJobs();
        }

        void FreeBuffers()
        {
            if (vcam != null) vcam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
            if (input.IsCreated) input.Dispose();
            if (edges.IsCreated) edges.Dispose();
            if (rowSums.IsCreated) rowSums.Dispose();
        }

        void OnDestroy()
        {
            disposed = true;
            handle.Complete();
            FreeBuffers();
        }

        [BurstCompile]
        struct SobelJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> rgba;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> edges;
            [WriteOnly] public NativeArray<int> rowSums;
            public int width;

            int L(int x, int y)
            {
                int i = (y * width + x) * 4;
                return (rgba[i] * 77 + rgba[i + 1] * 150 + rgba[i + 2] * 29) >> 8;
            }

            public void Execute(int y)
            {
                if (y == 0 || y >= width - 1) { rowSums[y] = 0; return; }
                int sum = 0;
                for (int x = 1; x < width - 1; x++)
                {
                    int a = L(x - 1, y - 1), b = L(x, y - 1), c = L(x + 1, y - 1);
                    int d = L(x - 1, y), f = L(x + 1, y);
                    int g = L(x - 1, y + 1), h = L(x, y + 1), k = L(x + 1, y + 1);
                    int gx = -a - 2 * d - g + c + 2 * f + k;
                    int gy = -a - 2 * b - c + g + 2 * h + k;
                    int m = math.min(255, (math.abs(gx) + math.abs(gy)) >> 2);
                    edges[y * width + x] = (byte)m;
                    sum += m;
                }
                rowSums[y] = sum;
            }
        }
    }
}
