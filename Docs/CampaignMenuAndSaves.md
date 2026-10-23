# Bạch Đằng 938: menu and campaign saves

The `beachBoat` scene starts on the title screen. **Play** begins the existing introduction. **Enter** advances the story; press **P** once to skip it. The intro displays these controls and ignores Space. **Load Game** restores the latest verified manual save. It is disabled before the first save.

During play, **Esc** pauses the world and minigame timers. Settings adjust the global volume and orbit-camera mouse sensitivity immediately and remember both choices through PlayerPrefs. Resume returns the cursor to an open minigame or dialogue when appropriate. X leaves a minigame without a reward; Esc opens the pause menu.

Use **E** at a save shelter in the camp, village or stake-field harbor, **F5**, or the pause menu to save. The save records exact player/rowboat positions and rotations, health, stamina upgrades, armor, collected wood, individual stake clusters, completed stall difficulty levels, training records, tide, fleet waves, wrecks and individual combatants. Open quest dialogue and its pending handoff action are restored too. An unfinished minigame attempt starts again when loaded; completed difficulty levels remain completed.

Exit offers three choices: **Yes** verifies and writes a save before quitting; **No** quits without changing the previous save; **Cancel** returns to the paused game. A failed save leaves the game open and preserves the old file.

The versioned JSON save is `campaign938.json` under `Application.persistentDataPath`. Its envelope contains a SHA-256 checksum. A temporary file is verified before an atomic replacement; `.bak` retains the preceding save and is read if the main file is damaged. No network service is required.

All four travel gates are available throughout the campaign. Traveling changes the story only when crossing the village gate at the authored departure/return stage. It never grants supplies or completes later quests merely by visiting another region.

The camp has two separate gates in the west palisade, with clear openings and destination signs: **Làng • nhiệm vụ 2** goes to the village wood/supply quests; **Bến sông Bạch Đằng** goes to the stake-field harbor and naval battle. Their interaction circles do not overlap, and only the nearest eligible gate receives E. The HUD routes the current objective through the appropriate gate instead of pointing at any nearby gate.

Walking uses the existing light footsteps at a slower cadence. Running uses four original heavier bootfall clips in `Assets/Resources/BachDang938/RunSteps`, a quicker cadence and greater impact volume. Footsteps stop when the player is still, airborne or swimming.

## Naval controls and wrecks

**Q** still draws/sheathes the sword. **F** enters the water; **Space** climbs from the water onto the nearest reachable ship or wreck, or back into the player's boat. There is no flag/counterattack button. Holding right mouse raises, aims and draws the bow; left click shoots. In sword mode, right mouse remains the block action.

The rowing shaft passes through both palm grips instead of targeting the wrist bones. Switching to the bow hides the paddle. Enemy soldiers, including the runtime boarding pool, match the player's anatomical rig height while keeping their feet on the deck; their hit capsules, bow grips and melee weapons are sized accordingly.

Sinking ships retain mesh colliders on their actual timber hulls, decks and cabin roofs. The large upright navigation box is disabled when a ship sinks. Swimmers sweep against these surfaces; AI swimmers can move around them. The retained mesh moves with the capsizing ship and remains grounded on the stakes. There is no artificial floating raft under the surviving crew.

Quest 3 distributes ships among separate berths across the stake field. The entire formation must reach the interior before the ebb begins; each hull only takes stake damage at its own interior berth. Flooded wrecks remain caught on the stakes with exposed timber surfaces. Space finds a reachable surface on the retained collider, and walking follows that moving surface while the wreck capsizes. A submerged surface cannot support the player.

The battle ends automatically when every ship has settled and no enemy soldier remains. The HUD shows both counters. Surviving crew fight the player/allies, and allied ships withdraw to separate harbor berths after victory, including when loading a victory save.

Quest 1 drum training has two eight-beat rounds, 55 seconds, and a one-second mistake penalty without losing correct beats. Ngô Quyền uses the same close, forward-facing, unobstructed conversation check as nearby villagers. Ambient NPCs walk faster, vary their pace, cover larger free-roaming areas, and scale animation cadence with actual movement speed.

## Dialogue audio

NPC dialogue uses offline Vietnamese recordings under `Assets/Resources/BachDang938/Voices`. The role cast adds 95 recordings for 40 dialogue lines, with 17 profiles across five neural base voices. Ngô Quyền uses audition 1 (Nam Minh, lower recorded pitch and measured cadence), played at normal speed. Young soldiers and veterans use separate recorded takes; farmers, fishers, merchants, porters and smiths have role and gender profiles. Residents sharing a display name can alternate between actual base voices, with only a small additional per-actor pitch variation. Renamed FemalePea avatars retain female voice selection. Activity prefixes keep the corresponding spoken village line. Dynamic minigame results have voiced completion messages.

Subtitles reveal progressively with a quiet tick whose tone varies by speaker. E first reveals the full text, then closes the dialogue and triggers its quest action on the next press. Opening another dialogue stops the preceding voice. Esc pauses speech and subtitle reveal; resuming continues them. Global volume/mute applies to both. Saving open dialogue preserves the exact recorded cast profile along with its identity, text and pending action. Older saves without the added voiceProfile field still select a compatible recording.

The clips were generated from the game's authored dialogue using Microsoft Edge TTS, with the user's explicit permission. `Tools/dialogue_cast.json` defines the casting; `Tools/generate_dialogue_cast.py` stages role recordings outside Assets and installs only a complete cast. Native Vietnamese NamMinh/HoaiMy and multilingual Andrew/Brian/Emma provide the five base voices. See [Microsoft voice and multilingual language support](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=text-to-speech). Presentation references found during research include [VTV's Bạch Đằng Giang documentary](https://vtv.vn/video/phim-tai-lieu-bach-dang-giang-vang-vong-duc-ngo-quyen-tap-1-473251.htm) and [the Ngô Vương Quyền short film production](https://maac.edu.vn/vi/thanh-nien-phim-ngan-lich-su-ngo-vuong-quyen-va-khat-vong-hon-ca-mot-do-an/). `Tools/generate_dialogue_voices.py` uses [edge-tts](https://github.com/rany2/edge-tts) as an authoring dependency only; the game does not send dialogue or require a network connection. It stages files outside Assets before import. `BeachBoatDialogueVoiceSetup.ApplyAndExit` configures the imported audio. `BeachBoatDialogueVoicePlaytest.RunAndExit` checks real speech waveforms/playback, different speaker profiles, progressive subtitle sounds, pause/resume, early reveal, stopping, mute and saved voice identity.

## Verification

Run `BeachBoatFrontEndPlaytest.RunAndExit` in Unity batch mode, then `BeachBoatFrontEndPlaytest.ColdLoadAndExit` in a separate Unity process. Reports are written under `Logs`; test saves use `Logs/FrontEndSaveQA`, leaving the player's own save untouched. These tests cover title/intro gating, real settings sliders, all shelters and their entrances, pause, cancellation, yes/no exit, failed writes, corrupted-save recovery, free travel, fresh-scene restores and a cold-process battle load.

Settings, Back and New Game are exercised through `EventSystem.RaycastAll` and pointer down/up/click events across rendered frames. A runtime error or exception fails the playtest. Every menu Graphic must have a CanvasRenderer; the decorative ink gradient explicitly requires that component and does not receive pointer raycasts.

`BeachBoatCampGateRoutesPlaytest.RunAndExit` checks P skip, separated gate geometry/signs, open approaches/passages, quest-specific HUD routes, actual trips to both destinations and back, village wood collection, preserved quest progress and overlapping-gate arbitration. `BeachBoatSeparateCampGates.ApplyAndExit` authors the two gates and adjusts the palisade around them.

`BeachBoatFrontEndSetup.ApplyAndExit` authors the three shelters and configures the menu texture/run-clip imports. It replaces only its own `CampaignSaveShelters` root when rerun.

## Menu artwork provenance

Mode: built-in ImageGen, new image, opaque background.

Saved asset: `Assets/Resources/BachDang938/MainMenu.png`.

Prompt: “Create a polished widescreen 16:9 hand-painted cinematic historical strategy-game main-menu background. Depict the early medieval Vietnamese Bạch Đằng river in year 938 from a wooden riverside command post at dawn. Reserve the left 40% as low-detail dark atmospheric forest and mist for readable menu text. Across the right two-thirds show ancient wooden sailing junks with russet sails, submerged wooden stakes, jade water, distant limestone hills and golden sunrise. Add a partial bronze drum and timber details at the right edge. Use bronze gold, dark teal and restrained vermilion. No modern objects, lettering, logos or UI.”
