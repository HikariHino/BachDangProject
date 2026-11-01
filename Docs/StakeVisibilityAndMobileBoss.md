# Stake visibility and mobile boss — 1 November 2026

## Actual visibility regression

Ordinary Editor Play Mode reported `Cannot combine mesh that does not allow access: Wood_Spike_02_base`. The imported mesh has Read/Write disabled. Combining it produced empty chunk meshes, while the earlier optimization still disabled all 1,246 originals. The previous batch check counted chunks and colliders without checking their vertex contents; that did not establish visible geometry in normal Play Mode.

`BeachBoatStakeMeshSetup.Build` now creates a readable, simplified mesh offline using the existing UnityMeshSimplifier dependency. The authored source and its import settings remain intact. The resource asset has 514 vertices instead of 11,893. Runtime chunks use that prepared mesh, upload and discard their temporary CPU geometry, and skip unreadable unsupported sources. Empty combined geometry is rejected; any unexpected failed chunk keeps the original renderers as a fallback. Individual authored colliders and saved tide values are preserved.

The completed field now contains 640,444 rendered vertices instead of 14,818,678 (95.7% fewer). Static stake chunks do not cast hundreds of small dynamic shadows. Building this geometry happens over startup frames, never in the ambush callback.

The new normal-Editor Play Mode scenario renders both the original stakes and optimized chunks, compares their silhouettes, checks actual nonempty vertices and world bounds, loads the low-tide save three more times, and captures a normal water-view proof. The observed silhouette intersection-over-union is 0.98446. High tide still covers the field as required by the campaign; low tide exposes it. Existing player saves are isolated from QA writes.

## Boss pressure

The boss closes distance at 52 units/s instead of 32, while normal hero movement is 28. He starts a strike within 38 units rather than winding up from 65. Preparation tracks the player, then commits the direction for the final 0.16 seconds so deliberate evasion remains possible. A short forward attack closes the gap without a delayed single damage check; contact is checked through the active sword window and can damage only once per strike. Some attacks chain a second strike, followed by a shorter recovery. Heavy attacks retain a longer warning and recovery. Physical arrow sidesteps and player shield/dodge rules remain. Movement keeps a 16-unit horizontal separation and selects an inboard position near rail corners rather than clamping both fighters into the same spot.

## Final verification

All **576 targeted checks passed** in ordinary Editor Play Mode through Unity CLI: stake visibility/mobile boss 23, full-fleet wreck performance 21, epilogue/duel 112, concurrent battle 68, returning tide/save recovery 238 and construction/traversal 114. The returning-tide fixture now performs the real carry/aim/hammer task and cancels the opening camera sequence before attempting its mid-battle save. Cinematic save restrictions remain in production.

The mobile-boss fixture starts 160 units away, retreats at normal hero speed, then strafes continuously. The boss closes to 16 units, lands real damage, performs multiple attacks and stays inside the rails. The existing duel suite also passes physical arrow hits/evasion, player dodge, feet support, cinematic order, retry, mid-duel loading and final victory.

With ten wrecks and all thirty support skiffs/sixty crew, wreck activation measured 17.49 ms; AI sections averaged 4.92 ms with p95 6.92 ms, and first sword/arrow hits 4.18 ms/0.065 ms. The ordinary Editor main-thread recorder averaged 38.81 ms in this fixture. These are local measurements, not an FPS promise; the earlier headless run and its empty ordinary-Editor meshes are not valid before/after rendering baselines. All thirteen player save/backup files retain their hashes.

Local proof: `Logs/Review29_Low_Tide_Stakes.png`; geometry/AI report: `Logs/StakeBoss01Playtest.json`.
