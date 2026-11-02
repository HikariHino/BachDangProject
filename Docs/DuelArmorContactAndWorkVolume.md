# Animated armor contact and work audio — 3 November 2026

## Visible hits missed by the old volumes

The previous duel used small hand-tuned bone capsules. A new independent CPU-skinned surface probe found 81 of 121 sampled visible armor points outside those volumes. A normal frontal attack could succeed while hits on other visible armor surfaces were rejected.

`DuelArmorContact` now fits cached convex envelopes to the actual readable close-detail skinned mesh, grouped by its bones. Twenty-six supporting directions keep each envelope close to the armor shape instead of using an enlarged root-distance hit area. The envelopes follow the real animated bones, including arms, head and legs. The existing capsules remain a fallback for an unsupported unreadable rig. These are fitted collision proxies, not per-frame skinned triangle colliders.

Contact includes relative target movement between consecutive poses, so a boss crossing a blade between frames is not lost just because he is clear at the final endpoint. Blade sampling is also retained immediately before and after the active downstroke. A frame spanning the start/end clips its sweep to that interval, rather than dropping the hit. Lift and recovery alone still do not hurt. Each swing still deals damage once. Loads/arena placement reset contact history, preventing teleport paths from being treated as combat movement.

The idle armor probe now misses 0 of 121 sampled points. Twelve walking, running, blocking and attack pose samples miss 0–3 of 121 points each. Tests also cover ordinary forward-moving attacks at 20 ms and 80 ms steps, a frame spanning both cut-window edges, a target crossing the blade, clear misses, shield behavior and retry/load. The contact benchmark averaged 0.006162 ms with zero measured allocations over 2,000 warmed queries on this machine. Mesh data is read during preparation; it is not baked or allocated per hit. This timing is not a whole-game FPS measurement.

## Requested three work sounds

Saw, Rope, Knot, Cloth and Crease retain the same recorded content with a 2.4× amplitude gain (+7.6 dB). Peaks stay at or below 0.528, leaving headroom. This raises sawing, tying/tightening and folding/creasing without changing the shared playback graph or its mute/volume controls. Scoop, Pour and SetDown remain byte-for-byte identical, so the rice audio keeps its previous level.

Reproduce only the five boosted clips with `python Tools/prepare_youtube_trade_audio.py --boost-actions-only`. Full regeneration applies the same gains. `Tools/trade_audio_manifest.json` records the measured PCM levels and hashes.

## Verification

All 724 targeted checks passed in headless Unity: armor/frame-edge/performance/audio 41; duel contact/controls 50; natural sword/short bow 32; mobile boss/stakes 23; epilogue/retry/load 112; and actual physical village operations 466. The village fixture now waits for the opening transition before entering a stall and asserts the requested 2.4× action RMS while preserving the rice bounds. Its successful run completes all four workstations, tool sounds, pause behavior and save/load cleanup. Thirteen existing player save/backup files retain their hashes; test writes use isolated directories.

Local evidence: `Logs/DuelArmor03Probes.json`, `Logs/DuelArmor03PoseCoverage.json`, `Logs/DuelArmor03Playtest.json` and `Logs/PhysicalVillagePlaytest.json`.
