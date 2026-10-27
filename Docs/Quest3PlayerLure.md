# Quest 3: player-led pursuit

The fleet launches together, with its existing separated columns and ranks,
1,160 world units farther upstream than the previous entry. It approaches slowly
but holds outside the stake field until the player rows close to a vessel.
Detection is its half-hull length plus 340 units. Swimming or an abandoned
rowboat cannot trigger detection.

The approach arrow points ahead of the nearest enemy bow, with hull clearance.
On detection it changes to a return route through the stake field. A shared
formation anchor follows the rower at 26 units/second; each ship retains its
separate lane and rank. The anchor cannot reverse the ranks through one another
when the rower circles. All actual vessel motion still uses the solid hull sweep.

After detection, the player must cross the middle of the field aboard the boat.
The pursuing formation then commits to its separate interior trap berths rather
than crowding around the small rowboat. All full hulls must be inside before the
controlled ebb begins. The existing common stake-contact event then catches and
sinks the fleet together. Idle time at the dock cannot advance these stages.

The full recorded pursuit order plays once after detection, behind any current
sentence on the existing single voice channel. Save data retains pursuit stage,
formation anchor and the consumed pursuit order. Loading a chase preserves ship
positions; loading an older active battle retains its existing berth approach
instead of restarting baiting. Intro, falling tide and wreck progress retain
their previous restore behavior.

`BeachBoatConcurrentBattlePlaytest` exercises waiting without bait, detection,
production rowing and hull sweeps on the return, pursuit/entry save restoration,
separate hulls, all-ships-inside ebb, shared sinking, retained wrecks, victory and
unchanged user save files. Its temporary saves are isolated under `.utmp`.

Validated in Unity 6000.6.0f1 on 2026-10-27: 41 concurrent pursuit/ambush
checks, 50 cinematic/voice/traversal checks and 61 player-guidance checks passed.
The complete hull formation triggered falling water at game time 110 seconds,
then all ten ships started sinking together at 119.3 seconds as the stakes
emerged. These are test-run observations, not a timed trigger: gameplay still
requires player detection, returning through the field and every hull inside.
The 3.07-second pursuit clip decodes successfully, matches its manifest digest
and peaks below 0.656. All checked user save files remained byte-for-byte intact.
