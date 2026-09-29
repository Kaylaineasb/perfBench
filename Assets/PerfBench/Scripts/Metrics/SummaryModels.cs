using System;
using System.Collections.Generic;

namespace PerfBench
{
    [Serializable]
    public class DeviceInfo
    {
        public string deviceModel;
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public int processorFrequencyMHz;
        public int systemMemoryMB;
        public string graphicsDeviceName;
        public string graphicsDeviceVendor;
        public string graphicsDeviceType;
        public string graphicsDeviceVersion;
        public int graphicsMemoryMB;
        public int screenWidth;
        public int screenHeight;
        public float screenDpi;
        public float maxRefreshHz;
        public int targetFps;
        public bool frameTimingSupported;
        public string unityVersion;
        public string appVersion;
    }

    [Serializable]
    public class StageSummary
    {
        public int index;
        public string id;
        public string name;
        public double durationS;
        public double warmupS;
        public float worldSpeed;
        public int renderWidth;
        public int renderHeight;
        public float renderScale;

        public int frames;
        public int framesMeasured;
        public float fpsAvg;
        public float fpsMin1s;
        public float fpsMax1s;
        public float fps1Low;
        public float fps01Low;
        public float frameMsMedian;
        public float frameMsP95;
        public float frameMsP99;
        public float frameMsMax;

        public int frameTimingSamples;
        public float cpuFrameMsAvg;
        public float cpuMainMsAvg;
        public float cpuRenderMsAvg;
        public float gpuFrameMsAvg;

        public int batteryPctStart;
        public int batteryPctEnd;
        public long chargeCounterStartUah;
        public long chargeCounterEndUah;
        public long chargeDeltaUah;
        public double currentAvgRaw;
        public float batteryTempStartC;
        public float batteryTempEndC;
        public float batteryTempMaxC;
        public int thermalStatusMax;
        public float thermalHeadroomMax;
        public bool wasPlugged;

        public int gcCollections;
        public long worldRows;
        public string worldHash;
    }

    [Serializable]
    public class RunSummary
    {
        public string runId;
        public string startedUtc;
        public string endedUtc;
        public bool completed;
        public string source;
        public string url;
        public int seed;
        public float stageSeconds;
        public List<string> sequence = new List<string>();
        public List<string> disabledModules = new List<string>();
        public DeviceInfo device;
        public List<StageSummary> stages = new List<StageSummary>();
    }
}
