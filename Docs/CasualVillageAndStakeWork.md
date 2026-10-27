# Opening camp, village supplies and physical stake work

The four village puzzles described below have since been replaced by physical
third-person trades. See [PhysicalVillageTrades.md](PhysicalVillageTrades.md)
for the current controls, timing targets and save compatibility. Camp lighting
and the boat-based stake construction remain as described here.

The opening camp contains 26 additional fireposts on path shoulders, near the
tent rows, gate and command stairs. They reuse the existing torch mesh and flame
atlas. Warm point lights cast no shadows; the existing scene budget still enables
at most eight nearby lights, and distant flames stop emitting. Three views at
the same 05:45 quest hour were captured before and after the change.

Each of the four village trades now opens directly into Medium gameplay.
Timber, rice and provisions require two orders; rope requires one connected path.
A single victory grants that trade's supplies. There is no Easy/Medium/Hard
unlock chain. H opens optional instructions; returning preserves the current
order, elapsed time and progress. The existing rations demonstration video is
available there. Ingredient shortage/excess colors and undo remain unchanged.

The journal and rank conditions score five minigames: the drum plus Medium at
the four trades. Existing Medium timing IDs are retained, and fully collected
supplies retain their old serialized completion marker. Partial old difficulty
chains restart at Medium; they never require Hard. No missing timing is invented
to award a rank.

Stake construction remains in the third-person 3D scene:

1. Stop the rowboat at an unfinished cluster and press E to begin work.
2. Press E to pick up a visible bundle from the boat.
3. Hold W to carry it along the deck to the bow, then E to position the stake.
4. Click the left mouse button for three separated mallet strikes.
5. Repeat for three bundles. Physical field stakes are driven into position as
   the player works; the existing five clusters still build the full barrier.

The character stands on the actual deck and uses a carry pose, walking legs and
a mallet swing. Camera orbit remains available; the rowing paddle is hidden
during work. Props are decorative and cannot push the hull or the character.
The boat remains subject to its ordinary solid collision and buoyancy rules.
X cancels an unfinished cluster; completed clusters remain. Pausing stops the
work. The lost-bundle recovery incident remains between the third and fourth
clusters. Finishing all five still raises the tide for the return to camp.

Manual saving is deferred while a bundle is being handled; automatic saving
already waits for a stable interaction state. Completed clusters restore through
the existing saved cluster flags, with no carried props or animation lock after
loading. The stake rank target allows 14 seconds of handling per cluster in
addition to navigation and the existing time allowance.

Validation uses isolated temporary save directories and verifies hashes of real
user saves. `BeachBoatCasualWorkPlaytest` plays all four Medium trades, checks
help/resume, earns their supplies and timing records, saves/loads, performs all
five physical work clusters, recovers the lost bundle, cancels an unfinished
cluster and loads the completed field. `BeachBoatCampLighting` verifies light,
particle and distance budgets and captures the three comparison views.

Final Unity 6000.6.0f1 validation on 2026-10-27: 21 lighting checks, 46 casual
trade/construction/save checks and 60 Quest 3 fleet/ebb/combat regression checks
passed. Real user saves remained unchanged in all three suites. The render
capture helpers create their readback textures after Camera.Render, so Unity's
asynchronous unused-asset cleanup cannot invalidate an unreferenced readback
texture during the first capture.
