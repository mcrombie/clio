# Moving groups and encounters

The prototype treats every living animal group as a unit with a persistent ID, population, location, trust and condition. Independent bands move and act as turns close. New stories use these rules; an older story can adopt them with **Orders → Enable unit encounters** on the map.

## Choose the group, then the action

Hover over an animal counter for a short preview; click it to open its selection card, or click a banner to inspect a band. The counter shows a silhouette, animal count and health bar at regional detail. Gold double edges identify domestic groups; red angular edges identify hostility, including hostile foreign companions. Selection highlights the group and shows its ID and strength. Dense stacks have a group-count badge; repeated clicks cycle their members. Owned companions are also individually accessible in **Units**.

**A / Attack**, **B / Befriend**, or **M / Move / meet** opens a choice sheet for a selected target. It compares fighting strength, explains the current approach chance and cost, and offers only available actions. Review the target before committing. **Leave alone / Esc** closes the sheet without spending a priority; **Move without contact** is available when the destination permits peaceful passage. Opening choices stops autoplay.

An accepted attack or peaceful approach costs **one priority**. A target can be in your current cell or on adjacent, explored land. For an adjacent target, that same priority includes moving your band and companions into reach. Travel still consumes provisioning capacity, and moving abandons your camp. A peaceful encounter also pays its offering; the band must retain capacity for its own needs. Invalid, unknown, dead, distant or repeated targets spend nothing. Atlas view reveals the board for inspection but does not make undiscovered targets reachable.

Each group can face your people once per chapter. You cannot attack your own band or companions, and you cannot befriend another household's domestic group away from its owner. Inter-band peaceful diplomacy does not use the animal Befriend command.

## Species and remembered trust

Trust belongs to the group, not its current cell. A successful peaceful encounter adds one contact; moving does not erase it. Rejection spends the offering and can injure or kill people. Attacking a group reduces remembered trust and can make it hostile. Surviving wild animals may flee, so continuing a relationship can require following the same ID across the known land.

The starting chances below are for a non-hostile group with no prior positive contacts. Trust can improve them, and hostility lowers most peaceful chances. The encounter sheet reports the actual current value.

| Group | Successful contacts for a bond | Starting peaceful chance | Offering per attempt | Character |
|---|---:|---:|---:|---|
| Deer | 6 | 78% | 12 | Quick, cautious groups that often flee; weak retaliation |
| Wolves | 10 | 66% | 18 | Wary packs; some are hostile and may pursue vulnerable bands |
| Aurochs | 12 | 56% | 24 | Strong grazing herds; some protect their ground aggressively |
| Mammoths | 18 | 28% | 42 | Tough family herds with dangerous charges |
| Dragon | 40 | 0.6% | 90 | Very strong and territorial; peaceful bonds are exceptionally difficult |

A bond converts the surviving group into a named domestic lineage; it does not create extra animals. Dogs help gathering and fighting, cattle produce food, deer help gathering, mammoths help gathering and fighting, and a bonded dragon adds fighting strength. All need care, deducted as chapters close:

| Companion | Capacity per animal per chapter |
|---|---:|
| Deer | 0.05 |
| Cattle | 0.06 |
| Dogs | 0.30 |
| Mammoths | 0.65 |
| Dragon | 8.00 |

Companions follow their household during movement and retreat. A fatal encounter releases surviving companions immediately; chapter resolution also releases groups whose owner has died or disappeared. Release preserves their IDs and remembered contacts, allowing them to range through the land again. Growth is bounded by prototype species and household support limits; this is not an individual-animal breeding simulation.

## Fighting, wounds and feuds

Fighting strength depends on group size, surviving condition and species; bands also use cohesion and companion support. An attack resolves damage immediately, followed by retaliation from survivors. Damage can kill members and leave wounds on the remaining group. The displayed condition combines the living count and those wounds; a weaker condition reduces strength. Small amounts heal when a chapter closes, with camps helping people recover. Deaths are not reversed by healing.

Animal attacks can provide food when the attacking band survives and kills animals. Strong opposition can drive people to retreat, and threatened wild groups can flee. Hostile animals can initiate encounters during chapter resolution. Independent bands seek food and can act against nearby enemies. These are aggregate decisions at chapter boundaries; the brief map animations do not advance combat or ecology in real time.

Attacking another band starts a lasting feud. Attacking that band's domestic animals also creates hostility with their owner. Surviving enemy bands may attack again; hostile foreign companions are valid combat targets. Destroying a band can transfer its remaining provisions to the surviving attacker. The current revision has no treaty, surrender, conquest or territory-ownership system, and no command to end a feud.

Events record actual movement, approaches, attacks, casualties, retreat, recovery and release. Major consequences open a reading sheet in manual play. Autoplay can keep flowing or pause for major events, and every account remains available in **Chronicle → Events**.

## Known land and visible movement

Known land is the default. It shows current groups in explored cells; it is not a last-seen intelligence system. Unknown groups have no map targets or inspector details. Both bands and animals animate between observed neighboring positions, with hit targets following the actual drawn counter. A move from an unknown origin or into an unseen destination is not animated. Opening a different world or changing the discovery view clears transition history.

## Existing stories and V4 saves

V1, V2 and V3 stories load under their original **Classic** rules. Their earlier seasons or historical pace, actions and command outcomes are preserved. Without an upgrade they retain the earlier format when saved. Classic hunting and taming keep their earlier behavior; the species table above describes mobile encounters.

**Enable unit encounters** records a transition at the current point in the story. It does not spend a priority, advance the calendar, or replace the population and stocks. A subsequent save uses **CLIO-STORY-4**, retaining the original initial rule set followed by the explicit upgrade command. New stories start with MobileUnits and also use V4. The header records founding culture, historical pace and initial rules; commands retain the specific animal or band IDs selected for encounters.

Loading reconstructs the game, encounter conditions and event journal before replacing the current campaign. Loaded games return to manual control without replaying old popups. Earlier executables cannot read V4. Keep the prior save if you want to continue using an older executable or the Classic rules; no reverse-upgrade command is implemented.

## Implementation and checks

`EncounterRules.Animal`, `Band` and `Outlook` are read-only profiles used by the UI and autoplay. `AttackAnimal(id)`, `BefriendAnimal(id)`, `AttackBand(id)` and ordinary movement/end-chapter commands own the changes. Immutable encounter records supply the journal's realized consequences, including separate combat and chapter-economy outcomes. Renderer animation keeps its own presentation state and never modifies simulation time, discovery or random streams.

See [VALIDATION.md](VALIDATION.md) for completed checks. The isolated renderer review is reproducible with `artifacts/mobile-renderer-review/renderer-review.ps1`; mobile journal and desktop integration harnesses are in `artifacts/encounters-review`. Those fixtures are separate from player stories.
