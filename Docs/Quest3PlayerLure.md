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

Light allied boats escort the player on the western flank during approach,
pursuit and falling water. Their separate stations leave the incoming enemy
lanes, the player's bow and the stake entrance clear. They wait outside the
stake field as the player leads the enemy inside. Large patrol craft retain
their normal routes. Escort movement uses the same solid hull sweep and water
path planner; slow frames advance in small time slices rather than losing time.

Allied crew cannot assign enemy targets, shoot or board during the lure. Enemy
boarding parties also remain aboard until the ambush. Crew from an older save
that prematurely launched an assault return to their own escort stations with
their saved health intact. Once stakes emerge and the ambush begins, ranged
attacks and boarding resume normally; victory still orders withdrawal.

The live-AI pursuit regression passed 51 checks on 2026-10-27, including actual
escort movement, unspent allied arrows throughout pursuit and ebb, no premature
enemy boarding, old-save assault recovery, resumption of allied bow fire at the
ambush, the complete physical fleet sinking together, and unchanged user saves.

Enemy hulls now accelerate and advance along their bow with a bounded
10-degree-per-second turn rate. The formation anchor only changes course while
advancing, with a small lateral deadband. Blocked vessels wait or advance on a
clear heading rather than sliding sideways away from one another. Every move
and turn still uses the conservative solid hull sweep, including smaller steps
on slow frames. Save data retains each live ship's forward speed; older saves
start with a gentle acceleration from their stored position and heading.

The zigzag-bait pursuit regression passed 55 checks on 2026-10-27. Across
12,180 moving hull samples, maximum observed yaw rate was 10.055 degrees per
second and bow/displacement deviation stayed below 0.970 degrees. The player
alternated sideways before returning through the field; all ten ships entered
with separate hulls, then began sinking together. Pursuit speed survived a
save/load, allied escorts kept their no-attack behavior, and user saves remained
byte-for-byte unchanged. These measurements cover live hull navigation, not the
deliberate listing and rolling animation after a ship is destroyed.
