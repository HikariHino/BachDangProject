# Combat rig and retry recovery

The 30 October recording exposed two separate faults. NPCUprightPosture kept relaxing the shoulders and wrists after the sword animator had evaluated, erasing the attack. Loading an on-foot save after defeat also retained the rigidbody's death-time kinematic flag. Boarding from that state could cache the frozen body and strand the player again on disembark.

Combat now owns the upper body while using a sword, bow or traversal pose; ambient posture and daily chores defer to it. Sword and bow modes exclude each other, reactivate their animator when necessary, and retain normal playback speed. The retaliating soldier's sword damage follows a short windup and rechecks reach at impact. Sword swings, confirmed impacts and retaliation arrow releases retain audio feedback.

NPC bow contacts use the particular avatar's hand frame and skin bind space. The player's authored bow grip profile is only used on its matching mesh. The bow stave, string and held arrow follow the solved contact points. Fingerless imported rigs use separate closed hand surfaces with four curved fingers and an opposed thumb; the original open-hand triangles are masked only while that hand grips a weapon, and restored afterwards. Original mesh references retain close-range topology; distant crowds continue to use their existing LODs. These hand surfaces are a procedural approximation, not a new finger-bone animation rig.

Save restoration first releases the old boat ownership, restores a dynamic walking baseline (capsule, motor, animator and controller), and then reacquires boarding/swimming/climbing according to the saved state. This handles ordinary walking, drawn sword, aiming at death, aboard retry, disembark after retry and leaving a loaded swimming state. Existing user save files are not rewritten by QA.

The swimmer regression also found overlap after turning the long horizontal body capsule. Navigation now tests the predicted body pose and separates overlapping swimmers while checking the correction against solid geometry.

Verification uses BeachBoatMotionRecoveryPlaytest (real avatar sword samples, hand and bow contacts, visible hand surfaces, bone lengths, actual physics movement after lethal damage/retry, boat movement/disembark, swimming restoration and SHA-256 checks of existing campaign saves), plus the discipline, archery, concurrent battle, epilogue and shield/crowd suites. Reports and rendered close-ups are generated under ignored Logs paths.

Final validation: 416 checks passed (motion/retry 86, discipline 79, archery 80, concurrent battle 68, epilogue 76, shield/crowd 27). All 13 existing campaign save files retained their SHA-256 hashes.
