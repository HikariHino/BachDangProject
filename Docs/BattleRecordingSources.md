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
| Enemy_Advance_1 | ok_KBwgS9eg | 230.48–231.10 |
| Enemy_Advance_2 | ok_KBwgS9eg | 231.48–232.72 |
| Enemy_Cheer_Crowd | ok_KBwgS9eg | 212.36–213.15 |
| Enemy_Panic_1 | ok_KBwgS9eg | 308.53–309.35 |
| Enemy_Panic_2 | ok_KBwgS9eg | 283.30–284.12 |
| Enemy_Panic_3 | ok_KBwgS9eg | 290.08–290.82 |
| NgoQuyen_Ambush | ok_KBwgS9eg | 269.06–270.47 |
| NgoQuyen_Attack | FEdW8VpWHA8 | 132.01–132.53 |

Reproduce with `Tools/prepare_battle_recordings.py` (the previous
`Tools/generate_battle_voices.py` entry point delegates to it). Full sources
and automatic captions stay in ignored `Logs/BattleVoiceSources`; the game
contains only the eight short excerpts. Processing: 32 kHz mono, rumble/hiss
filtering, bounded RMS/peak normalization, brief fades and 1.06x pitch-preserving
tempo. The manifest records source, cut, digest and byte size per asset.

Runtime speech uses exactly one AudioSource, without PlayOneShot or loops.
Command cues have priority and a quiet gap separates clips. Each advance or
panic excerpt plays at most once per battle; saved bitmasks retain this across
load. Shipwreck cries route through the same coordinator so ten sinking hulls
do not layer cries over the commander. Physical combat/wood impacts remain
independent effects.
