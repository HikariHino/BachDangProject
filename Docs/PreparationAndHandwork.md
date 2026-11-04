# Preparation consequences and handwork

The four physical village tasks now have a visible one-time quality reward:
finish within the displayed A deadline and make at most two mistakes. Claim
with E on the result screen. Timber and rope each add one quiver slot; rice
and provisions each add one medicine. Replaying can earn a missed quality
reward, but never duplicates a claimed reward. Quiver capacity is 6 normally,
8 after both work rewards and 10 if the optional archer reinforcement is chosen.
Practice archery retains its six-shot lesson; battle reload, checkpoint reset,
saved ammunition and combat HUD all respect the earned capacity.

Completed goods remain at the right-hand delivery crate. At most two existing
villagers borrow their normal navigation to fetch these goods and take them
to the village freight cart. Their original patrol, animation controller and
routines resume afterward at their current location. Transport outside the
player's visible area finishes without moving an actor through a visible portal.
The four freight types remain visible on the dock cart after arrival.

After supplies are ready, approaching the dock before battle reveals three
fallen packages. This is optional and does not alter story or rank requirements.
E picks up and places packages; X puts a held package back, or delegates the
incident to villagers when empty-handed. Recover all three within 65 seconds,
then approach one physical reward stand and press E: archer support (+2 quiver
slots) or medical support (+2 medicines). These rewards are mutually exclusive.
Late deliveries still save the goods but miss the extra reinforcement. Menus,
dialogues and the journal pause the timer. Leaving for battle delegates any
unfinished incident and releases held objects.

The new nested preparation save data stores shipment progress, reward flags,
incident time, recovered goods and the selected reinforcement. Unplaced cargo
returns to its pickup site on load. Legacy saves show already completed supplies
at the dock without inventing new quality rewards. Loading, defeat and leaving
an action release borrowed actors and held props; user save files are not
rewritten by QA.

Drum hands use a separate matched grip authored on the player's original mesh:
opposed thumbs, curled finger zones, inward-facing palms and upright resting
sticks matching the supplied photograph. Raising and striking share a continuous
forearm frame and preserve a straight wrist. A soft local fill keeps hand details
readable at night. The source body, bow rig and other work grips are unchanged.

The saw has a tapered steel blade with 22 teeth, metal rivets, a real open wooden
D handle and a cylindrical palm contact. Its visible handle defines pointer
projection, so grabbing the handle does not introduce a hidden drag offset.
Existing sawdust, log splitting, tool foley and timing rules remain active.

Validation uses native Unity headless CLI: PhysicalDrum, Preparation and
PhysicalVillage cover real input paths, grip orientation, contact, reward
branches, courier movement and cargo pickup, optional skipping, timer expiry,
legacy migration and save/load cleanup. Rendered captures are under Logs/.
