using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Parâmetros de carga de UM cenário (LEVE, MÉDIO, PESADO...).
    /// Todos os valores são absolutos (não dependem do aparelho), para que o conteúdo
    /// de cada frame seja idêntico em qualquer celular. O que varia entre aparelhos é o FPS.
    /// </summary>
    [CreateAssetMenu(menuName = "PerfBench/Scenario Profile", fileName = "Scenario_")]
    public class ScenarioProfile : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("Id usado no deeplink (ex.: light, medium, heavy).")]
        public string id = "light";
        public string displayName = "LEVE";

        [Header("Jogo")]
        [Tooltip("Velocidade do mundo em m/s.")]
        public float worldSpeed = 15f;
        [Tooltip("Distância entre fileiras de obstáculos, em metros.")]
        public float obstacleSpacing = 30f;

        [Header("Resolução / Anti-aliasing")]
        [Tooltip("Quantidade FIXA de pixels renderizados (megapixels). O render scale é calculado a partir da tela do aparelho.")]
        public float renderMegapixels = 0.9f;
        [Tooltip("1, 2, 4 ou 8.")]
        public int msaa = 1;
        public bool hdr = false;

        [Header("Complexidade de shader")]
        [Tooltip("Iterações do loop de ruído (ALU) por pixel nos materiais do jogo.")]
        public int shaderAluIterations = 4;

        [Header("Geometria instanciada (com instancing)")]
        public int instancedCount = 300;
        [Tooltip("Segmentos da esfera instanciada (16 ≈ 480 triângulos).")]
        public int instancedMeshSegments = 12;
        public bool instancedCastShadows = true;

        [Header("Objetos individuais (sem batching: 1 draw call cada)")]
        public int dynamicObjectCount = 20;
        public bool dynamicObjectsBlink = true;

        [Header("Fillrate")]
        [Tooltip("Camadas transparentes em tela cheia (overdraw).")]
        public int overdrawLayers = 0;
        public int overdrawAluIterations = 4;
        public int particleMax = 0;
        [Tooltip("Partículas emitidas por segundo.")]
        public float particleRate = 0f;

        [Header("Luzes e sombras")]
        public int pointLights = 0;
        public int shadowedSpotLights = 0;
        [Tooltip("0 desliga a sombra da luz principal.")]
        public int mainShadowResolution = 1024;
        [Range(1, 4)] public int shadowCascades = 1;
        public bool softShadows = false;
        public float shadowDistance = 40f;
        public int additionalShadowAtlasResolution = 1024;

        [Header("Pós-processamento")]
        public bool bloom = false;
        public bool bloomHighQuality = false;
        public bool depthOfField = false;
        public bool motionBlur = false;
        public bool colorGrading = false;

        [Header("Câmera virtual (render offscreen + readback + processamento)")]
        [Tooltip("0 desliga. Resolução quadrada do 'sensor'.")]
        public int virtualCameraResolution = 0;
        [Tooltip("Renderiza a cada N frames.")]
        public int virtualCameraInterval = 1;

        [Header("CPU")]
        [Tooltip("Elementos processados por frame nos jobs Burst (todas as threads de worker).")]
        public int cpuJobElements = 0;
        public int cpuJobIterations = 64;
        public int physicsBodies = 0;
        [Tooltip("Alocações de vida curta, em MB/s (independe do FPS).")]
        public float gcPressureMBps = 0f;
    }
}
