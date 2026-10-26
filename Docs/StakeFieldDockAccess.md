# Natural stakes and access to the reporting pier

The complete river field keeps the same 1,246 stake objects and five quest-cluster
assignments. The 1,095 repeated extension stakes are now scattered with a fixed
seed across irregular belts, with varied yaw, lean and exposed height. The scene
stores the final transforms; gameplay and loading never rerandomize them.

A 168-unit service passage bends along the western side of the field. A separate
branch leads into the player boat's authored berth at (-3505, 1880), within the
existing dock interaction range. Eighty-five stakes formerly obstructing these
paths were relocated into neighboring water; none were deleted or made
non-solid. Clearance also reserves each stake's actual horizontal mesh bounds.
Turning space is preserved at bends and beside the pier. Flood concealment and
ebb exposure remain unchanged.

`BeachBoatStakeAccessRevision.Apply()` backs up the saved scene, updates only
existing stake transforms, rolls them back on failure, and refuses a second
application once its marker exists. It does not rebuild patrol vessels or edit
user save files.

`BeachBoatStakeAccessPlaytest.RunInEditor()` checks the full hull along the entire
service path and dock branch at high and low tide, turning poses, the real
disembark interaction and irregular field coverage. It renders the field and
dock approach to `Logs/`. The concurrent battle test checks enemy entry, the
controlled ebb and sinking against the resulting layout.

Validation on 2026-10-26: all 17 access-layout checks passed, including 234 hull
poses at each tide level, turning room at all bends, and the real disembark
interaction. All 12 concurrent battle checks also passed; the ebb exposed all
1,246 stakes and all ten invaders sank. Reports: `Logs/StakeAccessPlaytest.json`
and `Logs/ConcurrentBattlePlaytest.json`. Images: `Logs/DockAccess_Clear.png`,
`Logs/NaturalStakeField_Overview.png` and `Logs/NaturalStakes_PlayerView.png`.
