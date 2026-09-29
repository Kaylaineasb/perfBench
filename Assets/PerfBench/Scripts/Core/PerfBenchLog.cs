using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Sinais para o sistema externo:
    ///  1) logcat com a tag PERFBENCH (adb logcat -s PERFBENCH)
    ///  2) arquivo perfbench/status.json no diretório do app (adb pull)
    /// </summary>
    public static class PerfBenchLog
    {
        const string Tag = "PERFBENCH";

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass logClass;
#endif

        public static string RootDir => Path.Combine(Application.persistentDataPath, "perfbench");
        public static string StatusPath => Path.Combine(RootDir, "status.json");

        public static void Signal(string msg)
        {
            Debug.Log("[PERFBENCH] " + msg);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (logClass == null) logClass = new AndroidJavaClass("android.util.Log");
                logClass.CallStatic<int>("i", Tag, msg);
            }
            catch (Exception e) { Debug.LogWarning("PERFBENCH logcat: " + e.Message); }
#endif
        }

        [Serializable]
        class Status
        {
            public string state;
            public string runId;
            public int stageIndex;
            public string stageId;
            public int stageCount;
            public double elapsedSeconds;
            public string outputDir;
            public string updatedUtc;
        }

        public static void WriteStatus(string state, BenchmarkRunner r)
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                var s = new Status
                {
                    state = state,
                    runId = r.RunId,
                    stageIndex = r.StageIndex,
                    stageId = r.CurrentProfile != null ? r.CurrentProfile.id : "",
                    stageCount = r.Timeline != null ? r.Timeline.stages.Count : 0,
                    elapsedSeconds = r.Elapsed,
                    outputDir = r.OutputDir,
                    updatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };
                File.WriteAllText(StatusPath, JsonUtility.ToJson(s, true));
            }
            catch (Exception e) { Debug.LogWarning("PERFBENCH status: " + e.Message); }
        }
    }
}
