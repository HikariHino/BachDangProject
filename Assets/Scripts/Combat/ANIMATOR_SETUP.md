# Unity Animator Setup for MeleeAttack & EnemyAttack

## Required Animator Parameters

| Parameter Name | Type | Description |
|---|---|---|
| `Attack` | Trigger | Triggers the attack animation |
| `ChargePower` | Float (0-1) | Blend tree parameter for charge level |
| `AttackWhiff` | Trigger | Triggers whiff (miss) animation |

## Required Animation States

### 1. Base Layer Structure
```
Base Layer
├── Locomotion (Blend Tree: Idle/Walk/Run)
├── Attack (State)
│   ├── Motion: Attack Animation Clip
│   └── Speed: 1.0 (or driven by ChargePower)
└── AttackWhiff (State) 
    └── Motion: Light/Whiff Animation
```

### 2. Attack State Configuration
- **Has Exit Time**: ✓ Checked
- **Exit Time**: 0.9 (allows animation event at ~80-90% to fire)
- **Fixed Duration**: ✗ Unchecked
- **Transition Duration**: 0.1s

### 3. Transitions
```
Any State → Attack
  Condition: Attack (Trigger)
  Has Exit Time: ✗
  Duration: 0.1s

Any State → AttackWhiff  
  Condition: AttackWhiff (Trigger)
  Has Exit Time: ✗
  Duration: 0.1s

Attack → Locomotion
  Has Exit Time: ✓
  Exit Time: 0.95
  Duration: 0.1s

AttackWhiff → Locomotion
  Has Exit Time: ✓
  Exit Time: 0.9
  Duration: 0.1s
```

## Animation Events Required

Add these events to your **Attack animation clip** at the appropriate frames:

| Frame/Normalized Time | Function | Purpose |
|---|---|---|
| ~0.6-0.8 (hit frame) | `OnAttackHitFrame()` | Spawn effect, deal damage (ONCE per swing) |
| ~0.95 (end) | `OnAttackEnd()` | Clean up state |

### How to Add Animation Events:
1. Select Attack animation clip in Project
2. In Inspector → Animation tab → Events section
3. Add event at hit frame → Function: `OnAttackHitFrame`
4. Add event near end → Function: `OnAttackEnd`

## Enemy Animation Events

For EnemyAttack, add to enemy's Attack clip:
| Frame | Function |
|---|---|
| Hit frame (~0.6-0.8) | `OnEnemyAttackHitFrame()` |
| End frame (~0.95) | `OnEnemyAttackEnd()` |

## Charge Animation Blending (Optional)

For smooth charge visual feedback:

1. Create **Blend Tree** for Attack state:
   - Parameter: `ChargePower`
   - Motions: 
     - LightAttack (threshold 0)
     - MediumAttack (threshold 0.5)
     - HeavyAttack (threshold 0.8)

2. Or use **single Attack animation** with Speed controlled by ChargePower.

## Inspector Setup Checklist

### Player (Main_Character)
- [ ] Animator component with above parameters
- [ ] MeleeAttack script attached
- [ ] AttackPoint assigned (child at weapon tip)
- [ ] EnemyLayer set to "Enemy" layer
- [ ] HitEffect component assigned (same GameObject)
- [ ] AttackRange set (default 1.5f)

### Enemies
- [ ] Animator with Attack trigger
- [ ] EnemyAttack script attached
- [ ] AttackPoint assigned
- [ ] PlayerLayer set to "Player" layer
- [ ] HitEffect assigned
- [ ] DetectionRange / AttackRange tuned

### Layers (Edit > Project Settings > Tags and Layers)
- [ ] Layer 8: "Player" (for Main_Character)
- [ ] Layer 9: "Enemy" (for enemies)

## Debug Tips

- Enable `showGizmos` in MeleeAttack/EnemyAttack to see attack range sphere in Scene view
- Check Console for "Attack committed", "Whiff", "Charge cancelled" logs
- `OnAttackHitFrame()` only fires **once per swing** guaranteed
- If attack feels "slow", check Animator transition Exit Time (should be ~0.9)

## Common Issues & Fixes

| Issue | Cause | Fix |
|---|---|---|
| Attack triggers but no damage | Missing Animation Event | Add `OnAttackHitFrame()` to Attack clip |
| Multiple effects per swing | No gate variable | Script already has `hasSpawnedEffectThisSwing` |
| Attack range too far | Hardcoded range in old code | Now uses `attackRange` variable + Animation Event |
| Whiff doesn't play | Missing AttackWhiff parameter | Add Trigger parameter + transition |
| Charge not working | Animator not getting ChargePower | Add Float parameter "ChargePower" |