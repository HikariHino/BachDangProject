# Recorded battle cues

These short film excerpts replace the generated battle speech at the player's
explicit request. The normal NPC dialogue cast is unchanged.

Sources:

- Original uploader: Duc Nguyen, https://www.youtube.com/watch?v=ok_KBwgS9eg
- https://www.youtube.com/watch?v=FEdW8VpWHA8

The films retain their original copyright. No CC0 or open-reuse license is
asserted by this project. Source links and exact timestamps preserve attribution
and provenance; this document is not a grant of rights to the source material.

| Resource | Video ID | Start–end (seconds) |
| --- | --- | --- |
| Enemy_Advance_1 | ok_KBwgS9eg | 99.03–102.55 |
| Enemy_Advance_2 (bait pursuit) | ok_KBwgS9eg | 230.28–233.35 |
| Enemy_Cheer_Crowd | ok_KBwgS9eg | 212.20–214.40 |
| Enemy_Panic_1 | ok_KBwgS9eg | 308.35–310.10 |
| Enemy_Panic_2 | ok_KBwgS9eg | 283.10–284.80 |
| Enemy_Panic_3 | ok_KBwgS9eg | 289.90–291.50 |
| NgoQuyen_Ambush | ok_KBwgS9eg | 268.16–270.87 |
| NgoQuyen_Attack | FEdW8VpWHA8 | 128.62–137.35 |

Reproduce with `Tools/prepare_battle_recordings.py` (the previous
`Tools/generate_battle_voices.py` entry point delegates to it). Full sources
and automatic captions stay in ignored `Logs/BattleVoiceSources`; the game
contains eight short excerpts plus the separate trap-entry order described below. Processing: 32 kHz mono, rumble/hiss
filtering, bounded RMS/peak normalization, 15 ms attack and 80 ms release fades,
and original playback tempo. Phrase boundaries retain the complete spoken
sentence and a trailing breath/reverb instead of splitting individual words.
The manifest records source, cut, digest and byte size per asset.

Runtime speech uses exactly one AudioSource, without PlayOneShot or loops.
Command cues queue behind an already playing sentence and a quiet gap separates clips. Each advance or
panic excerpt plays at most once per battle; saved bitmasks retain this across
load. Shipwreck cries route through the same coordinator so ten sinking hulls
do not layer cries over the commander. Physical combat/wood impacts remain
independent effects.

The second advance asset contains the complete pursuit order. It plays once
when the fleet first detects the player rowing nearby, queued after any current
sentence. Its saved consumption flag prevents replay after loading.
The invasion cinematic now uses the film's invasion statement about Giao Chau,
rather than the pursuit order heard during the original film's battle. It plays once, with a
caption; the regular scheduler remembers that consumed cue. A transition to
victory lets the current sentence finish, while defeat, reset and load cancel
the old scene's channel explicitly.

The invasion-line correction passed 48 victory/cinematic checks in Unity batch
mode, including the actual invasion caption, complete sentence scheduling and
unchanged player save files. The replacement decodes to 3.52 seconds with a
peak below 0.740 and matches the manifest digest.


Historical 30 October implementation (superseded below): Enemy_Trap_Order said “Giết hết chúng nó!” at trap entry. This short exact line uses the previously approved Microsoft Edge vi-VN-NamMinhNeural workflow (+40% rate), rather than claiming it is a film recording. Its manifest entry identifies it as synthesized. The sinking sequence uses one brief CC0 human crowd scream, then only NgoQuyen_Attack. No panic/cheer clip follows that sentence. A currently playing sentence is allowed to finish, and pending pre-command screams survive checkpoint restoration. The commander excerpt ends at 137.35 seconds; a centre/side spectral mask reduces stereo music/effects inside the speech, and the extra tail is removed. It is still an edited mixed recording, not an original studio voice stem.

## Human trap-entry shout — 3 November 2026

The synthesized trap-entry order has been replaced with the actor's original “Giết!” shout from `ok_KBwgS9eg`, 230.40–230.96 seconds. This is the same human voice/source as the preceding pursuit command. The short excerpt retains the complete first shout and its immediate tail, ending before the next phrase; it does not splice separate words, generate a cloned voice, speed up, or pitch-shift the performance. The subtitle is now “Quân Nam Hán: Giết!” to match the shorter actual recording.

Processing uses the existing centre/side background reduction, rumble/hiss filtering, bounded normalization and short endpoint fades. The result decodes to 0.56 seconds at 32 kHz mono, peak 0.746 and RMS 0.0946. Only `Enemy_Trap_Order.mp3` changes; the other eight recordings retain their content hashes. Existing source-film attribution and rights statements above still apply.

`Tools/prepare_entry_order.py` now delegates to the recorded-audio recipe with `--entry-only`; it no longer imports Edge TTS or sends text to a speech service. A full `prepare_battle_recordings.py` run also includes the same recorded entry. The single-channel scheduler, normal playback pitch and saved entry flags remain. A load taken after the shout will not replay it; use a save before trap entry to hear the replacement in sequence.

The headless `TrapVoice` scenario checks actual import, source metadata, matching subtitle, waiting for the complete pursuit sentence, live entry playback, non-looping/unshifted delivery, and preventing replay after both current and older saves. Test writes are isolated and existing player save/backup hashes are verified. All 24 checks passed; results are in `Logs/TrapVoicePlaytest.json`.
