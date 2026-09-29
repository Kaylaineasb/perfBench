using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PerfBench
{
    /// <summary>
    /// Opções da execução. Vêm do BenchmarkConfig e podem ser sobrescritas por deeplink.
    /// Leitor propositalmente mínimo: o contrato com o sistema do parceiro ainda está aberto.
    ///
    /// perfbench://start?seq=light,medium,heavy&amp;stage=180&amp;seed=42
    /// Aceita também: scenario=heavy, duration=180, loops=2, overlay=1, autoquit=0,
    /// fps=60 (teto manual), off=shadows,post (desliga módulos para calibração).
    /// </summary>
    public sealed class RunOptions
    {
        public List<string> sequence = new List<string>();
        public float stageSeconds;
        public int seed;
        public int loops = 1;
        public bool overlay;
        public bool autoQuit;
        public int fpsCap;
        public readonly HashSet<string> disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string source = "config";
        public string rawUrl = "";

        public bool IsDisabled(string id) => disabled.Contains(id);

        static string pendingUrl;

        public static RunOptions Resolve(BenchmarkConfig cfg)
        {
            var o = new RunOptions
            {
                sequence = new List<string>(cfg.defaultSequence),
                stageSeconds = cfg.defaultStageSeconds,
                seed = cfg.defaultSeed,
                loops = Mathf.Max(1, cfg.defaultLoops),
                overlay = cfg.defaultOverlay,
                autoQuit = cfg.autoQuitOnFinish
            };

            string url = !string.IsNullOrEmpty(pendingUrl) ? pendingUrl : Application.absoluteURL;
            pendingUrl = null;
            if (!string.IsNullOrEmpty(url) && url.StartsWith("perfbench:", StringComparison.OrdinalIgnoreCase))
                o.ApplyUrl(url);
            return o;
        }

        void ApplyUrl(string url)
        {
            source = "deeplink";
            rawUrl = url;
            autoQuit = true;   // controlado por máquina: fecha sozinho ao terminar
            overlay = false;   // overlay tem custo; só se pedido

            int q = url.IndexOf('?');
            if (q < 0) return;
            foreach (var pair in url.Substring(q + 1).Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                int eq = pair.IndexOf('=');
                string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim().ToLowerInvariant();
                string val = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')).Trim();

                switch (key)
                {
                    case "seq": case "sequence": case "scenario": case "scenarios":
                        sequence.Clear();
                        foreach (var s in val.Split(','))
                            if (!string.IsNullOrWhiteSpace(s)) sequence.Add(s.Trim());
                        break;
                    case "stage": case "duration":
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0) stageSeconds = f;
                        break;
                    case "seed":
                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sd)) seed = sd;
                        break;
                    case "loops":
                        if (int.TryParse(val, out var l) && l > 0) loops = l;
                        break;
                    case "fps":
                        if (int.TryParse(val, out var fps) && fps >= 0) fpsCap = fps;
                        break;
                    case "overlay": overlay = IsTrue(val); break;
                    case "autoquit": autoQuit = IsTrue(val); break;
                    case "off": case "disable":
                        foreach (var s in val.Split(','))
                            if (!string.IsNullOrWhiteSpace(s)) disabled.Add(s.Trim());
                        break;
                }
            }
        }

        static bool IsTrue(string v) => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);

        // App já aberto recebendo um novo deeplink: recarrega a cena com as novas opções.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookDeepLinks()
        {
            Application.deepLinkActivated -= OnDeepLink;
            Application.deepLinkActivated += OnDeepLink;
        }

        static void OnDeepLink(string url)
        {
            var r = BenchmarkRunner.Instance;
            // Alguns aparelhos disparam o evento também na abertura a frio: ignora duplicata.
            if (r != null && r.StartUrl == url && r.Elapsed < 2.0) return;
            pendingUrl = url;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
