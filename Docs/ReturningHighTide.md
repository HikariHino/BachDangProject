# High water for return journeys

Completing the final planting cluster immediately starts a smooth rise to high
tide, before the player reports back. The timeline releases the planting boat's
low-water lock and reserves high water through the preparation/reporting stages.
The rowboat only reserves low water while the planting objective is unfinished.

Winning Quest 3 starts the same rise and holds high water afterward. Victory still
requires all ships to settle and all surviving enemy soldiers to be defeated;
water does not rise while those combat objectives remain. The HUD describes the
rising/flooded state instead of continuing to announce exposed stakes.

The existing save records the current water height, target and direction. A new
save made during a rise continues from that saved height without jumping or
restarting. The load path also reconciles older completed-planting/victory saves
whose target was still low water. Battle saves retain their controlled ebb.

Sunk hulls are kept as grounded wrecks. Raising the water does not clear, respawn
or refloat them. Loading retains their saved position, rotation and broken timber
geometry. Water may cover more of the retained wreck as it rises.

`BeachBoatReturnTidePlaytest.RunInEditor()` performs the final planting action,
writes checksum-protected saves under an isolated `.utmp/ReturnTideQA` directory,
reads and applies those files during/after both rises, checks older low-water
saves, preserves a mid-battle ebb, and loads a victory scene through the real
front end. It compares hull and broken-mesh world positions and verifies that
the original user saves/backups remain unchanged. Results are written to
`Logs/ReturnTidePlaytest.json`.

Validation on 2026-10-26: all 232 return-tide checks passed, including actual
final-cluster completion, save files during/after both rises, legacy low-water
targets, the in-battle ebb, ten retained grounded wrecks, full front-end reload
and unchanged original user save/backup hashes.
The NPC load regression then passed all 1,546 checks against the five existing
user slots with this policy enabled, including the real scene-reload path and
unchanged save/backup file hashes (`Logs/NpcLoadPlaytest.json`).
