# Boss deck controls and blade contact — 1 November 2026

## Changed behavior

Holding Shift now sprints continuously on the commander deck (48 units/s versus 28 walking). Space jumps, and Ctrl performs the separate directional dodge. Sprint costs 13 stamina/s while moving; jumping costs eight and dodging eighteen. Aiming and guarding prevent sprinting. The HUD and quest description use these same bindings.

Three duel-only animator controllers preserve the existing sword, bow and unarmed locomotion while adding the existing humanoid jump clip. Jump height follows a ballistic arc; post-animation boot support includes that height rather than snapping an airborne character down onto the deck. Landing, sprinting and walking use existing wooden-deck footstep recordings. Retry clears airborne motion, dodge direction, cooldowns and unfinished swings. Old save data needs no migration; loading an airborne save safely resumes supported on the deck.

## Actual melee contact

Both boss and player melee attacks arm a visible sword swing instead of deducting health immediately by root distance/facing. After animation, grip and shield poses have finished, the cutting blade is swept against tight animated body volumes on the torso, head, arms and legs. These volumes follow the real rig, including its jump pose. They are not a large radial attack area or per-frame skinned mesh colliders.

The sampled equipped-blade trajectories define separate downward attack windows: normal normalized clip time 0.52–0.82 and heavy 0.66–0.83. Lifting and recovery cannot hurt. The handle is excluded, the same swing can damage once, and actual shield direction/stamina still reduce a hit. Dodging moves the body; it grants no artificial invulnerability if the blade actually connects. A miss never falls back to a distance-based hit.

Boss pursuit remains active. It approaches to real reach and finishes the visible attack before starting another combo swing, preventing animation restarts halfway through a strike. Contact testing caches bone references and uses analytic swept geometry; it does not bake meshes, allocate body hitboxes each frame, or synchronize physics transforms.

## Validation

Headless Unity Play Mode uses the real scene, real rig and real controllers. `BeachBoatDuelContactPlaytest` checks sprint distance/stamina, real jump animation, independently CPU-skinned boot clearance, landing, retry movement, inactive lift overlap, actual normal/heavy contact, missed swings inside the former radial damage zone, one hit per swing, and physical contact during a dodge. Player save/backup files are hashed and all test writes use an isolated directory.

All 320 final checks passed: duel controls/contact 50, epilogue/save/retry 112, stake/mobile boss 23, moving bow 51, shield/crowd 27 and HUD layout 57. The final new-control run also verifies real bow/shield jump states and matching compact HUD keys. Local proof: `Logs/Review29_Boss_Duel_Jump.png`; machine-readable results: `Logs/DuelContactPlaytest.json`.
