# Victory traversal and invasion intro

The player's recording `Screen Recording 2026-10-26 195247.mp4` shows a swimmer
beside the rowboat after the victory notification. Traversal used to be gated
by `RiverPlayerCombat.BattleActive`: the swimmer stopped updating when combat
ended, and SPACE could not return to the rowboat. Battle cleanup also forced
a swimmer into a logical rowing state.

`RiverBoardingCombat.TraversalActive` now follows the logical rowboat session.
Swimming, rising-water following, walking ashore, F exits and SPACE boarding
continue after victory and after loading a victory save. Ending combat clears
weapons and combat actors while retaining the player's traversal mode and deck
anchor. The existing solid hull and stake collision checks remain in force.

Fresh quest 3 battles queue a 6.5 second invasion shot after the complete enemy
fleet activates. The camera follows the lead ship, dollies from a wide view to
a close quarter view while narrowing its field of view, and returns to the
player's orbit camera. One complete enemy order with its caption accompanies
the shot. Gameplay input and saving wait for the shot to finish; pause menus
pause its clock. Reset/load/defeat cancel the shot and restore camera ownership.
Resuming a saved battle does not replay the introduction.

The approach spreads laterally by 10% where a full hull fits in clear water,
and adds 100 world units between ranks. All ships still activate together and
retain their distinct wreck berths inside the stake field.

`BeachBoatVictoryIntroPlaytest.RunInEditor()` exercises the real post-victory
Update/input path, tide following, boarding, old save restoration, concurrent
fleet activation, full-hull spawn clearance, cinematic camera motion and
restoration, skipped intro on resume, and full-sentence audio scheduling.
Its report is written to ignored `Logs/VictoryIntroPlaytest.json`; actual user
save files are hashed before and after, with all test data under `.utmp`.

Validation on 2026-10-26: victory/intro 47 checks; grounded boat/audio 41;
recovery/save/battle 87; complete concurrent fleet/ebb/ambush/withdrawal 22.
All reports passed, with original manual and temporary save files unchanged.
The eight packaged audio files decoded correctly, matched their manifest
SHA-256 values, and measured peaks at or below 0.747 full scale. The camera
snapshot in ignored `Logs/InvasionIntro938.png` was inspected with the normal
mission/vitals/health-bar HUD hidden and the invasion caption visible.
