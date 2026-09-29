using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SpeedStageManager : MonoBehaviour
{
    public enum SpeedStage { Leve, Medio, Avancado }

    [Header("Configurações de Nível")]
    public SpeedStage currentStage = SpeedStage.Leve;

    [Header("Velocidades de Cada Nível")]
    [SerializeField] private float speedLeve = 25f;
    [SerializeField] private float speedMedio = 50f;
    [SerializeField] private float speedAvancado = 65f;

    [Header("Tempo de Transição (em Segundos)")]
    [SerializeField] private float timePerStageInSeconds = 180f; 

    private float timer = 0f;
    
    // Lista para forçar o barramento de memória (RAM)
    private List<byte[]> memoryStressList = new List<byte[]>();

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        ApplySpeed();
    }

    void Update()
    {
        if (currentStage != SpeedStage.Avancado)
        {
            timer += Time.deltaTime;
            if (timer >= timePerStageInSeconds)
            {
                AdvanceStage();
                timer = 0f;
            }
        }

        ApplyHeavySystemStress();
    }

    private void ApplyHeavySystemStress()
    {
        switch (currentStage)
        {
            case SpeedStage.Leve:
                // S24 Frio
                memoryStressList.Clear();
                break;

            case SpeedStage.Medio:
                // Carga Moderada
                RunParallelMath(300000);
                break;

            case SpeedStage.Avancado:
                // CARGA EXTREMA: CPU + Forçar Barramento de Memória RAM
                RunParallelMath(1200000);

                // Aloca 5MB por frame e limpa para forçar o barramento da memória
                if (memoryStressList.Count > 50) memoryStressList.Clear();
                memoryStressList.Add(new byte[5 * 1024 * 1024]);
                break;
        }
    }

    private void RunParallelMath(int iterations)
    {
        Parallel.For(0, iterations, i =>
        {
            double dummy = Mathf.Sin(i) * Mathf.Cos(i) * Mathf.Tan(i) * Mathf.Sqrt(i + 1);
        });
    }

    private void AdvanceStage()
    {
        if (currentStage == SpeedStage.Leve)
        {
            currentStage = SpeedStage.Medio;
            Debug.Log("<color=yellow>[STAGE MUDOU]</color> Entrou no Nível MÉDIO!");
        }
        else if (currentStage == SpeedStage.Medio)
        {
            currentStage = SpeedStage.Avancado;
            Debug.Log("<color=red>[STAGE MUDOU]</color> Entrou no Nível AVANÇADO!");
        }

        ApplySpeed();
    }

    private void ApplySpeed()
    {
        switch (currentStage)
        {
            case SpeedStage.Leve:
                MovingObstacle.GlobalSpeed = speedLeve;
                break;
            case SpeedStage.Medio:
                MovingObstacle.GlobalSpeed = speedMedio;
                break;
            case SpeedStage.Avancado:
                MovingObstacle.GlobalSpeed = speedAvancado;
                break;
        }
    }
}