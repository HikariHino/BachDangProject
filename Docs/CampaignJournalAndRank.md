# Campaign journal, allied boarding and frame pacing

## Player flow

- Tab opens the campaign journal; Tab or Escape closes it. The world and
  minigame inputs pause while reading. The existing corner objective stays in
  its original layout during play.
- The journal retains completed, current and future tasks. Completed tasks
  use a green check. Achievements and the scoring guide have their own pages.
- The title screen also offers **Hướng dẫn xếp hạng** before New Game.
- Winning the fleet battle is not the campaign ending. Closing Ngô Quyền's
  victory-report dialogue advances to celebration; closing the celebration
  dialogue opens the results. Results can be stored in any of the ten slots.

## Score

The final score is the rounded sum of four categories, clamped to 0–100.
Ranks are S at 90+, A at 75+, B at 60+, and C below 60. Deaths and allied
casualties are recorded for achievements, but do not change these four
user-requested score categories.

| Category | Maximum | Calculation |
| --- | ---: | --- |
| Minigames | 35 | Best winning elapsed time / that difficulty's time limit, averaged over war drum plus all 12 village trade levels. Ratios at most 40%, 60%, 80%, and the remaining valid finishes award 35, 28, 20, and 12 points per level. Missing new-game records award zero. Mistake time penalties remain included. |
| Stake operation | 25 | Active time from starting the first cluster until all five are complete, including rowing and the recovery incident. Reference = max(180 seconds, nearest-neighbor route distance / (0.65 × rowboat speed) + five-cluster work duration + 90 seconds). At most reference, 1.5× reference, 2× reference, and longer award 25, 20, 15, and 8 points. |
| Personal kills | 25 | 25 × min(unique personal enemy kills / ceil(initial enemy count × 0.25), 1). Both sword and bow count. Allied kills and repeated kills of one pooled enemy identity do not. |
| Campaign time | 15 | At most 40, 60, 90 minutes, and longer award 15, 12, 8, and 4. Uses actual active elapsed seconds; excludes menus, Tab, opening narration, dialogue and defeat screen. |

Records survive checkpoints and save/load. Replaying a minigame can improve
its best successful record before the ending. The final result freezes after
the victory report; it cannot change by leaving the results open.

Old version-1 slots remain supported. Previous task checks are inferred using
the explicit story order, not numeric enum order. Missing historical timing
is labeled **Tạm tính**: unknown minigames use 20/35, unknown stake timing
15/25, and unknown campaign timing 8/15. Existing real records and kill
counts remain in use. Perfect-survival and S-rank achievements require a
journey with complete historical statistics.

## Battle behavior

Friendly boarding troops no longer respawn after ten seconds. Zero health,
casualty totals and dead deck defenders persist through encounter saves and
victory cleanup; starting a fresh encounter/checkpoint may reset the army.

Space chooses the nearest valid allied or enemy hull while swimming. Allied
deck state appends enum value 5, preserving the previous saved values 0–4.
The saved anchor identifies the actual allied ship. Climbing, carried deck
movement, bow height and sword hits use that carrier; loading does not attach
the player to their rowboat instead.

The navigation box includes the raised stern and must never be used as a
flat floor. Allied deck support now intersects the actual horizontal timber
triangles at each character's X/Z, cached in local spatial cells on first
boarding. Animated boot soles are aligned after pose evaluation. Both enemy
boarders and the player use this surface, including old floating save states.
Only the occupied carrier's navigation proxy is excluded from deck movement;
other actors and vessels still block. Passengers do not block their own hull.
The shared junk mesh retains CPU geometry for this support query.

Both authored warship fleets are enlarged by 20% once per scene instance,
before crew caches, physics hulls and battle lanes initialize. The player's
rowboat keeps its previous dimensions. Authored allied crew and spawned
reinforcements match the player's humanoid skeletal stature, preserving foot
contact; their swords keep the same world length. Enemy boarders retain the
existing player-size calibration after changing carriers. Warship scaling is
reapplied from scene data on load, so saves do not accumulate scale changes.

Scripted swimmers inspect the terrain's actual interpolated slope. A sphere
touching a beach from below can report a horizontal collision normal, which
formerly prevented enemies from reaching the bank. Walkable terrain supports
shore transitions; solid props, steep terrain and vessel hulls still block.

## Frame pacing

River navigation formerly allocated full-map component, cost and predecessor
arrays and scanned the entire map on its first query. It now caches visited
cells sparsely, reuses bounded search workspaces, indexes nearby stakes, and
captures traffic positions once per query. Live allied route requests are
spread across frames. Every route edge and every physical hull sweep still
checks clearance; closest reachable fallbacks never cross land or stakes.

The rowboat consumes a slow frame in slices of at most 50 ms instead of
discarding all time beyond 50 ms. Each slice performs its full collision
sweep. A 160 ms frame matches four 40 ms steps in the regression fixture.

Same-editor samples: 150 camp frames, 150 rowing frames, 200 battle-rowing
frames. These measurements include Unity Editor overhead and are not a
standalone-build FPS guarantee.

| Measurement | Before | After navigation optimization (before fleet enlargement) |
| --- | ---: | ---: |
| Rowing frame p95 | 20.25 ms | 15.71 ms |
| Battle-rowing frame p95 | 24.63 ms | 22.10 ms |
| Largest initial river-path sample | 3240.35 ms | 10.65 ms |
| Largest battle river-path sample | 158.11 ms | 19.97 ms |
| Camp peak managed allocation/frame | 119.70 MB | 0.34 MB |
| Battle peak managed allocation/frame | 89.65 MB | 1.03 MB |

After the requested 20% larger fleets and full-size allied crew, a further
500-frame Editor run measured frame p95 of 25.23 ms in camp, 29.44 ms rowing,
and 27.24 ms in battle rowing. River-path peaks remained 10.67 ms in camp
and 3.81 ms in battle; battle peak managed allocation was 1.04 MB/frame.
The route allocation stall is resolved, but these Editor measurements do not
claim a universal FPS increase, especially with the larger visible fleet.

Editor entry points: `BeachBoatMarineJournalPlaytest.RunInEditor()`,
`BeachBoatSolidRiverPlaytest.RunInEditor(true)` (navigation only),
`BeachBoatConcurrentBattlePlaytest.RunInEditor()`, and
`BeachBoatFramePacingReview.RunInEditor("label")`. They preserve the open
Editor. Generated reports and screenshots are under ignored `Logs/`.

Validation with the enlarged fleets: 69 marine/journal/save/rank checks,
19 navigation checks covering 16,000 allied hull poses, and 12 concurrent
fleet/tide/wreck/withdrawal checks passed. Deck regressions compare boot soles
against real triangle intersections on lower and raised decks, including
old floating saves, full disk save/load, and boarding both factions' ships.
