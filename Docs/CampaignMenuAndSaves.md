# Bạch Đằng 938: menu and campaign saves

The `beachBoat` scene starts on the title screen. **Play** begins the existing introduction. Space/Enter advances the story; hold Backspace to skip it. **Load Game** restores the latest verified manual save. It is disabled before the first save.

During play, **Esc** pauses the world and minigame timers. Settings adjust the global volume and orbit-camera mouse sensitivity immediately and remember both choices through PlayerPrefs. Resume returns the cursor to an open minigame or dialogue when appropriate. X leaves a minigame without a reward; Esc opens the pause menu.

Use **E** at a save shelter in the camp, village or stake-field harbor, **F5**, or the pause menu to save. The save records exact player/rowboat positions and rotations, health, stamina upgrades, armor, collected wood, individual stake clusters, completed stall difficulty levels, training records, tide, fleet waves, wrecks and individual combatants. Open quest dialogue and its pending handoff action are restored too. An unfinished minigame attempt starts again when loaded; completed difficulty levels remain completed.

Exit offers three choices: **Yes** verifies and writes a save before quitting; **No** quits without changing the previous save; **Cancel** returns to the paused game. A failed save leaves the game open and preserves the old file.

The versioned JSON save is `campaign938.json` under `Application.persistentDataPath`. Its envelope contains a SHA-256 checksum. A temporary file is verified before an atomic replacement; `.bak` retains the preceding save and is read if the main file is damaged. No network service is required.

All four travel gates are available throughout the campaign. Traveling changes the story only when crossing the village gate at the authored departure/return stage. It never grants supplies or completes later quests merely by visiting another region.

Walking uses the existing light footsteps at a slower cadence. Running uses four original heavier bootfall clips in `Assets/Resources/BachDang938/RunSteps`, a quicker cadence and greater impact volume. Footsteps stop when the player is still, airborne or swimming.

## Naval controls and wrecks

**Q** still draws/sheathes the sword. **F** enters the water; **Space** climbs from the water onto the nearest reachable ship or back into the player's boat. Climbing cannot also issue an ambush command in the same frame. Space issues the counterattack order while aboard the rowboat. Holding right mouse raises, aims and draws the bow; left click shoots. In sword mode, right mouse remains the block action.

The rowing shaft passes through both palm grips instead of targeting the wrist bones. Switching to the bow hides the paddle. Enemy soldiers, including the runtime boarding pool, match the player's anatomical rig height while keeping their feet on the deck; their hit capsules, bow grips and melee weapons are sized accordingly.

Sinking ships retain mesh colliders on their actual timber hulls, decks and cabin roofs. The large upright navigation box is disabled when a ship sinks. Swimmers sweep against these surfaces; AI swimmers can move around them. The retained mesh moves with the capsizing ship and settles on the riverbed. There is no artificial floating raft under the surviving crew.

## Verification

Run `BeachBoatFrontEndPlaytest.RunAndExit` in Unity batch mode, then `BeachBoatFrontEndPlaytest.ColdLoadAndExit` in a separate Unity process. Reports are written under `Logs`; test saves use `Logs/FrontEndSaveQA`, leaving the player's own save untouched. These tests cover title/intro gating, real settings sliders, all shelters and their entrances, pause, cancellation, yes/no exit, failed writes, corrupted-save recovery, free travel, fresh-scene restores and a cold-process battle load.

Settings, Back and New Game are exercised through `EventSystem.RaycastAll` and pointer down/up/click events across rendered frames. A runtime error or exception fails the playtest. Every menu Graphic must have a CanvasRenderer; the decorative ink gradient explicitly requires that component and does not receive pointer raycasts.

`BeachBoatFrontEndSetup.ApplyAndExit` authors the three shelters and configures the menu texture/run-clip imports. It replaces only its own `CampaignSaveShelters` root when rerun.

## Menu artwork provenance

Mode: built-in ImageGen, new image, opaque background.

Saved asset: `Assets/Resources/BachDang938/MainMenu.png`.

Prompt: “Create a polished widescreen 16:9 hand-painted cinematic historical strategy-game main-menu background. Depict the early medieval Vietnamese Bạch Đằng river in year 938 from a wooden riverside command post at dawn. Reserve the left 40% as low-detail dark atmospheric forest and mist for readable menu text. Across the right two-thirds show ancient wooden sailing junks with russet sails, submerged wooden stakes, jade water, distant limestone hills and golden sunrise. Add a partial bronze drum and timber details at the right edge. Use bronze gold, dark teal and restrained vermilion. No modern objects, lettering, logos or UI.”
