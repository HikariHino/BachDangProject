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
| NgoQuyen_Attack | FEdW8VpWHA8 | 128.62–137.80 |

Reproduce with `Tools/prepare_battle_recordings.py` (the previous
`Tools/generate_battle_voices.py` entry point delegates to it). Full sources
and automatic captions stay in ignored `Logs/BattleVoiceSources`; the game
contains only the eight short excerpts. Processing: 32 kHz mono, rumble/hiss
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
