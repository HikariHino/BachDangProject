using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using System.Text;

/// <summary>
/// One-click setup for Enemy AI - adds all required components and validates setup.
/// Run via: Tools/BachDang/Setup Enemy AI
/// </summary>
public static class SetupEnemyAI
{
    [MenuItem("Tools/BachDang/Setup Enemy AI")]
    public static void SetupEnemy()
    {
        GameObject enemy = Selection.activeGameObject;
        if (enemy == null)
        {
            EditorUtility.DisplayDialog("Error", "Select an enemy GameObject in Hierarchy first!", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(enemy, "Setup Enemy AI");

        // 1. Ensure required components
        var animator = enemy.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(enemy);

        var enemyAttack = enemy.GetComponent<EnemyAttack>();
        if (enemyAttack == null) enemyAttack = Undo.AddComponent<EnemyAttack>(enemy);

        var health = enemy.GetComponent<Health>();
        if (health == null) health = Undo.AddComponent<Health>(enemy);
        health.maxHP = 50;

        var hitEffect = enemy.GetComponent<HitEffect>();
        if (hitEffect == null) hitEffect = Undo.AddComponent<HitEffect>(enemy);
        enemyAttack.hitEffect = hitEffect;

        // 2. Physics setup
        var capsule = enemy.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = Undo.AddComponent<CapsuleCollider>(enemy);
        capsule.height = 2f;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0, 1f, 0);

        var rb = enemy.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(enemy);
        rb.freezeRotation = true;
        rb.mass = 1f;
        rb.useGravity = true;

        // 3. NavMeshAgent (for pathfinding)
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(enemy);
        
        // Configure agent
        agent.speed = enemyAttack.walkSpeed;
        agent.stoppingDistance = enemyAttack.stopRange;
        agent.autoBraking = true;
        agent.acceleration = 8f;
        agent.angularSpeed = 360f;
        agent.radius = capsule.radius;
        agent.height = capsule.height;
        agent.baseOffset = 0f;

        // 4. AttackPoint setup
        Transform attackPoint = enemy.transform.Find("AttackPoint");
        if (attackPoint == null)
        {
            var apObj = new GameObject("AttackPoint");
            Undo.RegisterCreatedObjectUndo(apObj, "Setup Enemy AI");
            attackPoint = apObj.transform;
            attackPoint.SetParent(enemy.transform);
            attackPoint.localPosition = new Vector3(0f, 1.5f, 1f);
        }
        enemyAttack.attackPoint = attackPoint;

        // 5. Layer setup
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1)
        {
            enemyLayer = CreateLayer("Enemy");
        }
        enemy.layer = enemyLayer;
        foreach (Transform child in enemy.GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = enemyLayer;
        }

        // 6. PlayerLayer assignment
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1)
        {
            playerLayer = CreateLayer("Player");
        }
        enemyAttack.playerLayer = LayerMask.GetMask("Player");

        // 7. Animation setup guidance
        Debug.Log($"=== Enemy '{enemy.name}' AI Setup Complete ===");
        Debug.Log($"✓ Components: Animator, EnemyAttack, Health, HitEffect, CapsuleCollider, Rigidbody, NavMeshAgent");
        Debug.Log($"✓ Layer: '{LayerMask.LayerToName(enemy.layer)}' (Enemy)");
        Debug.Log($"✓ Player Layer Mask: 'Player'");
        Debug.Log($"✓ AttackPoint: {attackPoint.name} at localPos {attackPoint.localPosition}");
        Debug.Log($"");
        Debug.Log($"📋 NEXT STEPS REQUIRED:");
        Debug.Log($"1. TAG PLAYER: Select Main_Character → Tag = 'Player'");
        Debug.Log($"2. BAKE NAVMESH: Window > AI > Navigation > Bake");
        Debug.Log($"3. ANIMATOR PARAMETERS needed on Enemy's Animator Controller:");
        Debug.Log($"   - 'Attack' (Trigger)");
        Debug.Log($"   - 'IsMoving' (Bool)");
        Debug.Log($"   - 'MoveSpeed' (Float)");
        Debug.Log($"   - 'Die' (Trigger) - optional");
        Debug.Log($"4. ANIMATION EVENTS on Attack clip:");
        Debug.Log($"   - Hit frame (~0.7) → OnEnemyAttackHitFrame()");
        Debug.Log($"   - End frame (~0.95) → OnEnemyAttackEnd()");
        Debug.Log($"5. ASSIGN AUDIO: Select enemy → HitEffect → Set Swing Sound & Hit Sound");

        // Select the enemy so user can see it
        Selection.activeGameObject = enemy;
    }

    [MenuItem("Tools/BachDang/Validate Enemy Setup")]
    public static void ValidateSetup()
    {
        GameObject enemy = Selection.activeGameObject;
        if (enemy == null)
        {
            EditorUtility.DisplayDialog("Error", "Select an enemy GameObject!", "OK");
            return;
        }

        StringBuilder issues = new StringBuilder();
        issues.AppendLine($"=== Validation: {enemy.name} ===");
        bool hasIssues = false;

        // Check components
        CheckComponent<Animator>(enemy, ref issues, ref hasIssues);
        CheckComponent<EnemyAttack>(enemy, ref issues, ref hasIssues);
        CheckComponent<Health>(enemy, ref issues, ref hasIssues);
        CheckComponent<HitEffect>(enemy, ref issues, ref hasIssues);
        CheckComponent<CapsuleCollider>(enemy, ref issues, ref hasIssues);
        CheckComponent<Rigidbody>(enemy, ref issues, ref hasIssues);
        CheckComponent<NavMeshAgent>(enemy, ref issues, ref hasIssues);

        // Check layers
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemy.layer != enemyLayer)
        {
            issues.AppendLine($"❌ Layer: Expected 'Enemy' ({enemyLayer}), got '{LayerMask.LayerToName(enemy.layer)}' ({enemy.layer})");
            hasIssues = true;
        }
        else
        {
            issues.AppendLine($"✅ Layer: 'Enemy'");
        }

        // Check player tag
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            issues.AppendLine($"❌ No GameObject tagged 'Player' in scene!");
            hasIssues = true;
        }
        else
        {
            issues.AppendLine($"✅ Player found: '{player.name}' (Tag: Player)");
        }

        // Check NavMesh
        if (NavMesh.CalculateTriangulation().indices.Length == 0)
        {
            issues.AppendLine($"⚠️ NavMesh not baked! Window > AI > Navigation > Bake");
            hasIssues = true;
        }
        else
        {
            issues.AppendLine($"✅ NavMesh baked");
        }

        // Check Animator parameters
        var animator = enemy.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            CheckAnimatorParam(animator, "Attack", AnimatorControllerParameterType.Trigger, ref issues, ref hasIssues);
            CheckAnimatorParam(animator, "IsMoving", AnimatorControllerParameterType.Bool, ref issues, ref hasIssues);
            CheckAnimatorParam(animator, "MoveSpeed", AnimatorControllerParameterType.Float, ref issues, ref hasIssues);
        }
        else
        {
            issues.AppendLine($"⚠️ Animator missing or no controller assigned");
            hasIssues = true;
        }

        // Check AttackPoint
        var enemyAttack = enemy.GetComponent<EnemyAttack>();
        if (enemyAttack != null && enemyAttack.attackPoint == null)
        {
            issues.AppendLine($"⚠️ AttackPoint not assigned!");
            hasIssues = true;
        }

        // Result
        if (!hasIssues)
        {
            issues.AppendLine($"\n🎉 All checks passed! Enemy should work.");
        }
        else
        {
            issues.AppendLine($"\n❌ Found issues above. Fix them and re-validate.");
        }

        Debug.Log(issues.ToString());
        EditorUtility.DisplayDialog("Validation Result", issues.ToString(), "OK");
    }

    static void CheckComponent<T>(GameObject go, ref StringBuilder issues, ref bool hasIssues) where T : Component
    {
        var comp = go.GetComponent<T>();
        if (comp == null)
        {
            issues.AppendLine($"❌ Missing: {typeof(T).Name}");
            hasIssues = true;
        }
        else
        {
            issues.AppendLine($"✅ {typeof(T).Name}");
        }
    }

    static void CheckAnimatorParam(Animator animator, string name, AnimatorControllerParameterType type, ref StringBuilder issues, ref bool hasIssues)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == name && param.type == type)
            {
                issues.AppendLine($"✅ Animator Param: '{name}' ({type})");
                return;
            }
        }
        issues.AppendLine($"❌ Missing Animator Param: '{name}' ({type})");
        hasIssues = true;
    }

    static int CreateLayer(string layerName)
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
                return i;
            }
        }
        return -1;
    }
}