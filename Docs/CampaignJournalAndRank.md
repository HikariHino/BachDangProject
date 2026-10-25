# Campaign journal, allied boarding and frame pacing

## Player flow

- Tab opens the campaign journal; Tab or Escape closes it. The world and
  minigame inputs pause while reading. The existing corner objective stays in
  its original layout during play.
- The journal retains completed, current and future tasks. Completed tasks
  use a green check. Achievements and the scoring guide have their own pages.
- The title screen also offers **Hướng dẫn xếp hạng** before New Game.
- Escape during ordinary world play opens Pause; another Escape resumes.
  During drum, supply relay, trade or camp recovery minigames it closes the
  current activity directly to the world. Previously earned levels, material
  collection, best times and claimed rewards are retained. Cancelling an
  unfinished attempt does not complete its quest or claim a new reward.
  The same press is consumed so it cannot also open Pause. An open Tab
  journal and existing settings/save confirmation pages keep their own back
  behavior. The minigame instruction/result text describes this distinction.
- Winning the fleet battle is not the campaign ending. Closing Ngô Quyền's
  victory-report dialogue advances to celebration; closing the celebration
  dialogue opens the results. Results can be stored in any of the ten slots.

## Timed ranks (rules version 3)

A rank requires **all nine conditions**: drum, three levels at each of the
four trades, the five-log village search, all five stake clusters, personal kills, and campaign time.
The ending awards the highest rank satisfying every condition; otherwise C.
Fast results in another activity cannot compensate for a missing or slow one.
The old weighted score is no longer used or displayed.

| Condition | S | A | B |
| --- | --- | --- | --- |
| Drum, two full rounds | ≤22 s | ≤33 s | ≤44 s |
| Each individual trade level | ≤60% of its timeout | ≤80% | ≤95% |
| Stake operation | ≤reference | ≤1.5×reference | ≤2×reference |
| Find all five village logs | ≤search reference | ≤1.5×reference | ≤2×reference |
| Personal kills | ≥target | ≥ceil(0.75×target) | ≥ceil(0.5×target) |
| Campaign time, reported victory | ≤40 min | ≤60 min | ≤90 min |

Trade limits come directly from `VillageWorkStall.LimitFor`, shared with
the minigame timer. Timber Dễ/Vừa/Khó timeouts are 100/135/180 s, rope
100/75/55 s, rice 100/100/110 s, and provisions 90/70/50 s. Every level
has visible absolute S/A/B deadlines, its actual best time, and earned tier
in both the journal and minigame UI. Mistake penalties remain included.

Stake reference = max(180 seconds, nearest-neighbor route distance /
(0.65 × rowboat speed) + five-cluster work duration + 90 seconds).
The timer covers planting, rowing and the recovery incident. Kill target =
ceil(initial enemy count ×0.25), normally 16; resulting S/A/B targets are
16/12/8. Each enemy identity counts once, using player sword or bow kills.
Campaign time excludes menus, Tab, opening narration, dialogue and defeat.
Its achievement is confirmed only after reporting victory.

Records survive checkpoints and save/load. Replaying a minigame can improve
its best successful record before the ending. The final result freezes after
the victory report; it cannot change by leaving the results open.

Old save-file version-1 slots remain supported. Previous task checks use the
explicit story order. Missing historical timing is shown as **Chưa ghi**;
there is no assumed passing achievement. Endings saved under the old point
rules are re-evaluated from real records and marked with rankRulesVersion=3.
Missing historical data remains **Tạm tính**. The legacy finalScore field is
retained for file compatibility but is no longer used to determine rank.

The village now chooses five clear positions around inhabitants and market
approaches per new journey, with random offsets and yaw. Pickups have no
floating wood labels or distant route marker. Only the closest log within
10 world units offers `[E] Nhặt`. Save slots retain each log's identity,
position, rotation, collection state, search duration and target. Existing
seven-log saves retain already collected identities when reduced to five.

Search reference = max(180 seconds, exact shortest order through the five
positions from the village arrival / walking speed ×1.8 +100 seconds).
The 1.8 factor permits walking around buildings; 100 seconds permits visual
search without markers. Timing begins on entry to the wood-search quest and
stops on the fifth pickup. Pause, Tab, dialogue and screen transitions are
excluded. Leaving and returning does not reset it. A historical save without
a measured search time cannot receive an invented timed search achievement.

## Nested missions and optional training

The mission page expands supply preparation into four trade headings and
12 Dễ/Vừa/Khó subtasks. Green checks show actual completed levels; old saves
can show completion with a missing time until replayed. Parent supply
completion still requires collecting all four finished trades. The 15 main
mission count excludes these detail rows and optional activities.

Stamina training is a separate optional heading with three levels, +20 max
stamina each and +60 total. It may be skipped without blocking story progress
or any rank. Its reward and completion state continue to use PlayerVitals.

## Drum training station

The former isolated drum is relocated at runtime to a clear side of the
camp yard, relative to CampLayoutOrigin (-48, -120 in X/Z). This leaves the
main central path clear. It has a solid timber rack, hide drumheads, bronze
rims/studs, banners and a training sign. The station name stays unchanged
for save compatibility; nearby E interaction uses a 26-unit radius.
The new command UI presents a drum face, eight-beat sequence, military
signals, two-round progress, timer and exact rank deadlines. The existing
1/2/3 input and mistake penalty are preserved.

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

Validation with the enlarged fleets and timed achievement rules: 129 marine/journal/save/rank/drum checks,
19 navigation checks covering 16,000 allied hull poses, and 12 concurrent
fleet/tide/wreck/withdrawal checks passed. Deck regressions compare boot soles
against real triangle intersections on lower and raised decks, including
old floating saves, full disk save/load, and boarding both factions' ships.

Contextual Escape validation: BeachBoatEscapePlaytest.RunInEditor() passed 92
checks for mandatory drum cancellation, completed drum replay, all four trade
stalls (instructions, completed replays, win and loss), optional stamina
training (unclaimed and claimed rewards), journal priority and Pause/settings
back behavior. The test does not write player save slots.

## Traversal and combat correction

Returning to the rowboat from a bank or the water uses its current hull, with
E or Space, and restores the rowing controller, motor, collider and body mode.
Leaving a warship with F chooses a clear water point beyond an actual gunwale.
Space can immediately climb back. Deck walking and visual support use the
same animated sole offset; broad hull boxes do not block melee between actors
on the same deck. Boarders retain their landed carrier when the player changes
ships, and seek separate landing spaces instead of stacking on one crewman.

Swimmers use bounded local paths around stakes while every executed movement
still sweeps solid colliders. An existing overlap from an old save is separated
before subsequent motion. Planning is limited to two actors per rendered frame.
The dedicated River_Sword_Combat controller uses a standing idle with fitted
arms and a palm-aligned handle, and requires running input to enter its run
state. Entering shore walking clears seated humanoid animation state.

`BeachBoatTraversalWoodPlaytest.RunInEditor()` covers wood layout/pickup/save
compatibility, search rank boundaries, a full swimming/shore/rowing round trip,
solid-pole detours and embedded-pole recovery, allied-deck movement, F/Space
exit/reclimb, and actual enemy movement and melee damage aboard an allied ship.
Reports are under `Logs/TraversalWoodPlaytest.json` and
`Logs/MarineJournalPlaytest.json`; test save files remain under ignored Logs.
