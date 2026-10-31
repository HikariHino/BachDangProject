# Wreck combat performance and boss deck — 31 October 2026

Swimmer crowd queries now use the ActorSolid layer (14), which was unused by authored scene and prefab objects. Contact capsules stay solid against the environment and projectiles. Static hull/stake sweeps still use the complete collision mask; only crowd neighbour gathering excludes scenery. Path expansion is limited to 64 nodes and one plan per frame, with swept local passing while waiting. Navigation synchronizes the physics snapshot once per actor movement rather than for every attempted passing direction. Nearby hostile deck-landing searches cache carrier-local results for 0.3 seconds; distant swimmers approach the boat without repeatedly scanning all landing candidates.

Closed-hand geometry variants and renderer bindings are prepared over startup frames. Unchanged grip poses reuse the same mesh and cached renderer list, including on restoration. Blood particle systems, the bounded damage-number pool and number glyphs are prepared before the first combat impact. The boss arena is also prepared before its reveal.

The third wreck search now leads to a 3.6-second scripted knockdown, backward throw, landing and recovery before combat control resumes. It reuses the humanoid rig with bounded arm solving and existing impact/sword sounds; it is not a newly recorded animation clip. A camera frames both fighters. The source warship's raised stern cabins are omitted in the isolated duel so their roof cannot obscure the throw. Attacks, dodges and manual/automatic saving are blocked during the throw. The temporary checkpoint is captured at stable duel positions beforehand; retry enters controllable combat rather than retaining the cinematic pose.

Both fighters receive deck support after Animator and procedural weapon poses, at execution order 1100. This prevents later animation from undoing an earlier foot correction. A continuous solid floor fills the seams between visible planks. Returning to the river restores the normal camera, animator and motor; existing duel save fields retain their format and meaning.

## CPU measurements

Unity Editor 6000.6.0f1, 120 hostile-swimmer steps of 0.02 simulated seconds after simultaneous sinking of all ten ships. The same scene and test setup were used. Times below are CPU sections, not complete frame times or a GPU/FPS benchmark.

| Section | Earlier measurement | Final measurement |
| --- | ---: | ---: |
| Maximum hostile step, original branch head | 34.02 ms | 11.54 ms |
| Mean hostile step, before the dedicated actor-layer filter | 16.51 ms | 1.90 ms |
| 95th percentile hostile step, before actor-layer filter | 18.89 ms | 3.00 ms |
| Ten simultaneous wreck activations | 18.88 ms | 18.82 ms |
| First activation and pose of a new sword rig | 18.07 ms | 2.74 ms |

The final first actual sword damage event took 4.30 ms and the first arrow damage event 0.09 ms, retaining blood, sound and actual damage numbers. Repeated sword poses averaged 0.009 ms; the measured pose loop allocated zero managed bytes. This does not claim that every combat operation allocates zero bytes. The activation measurement still includes a one-frame workload; the largest improvement is the repeated post-wreck swimmer work.

## CLI validation

All 505 checks passed: wreck/performance 19, full concurrent battle 68, shield/crowd 27, combat motion and retry 86, archery 80, moving bow aim 51, camp discipline 79, and epilogue/boss/stalls 95. The crowd test passes two opposing five-person swimming groups around a real pole and checks penetration. The boss suite verifies visible cinematic rendering, control/saving restrictions, pre-duel retry, animated walking/sword/bow feet clearance, a solid deck, physical arrow damage, dodge, mid-duel load, victory return and final reporting.

QA writes use isolated .utmp directories. SHA-256 checks confirmed all 13 existing player save files remained unchanged. Local reports and rendered proof images are in Logs and are not game assets.
