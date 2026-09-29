using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    public static float GlobalSpeed = 15f; 
    private ObstacleManager manager;

    void Start()
    {
        // Encontra o manager na cena uma única vez
        manager = FindFirstObjectByType<ObstacleManager>();
    }

    void Update()
    {
        transform.Translate(Vector3.back * GlobalSpeed * Time.deltaTime);

        if (transform.position.z < -5f)
        {
            if (manager != null)
            {
                // Devolve para a fila do Pool
                manager.ReturnToPool(this.gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}