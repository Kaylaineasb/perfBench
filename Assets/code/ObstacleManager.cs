using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    [Header("Pool Setup")]
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private int poolSize = 10;
    private Queue<GameObject> obstaclePool;

    [Header("Spawn Settings (Dinâmico)")]
    // Em vez de um tempo fixo, definimos a distância em metros entre um obstáculo e outro
    [SerializeField] private float distanceBetweenObstacles = 30f; 
    private float spawnTimer;
    
    private float[] lanes = new float[] { -2f, 0f, 2f };
    private float spawnZPosition = 40f;

    void Start()
    {
        obstaclePool = new Queue<GameObject>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.SetActive(false);
            obstaclePool.Enqueue(obj);
        }
    }

    void Update()
    {
        // O tempo necessário muda dinamicamente com base na velocidade global
        // Usamos MovingObstacle.GlobalSpeed para calcular o ritmo ideal
        float dynamicSpawnInterval = distanceBetweenObstacles / MovingObstacle.GlobalSpeed;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= dynamicSpawnInterval)
        {
            SpawnObstacleFromPool();
            spawnTimer = 0f;
        }
    }

    void SpawnObstacleFromPool()
    {
        if (obstaclePool.Count == 0) return;

        GameObject obstacle = obstaclePool.Dequeue();

        if (obstacle != null)
        {
            float randomLane = lanes[Random.Range(0, lanes.Length)];
            obstacle.transform.position = new Vector3(randomLane, 1f, spawnZPosition);
            obstacle.SetActive(true);
        }
    }

    public void ReturnToPool(GameObject obstacle)
    {
        obstacle.SetActive(false);
        obstaclePool.Enqueue(obstacle);
    }
}