using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Partículas transparentes aditivas voando em direção à câmera (simulação de partículas
    /// na CPU + fillrate na GPU). Seed fixa; a taxa de emissão é por segundo, então a
    /// quantidade em tela é equivalente entre aparelhos (não bit a bit idêntica).
    /// </summary>
    public class ParticleModule : LoadModule
    {
        public override string Id => "particles";

        ParticleSystem ps;
        public override int ActiveObjectCount => ps != null ? ps.particleCount : 0;

        protected override void OnInitialize()
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(new Vector3(0f, 6f, 95f), Quaternion.Euler(0f, 180f, 0f));
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = 3f;
            main.startSpeed = 30f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f, 0.6f), new Color(0.3f, 0.7f, 1f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1;

            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)Ctx.seed;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 14f, 20f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Ctx.particle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            if (ps == null) return;
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, p.particleMax);
            var emission = ps.emission;
            emission.rateOverTime = p.particleMax > 0 ? p.particleRate : 0f;

            if (p.particleMax > 0 && Ctx.particle != null) { if (!ps.isPlaying) ps.Play(); }
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
