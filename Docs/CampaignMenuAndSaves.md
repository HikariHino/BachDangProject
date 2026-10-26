# Bạch Đằng 938: menu and campaign saves

The `beachBoat` scene starts on the title screen. **Play** begins the existing introduction. **Enter** advances the story; press **P** once to skip it. The intro displays these controls and ignores Space. **Load Game** opens ten selectable save slots. Each occupied slot shows its chapter, timestamp and save location; empty/unreadable slots cannot be loaded. The title summarizes the latest readable journey and disables Load before the first valid save.

During play, **Esc** pauses the world and minigame timers. Settings adjust the global volume and orbit-camera mouse sensitivity immediately and remember both choices through PlayerPrefs. Resume returns the cursor to an open minigame or dialogue when appropriate. X leaves a minigame without a reward; Esc opens the pause menu.

Use **E** at a save shelter in the camp, village or stake-field harbor, **F5**, or the pause menu to open the same ten-slot picker. Selecting any occupied slot always asks for explicit overwrite confirmation, including when free slots remain. Cancel returns to the slot list without writing. Confirm writes only that slot; the other nine remain untouched. The save records exact player/rowboat positions and rotations, health, stamina upgrades, armor, collected wood, individual stake clusters, completed stall difficulty levels, training records, tide, fleet waves, wrecks and individual combatants. Open quest dialogue and its pending handoff action are restored too. An unfinished minigame attempt starts again when loaded; completed difficulty levels remain completed.

Exit offers three choices: **Yes** opens the slot picker, verifies and writes the selected slot before quitting; **No** quits without changing the previous save; **Cancel** returns to the paused game. A failed save leaves the game open and preserves the old file.

The versioned JSON saves live under `Application.persistentDataPath`: slot one keeps the original `campaign938.json` path for compatibility with existing journeys; slots 2–10 use `campaign938-02.json` through `campaign938-10.json`. Its envelope contains a SHA-256 checksum. A temporary file is verified before an atomic replacement; `.bak` retains the preceding save and is read if the main file is damaged. No network service is required.

All four travel gates are available throughout the campaign. Traveling changes the story only when crossing the village gate at the authored departure/return stage. It never grants supplies or completes later quests merely by visiting another region.

The camp has two separate gates in the west palisade, with clear openings and destination signs: **Làng • nhiệm vụ 2** goes to the village wood/supply quests; **Bến sông Bạch Đằng** goes to the stake-field harbor and naval battle. Their interaction circles do not overlap, and only the nearest eligible gate receives E. The HUD routes the current objective through the appropriate gate instead of pointing at any nearby gate.

Walking and running use separate edited grass recordings; walking on a ship uses wood recordings. Steps stop when the player is still, airborne or swimming, and standing on a moving ship does not count as walking. Rowing, bow release and sword swings rotate through short recorded variants. See [ActionAudioSources.md](ActionAudioSources.md) for sources and processing details.

## Naval controls and wrecks

**Q** still draws/sheathes the sword. **F** enters the water; **Space** climbs from the water onto the nearest reachable ship or wreck, or back into the player's boat. There is no flag/counterattack button. Holding right mouse raises, aims and draws the bow; left click shoots. In sword mode, right mouse remains the block action.

The rowing shaft passes through both palm grips instead of targeting the wrist bones. Switching to the bow hides the paddle. Enemy soldiers, including the runtime boarding pool, match the player's anatomical rig height while keeping their feet on the deck; their hit capsules, bow grips and melee weapons are sized accordingly.

Sinking ships retain mesh colliders on their actual timber hulls, decks and cabin roofs. The large upright navigation box is disabled when a ship sinks. Swimmers sweep against these surfaces; AI swimmers can move around them. The retained mesh moves with the capsizing ship and remains grounded on the stakes. There is no artificial floating raft under the surviving crew.

Quest 3 distributes ships among separate berths across the stake field. The entire formation must reach the interior before the ebb begins; each hull only takes stake damage at its own interior berth. Flooded wrecks remain caught on the stakes with exposed timber surfaces. Space finds a reachable surface on the retained collider, and walking follows that moving surface while the wreck capsizes. A submerged surface cannot support the player.

The battle ends automatically when every ship has settled and no enemy soldier remains. The HUD shows both counters. Surviving crew fight the player/allies, and allied ships withdraw to separate harbor berths after victory, including when loading a victory save.

Quest 1 drum training has two eight-beat rounds, 55 seconds, and a one-second mistake penalty without losing correct beats. Ngô Quyền uses the same close, forward-facing, unobstructed conversation check as nearby villagers. Ambient NPCs walk faster, vary their pace, cover larger free-roaming areas, and scale animation cadence with actual movement speed.

## Dialogue audio

NPC dialogue uses bundled Vietnamese speech under `Assets/Resources/BachDang938/Voices`. Ngô Quyền, the narrator, soldiers and residents use separate voices. Farmers, fishers, merchants, porters and smiths have role and gender profiles; residents sharing a display name can alternate voices. Renamed FemalePea avatars retain female voice selection. Activity prefixes keep the corresponding spoken village line. Dynamic minigame results have voiced completion messages.

Subtitles reveal progressively with a quiet tick whose tone varies by speaker. E first reveals the full text, then closes the dialogue and triggers its quest action on the next press. Opening another dialogue stops the preceding voice. Esc pauses speech and subtitle reveal; resuming continues them. Global volume/mute applies to both. Saving open dialogue preserves the exact recorded cast profile along with its identity, text and pending action. Older saves without the added voiceProfile field still select a compatible recording.

The current cast uses 103 offline clips made locally with 18 different Vietnamese preset voices from [VieNeu-TTS](https://github.com/pnnbao97/VieNeu-TTS) ([model card and license](https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo)). Ngô Quyền uses Minh Đức, the introduction uses Thanh Bình, and soldiers use Quốc Tuấn/Hải Đăng; residents receive distinct male/female regional presets. Cadence is 10–14% faster using pitch-preserving tempo adjustment, trimmed lead/tail silence and conservative loudness normalization. These remain synthesized voices, not actor recordings. The previous Edge recordings remain fallback assets for compatibility.

`Tools/dialogue_native_cast.json` assigns the cast. Set `BACHDANG_VOICE_CACHE` to a local authoring-cache directory with `voice-native-tools` and `huggingface` subdirectories; install `vieneu==3.8.3` and `imageio-ffmpeg==0.6.0` into the tools directory. `Tools/generate_native_dialogue.py` caches each take and installs only a complete cast. Models and Python libraries stay outside Assets; gameplay needs no service or model runtime. Model/package versions should be pinned to the tested environment before regeneration. `BeachBoatDialogueVoiceSetup.ApplyAndExit` configures imported clips. The dialogue test checks actual audible waveforms, playback, cast identity, subtitle reveal, pause/resume and save compatibility.

## October 23 campaign revision

Hàng gạo keeps three required difficulty levels. The hardest level now has three bags, 110 seconds and three allowed errors. Each generated order has an independently solvable route of at most four scoops, with three spare moves. A twelve-order recent history avoids repeated scoop/target combinations. Undo and reset still preserve the timer and mistakes.

The allied fleet contains ten ships, each registered with combat and campaign saves. Their broad patrol destinations are joined by a cached deep-water navigation grid that reserves every authored stake, including stakes that have not yet been planted. Patrol speed is 24 world units/sec, turn speed 32 degrees/sec. Target selection assigns allies across the ten enemy ships. Whole-hull sweeps cover translation and rotation. Traffic clearance leaves enough room to turn, and blocked routes are planned around other vessels. The northern invasion approach is reserved so allied patrols cannot pin incoming enemies. Victory retains the return-to-harbor order for every ship.

The rowboat's hull, seat X/Z coordinates and collision probes use .65 of the authored footprint. Player scale stays unchanged; the combat camera moves back enough to show the compact hull. Enemy warships retain their 275-unit hull, over four times the player's visible hull length.

Swimming into clear shallow terrain automatically switches to grounded movement. Walking, running, jumping, Q sword switching, bow aiming/firing and sword hits work on shore; entering deep water resumes swimming. Shore movement is appended as state 4 so older boat/swim/climb/deck saves remain compatible. A shore save restores physics, the controller and exact coordinates. Hostile swimmers can follow the player onto shore. Bow and sword hits both show floating damage numbers equal to actual health lost, from one bounded pool.

QA: `BeachBoatFrontEndPlaytest` now verifies ten independent slots, full-slot overwrite/cancel, atomic failure recovery and real scene reloads, using a guarded private Logs directory. `BeachBoatCampaignRevisionPlaytest` checks the narrator, actual travel on every patrol, player dimensions, physical walking after swimming, shore bow/sword damage, damage popups, deep-water return, shore save restoration and screenshots of both save dialogs. The village test independently solves randomized rice orders, and the battle-flow test verifies sinking and all allied returns.

## October 24 solid river revision

Allied navigation reserves the stake field at every tide. Boats stop and replan when the full hull cannot pass; there is no move-through-obstacle yielding fallback. Thin posts and obstacles crossed between the start and end of a turn are included in the sweep. The player rowboat and live enemy vessels use the same physical hull guard. Enemy ships enter over submerged stakes at high tide and remain subject to real three-dimensional contacts as water falls.

The scene restores existing disabled solid scenery and adds mesh colliders to substantial timber, stalls, shelters, rock, barrel, fence, dock and similar props. Terrain tree/rock prototypes also retain solid bodies, including nineteen previously empty prototypes; standalone village trees are included. Ground-contact filters distinguish upward terrain support from tree trunks and walls. NPCs use body capsules and movement sweeps; swimming and melee movement check solid surfaces. Carried fighters are associated with their own ship so a passenger cannot block the vessel carrying them. Boarding allies are placed using the real deck/foot height and leave above the rail before descending into the water. Swimmers choose the detour direction toward their destination and use separate landing stations. Wrecks retain their existing real timber surfaces.

Ten enemy vessels use four separated columns and three depth rows, sized from their real hull dimensions. Authored approach routes are preserved at runtime, and staggered releases prevent intersecting spawns. Older saves with the former route shape resume on the nearest forward segment.

`BeachBoatSolidRiverPlaytest` simulates every allied hull against planted stakes and independently checks thin-post tunnelling, rotating sterns, lateral yielding, free movement and all eight decoded running waveforms. `BeachBoatBattleFlowPlaytest` covers complete fleet entry, ebb, retained wreck climbing, automatic victory and all allied returns.

Final verification on Unity 6000.6.0f1: solid-river, complete battle/ten returns, two-sided boarding, ten-slot frontend/save reload and a separate-process cold load all passed. The solid test also boards the player and rows away from the dock with full contact enabled. QA uses private Logs saves and never writes the player’s own slots.
