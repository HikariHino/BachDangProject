using UnityEngine;
using UnityEditor;

/// <summary>
/// Quick test scene setup - creates a basic combat scene to verify all components work.
/// Run via: Tools/BachDang/Create Test Combat Scene
/// </summary>
public static class TestCombatSceneSetup
{
    [MenuItem("Tools/BachDang/Create Test Combat Scene")]
    public static void CreateTestScene()
    {
        // 1. Create Player
        var player = new GameObject("Main_Character");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Default");
        
        var playerAnim = player.AddComponent<Animator>();
        var playerCol = player.AddComponent<CapsuleCollider>();
        playerCol.height = 2f;
        playerCol.radius = 0.3f;
        var playerRb = player.AddComponent<Rigidbody>();
        playerRb.freezeRotation = true;
        
        var playerHealth = player.AddComponent<Health>();
        playerHealth.maxHP = 100;
        
        var meleeAttack = player.AddComponent<MeleeAttack>();
        meleeAttack.attackRange = 1.5f;
        meleeAttack.minDamage = 10;
        meleeAttack.maxDamage = 30;
        meleeAttack.maxChargeTime = 1.5f;
        meleeAttack.attackCooldown = 0.5f;
        
        // Create attack point at sword position
        var attackPoint = new GameObject("AttackPoint").transform;
        attackPoint.SetParent(player.transform);
        attackPoint.localPosition = new Vector3(0.5f, 1.5f, 1f);
        meleeAttack.attackPoint = attackPoint;
        
        // Add HitEffect
        var hitEffect = player.AddComponent<HitEffect>();
        meleeAttack.hitEffect = hitEffect;
        
        // 2. Create Enemy
        var enemy = new GameObject("Enemy_LinhDich_01");
        enemy.layer = LayerMask.NameToLayer("Default");
        
        var enemyAnim = enemy.AddComponent<Animator>();
        var enemyCol = enemy.AddComponent<CapsuleCollider>();
        enemyCol.height = 2f;
        enemyCol.radius = 0.3f;
        var enemyRb = enemy.AddComponent<Rigidbody>();
        enemyRb.freezeRotation = true;
        
        var enemyHealth = enemy.AddComponent<Health>();
        enemyHealth.maxHP = 50;
        
        var enemyAttack = enemy.AddComponent<EnemyAttack>();
        enemyAttack.attackRange = 1.2f;
        enemyAttack.damage = 15;
        enemyAttack.attackCooldown = 2f;
        enemyAttack.detectionRange = 8f;
        
        var enemyAttackPoint = new GameObject("AttackPoint").transform;
        enemyAttackPoint.SetParent(enemy.transform);
        enemyAttackPoint.localPosition = new Vector3(0, 1.5f, 1f);
        enemyAttack.attackPoint = enemyAttackPoint;
        
        var enemyHitEffect = enemy.AddComponent<HitEffect>();
        enemyAttack.hitEffect = enemyHitEffect;
        
        enemy.transform.position = new Vector3(3, 0, 0);
        
        // 3. Create Layers
        CreateLayerIfNeeded("Player");
        CreateLayerIfNeeded("Enemy");
        
        // Set layers
        player.layer = LayerMask.NameToLayer("Player");
        enemy.layer = LayerMask.NameToLayer("Enemy");
        
        // Configure layer masks
        meleeAttack.enemyLayer = LayerMask.GetMask("Enemy");
        enemyAttack.playerLayer = LayerMask.GetMask("Player");
        
        // 4. Setup Camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 3, -5);
            cam.transform.rotation = Quaternion.Euler(15, 0, 0);
        }
        
        // 5. Create Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5, 1, 5);
        
        Selection.activeGameObject = player;
        
        Debug.Log("Test Combat Scene created! Press Play and hold Left Mouse to charge attack.");
        Debug.Log("Controls: Hold LMB to charge, release to attack. RMB to cancel charge.");
    }
    
    static void CreateLayerIfNeeded(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) == -1)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            
            for (int i = 8; i < 32; i++)
            {
                var layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Created layer: {layerName} at index {i}");
                    break;
                }
            }
        }
    }
}