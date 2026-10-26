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

Six additional allied archers take dry-bank positions above high water at
the shipwreck ambush. They remain hidden before the ambush, fire every
4.5–6.5 seconds, and use a fixed pool of 12 visible arrows. The probability
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
ducks enemy/crowd volume. Three existing recorded human cries from the CC0
pack credited in `ShipwreckAudioCredits.md` add variety during the wreck battle.
All three audio channels pause in menus/dialogue, and stop on defeat/reset.

Seven new speech clips are generated offline using the locally cached
VieNeu model already used by the campaign cast. Ngô Quyền retains Minh Đức;
invaders use Thái Sơn, Đức Trí and Hải Đăng. These are synthesized performances,
not historical voice recordings. The exact text, cast, tempo and size are in
`Assets/Resources/BachDang938/BattleVoices/manifest.json`; reproduction uses
`Tools/generate_battle_voices.py`. No model/runtime dependency ships in Assets.
Save fields retain heard cues and a pending second order so loading does
not replay the opening ambush command or lose a scheduled counterattack cue.

## Verified on 2026-10-26

- Recovery/ambush/voice suite: 71 checks passed. The completed-slot warm
  restore measured 0.0266 seconds in the final headless run.
- NPC save regression: 1,546 checks passed across five original user slots,
  including repeated restore, legacy patrol migration and a real scene reload.
- Tide/wreck save regression: 232 checks passed, including mid-rise, ebb,
  victory and retained-wreck roundtrips.
- All seven MP3 clips decoded, were non-silent and stayed below full scale;
  combined size is 187,364 bytes. Phase triggers, pauses and saved cue flags
  were exercised through the runtime components.

Tests ran through Unity CLI in headless mode. They do not replace a visual
layout review or listening pass on the player's speakers. The warm restore
measurement excludes application startup and the loading-panel frame.
