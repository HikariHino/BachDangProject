# First-time guidance and report recovery

The wood objective now explains where the five fixed village logs are and how
to collect them with E. The small nearby interaction prompt includes the
current count; distant floating wood labels remain hidden. Restoring a wood
save preserves its collected pieces and achievement timer.

The first visit to the rations stall offers a worked sample before the timed
Easy attempt. Two people need four rice, two fish and one salt for the whole
group. Practice uses the actual ingredient controls, supports undo/reset, has
no timer or mistake penalties and grants no goods, unlocked levels or ranked
minigame result. Campaign completion time pauses during practice. After the
sample, Start begins the normal timed attempt. Existing saved Easy/Medium
completion skips the sample. The worked sample shows the multiplication and
added/required counts. Timed rounds show only the original per-person rice/fish
requirements and whole-group salt requirement. They hide calculated totals
and added counts, including in wrong-answer feedback. Live input updates each
card to yellow for missing, green for enough or red for excess, with the same
status spelled out. An in-round legend explains colors and +1/undo controls.
The rules retain the introductory example.

The three guarded Ngô Quyền report callbacks (prepared supplies, completed
stakes, and victory) refill health and stamina. They retain armor charges,
training and maximum stamina. Entering a camp gate does not heal; repeating
an already completed callback does not heal; loading restores recorded vitals
without granting a refill. A short green HUD notice confirms report recovery.

Quest 3 uses `RiverBattleObjective` for consistent objective titles, descriptions,
direction hints and battle notices. The arrow points to the prepared launch
area during the lure and falling-water phases, rather than an enemy soldier.
The HUD asks the player to wait outside the stake field. Only the actual ambush
event switches the objective and hints to combat/boarding. The entry count
updates when ships enter the trap. Existing saves derive the correct phase
from the restored fleet/timeline; no new save fields or schema version are used.

Validation on 2026-10-27:

- `BeachBoatFeedbackPlaytest`: 60 checks passed, including the actual sample
  button, timer/achievement separation, fixed pickups, report healing guards,
  save restoration, quest 3 phase hints and unchanged real save hashes. The
  later color revision also verifies missing/enough/excess using actual input,
  hidden timed answers/counters and non-revealing wrong-answer feedback.
- `BeachBoatVillageWorkPlaytest`: all four stalls and Easy/Medium/Hard passed;
  164 randomized attempts, 197 distinct timber orders, 39 hard rice orders and
  56 exhaustive scoop cases passed. Timeout, retry, difficulty locks, drum input,
  claiming goods and the return-to-camp transition passed.
- `BeachBoatConcurrentBattlePlaytest`: 22 checks passed for simultaneous entry,
  full-fleet penetration, ebb, exposed stakes, all ten retained wrecks, allied
  withdrawal and unchanged original saves.
- The rations practice render was inspected at 1280×720; the formulas and
  whole-group salt rule fit without overlapping the controls.
- The timed revision render at 1280×720 shows all three colored states, original
  recipe requirements and no calculated total or added-quantity counter.

All test saves are isolated under ignored `.utmp`; reports and renders are in
ignored `Logs`. The village regression also hashes real player saves before
and after the run.
