using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Orquestra a execução: resolve opções, monta a linha do tempo de blocos,
    /// inicializa os módulos de carga, troca de cenário no tempo certo e sinaliza o fim.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class BenchmarkRunner : MonoBehaviour
    {
        public static BenchmarkRunner Instance { get; private set; }

        [SerializeField] BenchmarkConfig config;
        [SerializeField] Camera mainCamera;
        [SerializeField] Light mainLight;
        [SerializeField] Transform player;

        [Header("Materiais (criados pelo menu PerfBench)")]
        [SerializeField] Material stressLitMaterial;
        [SerializeField] Material overdrawMaterial;
        [SerializeField] Material particleMaterial;

        public BenchmarkConfig Config => config;
        public RunOptions Options { get; private set; }
        public StageTimeline Timeline { get; private set; }
        public BenchmarkContext Context { get; private set; }
        public IReadOnlyList<LoadModule> Modules => modules;

        public double Elapsed { get; private set; }
        public int StageIndex { get; private set; } = -1;
        public double Distance { get; private set; }
        public bool IsFinished { get; private set; }
        public string RunId { get; private set; }
        public string OutputDir { get; private set; }
        public string StartUrl { get; private set; }
        public string StartedUtc { get; private set; }

        public StageTimeline.Stage CurrentStage =>
            Timeline.stages[Mathf.Clamp(StageIndex, 0, Timeline.stages.Count - 1)];
        public ScenarioProfile CurrentProfile =>
            Timeline != null && Timeline.stages.Count > 0 ? CurrentStage.profile : null;
        public double StageElapsed => Elapsed - CurrentStage.start;
        public float Speed => CurrentProfile != null ? CurrentProfile.worldSpeed : 0f;

        /// <summary>Disparado ao entrar em cada bloco (índice na linha do tempo).</summary>
        public event Action<int> StageStarted;
        /// <summary>Disparado no fim; true = completou, false = interrompido.</summary>
        public event Action<bool> RunEnded;

        readonly List<LoadModule> modules = new List<LoadModule>();
        double t0;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;

            Options = RunOptions.Resolve(config);
            StartUrl = Options.rawUrl;
            // Padrão do Android é 30 fps: desbloqueia já aqui (o RenderQualityModule refina).
            Application.targetFrameRate = Options.fpsCap > 0
                ? Options.fpsCap
                : Mathf.RoundToInt(RenderQualityModule.DetectMaxRefresh());

            var seq = new List<ScenarioProfile>();
            for (int loop = 0; loop < Options.loops; loop++)
                foreach (var id in Options.sequence)
                {
                    var p = config.Find(id);
                    if (p != null) seq.Add(p);
                    else Debug.LogWarning($"[PERFBENCH] Cenário desconhecido: '{id}'");
                }
            if (seq.Count == 0) seq.AddRange(config.profiles);
            Timeline = new StageTimeline(seq, Options.stageSeconds);

            StartedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            RunId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_seed" + Options.seed;
            OutputDir = Path.Combine(PerfBenchLog.RootDir, RunId);
            Directory.CreateDirectory(OutputDir);

            Context = new BenchmarkContext
            {
                runner = this,
                options = Options,
                seed = Options.seed,
                camera = mainCamera != null ? mainCamera : Camera.main,
                mainLight = mainLight,
                player = player,
                stressLit = stressLitMaterial,
                overdraw = overdrawMaterial,
                particle = particleMaterial
            };

            GetComponentsInChildren(true, modules);
        }

        void Start()
        {
            foreach (var m in modules)
                if (m is WorldSimulation w) Context.world = w;

            foreach (var m in modules)
            {
                if (Options.IsDisabled(m.Id) && !(m is WorldSimulation))
                {
                    m.enabled = false;
                    continue;
                }
                m.Initialize(Context);
            }

            t0 = Time.unscaledTimeAsDouble;
            Elapsed = 0;
            PerfBenchLog.Signal($"START run={RunId} seq={SequenceString()} stage={Options.stageSeconds.ToString(CultureInfo.InvariantCulture)} seed={Options.seed} out={OutputDir}");
            EnterStage(0);
        }

        void Update()
        {
            if (IsFinished) return;

            Elapsed = Time.unscaledTimeAsDouble - t0;
            int idx = Timeline.StageIndexAt(Elapsed);
            if (idx >= Timeline.stages.Count) { Finish(true); return; }
            if (idx != StageIndex) EnterStage(idx);
            Distance = Timeline.DistanceAt(Elapsed);
        }

        void EnterStage(int index)
        {
            StageIndex = index;
            var p = CurrentProfile;
            foreach (var m in modules)
                if (m.enabled) m.ApplyProfile(p);

            PerfBenchLog.Signal($"STAGE index={index} id={p.id} name={p.displayName}");
            PerfBenchLog.WriteStatus("running", this);
            StageStarted?.Invoke(index);
        }

        void Finish(bool completed)
        {
            if (IsFinished) return;
            IsFinished = true;
            RunEnded?.Invoke(completed);   // métricas gravam o summary.json antes do sinal END
            PerfBenchLog.WriteStatus(completed ? "finished" : "aborted", this);
            PerfBenchLog.Signal($"END run={RunId} status={(completed ? "finished" : "aborted")} out={OutputDir}");

            if (completed && Options.autoQuit) StartCoroutine(QuitSoon());
        }

        IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        void OnApplicationPause(bool paused)
        {
            if (!IsFinished && StageIndex >= 0)
                PerfBenchLog.Signal(paused ? "PAUSED (medição comprometida)" : "RESUMED");
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (!IsFinished && StageIndex >= 0) Finish(false);
            Instance = null;
        }

        string SequenceString()
        {
            var ids = new string[Timeline.stages.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = Timeline.stages[i].profile.id;
            return string.Join(",", ids);
        }
    }
}
