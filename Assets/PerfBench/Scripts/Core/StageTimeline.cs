using System.Collections.Generic;

namespace PerfBench
{
    /// <summary>
    /// Linha do tempo dos blocos. A distância percorrida é uma função analítica do tempo
    /// (soma de velocidade × duração de cada bloco), então a posição do mundo em
    /// um instante t é a mesma em qualquer aparelho, qualquer que seja o FPS.
    /// </summary>
    public sealed class StageTimeline
    {
        public struct Stage
        {
            public int index;
            public ScenarioProfile profile;
            public double start;
            public double duration;
            public double distanceStart;
            public double End => start + duration;
            public double DistanceEnd => distanceStart + profile.worldSpeed * duration;
        }

        public readonly List<Stage> stages = new List<Stage>();
        public double TotalDuration { get; }

        public StageTimeline(IList<ScenarioProfile> sequence, double stageSeconds)
        {
            double t = 0, d = 0;
            for (int i = 0; i < sequence.Count; i++)
            {
                var s = new Stage { index = i, profile = sequence[i], start = t, duration = stageSeconds, distanceStart = d };
                stages.Add(s);
                t = s.End;
                d = s.DistanceEnd;
            }
            TotalDuration = t;
        }

        /// <summary>Índice do bloco no tempo t; retorna stages.Count quando acabou.</summary>
        public int StageIndexAt(double t)
        {
            for (int i = 0; i < stages.Count; i++)
                if (t < stages[i].End) return i;
            return stages.Count;
        }

        public double DistanceAt(double t)
        {
            if (stages.Count == 0) return 0;
            if (t < 0) t = 0;
            int i = StageIndexAt(t);
            if (i >= stages.Count) i = stages.Count - 1;
            var s = stages[i];
            return s.distanceStart + s.profile.worldSpeed * (t - s.start);
        }

        /// <summary>Bloco que cobre a distância d (usado para gerar obstáculos à frente).</summary>
        public int StageIndexAtDistance(double d)
        {
            for (int i = 0; i < stages.Count; i++)
                if (d < stages[i].DistanceEnd) return i;
            return stages.Count - 1;
        }
    }
}
