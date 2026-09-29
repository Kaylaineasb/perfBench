using UnityEngine;

public class AutomatedPlayer : MonoBehaviour
{
    [Header("Configurações de Pista")]
    private float[] lanes = new float[] { -2f, 0f, 2f };
    private int currentLane = 1; 
    
    [Header("Movimentação Lateral & Pulo")]
    [SerializeField] private float laneChangeSpeed = 15f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = 25f;
    
    [Header("Inteligência do Bot (Raycast)")]
    [SerializeField] private float detectionDistance = 12f;
    [SerializeField] private LayerMask obstacleLayer;

    private Vector3 targetPosition;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        targetPosition = new Vector3(lanes[currentLane], 1.0f, transform.position.z);
    }

    void Update()
    {
        DecideNextMove();

        targetPosition.x = lanes[currentLane];
        Vector3 newPos = transform.position;
        newPos.x = Mathf.MoveTowards(transform.position.x, targetPosition.x, laneChangeSpeed * Time.deltaTime);
        
        ApplyGravityAndJump(ref newPos);
        transform.position = newPos;
    }

    void DecideNextMove()
    {
        // FIX: O raio agora é desenhado estavelmente na altura do chão (1.0f)
        Vector3 rayOrigin = new Vector3(transform.position.x, 1.0f, transform.position.z);
        Debug.DrawRay(rayOrigin, Vector3.forward * detectionDistance, Color.green);

        if (Mathf.Abs(transform.position.x - lanes[currentLane]) > 0.1f)
        {
            return; 
        }

        RaycastHit hit;
        // FIX: O Raycast agora sai da base do chão, evitando ignorar obstáculos enquanto pula
        if (Physics.Raycast(rayOrigin, Vector3.forward, out hit, detectionDistance, obstacleLayer))
        {
            if (currentLane == 1)
            {
                // FIX: Random.Range para inteiros inclui o mínimo e EXCLUI o máximo. 
                // Para sortear entre 0 e 2 (esquerda ou direita), precisamos ir até 3.
                currentLane = Random.Range(0, 2) == 0 ? 0 : 2;
            }
            else
            {
                currentLane = 1;
            }
        }
    }

    void ApplyGravityAndJump(ref Vector3 currentPos)
    {
        if (currentPos.y <= 1.0f) 
        {
            currentPos.y = 1.0f;
            isGrounded = true;
            velocity.y = 0f;
        }
        else
        {
            isGrounded = false;
            velocity.y -= gravity * Time.deltaTime;
        }

        currentPos.y += velocity.y * Time.deltaTime;
    }
}