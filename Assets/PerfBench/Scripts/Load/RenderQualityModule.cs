using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PerfBench
{
    /// <summary>
    /// Resolução interna fixa (em pixels), MSAA, HDR, sombras, complexidade de shader,
    /// pós-processamento e teto de FPS no refresh máximo do painel.
    ///
    /// Trabalha sobre uma CÓPIA do URP Asset em runtime, para não alterar o asset do projeto.
    /// Sub-ids desligáveis por deeplink: resolution, msaa, shadows, shader, post.
    /// </summary>
    public class RenderQualityModule : LoadModule
    {
        public override string Id => "render";

        public int TargetFps { get; private set; }
        public float MaxRefreshHz { get; private set; }
        public float RenderScale { get; private set; } = 1f;
        public int RenderWidth { get; private set; }
        public int RenderHeight { get; private set; }

        UniversalRenderPipelineAsset urp;
        RenderPipelineAsset originalQualityRP;
        bool swappedRP;

        VolumeProfile volumeProfile;
        Bloom bloom;
        DepthOfField dof;
        MotionBlur motionBlur;
        ColorAdjustments colorAdjustments;
        Tonemapping tonemapping;
        UniversalAdditionalCameraData camData;

        static readonly int AluId = Shader.PropertyToID("_PB_AluIterations");

        protected override void OnInitialize()
        {
            // 1) Teto de FPS = refresh máximo do painel. vSyncCount é ignorado no Android,
            //    então a carga extra precisa vir do custo por frame, não da quantidade de frames.
            MaxRefreshHz = DetectMaxRefresh();
            TargetFps = Ctx.options.fpsCap > 0 ? Ctx.options.fpsCap : Mathf.RoundToInt(MaxRefreshHz);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;

            // 2) Cópia do URP Asset
            var src = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (src != null)
            {
                originalQualityRP = QualitySettings.renderPipeline;
                urp = Instantiate(src);
                urp.name = src.name + " (PerfBench runtime)";
                QualitySettings.renderPipeline = urp;
                swappedRP = true;
            }
            else Debug.LogError("[PERFBENCH] URP não está ativo: módulo de renderização desligado.");

            // 3) Volume próprio de pós-processamento
            var volGo = new GameObject("PerfBench Volume");
            volGo.transform.SetParent(transform, false);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = volumeProfile;

            bloom = volumeProfile.Add<Bloom>(true);
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(0.9f);

            dof = volumeProfile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(10f);
            dof.focalLength.Override(60f);
            dof.aperture.Override(2.8f);

            motionBlur = volumeProfile.Add<MotionBlur>(true);
            motionBlur.quality.Override(MotionBlurQuality.High);
            motionBlur.intensity.Override(0.6f);

            colorAdjustments = volumeProfile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(0.1f);
            colorAdjustments.contrast.Override(15f);
            colorAdjustments.saturation.Override(10f);

            tonemapping = volumeProfile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            if (Ctx.camera != null)
            {
                Ctx.camera.allowHDR = true;
                Ctx.camera.allowMSAA = true;
                camData = Ctx.camera.GetUniversalAdditionalCameraData();
            }
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            if (urp != null)
            {
                // Resolução: mesma CONTAGEM de pixels em todo aparelho.
                long screenPixels = Math.Max(1L, (long)Screen.width * Screen.height);
                float scale = Off("resolution") ? 1f : Mathf.Sqrt(p.renderMegapixels * 1e6f / screenPixels);
                RenderScale = Mathf.Clamp(scale, 0.1f, 2f);
                urp.renderScale = RenderScale;
                RenderWidth = Mathf.RoundToInt(Screen.width * RenderScale);
                RenderHeight = Mathf.RoundToInt(Screen.height * RenderScale);
                if (Mathf.Abs(scale - RenderScale) > 0.001f)
                    Debug.LogWarning($"[PERFBENCH] renderScale {scale:0.00} fora de [0.1, 2]; limitado a {RenderScale:0.00}.");

                urp.msaaSampleCount = Off("msaa") ? 1 : Mathf.ClosestPowerOfTwo(Mathf.Clamp(p.msaa, 1, 8));
                urp.supportsHDR = p.hdr;

                bool shadows = !Off("shadows") && p.mainShadowResolution > 0;
                urp.shadowDistance = p.shadowDistance;
                urp.shadowCascadeCount = Mathf.Clamp(p.shadowCascades, 1, 4);
                if (shadows) TrySet(urp, "mainLightShadowmapResolution", p.mainShadowResolution);
                TrySet(urp, "additionalLightsShadowmapResolution", p.additionalShadowAtlasResolution);
                if (Ctx.mainLight != null)
                    Ctx.mainLight.shadows = shadows ? (p.softShadows ? LightShadows.Soft : LightShadows.Hard) : LightShadows.None;
            }

            Shader.SetGlobalFloat(AluId, Off("shader") ? 0f : p.shaderAluIterations);

            bool post = !Off("post");
            bloom.active = post && p.bloom;
            bloom.highQualityFiltering.Override(p.bloomHighQuality);
            dof.active = post && p.depthOfField;
            motionBlur.active = post && p.motionBlur;
            colorAdjustments.active = post && p.colorGrading;
            tonemapping.active = post && p.hdr;
            if (camData != null)
                camData.renderPostProcessing = post && (bloom.active || dof.active || motionBlur.active || colorAdjustments.active || tonemapping.active);
        }

        public static float DetectMaxRefresh()
        {
            double hz = Screen.currentResolution.refreshRateRatio.value;
            foreach (var r in Screen.resolutions)
                hz = Math.Max(hz, r.refreshRateRatio.value);
            if (double.IsNaN(hz) || hz < 30) hz = 60;
            return (float)hz;
        }

        // Algumas propriedades do URP Asset têm setter interno dependendo da versão:
        // reflection evita erro de compilação e só avisa se não existir.
        static void TrySet(object target, string prop, int value)
        {
            try
            {
                var pi = target.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var setter = pi?.GetSetMethod(true);
                if (setter == null) { Debug.LogWarning($"[PERFBENCH] URP: propriedade '{prop}' não encontrada."); return; }
                object v = pi.PropertyType.IsEnum ? Enum.ToObject(pi.PropertyType, value) : (object)value;
                setter.Invoke(target, new[] { v });
            }
            catch (Exception e) { Debug.LogWarning($"[PERFBENCH] URP: falha ao definir '{prop}': {e.Message}"); }
        }

        void OnDestroy()
        {
            if (swappedRP) QualitySettings.renderPipeline = originalQualityRP;
            if (urp != null) Destroy(urp);
            if (volumeProfile != null) Destroy(volumeProfile);
        }
    }
}
