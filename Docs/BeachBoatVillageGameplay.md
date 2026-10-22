# Village preparation and checkpoints — beachBoat

The implementation is bound to `Assets/Scenes/beachBoat.unity`.

## Playing quest 2

After collecting the wood, follow the route guide to four staffed village stalls. Press E at a stall, read the rules, choose a difficulty, and start. The timer begins only after starting. Use the mouse or the displayed number keys; the rope puzzle uses mouse clicks to rotate tiles and Space to check the connection.

The four stalls are distributed across four household clusters in the riverside village. The editor placement uses a fixed random seed and checks building/rock footprints, terrain trees, walking approaches and NPC routes. Positions are saved in the scene; stalls do not jump to new locations when reopening an activity or retrying a checkpoint. `BeachBoatVillageWorkSetup.ScatterStallsAndExit` reapplies that layout.

- Timber: choose two intact pieces whose lengths add up to the requested total. Cracked timber cannot be used.
- Rope: rotate a 3×3 grid to connect the left entry to the right exit within the rotation budget.
- Rice: reach the exact quantity using two scoop sizes and subtraction of one small scoop, within a limited number of moves.
- Provisions: calculate the total ingredients from the number of soldiers and the displayed ration formula, then pack them.

Orders are randomized. Difficulty changes the time allowance, required orders, or move/mistake budget. Every mistake costs two seconds. Running out of time or exceeding the mistake limit shows a failure screen; R retries the stall. Escape closes the workbench without awarding unfinished work. After winning, click “Nhận vật tư” or press Enter to record completion. All four stalls are required before reporting back to camp; completed stalls can be replayed.

## Quest 3 and defeat

Entering quest 3 discards the boat's previous parking position. Launch placement is searched from the original dock location and checked for a clear 120-unit exit at current, low, and high tide. A boat embedded near the quay also attempts recovery when the player holds the rowing input.

The invaders follow the northern river route while high water conceals the stakes. Ebb begins only when an active enemy hull overlaps the actual planted stake field; there is no travel timer or distant-waypoint fallback. The battle ebb takes 22 game seconds. Exposed stakes contacting a hull immediately cause impact, flying wood, listing and a seven-second plunge, independently of shooting or flag input. A stationary hull is checked too, so emerging stakes underneath it still cause a wreck. The sinking depth includes the mast instead of hiding a still-visible ship. The field now contains 151 shared-mesh stakes covering both enemy lanes, with all five quest 2 approaches kept clear. Checkpoint retry cancels an unfinished ebb and restores the high-water launch state.

Quest 3 also requires direct player combat. Twenty deck archers occupy the ten warships, with shared textured character assets. Surviving archers transfer onto visible floating deck fragments when their ship wrecks, and remain targetable after the mast has submerged. Kill at least one archer in each of the five waves before Space orders the allied finishing volley for that wave. This order clears its other archers without awarding artificial player kills. The route guide follows surviving archers and then the command point. Victory requires ten sunk ships and five completed counterattack orders, so sinking every ship before firing cannot softlock the quest. Hold right mouse to aim (slowing the rowing boat), hold/release left mouse to draw/fire, and press R to refill the six-arrow quiver from supplies on the boat. Reloading takes two seconds and supplies do not run out. A quick shot causes one damage; a draw of at least 0.55 seconds causes two, enough to kill a fresh guard. Hits resolve when the visible arrow arrives.

Nearby surviving archers prepare return fire with a visible 1.4-second warning. Release aim and row/boost away from the firing point to evade the incoming arrow; remaining at its impact point costs 12 health. There is one shared return-fire cadence, rather than all twenty archers damaging the player simultaneously. Kills, ammunition, incoming arrows, aim camera settings and wave suppression reset on checkpoint retry. Killing guards does not directly sink a ship: the planted, exposed stakes remain responsible for shipwrecks.

## Audio

`BeachBoatSoundscape` provides river/wind loops, intermittent birds, player footsteps, paddle splashes, bow twangs, drum beats, stake hammering, ship impact and workbench feedback. Water ambience starts silent and fades according to the player's horizontal distance to flooded terrain, reaching zero 200 scene units from the shore. Terrain samples are cached between half-second checks; wind and birds remain audible inland. Personal sounds use 2D playback so the orbit camera cannot move outside their hearing radius. Successful E interactions play a shared confirmation cue: wood pickup, quest dialogue opening/acceptance, gates, boarding/disembarking, planting/recovery, facilities and minigame/stall opening or delivery. Duplicate notifications in one frame produce a single cue. The old `FootstepSound` had a fixed 20-unit maximum distance despite the player's camera sitting much farther away; its timer is disabled only for this scene's player. The new footstep trigger follows actual grounded travel, and rowing sound follows boat movement.

The existing imported footsteps and impact clips are reused. Nine original procedural ambience/foley WAVs add approximately 1.22 MB on disk. They are shared, with no per-frame audio clip creation. The wind loop starts at zero, fades to a soft varying bed (0.025–0.045 before master volume) and uses a 700 Hz low-pass filter to suppress the continuous water-like hiss. The opening ducks both wind and shoreline ambience to 15%; footsteps and interaction cues keep their existing volume. At the inland camp spawn, river volume remains zero. F10 toggles the scene soundscape's mute state. The installer also clears Unity Editor's Game View audio mute and ensures the playing camera owns the listener. This adds sound effects and ambience; it does not synthesize spoken dialogue.

Zero health opens a blocking defeat screen and pauses gameplay. R, Enter, or the retry button restores the checkpoint for the current mission stage, player controls, health and stamina. Wood and stake progress revert to that checkpoint. Successfully claimed village stalls remain completed. Death during battle returns to the quest 3 launch checkpoint with the previously planted stake field intact, and clears the fleet, score, and command state so the battle can restart. Empty stamina causes exhaustion, not defeat. Checkpoints are in-memory for the current Play session.

## CLI regression checks

Run with Unity 6000.6.0f1, `-batchmode -force-d3d11 -projectPath <project> -executeMethod <method> -logFile <log>`:

- `BeachBoatVillageWorkPlaytest.RunAndExit`: dispersed stall locations and unobstructed interaction points, actual UI pointer routing for difficulty/start/reward, timeouts, mistakes, retry, reward gating, all four quest completions, and 144 randomized attempts across all three difficulty levels.
- `BeachBoatCheckpointPlaytest.RunAndExit`: wood/stake restoration, claimed supplies, defeat UI, fatal boat collision, clean battle retry and a second fleet spawn.
- `BeachBoatBattleLaunchPlaytest.RunAndExit`: four bad parking fixtures, all-tide clearance, forward rowing and embedded-quay recovery.
- `BeachBoatQuest3Playtest.RunAndExit`: hold the fleet upstream for 80 game seconds to reject premature ebb, then verify hull entry, gradual water fall and all ten ships sinking without any shooting or flag order. Finish five waves against the floating survivors and verify victory, commands, health/stamina fill and minimap position. Renders `Ebb_High`, `Ebb_Emerging` and `Ebb_Impact` previews.
- `BeachBoatQuest3BlockerAudit.PlaytestAndExit`: rowing, boost, collision damage and checkpoint recovery.
- `BeachBoatRiverCombatPlaytest.RunAndExit`: real guard hitboxes, missed/weak/charged arrows, cooldown, reload, combat-gated flag orders, enemy return fire, dodging, combat reset on death, and rendered player/enemy bow poses.
- `BeachBoatAudioPlaytest.RunAndExit`: exactly one listener on the story camera, decoded clips, inland water silence/near-water fade-in, mute/unmute, real pickup/gate/quest interactions and PCM output from an isolated interaction cue.
- `BeachBoatStartupAudioPlaytest.RunAndExit`: actual opening at the camp spawn, ducked ambience, zero inland river volume, filtered soft wind after skipping the opening, and wind output RMS. Reports the active sources so a distant water source cannot be mistaken for the wind loop.
- `BeachBoatBusyWorldPlaytest.RunAndExit`: NPC movement, population budget, dialogue and collision checks across five districts.

Reports and preview images are written under ignored `Logs/`; test fixtures do not save Play Mode state into the scene.
