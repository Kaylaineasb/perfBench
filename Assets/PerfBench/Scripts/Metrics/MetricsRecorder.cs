using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Coleta métricas técnicas e grava no aparelho:
    ///   perfbench/&lt;runId&gt;/metrics.csv   — 1 linha por segundo
    ///   perfbench/&lt;runId&gt;/summary.json  — estatísticas por cenário + informações do aparelho
    /// Opcionalmente exibe um overlay (deeplink overlay=1).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class MetricsRecorder : MonoBehaviour
    {
        [SerializeField] BenchmarkRunner runner;
        [SerializeField] int overlayFontSize = 30;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        ProfilerRecorder drawCalls, batches, setPass, triangles, vertices, totalMem, gcUsed, gcAlloc;
        readonly FrameTiming[] timings = new FrameTiming[1];
        StreamWriter csv;
        readonly StringBuilder sb = new StringBuilder(512);
        BatteryProbe battery;
        BatterySample lastBattery = BatterySample.Invalid;
        RenderQualityModule render;
        ParticleModule particles;
        RunSummary run;

        // Janela de 1 s
        int wFrames, wTiming, lastSecond = -1;
        double wSumMs, wCpu, wCpuMain, wCpuRender, wGpu;
        float wMin = float.MaxValue, wMax;

        // Cenário atual
        class StageAcc
        {
            public int index;
            public StageTimeline.Stage stage;
            public readonly List<float> frameMs = new List<float>(40000);
            public int totalFrames, timingCount, currentSamples, gcStart;
            public double cpu, cpuMain, cpuRender, gpu, currentSum;
            public float fpsMin1s = float.MaxValue, fpsMax1s, tempMax = float.NaN, headroomMax = float.NaN;
            public int thermalMax = -1;
            public bool plugged;
            public BatterySample start;
        }
        StageAcc acc;

        bool showOverlay;
        string overlayText = "";
        GUIStyle overlayStyle, shadowStyle;

        void Start()
        {
            if (runner == null) runner = BenchmarkRunner.Instance;
            if (runner == null) { enabled = false; return; }

            foreach (var m in runner.Modules)
            {
                if (m is RenderQualityModule r && m.enabled) render = r;
                if (m is ParticleModule p && m.enabled) particles = p;
            }

            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            totalMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            battery = new BatteryProbe();
            lastBattery = battery.Sample();

            csv = new StreamWriter(Path.Combine(runner.OutputDir, "metrics.csv"), false, new UTF8Encoding(false));
            csv.WriteLine("utc_ms,elapsed_s,stage_index,stage_id,stage_elapsed_s,speed_mps,distance_m," +
                          "fps_avg,fps_min_frame,fps_max_frame,frame_ms_avg,frame_ms_max," +
                          "cpu_frame_ms,cpu_main_ms,cpu_render_ms,gpu_frame_ms," +
                          "draw_calls,batches,setpass,triangles,vertices,active_objects,particles," +
                          "total_mem_mb,gc_used_mb,gc_alloc_frame_kb,gc_collections," +
                          "battery_pct,current_now_raw,charge_counter_uah,voltage_mv,battery_temp_c,plugged," +
                          "thermal_status,thermal_headroom,render_w,render_h,target_fps");
            csv.Flush();

            run = new RunSummary
            {
                runId = runner.RunId,
                startedUtc = runner.StartedUtc,
                source = runner.Options.source,
                url = runner.Options.rawUrl,
                seed = runner.Options.seed,
                stageSeconds = runner.Options.stageSeconds,
                device = CollectDevice()
            };
            foreach (var s in runner.Timeline.stages) run.sequence.Add(s.profile.id);
            run.disabledModules.AddRange(runner.Options.disabled);

            showOverlay = runner.Options.overlay;
            runner.StageStarted += OnStageStarted;
            runner.RunEnded += OnRunEnded;
            if (runner.StageIndex >= 0) OnStageStarted(runner.StageIndex);
        }

        void Update()
        {
            if (runner == null || runner.IsFinished || acc == null) return;

            float ms = Time.unscaledDeltaTime * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            bool hasTiming = FrameTimingManager.GetLatestTimings(1, timings) > 0;
            var ft = timings[0];

            wFrames++;
            wSumMs += ms;
            if (ms < wMin) wMin = ms;
            if (ms > wMax) wMax = ms;
            if (hasTiming)
            {
                wTiming++;
                wCpu += ft.cpuFrameTime; wCpuMain += ft.cpuMainThreadFrameTime;
                wCpuRender += ft.cpuRenderThreadFrameTime; wGpu += ft.gpuFrameTime;
            }

            acc.totalFrames++;
            if (runner.StageElapsed >= runner.Config.warmupSeconds)
            {
                acc.frameMs.Add(ms);
                if (hasTiming)
                {
                    acc.timingCount++;
                    acc.cpu += ft.cpuFrameTime; acc.cpuMain += ft.cpuMainThreadFrameTime;
                    acc.cpuRender += ft.cpuRenderThreadFrameTime; acc.gpu += ft.gpuFrameTime;
                }
            }

            int sec = (int)Math.Floor(runner.Elapsed);
            if (sec != lastSecond)
            {
                if (lastSecond >= 0) WriteSecond();
                lastSecond = sec;
            }
        }

        void WriteSecond()
        {
            if (wFrames == 0 || csv == null) return;
            lastBattery = battery.Sample();

            float fpsAvg = (float)(wFrames * 1000.0 / wSumMs);
            double cpu = wTiming > 0 ? wCpu / wTiming : -1, cpuMain = wTiming > 0 ? wCpuMain / wTiming : -1;
            double cpuRender = wTiming > 0 ? wCpuRender / wTiming : -1, gpu = wTiming > 0 ? wGpu / wTiming : -1;
            int activeObjects = 0;
            foreach (var m in runner.Modules) if (m.enabled) activeObjects += m.ActiveObjectCount;
            int particleCount = particles != null ? particles.ActiveObjectCount : 0;
            var p = runner.CurrentProfile;

            if (acc != null && runner.StageElapsed >= runner.Config.warmupSeconds + 1.0)
            {
                acc.fpsMin1s = Mathf.Min(acc.fpsMin1s, fpsAvg);
                acc.fpsMax1s = Mathf.Max(acc.fpsMax1s, fpsAvg);
            }
            if (acc != null && lastBattery.valid)
            {
                if (!float.IsNaN(lastBattery.temperatureC)) acc.tempMax = float.IsNaN(acc.tempMax) ? lastBattery.temperatureC : Mathf.Max(acc.tempMax, lastBattery.temperatureC);
                if (!float.IsNaN(lastBattery.thermalHeadroom)) acc.headroomMax = float.IsNaN(acc.headroomMax) ? lastBattery.thermalHeadroom : Mathf.Max(acc.headroomMax, lastBattery.thermalHeadroom);
                acc.thermalMax = Mathf.Max(acc.thermalMax, lastBattery.thermalStatus);
                if (lastBattery.plugged > 0) acc.plugged = true;
                if (lastBattery.currentNowRaw != long.MinValue && lastBattery.currentNowRaw != int.MinValue) { acc.currentSum += lastBattery.currentNowRaw; acc.currentSamples++; }
            }

            sb.Clear();
            sb.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Append(',')
              .Append(runner.Elapsed.ToString("0.000", Inv)).Append(',')
              .Append(runner.StageIndex).Append(',')
              .Append(p != null ? p.id : "").Append(',')
              .Append(runner.StageElapsed.ToString("0.000", Inv)).Append(',')
              .Append(runner.Speed.ToString("0.##", Inv)).Append(',')
              .Append(runner.Distance.ToString("0.0", Inv)).Append(',')
              .Append(fpsAvg.ToString("0.00", Inv)).Append(',')
              .Append((1000f / wMax).ToString("0.00", Inv)).Append(',')
              .Append((1000f / wMin).ToString("0.00", Inv)).Append(',')
              .Append((wSumMs / wFrames).ToString("0.000", Inv)).Append(',')
              .Append(wMax.ToString("0.000", Inv)).Append(',')
              .Append(cpu.ToString("0.000", Inv)).Append(',')
              .Append(cpuMain.ToString("0.000", Inv)).Append(',')
              .Append(cpuRender.ToString("0.000", Inv)).Append(',')
              .Append(gpu.ToString("0.000", Inv)).Append(',')
              .Append(Rec(drawCalls)).Append(',')
              .Append(Rec(batches)).Append(',')
              .Append(Rec(setPass)).Append(',')
              .Append(Rec(triangles)).Append(',')
              .Append(Rec(vertices)).Append(',')
              .Append(activeObjects).Append(',')
              .Append(particleCount).Append(',')
              .Append(Mb(totalMem)).Append(',')
              .Append(Mb(gcUsed)).Append(',')
              .Append(gcAlloc.Valid ? (gcAlloc.LastValue / 1024.0).ToString("0.0", Inv) : "-1").Append(',')
              .Append(GC.CollectionCount(0)).Append(',')
              .Append(lastBattery.levelPct).Append(',')
              .Append(L(lastBattery.currentNowRaw)).Append(',')
              .Append(L(lastBattery.chargeCounterUah)).Append(',')
              .Append(lastBattery.voltageMv).Append(',')
              .Append(F(lastBattery.temperatureC, "0.0")).Append(',')
              .Append(lastBattery.plugged).Append(',')
              .Append(lastBattery.thermalStatus).Append(',')
              .Append(F(lastBattery.thermalHeadroom, "0.000")).Append(',')
              .Append(render != null ? render.RenderWidth : Screen.width).Append(',')
              .Append(render != null ? render.RenderHeight : Screen.height).Append(',')
              .Append(render != null ? render.TargetFps : Application.targetFrameRate);
            csv.WriteLine(sb.ToString());
            csv.Flush();

            if (showOverlay)
            {
                overlayText =
                    $"{(p != null ? p.displayName : "")}  {runner.StageElapsed:0}s / {runner.CurrentStage.duration:0}s  (bloco {runner.StageIndex + 1}/{runner.Timeline.stages.Count})\n" +
                    $"FPS {fpsAvg:0}  (alvo {(render != null ? render.TargetFps : Application.targetFrameRate)})\n" +
                    $"CPU {cpu:0.0} ms  GPU {gpu:0.0} ms\n" +
                    $"Draw {Rec(drawCalls)}  Tris {(triangles.Valid ? triangles.LastValue / 1000 : -1)}k\n" +
                    $"Objetos {activeObjects}  Partículas {particleCount}\n" +
                    $"Render {(render != null ? render.RenderWidth : 0)}x{(render != null ? render.RenderHeight : 0)}\n" +
                    $"Bat {lastBattery.levelPct}%  I {L(lastBattery.currentNowRaw)}  {F(lastBattery.temperatureC, "0.0")}°C  térmico {lastBattery.thermalStatus}" +
                    (lastBattery.plugged > 0 ? "  [CARREGANDO]" : "");
            }

            wFrames = 0; wTiming = 0; wSumMs = 0; wCpu = wCpuMain = wCpuRender = wGpu = 0;
            wMin = float.MaxValue; wMax = 0;
        }

        void OnStageStarted(int index)
        {
            if (acc != null) CloseStage();
            acc = new StageAcc
            {
                index = index,
                stage = runner.Timeline.stages[index],
                start = battery.Sample(),
                gcStart = GC.CollectionCount(0)
            };
        }

        void CloseStage()
        {
            if (acc == null) return;
            var a = acc;
            acc = null;
            var end = battery.Sample();
            var p = a.stage.profile;
            var s = new StageSummary
            {
                index = a.index,
                id = p.id,
                name = p.displayName,
                durationS = a.stage.duration,
                warmupS = runner.Config.warmupSeconds,
                worldSpeed = p.worldSpeed,
                renderWidth = render != null ? render.RenderWidth : Screen.width,
                renderHeight = render != null ? render.RenderHeight : Screen.height,
                renderScale = render != null ? render.RenderScale : 1f,
                frames = a.totalFrames,
                framesMeasured = a.frameMs.Count,
                frameTimingSamples = a.timingCount,
                cpuFrameMsAvg = a.timingCount > 0 ? (float)(a.cpu / a.timingCount) : -1,
                cpuMainMsAvg = a.timingCount > 0 ? (float)(a.cpuMain / a.timingCount) : -1,
                cpuRenderMsAvg = a.timingCount > 0 ? (float)(a.cpuRender / a.timingCount) : -1,
                gpuFrameMsAvg = a.timingCount > 0 ? (float)(a.gpu / a.timingCount) : -1,
                fpsMin1s = a.fpsMin1s == float.MaxValue ? -1 : a.fpsMin1s,
                fpsMax1s = a.fpsMax1s,
                batteryPctStart = a.start.levelPct,
                batteryPctEnd = end.levelPct,
                chargeCounterStartUah = a.start.chargeCounterUah,
                chargeCounterEndUah = end.chargeCounterUah,
                chargeDeltaUah = (a.start.chargeCounterUah > 0 && end.chargeCounterUah > 0) ? end.chargeCounterUah - a.start.chargeCounterUah : 0,
                currentAvgRaw = a.currentSamples > 0 ? a.currentSum / a.currentSamples : 0,
                batteryTempStartC = San(a.start.temperatureC),
                batteryTempEndC = San(end.temperatureC),
                batteryTempMaxC = San(a.tempMax),
                thermalStatusMax = Mathf.Max(a.thermalMax, end.thermalStatus),
                thermalHeadroomMax = San(a.headroomMax),
                wasPlugged = a.plugged || end.plugged > 0,
                gcCollections = GC.CollectionCount(0) - a.gcStart
            };

            if (a.frameMs.Count > 0)
            {
                var f = a.frameMs.ToArray();
                Array.Sort(f);
                double sum = 0;
                foreach (var v in f) sum += v;
                s.fpsAvg = (float)(f.Length * 1000.0 / sum);
                s.frameMsMedian = Pct(f, 0.50);
                s.frameMsP95 = Pct(f, 0.95);
                s.frameMsP99 = Pct(f, 0.99);
                s.frameMsMax = f[f.Length - 1];
                s.fps1Low = LowFps(f, 0.01);
                s.fps01Low = LowFps(f, 0.001);
            }

            var world = runner.Context.world;
            if (world != null)
            {
                uint h = world.WorldHashUpTo(a.stage.DistanceEnd, out int rows);
                s.worldHash = h.ToString("X8");
                s.worldRows = rows;
            }
            run.stages.Add(s);
        }

        void OnRunEnded(bool completed)
        {
            if (run == null) return;
            WriteSecond();
            CloseStage();
            run.completed = completed;
            run.endedUtc = DateTime.UtcNow.ToString("o", Inv);
            try { File.WriteAllText(Path.Combine(runner.OutputDir, "summary.json"), JsonUtility.ToJson(run, true)); }
            catch (Exception e) { Debug.LogWarning("[PERFBENCH] summary.json: " + e.Message); }
            DisposeAll();
        }

        void OnDestroy()
        {
            if (runner != null) { runner.StageStarted -= OnStageStarted; runner.RunEnded -= OnRunEnded; }
            DisposeAll();
        }

        void DisposeAll()
        {
            if (csv != null) { csv.Flush(); csv.Dispose(); csv = null; }
            drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose();
            vertices.Dispose(); totalMem.Dispose(); gcUsed.Dispose(); gcAlloc.Dispose();
        }

        DeviceInfo CollectDevice() => new DeviceInfo
        {
            deviceModel = SystemInfo.deviceModel,
            operatingSystem = SystemInfo.operatingSystem,
            processorType = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            processorFrequencyMHz = SystemInfo.processorFrequency,
            systemMemoryMB = SystemInfo.systemMemorySize,
            graphicsDeviceName = SystemInfo.graphicsDeviceName,
            graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
            graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
            graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
            graphicsMemoryMB = SystemInfo.graphicsMemorySize,
            screenWidth = Screen.width,
            screenHeight = Screen.height,
            screenDpi = Screen.dpi,
            maxRefreshHz = render != null ? render.MaxRefreshHz : -1,
            targetFps = render != null ? render.TargetFps : Application.targetFrameRate,
            frameTimingSupported = FrameTimingManager.IsFeatureEnabled(),
            unityVersion = Application.unityVersion,
            appVersion = Application.version
        };

        // Percentil por posição (vetor ordenado)
        static float Pct(float[] sorted, double q) =>
            sorted[Mathf.Clamp((int)Math.Ceiling(q * sorted.Length) - 1, 0, sorted.Length - 1)];

        // "1% low": FPS médio dos piores 1% frames (maiores frame times)
        static float LowFps(float[] sorted, double fraction)
        {
            int n = Math.Max(1, (int)Math.Ceiling(sorted.Length * fraction));
            double sum = 0;
            for (int i = sorted.Length - n; i < sorted.Length; i++) sum += sorted[i];
            return (float)(n * 1000.0 / sum);
        }

        static string Rec(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString(Inv) : "-1";
        static string Mb(ProfilerRecorder r) => r.Valid ? (r.LastValue / 1048576.0).ToString("0.0", Inv) : "-1";
        static string L(long v) => v == long.MinValue || v == int.MinValue ? "" : v.ToString(Inv);
        static string F(float v, string fmt) => float.IsNaN(v) ? "" : v.ToString(fmt, Inv);
        static float San(float v) => float.IsNaN(v) || float.IsInfinity(v) ? -1f : v;

        void OnGUI()
        {
            if (!showOverlay || string.IsNullOrEmpty(overlayText)) return;
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.label) { fontSize = overlayFontSize };
                overlayStyle.normal.textColor = Color.white;
                shadowStyle = new GUIStyle(overlayStyle);
                shadowStyle.normal.textColor = Color.black;
            }
            var safe = Screen.safeArea;
            var rect = new Rect(safe.x + 24, Screen.height - safe.yMax + 24, safe.width - 48, safe.height);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), overlayText, shadowStyle);
            GUI.Label(rect, overlayText, overlayStyle);
        }
    }
}
