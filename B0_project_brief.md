# Brief 0: Project Brief — Clio

## What Clio is

**Clio is a historical simulation engine.** It consumes maps produced by World Builder and simulates a millennium of settlement, cultural divergence, and geopolitics across whatever map it is given.

Clio is not a simulation *of Azhora*. Azhora is the first map it will be developed and tested against, and its named peoples are an eventual calibration target, but the engine's contract is with **World Builder's output format**, not with any particular world. A different map is a different data file, not a different program.

The project is also a portfolio piece and blog series, documenting the design process — briefs, standing rules, defect log — as much as the result. A narrative layer generating in-world chronicles from simulation output is a future goal, out of scope until the mechanics run consistently.

## The premise: one culture, many descendants

**One founding culture settles the map. Every people that exists at the terminal year descends from it** — through migration into new terrain, isolation from the parent population, and cultural drift accumulating across the centuries that follow.

This is the project's central mechanical claim, and it is what the simulation has to earn. A continent's worth of distinct peoples should fall out of one population, a map, and time. Nothing about where the descendant cultures end up, how many there are, or which of them dominate is specified in advance.

## Two phases

**Phase 1 — Emergent simulation.** Build the above and nothing else. Locked mechanics only: population growth, carrying capacity, expansion, cultural divergence, **and the political layer** — polity formation, expansion, conquest, collapse. No authored timeline events, no pressure layer. Whatever history emerges is whatever the mechanics produce, and it is allowed to be disappointing.

**Phase 2 — Directed simulation.** Introduce authored events and a pressure layer to bias runs toward an intended history, in the manner of Civilization IV's *Rhye's and Fall of Civilization* — authored history layered over genuine mechanics rather than either pure emergence or a script.

The reasoning for this order: the unpressured simulation is the experimental control. Building pressure first would mean having nothing to measure it against.

**The gate between phases is not "Phase 1 produces good history."** Phase 2 exists partly to address shortcomings found in the unpressured model, so requiring plausible histories before starting it would be circular. Phase 1 is complete when the simulation is **reproducible, understood, and its limitations measured and written down**. An implausible but well-characterized control run is still a valid control — arguably a more useful one, since it says precisely what the pressure layer needs to correct.

## The three layers

In decreasing order of fixedness. Only the first is live in Phase 1.

1. **Locked mechanics (invariants).** Population growth, ecological carrying capacity over terrain × climate × water context, resource-driven expansion, cultural divergence, and the political state-transition rules. The experimental control, off-limits to editing during tuning. If a run only works because an invariant changed, that's a detectable failure rather than a fix.

   **Every Phase 1 state-transition rule is locked once settled**, each on its own clock: the core demographic and frontier-expansion rules (INVARIANTS §§1–5) before any simulation code exists, cultural divergence (§6) once calibration runs have answered what only runs can answer, and the political state transitions (§7) likewise. There is no category of Phase 1 mechanic that stays permanently adjustable — Phase 1 is locked machinery whose only free variables are global coefficients.
2. **Authored events (Phase 2).** **Declared exogenous state changes injected on a timeline** — a plague, a climate shift, a resource exhausted. Discrete, declared, not tuned. Phase 1's founding settlement is *initial state*, not an authored event, and does not require this system.
3. **Pressure layer (Phase 2).** **Tunable soft biases applied to probabilities and weights** — never results set directly. Per *Rhye's*, where nearly all the difficulty lives.

   The distinction between 2 and 3 is *injection versus bias*, not timing. Both carry temporal variation by nature — an event has a date, a pressure may have a window. What separates them is that an event sets state and a pressure tilts a probability.

   Neither means Phase 1 is uncalibrated: Phase 1 tunes **global** coefficients freely. What Phase 1 has no mechanism for is authoring a value that differs by place or date.

## Success criterion

**The two phases are judged by different criteria, and conflating them is a mistake.** Phase 1 has no intended arc — that is the entire point of it being the control.

**Phase 1 succeeds when the simulation is characterized**, across many seeds:

- **Reproducible** — identical output from an identical **run identity**: seed, code version, scenario configuration, source-map hash, and dependency lock. A seed alone is not enough. NumPy's `Generator` gives no stream-compatibility guarantee across NumPy versions, so a dependency upgrade can change every result while every seed stays the same (INVARIANTS.md, Run cohorts).
- **Variant where it should be** — divergence and polity outcomes differ across seeds. Identical results in every run mean something deterministic is driving what was supposed to be contingent.
- **Bounded** — culture count, polity count and extent, terminal population, settlement completion all fall within measured envelopes that are stated in advance and checked.
- **Understood at its limits** — where the unaided model falls short is written down. Islands unreachable, resolution dependence, whatever else runs reveal.

None of that asks whether the history is any good. An implausible but well-characterized Phase 1 is a valid — arguably more useful — control.

**Phase 2 succeeds when an intended arc tends to emerge across seeds.** Standard test: run N times and check outcome frequency. An intended outcome occurring in most runs but not all means the pressure is calibrated well; occurring in every run means something is likely hardcoded and should be hunted down.

**Cross-seed distributions are a primary test, not the only one.** Reproducibility and boundedness are equally load-bearing: a simulation that varies beautifully but cannot be reproduced, or whose outputs wander outside any stated envelope, has failed regardless of how interesting its variance looks. Every one of these criteria is designed to be capable of failing — a session that can't produce a failing run isn't testing anything.

## Engine/scenario boundary

The engine reads scenarios as data. Map, coefficients, and configuration live in files the engine consumes; no branching on a named people or a named place appears anywhere in engine code.

Note the line runs between formula and parameter, not between "invariant" and "engine": the invariant **formulas** are engine code, while their **coefficients** are scenario data. An invariant is not a data file.

The line runs between **machinery and parameters**. Divergence drift, cohesion, and clustering logic are engine mechanics; the drift rate `σ`, the cohesion rate `μ`, the clustering threshold `τ`, and the **global travel-cost → weight mapping** are scenario data. The same holds for growth and capacity: the functions are engine, their coefficients are scenario.

**Contact weights are derived, not authored.** Scenario data supplies the global mapping and its coefficients; the realized per-edge field `wᵢⱼ` is computed from the map, and from Brief 5 modified endogenously by political membership. An authored per-edge table would be regional pressure in disguise and is forbidden in Phase 1 — the provenance test in AGENTS.md applies here as much as anywhere.

A scenario editor or settings UI is explicitly not part of this project — only the file boundary needs to stay clean enough that one is possible later.

## Azhora: the first map

Azhora is the prototype scenario, developed and tested first, but it is one input among possible others.

Under the descent premise the named peoples below are **branches descending from the one founding population, not arrivals from off-continent**, with dates marking when a branch occupied a region rather than when it landed.

- Boueni in the far-north Cold Stones from year 1, spreading over ~300 years with their language diverging into subcultures
- Meroshi at Marosh, 306
- Pyrosi at the Pyros River regions ~500, pressing into Marosh territory
- Iben at Ibenal/Iben Wood and Mittoli at Mithala, both 614
- Grassic in the southeast, 644
- Crefs from the far-north Cold Stones, expanding southward to reach the deep south by year 1000

In Phase 1 these are not inputs at all. They become Phase 2 calibration targets — a description of the history the pressure layer will try to bias toward, once there is a working simulation to bias.

**Founding culture: the Boueni**, seeded in the far-north Cold Stones at year 1. "Boueni" names both the people and their language, and every later people and language on the continent descends from it. The other six are therefore Boueni descendant branches, and their languages are daughter languages of Boueni.

This has a consequence for Brief 4: the cultural state — an 8-dimensional vector carried **per cell**, not per group — is simultaneously ethnic and linguistic, since the two diverge together from a single ancestor. Whether they should ever be separable — a group adopting a neighbor's language while keeping a distinct identity, or the reverse — is a real question, but not one for the first divergence implementation. Start with one merged culture/language state and split it only if runs demand it.

## Tech context

Python 3.13, with uv, pytest, and ruff. World state is held as NumPy arrays over cells rather than objects per cell — an architectural requirement rather than an optimization, since it decides whether a multi-seed experiment runs in seconds or hours. Randomness follows strict reproducibility rules: one seeded `Generator` per run, spawned into independent per-subsystem streams. All pinned in AGENTS.md.

Map data is produced by World Builder (React/TypeScript/Electron) and consumed by Clio. Clio does not generate or edit map data. The map data contract is defined in Brief 2 and is the engine's real external interface.

Population growth is discrete exponential, specified in INVARIANTS.md. Land, food, technology, and infrastructure compose into carrying capacity; political stability acts in **both** positions — suppressing the growth rate via `S` and killing via `H_political` in the survival product — and disease applies as a survival factor after growth, since a rate multiplier cannot kill anyone and would perversely slow the decline of an overcrowded population. All formula positions are fixed now and switch on across Briefs 1, 3, and 5 — political stability last, at Brief 5, since it needs the political layer. The cultural divergence formula is drafted in INVARIANTS.md §6 but is **not yet settled** — parts of it can only be fixed by calibration runs.

## Non-goals (explicit; revisit only by amending this brief)

- No user-facing scenario editor or settings UI.
- No narrative/chronicle generation beyond the Brief 6 plumbing probe.
- No authored events or pressure layer until Phase 1 is reproducible, understood, and its limits measured.
- No attempt at a full multi-culture calibrated history before the Phase 1 spine proves the loop, the map contract, the demographic model, divergence, and the political layer, in sequence.

## Relationship to other documents

- **ROADMAP.md** sequences the briefs by phase and marks where balancing begins.
- **INVARIANTS.md** is the locked-mechanics specification: growth and survival (§1), carrying capacity (§2), shock and order terms (§3), extinction floor (§4), frontier expansion (§5), cultural divergence (§6, not yet settled), political state transitions (§7, reserved). It also defines run cohorts and run identity. **Authored by the project owner; implementers are forbidden to edit it** — a needed change is escalated, never made.
- **AGENTS.md** is the standing rules file.
- **DEFECTS.md** logs suspected invariant violations, unexpected determinism, and Phase 2 mechanics creeping into Phase 1.
- **B1_core_loop.md** is the first implementation brief.
- **CRADLE.md** is a narrative style definition derived from the author's own history book, for eventual use by the narrative layer.
