using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Debug script - attach to enemy to see real-time AI state in Inspector.
/// Helps diagnose why enemy isn't moving/attacking.
/// </summary>
public class EnemyAIDebug : MonoBehaviour
{
    [Header("Debug Info (Read Only)")]
    public string currentState = "Idle";
    public float distanceToPlayer = 0f;
    public float detectionRange = 8f;
    public float attackRange = 1.5f;
    public float stopRange = 1.2f;
    public float cooldownTimer = 0f;
    public bool playerInRange = false;
    public bool canAttack = false;
    public bool hasNavMeshAgent = false;
    public bool navMeshOnNavMesh = false;
    public Vector3 agentDestination;
    public Vector3 agentVelocity;
    public float agentSpeed = 0f;
    public bool hasPath = false;
    public float pathRemainingDistance = 0f;

    private EnemyAttack enemyAttack;
    private NavMeshAgent agent;
    private Transform playerTransform;

    void Awake()
    {
        enemyAttack = GetComponent<EnemyAttack>();
        agent = GetComponent<NavMeshAgent>();
        
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (enemyAttack == null) return;
        
        // Use reflection to read private fields
        var stateField = typeof(EnemyAttack).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cooldownField = typeof(EnemyAttack).GetField("cooldownTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var playerHealthField = typeof(EnemyAttack).GetField("playerHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var playerTransformField = typeof(EnemyAttack).GetField("playerTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (stateField != null) currentState = stateField.GetValue(enemyAttack).ToString();
        if (cooldownField != null) cooldownTimer = (float)cooldownField.GetValue(enemyAttack);
        if (playerTransformField != null) playerTransform = (Transform)playerTransformField.GetValue(enemyAttack);

        detectionRange = enemyAttack.detectionRange;
        attackRange = enemyAttack.attackRange;
        stopRange = enemyAttack.stopRange;

        // Calculate distance
        if (playerTransform != null)
        {
            distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            playerInRange = distanceToPlayer <= detectionRange;
            canAttack = distanceToPlayer <= attackRange && cooldownTimer <= 0f;
        }

        // NavMeshAgent info
        hasNavMeshAgent = agent != null;
        if (agent != null)
        {
            navMeshOnNavMesh = agent.isOnNavMesh;
            agentDestination = agent.destination;
            agentVelocity = agent.velocity;
            agentSpeed = agent.speed;
            hasPath = agent.hasPath;
            pathRemainingDistance = agent.remainingDistance;
        }
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        
        // Draw debug info in game view
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label($"<b>Enemy AI Debug: {gameObject.name}</b>", new GUIStyle { richText = true, fontSize = 14 });
        GUILayout.Space(5);
        
        GUILayout.Label($"State: <color=yellow>{currentState}</color>", richStyle);
        GUILayout.Label($"Distance to Player: <color=cyan>{distanceToPlayer:F2}</color> / Detection: {detectionRange} / Attack: {attackRange}", richStyle);
        GUILayout.Label($"Player In Range: {(playerInRange ? "<color=green>YES</color>" : "<color=red>NO</color>")}", richStyle);
        GUILayout.Label($"Can Attack: {(canAttack ? "<color=green>YES</color>" : "<color=red>NO</color>")}", richStyle);
        GUILayout.Label($"Cooldown: {cooldownTimer:F2}s", richStyle);
        
        GUILayout.Space(10);
        GUILayout.Label("<b>NavMeshAgent:</b>", richStyle);
        GUILayout.Label($"Has Agent: {(hasNavMeshAgent ? "<color=green>YES</color>" : "<color=red>NO</color>")}", richStyle);
        if (hasNavMeshAgent)
        {
            GUILayout.Label($"On NavMesh: {(navMeshOnNavMesh ? "<color=green>YES</color>" : "<color=red>NO</color>")}", richStyle);
            GUILayout.Label($"Has Path: {(hasPath ? "<color=green>YES</color>" : "<color=red>NO</color>")}", richStyle);
            GUILayout.Label($"Remaining Dist: {pathRemainingDistance:F2}", richStyle);
            GUILayout.Label($"Speed: {agentSpeed:F2}", richStyle);
            GUILayout.Label($"Velocity: {agentVelocity.magnitude:F2}", richStyle);
            GUILayout.Label($"Destination: {agentDestination}", richStyle);
        }
        
        GUILayout.EndArea();
    }

    GUIStyle richStyle => new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
}