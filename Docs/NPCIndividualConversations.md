# Individual NPC conversations — 1 November 2026

Twenty-eight named camp/village actors now have dedicated scripts instead of most soldiers repeating the same readiness sentence. Each has a distinct line for preparation, the completed-field/battle phase and post-victory: **84 unique named-character lines**. Their on-screen labels identify their job, such as rice storekeeper, sword repairer, rear gate guard and the injured soldier. The elder and wood donor retain the five-piece wood clue.

Background residents use seven occupations, two stable personalities per occupation and male/female casting. These are shared repertories for the procedural crowd, not a claim that the thousand background actors have a thousand separate human voice actors. Assignment is deterministic from the actor's hierarchy identity; it does not change randomly on each conversation. The 28 named roles take priority over inherited ambient role tags.

The four village vendors have individual greeting, replay and completion lines. The sawyer, rope worker, rice vendor and provisions packer use four different presets. Their brief speech and caption run without opening a modal dialogue or delaying the minigame. Closing a stall stops its greeting. Mission dialogue takes over the shared vendor channel immediately; checkpoint load stops any stale vendor speech too. The small caption is left of the tabletop interaction view, below the rank HUD.

The content contains **138 different texts and 180 offline takes**, using **15 Vietnamese preset voices**, about **8.82 MiB** of MP3 assets. Ngô Quyền's existing Minh Đức cast and the Thanh Bình narrator remain separate. New clips are synthesized locally with the already installed VieNeu-TTS authoring setup; these are synthesized speech, not new human actor recordings. The authoring model is not included in the game. Normalized levels, short leading/trailing margins and a pitch-preserving 1.10× tempo keep new speech brisk without lowering the playback pitch.

`NPCConversationLibrary` resolves ordinary actor conversations only. Dialogue with a quest callback, the commander and anonymous mission exchanges retain their authored text and actions. Save dialogue continues to store exact text, actor identity and cast profile. Existing clips remain for older saves; new conversations use the new scripts. No campaign save schema or quest enum values change.

## Authoring and validation

Edit `Assets/Resources/BachDang938/Voices/NPCConversations.json`, then run from the project root:

```powershell
& '<python>' Tools/prepare_npc_conversations.py --cache '<existing-voice-authoring-cache>'
```

The cache must contain the previously configured `voice-native-tools` and `huggingface` folders described in [the audio authoring notes](CampaignMenuAndSaves.md). The script enables offline model access, stages the complete cast outside Assets and installs only a successful complete batch. Nothing is sent to a remote TTS service. Run this NPC generator after any regeneration of the legacy cast so its profiles remain in the manifest. `BeachBoatNPCConversationSetup.Apply` imports only the new NPCast folder as preloaded mono 24 kHz speech. Game playback has no Python/model dependency.

All **1,205 targeted Unity CLI checks passed**: NPC conversations 683, dialogue/subtitle/pause/save regression 313, physical village work and five construction sites 97, epilogue/boss/save/final report 112. Checks inspect every new bundled waveform, all 84 named-character lines, stable character cast across stages, role/gender alternatives, exact dialogue restoration, non-modal vendor playback, one-time mission callbacks, interruption and checkpoint cleanup. All thirteen existing campaign save/backup files retain their hashes. QA reports and `Logs/NPCVendorPreview.png` are ignored local artifacts. The vendor caption was visually inspected in the actual game backbuffer.
