using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Configuração geral: perfis disponíveis e valores padrão usados quando o app
    /// é aberto sem deeplink (toque no ícone).
    /// </summary>
    [CreateAssetMenu(menuName = "PerfBench/Benchmark Config", fileName = "BenchmarkConfig")]
    public class BenchmarkConfig : ScriptableObject
    {
        public List<ScenarioProfile> profiles = new List<ScenarioProfile>();

        [Header("Padrões (sem deeplink)")]
        public List<string> defaultSequence = new List<string> { "light", "medium", "heavy" };
        public float defaultStageSeconds = 180f;
        public int defaultSeed = 42;
        public int defaultLoops = 1;
        public bool defaultOverlay = true;
        public bool autoQuitOnFinish = false;

        [Header("Medição")]
        [Tooltip("Segundos iniciais de cada cenário excluídos das estatísticas (troca de resolução/MSAA causa travadas).")]
        public float warmupSeconds = 3f;

        [Header("Jogo")]
        public float firstRowDistance = 40f;

        public ScenarioProfile Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            key = key.Trim().ToLowerInvariant();
            switch (key)
            {
                case "leve": case "l": key = "light"; break;
                case "medio": case "médio": case "m": key = "medium"; break;
                case "pesado": case "avancado": case "avançado": case "h": key = "heavy"; break;
            }
            foreach (var p in profiles)
                if (p != null && string.Equals(p.id, key, StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }
    }
}
