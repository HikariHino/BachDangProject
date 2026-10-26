# Hull durability, temporary recovery and shoreline ambush

The player's rowboat has 100 hull durability, displayed above the health HUD
while the boat session is active. Colliding with solid boats, stakes, rocks,
structures, trees or shallow banks damages the hull rather than the player's
health. Damage scales with impact speed; gentle docking below 12 units/s is
harmless. Full normal-speed impacts cost 28 (stake), 26 (ship), 21 (rock),
17 (structure), 15 (tree), or 12 (bank). Contacts have a 1.2-second cooldown.
Hull clearance still rejects movement through the obstacle before damage.
At zero durability the boat stops, cannot be reused, and the defeat screen
offers recovery from the latest successful saved point.

The separate `campaign938-auto.json` file stores a rolling recovery snapshot.
It uses the same SHA-256 envelope, verified temporary write, atomic replace
and `.bak` fallback as manual saves. It never consumes or overwrites any of
the ten manual slots. Autosaving begins with a valid new-journey baseline,
then repeats every 60 seconds of active gameplay. Menus, dialogue, opening
sequences and defeat do not count. A due save waits until a minigame,
stake-planting animation or climb finishes, rather than recording a partial
activity. Failed writes retry after ten real seconds and show a notice.

**Tiếp tục** loads the newest valid temporary or manual snapshot. Manual
saves have UTC tick timestamps, including when they happen in the same
second as an autosave; legacy dates remain supported. **Load Game / Ô lưu**
always opens the ten manual slots and restores the selected slot's exact
progress. Explicitly loading an older slot starts a new temporary branch
from that progress, leaving the chosen slot unchanged. Recovery snapshots
are deep-copied so live achievement timers cannot mutate a retry point.

Version-1 saves without durability fields start with 100 hull durability.
New saves retain the actual damaged hull. Invalid/broken-hull snapshots are
not accepted as playable recovery data. Health, tide, wrecks, crew stations,
boarding, personal/allied kills, quest progress and achievements retain the
existing save/restore path.

Completed manual slots display **✓ HOÀN THÀNH**, the recorded rank, a green
card and light-green text. Unfinished saves keep the normal card style.
Loading a snapshot for the currently open scene restores it in place after
one loading-panel frame, avoiding another terrain/asset/actor scene load.
Other scenes still use the full scene-load path. `LastLoadSeconds` records
the measured in-place restore duration for QA; it is not a loading promise
for a cold application startup.

Grass, ferns, shrubs and low plants are passable. The editor revision
identifies their source meshes, including generated `Tree_<guid>` terrain
prefabs, disables their colliders and keeps tree trunks, rocks, buildings,
actors and stakes solid. The revision retains every vegetation instance.

Eighteen additional allied archers form six groups along dry shorelines,
including both dry landmasses around the stake field and the reporting-dock
bank. A terrain grid and connected-land search find smaller islands that the
previous radial search missed. They remain hidden before the ambush, fire
every 12–18 seconds each, and use a fixed pool of 24 visible arrows. The longer
individual cooldown retains roughly the previous overall firing rate. The probability
of aiming a hit decreases from 65% at close range to 25% at 4,200 units;
cover can stop an arrow. A successful shot has a 25% lethal-damage roll;
otherwise it inflicts one of a guard's two health points. Damage is applied
when the arrow arrives and only if that enemy is still alive. Kills count
for allies, never toward the player's personal-kill achievement. Restoring
a save clears in-flight arrows and resets the shoreline ambush; enemy health
and allied kill totals are restored by the normal campaign save.

QA: `BeachBoatRecoveryPlaytest.RunInEditor()` exercises real isolated save
files, the minute timer, pause, hull defeat/retry, legacy durability, backup
recovery, slot colors, completed/unfinished in-place loads and real shoreline
arrow combat. It hashes all original player save files to verify they remain
unchanged. Results are written to ignored `Logs/RecoveryPlaytest.json`.

## Battle voices

Invaders now cheer during their advance, then panic and call for retreat
when the ambush begins. Two timed Ngô Quyền orders announce archery and the
counterattack, with a small subtitle near the bottom edge. Commander speech
is serialized on the same voice channel as enemy calls. Wreck vocal requests
also use that coordinator; the CC0 pack credited in `ShipwreckAudioCredits.md`
remains a fallback when no battle-voice component exists. The voice channel
pauses in menus/dialogue and stops on defeat/reset.

The original generated battle cast has been replaced with eight short recorded
film excerpts at the player's request. Normal NPC dialogue voices are unchanged.
The single speech channel never loops or layers OneShots; each advance/panic
excerpt is consumed once per battle and its used bit is saved. Shipwreck vocal
cues route through that same channel. Physical impacts remain separate effects.
The source links, exact cut times and reproduction instructions are in
`Docs/BattleRecordingSources.md` and the asset manifest. The old generation
entry point now delegates to `Tools/prepare_battle_recordings.py` so it cannot
accidentally restore the rejected generated speech.
Save fields retain heard cues and a pending second order so loading does
not replay the opening ambush command or lose a scheduled counterattack cue.

## Verified on 2026-10-26

- Recovery/ambush/voice suite: 71 checks passed. The completed-slot warm
  restore measured 0.0266 seconds in the final headless run.
- NPC save regression: 1,546 checks passed across five original user slots,
  including repeated restore, legacy patrol migration and a real scene reload.
- Tide/wreck save regression: 232 checks passed, including mid-rise, ebb,
  victory and retained-wreck roundtrips.
- All eight MP3 clips decoded, were non-silent and stayed below full scale;
  the latest recorded batch totals 89,344 bytes. Phase triggers, pauses and saved cue flags
  were exercised through the runtime components.

Tests ran through Unity CLI in headless mode. They do not replace a visual
layout review or listening pass on the player's speakers. The warm restore
measurement excludes application startup and the loading-panel frame.

## Ebb and shoreline regression

The rowboat follows current water height before traversal/sword early returns,
including when the player is on land, swimming, climbing or visiting another
deck. Old saved boat heights are reconciled against the restored tide on load.
Pausing still stops traversal. Walking resumes in water shallower than 40%
of the player's capsule height; entering swim requires water deeper than 65%
and the actual feet to be submerged. This hysteresis avoids repeated swim/walk
flips at the beach, and the feet test protects a player standing on a pier
above deep seabed. Receding water can return an idle swimmer to standing.

When the bank actually emerges beneath a parked boat, a 5×5 keel-footprint
support sample keeps the hull above the sand rather than forcing it through
the terrain down to water level. A nearby player can press E to push toward
deeper water in small swept steps. Solid stakes, hulls and structures still
block that motion. Backing out of shallow water is allowed when clearance
improves. Rising tide refloats the boat. Old embedded saved poses are corrected
on restore, and an interrupted push is not resumed automatically after load.

`BeachBoatShorelinePlaytest.RunInEditor()` verifies all traversal modes across
high/low water, paused movement, shallow/deep transition, hysteresis, standing
on exposed sand, reboarding and old saved floating-boat poses. It also checks
18 prepared archer stations on two dry landmasses, dock-bank coverage, dry
high-water footing, eight loadable audio cues and unchanged real user saves.
The report is written to ignored `Logs/ShorelinePlaytest.json`.

The shoreline suite passed 59 checks on 2026-10-26. The recovery/battle suite
passed another 85 checks after this revision, including 390 simulated arrow
launches, 13 real enemy hits and 7 allied kills. Original manual and temporary
save files remained byte-identical. Decoded speech/crowd clips measured peak
levels below 0.82 (full scale 1.0), with no clipped samples.

The later parked-boat revision passed 40 checks in
`BeachBoatGroundedBoatPlaytest.RunInEditor()`: compact title digits, high/low
shoreline parking, immediate landing when exiting onto exposed sand, actual
mesh vertices above sand, no paused displacement,
solid-object blocking, nearby-player push-off, deep-water recovery, old embedded
save repair and refloating. Its audio checks confirm one non-looping channel,
exhausted playlists that do not restart, the real shipwreck callback routing
vocals to the fleet's shared voice channel, queued wreck vocals, short commands
and preserved used-cue masks after load. Saves also support inactive parent
boats when capturing/restoring their deck defenders.
