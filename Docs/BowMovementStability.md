# Bow movement stability

The 31 October 16:15 recording shows the player retaining the aim HUD while the body falls back toward an ordinary walking pose. The previous bow controller switched the entire humanoid between BowIdle and Walking. Its walking root orientation also changed the aiming torso; the movement QA measured about 70 degrees of relative torso twist. RiverPlayerCombat simultaneously wrote the dynamic rigidbody's rotation in LateUpdate, defeating interpolated movement.

Player_Bow_Locomotion blends the existing idle/walk clips for the legs and retains the bow pose on an overriding humanoid body/arms layer. The aim layer also owns the imported animation root orientation, while applyRootMotion stays disabled and Rigidbody remains responsible for the actual character root. AimMove follows movement speed with damping. No new third-party animation is downloaded.

Normal aimed walking turns only in Character_Movement.FixedUpdate through MoveRotation. LateUpdate solves the arms and bow without changing a dynamically controlled actor's rotation; scripted kinematic stances use a bounded turn. The rowboat seat preserves current bow facing, and deck strafing no longer faces the movement direction before every bow pose. Lowering the bow restores the previous walking controller.

BeachBoatBowMovementPlaytest exercises actual physics walking in eight directions, torso pose, both animation layers, bow/hand contact, render updates between physics steps, turning the camera, restoring the normal controller, seating on the rowboat and strafing on a friendly deck. QA uses isolated save paths and verifies existing campaign file hashes. Reports and pose captures are written to ignored Logs paths.

Validation: 296 checks passed through Unity CLI: bow movement 51, archery 80, motion/retry 86 and discipline 79. Existing campaign save hashes were unchanged.
