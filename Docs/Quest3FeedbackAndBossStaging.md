# Quest 3 feedback and boss staging — 1 November 2026

Follow-up: [stake visibility and mobile boss](StakeVisibilityAndMobileBoss.md) corrects a normal-Editor rendering regression that the earlier batch checks missed. The old checks counted chunks/colliders without asserting nonempty geometry.

Ngô Quyền gives his voiced order and a visible command gesture after the second distinct bow threat or a weapon hit. Soldiers use separate surrounding goals and actual attacks; defeat waits for player health to reach zero. Earlier checkpoint loads clear retaliation according to their saved state.

Construction teams use deterministic irregular positions and varied headings at all five sites. A layout version migrates unfinished old rows while preserving completed or parked legacy support fleets. New layouts reload their saved transforms. Thirty support boats and sixty soldiers survive recovery and repeated loads. Spawned crew now receive the existing distance-based character meshes and shadow settings.

Carrying a stake uses the existing walking animation on three fitted solid timber work planks inside the rowboat. Movement follows carrier-local positions continuously instead of snapping between disconnected bench surfaces. F from a wreck searches nearby clear water from the player's current position, then initializes swimming before writing the final position. The prisoner-search route points through the camp river gate before pointing at wrecks.

The boss challenge and throw occur at the original wreck, during the encounter. The hero lands on exposed supported timber above the restored high tide; only then does a fade enter the wide duel arena. Retry is saved after this transition. The source sloped deck is omitted in that arena, and cached skinned boot envelopes keep both fighters above the visible floor after animation. The boss moves and recovers faster, deals slightly higher damage and physically sidesteps some eligible incoming arrows with a cooldown. A dodge does not grant invulnerability.

Static stake visuals are combined in spatial chunks: 1,246 source renderers become 271 chunks while individual solid colliders remain. The 60 support soldiers now change mesh detail with camera distance; the tested example falls from 111,101 to 41,106 vertices. These changes reduce rendering work without removing the support fleet or solid stakes.

## Verification

Native Unity 6000.6.0f1 batch mode, beachBoat scene: all **803 checks** passed. Suites: CasualWork 97, SupportFleet 26, Review29 42, Revision31 114, Epilogue 112, WreckPerformance 20, MotionRecovery 86, BowMovement 51, ShieldCrowd 27, Archery 80, Discipline 80 and ConcurrentBattle 68.

Checks include all five construction sites; supported continuous carrying (17.84 units travelled, maximum sampled step 0.226); layout migration and repeated loading; current-position wreck exit and the next swim frame; camp gate guidance; real army damage and retry; the original-wreck throw before arena activation; physical projectiles and boss sidesteps. Independent CPU skinning of the actual character vertices checks both fighters against visible deck triangles over walking, sword and bow poses.

The representative headless performance fixture uses ten ships at actual water height plus all thirty support boats and sixty soldiers. Ten wreck activations took 16.02 ms; measured AI sections averaged 4.14 ms, p95 5.62 ms, maximum 24.80 ms. The main-thread recorder averaged 13.26 ms. First actual sword and arrow hits measured 4.64 ms and 0.075 ms. These are local Editor CPU measurements, not an end-to-end GPU/FPS guarantee or a direct comparison with the older underwater fixture.

QA saves use isolated `.utmp` paths. SHA-256 verification confirms all thirteen existing campaign save files remained unchanged. Reports, logs and rendered proofs are local ignored artifacts under `Logs`, not shipped assets. Batch logs: `Revision01Verified.log` contains the first five passing suites and an obsolete performance-fixture setup failure. After correcting that fixture to restore the full fleet through the production save path, `Revision01BattleFinal.log` passes performance and the remaining six suites; the latest JSON reports are all successful.

Run from the project root in PowerShell, with the project closed in any other Editor instance:

```powershell
& 'B:\Unity\6000.6.0f1\Editor\Unity.exe' -batchmode `
  -projectPath 'A:\FPT subject\New folder\BachDangProject' `
  -executeMethod BeachBoatBatchRevision31.Run `
  -qaSuites CasualWork,SupportFleet,Review29,Revision31,Epilogue,WreckPerformance,MotionRecovery,BowMovement,ShieldCrowd,Archery,Discipline,ConcurrentBattle `
  -logFile 'A:\FPT subject\New folder\BachDangProject\Logs\Revision31Batch.log'
```

Do not add `-quit`: the harness exits after the asynchronous play-mode suites finish. It requires native batch mode and does not operate desktop windows. The throw remains a procedural sequence using the existing rig; graphics-driver-dependent frame pacing still needs observation in normal gameplay.
