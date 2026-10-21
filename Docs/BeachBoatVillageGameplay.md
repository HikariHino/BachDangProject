# Village preparation and checkpoints — beachBoat

The implementation is bound to `Assets/Scenes/beachBoat.unity`.

## Playing quest 2

After collecting the wood, follow the route guide to four staffed village stalls. Press E at a stall, read the rules, choose a difficulty, and start. The timer begins only after starting. Use the mouse or the displayed number keys; the rope puzzle uses mouse clicks to rotate tiles and Space to check the connection.

- Timber: choose two intact pieces whose lengths add up to the requested total. Cracked timber cannot be used.
- Rope: rotate a 3×3 grid to connect the left entry to the right exit within the rotation budget.
- Rice: reach the exact quantity using two scoop sizes and subtraction of one small scoop, within a limited number of moves.
- Provisions: calculate the total ingredients from the number of soldiers and the displayed ration formula, then pack them.

Orders are randomized. Difficulty changes the time allowance, required orders, or move/mistake budget. Every mistake costs two seconds. Running out of time or exceeding the mistake limit shows a failure screen; R retries the stall. Escape closes the workbench without awarding unfinished work. After winning, click “Nhận vật tư” or press Enter to record completion. All four stalls are required before reporting back to camp; completed stalls can be replayed.

## Quest 3 and defeat

Entering quest 3 discards the boat's previous parking position. Launch placement is searched from the original dock location and checked for a clear 120-unit exit at current, low, and high tide. A boat embedded near the quay also attempts recovery when the player holds the rowing input.

Zero health opens a blocking defeat screen and pauses gameplay. R, Enter, or the retry button restores the checkpoint for the current mission stage, player controls, health and stamina. Wood and stake progress revert to that checkpoint. Successfully claimed village stalls remain completed. Death during battle returns to the quest 3 launch checkpoint with the previously planted stake field intact, and clears the fleet, score, and command state so the battle can restart. Empty stamina causes exhaustion, not defeat. Checkpoints are in-memory for the current Play session.

## CLI regression checks

Run with Unity 6000.6.0f1, `-batchmode -force-d3d11 -projectPath <project> -executeMethod <method> -logFile <log>`:

- `BeachBoatVillageWorkPlaytest.RunAndExit`: actual UI pointer routing for difficulty/start/reward, timeouts, mistakes, retry, reward gating, all four quest completions, and 144 randomized attempts across all three difficulty levels.
- `BeachBoatCheckpointPlaytest.RunAndExit`: wood/stake restoration, claimed supplies, defeat UI, fatal boat collision, clean battle retry and a second fleet spawn.
- `BeachBoatBattleLaunchPlaytest.RunAndExit`: four bad parking fixtures, all-tide clearance, forward rowing and embedded-quay recovery.
- `BeachBoatQuest3Playtest.RunAndExit`: battle completion, commands, health/stamina fill and minimap position.
- `BeachBoatQuest3BlockerAudit.PlaytestAndExit`: rowing, boost, collision damage and checkpoint recovery.
- `BeachBoatBusyWorldPlaytest.RunAndExit`: NPC movement, population budget, dialogue and collision checks across five districts.

Reports and preview images are written under ignored `Logs/`; test fixtures do not save Play Mode state into the scene.
