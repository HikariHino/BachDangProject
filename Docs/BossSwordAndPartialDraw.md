# Boss melee, short bow draws and dry deck footsteps — 1 November 2026

## Reproduced melee failure

The earlier contact fixture placed an opponent directly onto a sampled blade. It established collision response, but not that an ordinary facing attack could connect. The new `BeachBoatBossCombat02Playtest` walks up through the real controller, leaves the boss in place, starts a real swing, and plays it fully without repositioning the target or forcing its animation time. Before the fix this dealt zero damage at the 16-unit separation limit.

The duel now keeps nine units of body clearance, approaches to twelve and closes a committed strike to ten. The prior spacing kept the real weapon out of reach. The player rig is kept animated during the duel even outside the camera frustum, with its previous culling mode restored on exit. Imported wrist axes also sent the player's cutting edge behind the body. A duel-only hand/arm retarget follows a raised, downward and recovery grip path while retaining the lower-body attack/jump clip and closed fingers. The weapon remains attached to the actual hand; contact still uses its visible blade, not extra invisible reach or radial damage. The new natural attack deals twelve damage exactly once. Boss pursuit still lands real hits against an ordinarily moving player; leaving sword range still prevents contact.

## Short bow release

The old input bridge silently discarded releases below 0.15 seconds. `StepBowDraw` now handles a started press/release consistently across the campaign, including the boss deck. A tap or partial draw releases a weaker arrow; holding the meter is optional. Speed, gravity, short/full damage, reload, ammunition and physical collisions remain. Repeated release events cannot duplicate an arrow, and leaving aim cancels an unfinished draw.

The new scenario uses the actual posed bow, release input bridge and physical projectile simulation. Draws of 0.04 and 0.3 seconds both consume one arrow and deal five boss damage; a 1.1-second draw deals ten. It does not invoke the private projectile-launch helper.

## Deck recordings

All six `Deck_Wood` clips use different takes from the existing credited CC0 wood recording. Each is now 160 ms instead of 355 ms, with a short decay envelope and a 2.4 kHz low-pass to remove the long ringing tail. Measured RMS is 0.082–0.103 and peaks 0.39–0.61, so the change does not depend on making an old sound inaudible. They finish before the 230 ms sprint cadence and preserve normal mute/volume controls.

Rebuild only these six assets with `python Tools/prepare_youtube_action_audio.py --deck-only` using the cached source listed in `ActionAudioSources.md`. The manifest retains source cut times and actual output hashes. Other action recordings are preserved.

## Validation

All 374 targeted checks passed in headless Unity: natural boss attacks/short draws 32, physical-contact and controls 50, mobile boss/stakes 23, epilogue/retry 112, moving bow 51 and action audio/battle 106. The older audio fixture was updated to complete mandatory archery, enter the actual counterattack phase and supply the current arrow-launch argument; its saves now use an isolated directory. Tests isolate save writes and verify all thirteen player save/backup hashes. Local reports: `Logs/BossCombat02Playtest.json`, `Logs/BossCombat02NaturalSword.json` and `Logs/DuelContactPlaytest.json`.
