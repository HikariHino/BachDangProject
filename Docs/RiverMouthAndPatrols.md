# River mouth and light patrols

The beachBoat scene now contains 1,246 quest stakes (877 added). Five transverse
belts extend across the connected river channel, including the previously empty
western side. Existing stakes and five quest-cluster identities are retained;
new stakes are assigned to those same clusters. Local planting approaches remain
clear. Stakes stay below high tide and emerge during the existing controlled ebb.
Enemy fleet entry, dispersed wreck berths and the simultaneous ambush remain unchanged.

The later [dock-access revision](StakeFieldDockAccess.md) irregularly scatters the
extension stakes and reserves a narrow winding passage into the reporting pier.

The ten allied vessels now comprise two existing warships and eight light patrol
boats using the player's wooden hull at its gameplay footprint. Their NPCs retain
normal character stature. Each light boat carries one seated operator before
combat; its two boarding fighters use fore/aft stations instead of overlapping
side-by-side in the narrow hull.

Six vessels patrol downstream and four patrol the western upstream water. The
16-point loops span roughly 3,039–4,307 world units, with spaced starting phases.
Navigation reserves the actual enemy approach corridor, rather than all upstream
water. Planning and movement use the same increased 90-unit traffic margin;
solid hull/stake sweeps remain mandatory.

Save data records patrol layout version 2. Older saves retain quest completion
while allied vessels restart on the new safe loops. New saves retain their actual
patrol positions. No player save files are rewritten by the scene tool or tests.

`BeachBoatRiverMouthRevision.Apply()` creates a scene backup under `.utmp`, then
authors the layout. `BeachBoatRiverMouthPlaytest.RunInEditor()` checks transverse
coverage, concealment/exposure, cluster restoration, both save-layout cases and
50 simulated seconds of real navigation. Results and camera-rendered previews
are written under ignored `Logs/`.

Validation on 2026-10-26: the river-mouth playtest passed 67 checks, including
1,000 navigation substeps (50 simulated seconds). Every vessel travelled
1,389–1,839 units and retained a clear solid hull position. The concurrent fleet
regression passed 12 checks: all ten enemies enter, the controlled ebb exposes
all 1,246 stakes, the ships sink and the allied fleet receives its retreat order.
