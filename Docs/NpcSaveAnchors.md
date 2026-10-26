# NPC positions after loading

The patrol-layout migration moved allied boats onto new safe routes, but the old
boarding save then restored stationed crew to their former world coordinates.
Loading the user's slot 1 reproduced 24 crew members outside their own hulls,
some more than 2,000 world units away.

Stationary defenders now restore to their authored station relative to their
current carrier. Their health and active/dead flags remain saved independently.
Living boarders restore against their actual deck coordinates. Legacy fighters
on migrated light boats use valid new deck stations; an interrupted climb to a
migrated vessel resumes beside that vessel in the water. Land walkers are not
teleported by the load process.

The tide restores before vessels. Guard health/parent restoration runs before
boarding poses, preventing ResetGuard from replacing a restored swimming or
sword state. A final reconciliation runs after the saved player coordinates are
applied. Regular enemy deck guards now save a relative pose; older saves retain
their authored deck station. No user save is rewritten by this repair.

`BeachBoatNpcLoadPlaytest.RunInEditor()` checks every available user slot without
writing to it, repeated current-layout loads, legacy migration with enemy/allied
boarders, saved casualties and the actual front-end scene reload path. On
2026-10-26 it passed 1,546 checks across all five existing slots. Checks include
real mesh support, boot height, stature, carrier identity, quest progress, health,
and unchanged SHA-256 hashes of the original save and backup files. The report is
`Logs/NpcLoadPlaytest.json`; the initial reproduction is `Logs/NpcLoadBeforeFix.json`.

The audio/combat regression also passed its 106 checks after this change. Two
additional checks confirm that the player remains supported by the actual light
boat deck after current-layout and legacy-layout loads. Corrected slot-1 crew
renders are `Logs/NpcLoadFixed_Warship.png` and `Logs/NpcLoadFixed_LightBoat.png`.
