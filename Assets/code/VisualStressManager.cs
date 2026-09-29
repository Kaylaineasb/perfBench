using UnityEngine;

public class VisualStressManager : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private SpeedStageManager stageManager;
    [SerializeField] private Light mainLight; // Luz principal da cena para efeito de piscar
    [SerializeField] private GameObject stressObjectPrefab; // Qualquer cubo ou objeto para "voar"

    [Header("Objetos Voando (Estresse de GPU)")]
    [SerializeField] private int objectsMedio = 30;
    [SerializeField] private int objectsAvancado = 100;
    private GameObject[] flyingObjects;

    private float pulseTimer;

    void Start()
    {
        if (stageManager == null) stageManager = GetComponent<SpeedStageManager>();

        // Prepara o pool de objetos voadores decorativos
        flyingObjects = new GameObject[objectsAvancado];
        for (int i = 0; i < objectsAvancado; i++)
        {
            if (stressObjectPrefab != null)
            {
                flyingObjects[i] = Instantiate(stressObjectPrefab, transform);
            }
            else
            {
                // Se não passar prefab, cria cubos simples via código
                flyingObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flyingObjects[i].transform.SetParent(transform);
            }
            flyingObjects[i].transform.localScale = Vector3.one * 0.5f;
            flyingObjects[i].SetActive(false);
        }
    }

    void Update()
    {
        if (stageManager == null) return;

        switch (stageManager.currentStage)
        {
            case SpeedStageManager.SpeedStage.Leve:
                // TUDO DESATIVADO: Jogo leve e limpo
                SetFlyingObjectsActive(0);
                ResetLight();
                break;

            case SpeedStageManager.SpeedStage.Medio:
                // ESTRESSE MÉDIO: Alguns objetos voando e luz piscando devagar
                SetFlyingObjectsActive(objectsMedio);
                UpdateFlyingObjects(speed: 10f);
                PulsateLight(frequency: 5f);
                break;

            case SpeedStageManager.SpeedStage.Avancado:
                // ESTRESSE PESADO: 100 objetos voando e tela piscando rápido (caos visual)
                SetFlyingObjectsActive(objectsAvancado);
                UpdateFlyingObjects(speed: 25f);
                PulsateLight(frequency: 15f);
                break;
        }
    }

    // Liga/Desliga os objetos extras para não gastar memória além do necessário
    private void SetFlyingObjectsActive(int count)
    {
        for (int i = 0; i < flyingObjects.Length; i++)
        {
            if (flyingObjects[i] != null)
            {
                flyingObjects[i].SetActive(i < count);
            }
        }
    }

    // Animação matemática simples para fazer os objetos "voarem" pelo ar
    private void UpdateFlyingObjects(float speed)
    {
        for (int i = 0; i < flyingObjects.Length; i++)
        {
            if (flyingObjects[i].activeSelf)
            {
                // Faz os objetos orbitarem e flutuarem no cenário
                float time = Time.time * (speed * 0.1f) + i;
                Vector3 pos = new Vector3(
                    Mathf.Sin(time + i) * 12f,
                    Mathf.Cos(time * 0.5f) * 5f + 8f,
                    Mathf.Cos(time + i) * 20f + 10f
                );
                flyingObjects[i].transform.position = pos;
                flyingObjects[i].transform.Rotate(Vector3.up * speed * 10f * Time.deltaTime);
            }
        }
    }

    // Faz a luz da cena piscar trocando a intensidade (Efeito visual contínuo)
    private void PulsateLight(float frequency)
    {
        if (mainLight != null)
        {
            pulseTimer += Time.deltaTime * frequency;
            mainLight.intensity = Mathf.Lerp(0.3f, 2.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);
        }
    }

    private void ResetLight()
    {
        if (mainLight != null) mainLight.intensity = 1.0f;
    }
}