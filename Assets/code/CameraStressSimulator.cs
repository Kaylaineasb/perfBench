using UnityEngine;

public class CameraStressSimulator : MonoBehaviour
{
    [Header("Simulação de Câmera (Processamento de Imagem)")]
    private RenderTexture fakeCameraTexture;
    private Texture2D processingBuffer;
    private Camera stressCamera;

    [SerializeField] private SpeedStageManager stageManager;

    void Start()
    {
        if (stageManager == null) stageManager = GetComponent<SpeedStageManager>();

        // Cria uma câmera virtual invisível para simular a leitura do sensor
        GameObject camObj = new GameObject("FakeCameraSensor");
        camObj.transform.SetParent(transform);
        stressCamera = camObj.AddComponent<Camera>();
        stressCamera.enabled = false; // Vamos disparar a renderização manualmente

        // Aloca uma textura em alta resolução (Simulando sensor 2K)
        fakeCameraTexture = new RenderTexture(2048, 2048, 24, RenderTextureFormat.ARGB32);
        fakeCameraTexture.Create();
        stressCamera.targetTexture = fakeCameraTexture;

        processingBuffer = new Texture2D(512, 512, TextureFormat.RGBA32, false);
    }

    void Update()
    {
        if (stageManager == null) return;

        // No nível Avançado, força o processamento de imagem idêntico ao uso de câmera
        if (stageManager.currentStage == SpeedStageManager.SpeedStage.Avancado)
        {
            SimulateCameraFrameProcessing();
        }
    }

    private void SimulateCameraFrameProcessing()
    {
        // 1. Força a GPU a renderizar um frame em alta resolução (Simula captura do sensor)
        stressCamera.Render();

        // 2. Leitura de pixels da GPU para a RAM (Gargalo idêntico ao da câmera)
        RenderTexture.active = fakeCameraTexture;
        processingBuffer.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        processingBuffer.Apply();
        RenderTexture.active = null;

        // 3. Processamento de Matriz de Pixels (Simula algoritmo de Visão Computacional / Face Detection)
        Color32[] pixels = processingBuffer.GetPixels32();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Operação de manipulação de canal de cor / matriz por pixel
            byte avg = (byte)((pixels[i].r + pixels[i].g + pixels[i].b) / 3);
            pixels[i].r = avg;
        }
    }

    private void OnDestroy()
    {
        if (fakeCameraTexture != null) fakeCameraTexture.Release();
    }
}