# Clio — Brief Roadmap

## Purpose

Maps the sequence of briefs for Clio, a historical simulation engine that consumes World Builder maps. Distinguishes plannable "spine" briefs from balancing work, which can't be written as briefs in advance because it responds to what actual runs produce.

Azhora is the first map Clio is developed and tested against, not the engine's subject. The engine's contract is with World Builder's output format; a different world is a different data file.

## Two phases

**Phase 1 — Emergent simulation.** One founding culture settles the map. Every later people descends from it through migration, isolation, and cultural drift. No authored timeline events, no pressure layer. Whatever history comes out is whatever the mechanics produce.

**Phase 2 — Directed simulation.** Authored events and the pressure layer are introduced to bias runs toward an intended history.

**The gate between phases is not "Phase 1 produces good history."** Phase 2 exists partly to address shortcomings found in the unpressured model, so requiring plausible histories first would be circular. Phase 1 is complete when the simulation is **reproducible, understood, and its limitations measured and written down**. An implausible but well-characterized control is still a valid control — arguably a more useful one, since it states precisely what the pressure layer needs to correct.

### Why this order

Authored events and the pressure layer come last, for two reasons.

**The unpressured run is the experimental control.** You can't tell whether a pressure is doing real work until you know what the simulation does without it. Building the pressure layer before having a baseline means having nothing to compare against — the control has to exist first.

**Descent removes most of the authored input.** With one founding culture and everything descending from it, six of the seven named peoples are outcomes of divergence rather than exogenous events. Far less needs authoring, and what remains is initial state rather than a timeline.

A useful consequence: the cross-seed variance test becomes meaningful in Phase 1, well before any pressure exists. Cultural divergence is contingent by nature, so "do the same seeds produce the same cultures?" is a real, falsifiable question at Brief 4 rather than something that has to wait for calibration.

---

## Phase 1 briefs (emergent simulation)

**0. Project Brief** — vision, the layer architecture, the generalization principle (engine reads scenarios as data), the cross-seed variance success criterion, and explicit non-goals. Establishes AGENTS.md as the standing rules file every later brief must respect.

**1. Core Simulation Loop** — tick loop, world state, and a minimal locked-invariants contract, proven on the simplest possible case: one population, one small toy map, no divergence, no events, no pressure. Written in full separately.

**2. World Builder Map Integration** — replace the toy map with a World Builder export, using Azhora as the first real input. Defines **the map data contract**, Clio's principal external interface, specified against the World Builder format generally rather than against Azhora's particulars. If the Brief 1 loop needs rework to accept a real map rather than just swapping a data source, that is a signal the abstraction boundary is in the wrong place — and this brief is where it would show.

The importer produces: axial coords, terrain id, climate id, capacity, passability, six-neighbor adjacency, river-edge attributes, region id per cell, plus a source-map hash and schema version.

⚠ **This brief has a World Builder work item attached and is not purely a Clio brief.** The saved Azhora map still carries legacy climate values (`arid`, `cold`, `oceanic`, `temperate`) that World Builder migrates on load; a standalone Clio reader would bypass that migration entirely. Clio must **not** reimplement those migration rules — duplicated normalization logic in two languages will drift apart and produce silently different maps. World Builder owns normalization and gains a canonical versioned export; Clio validates `formatVersion` and `climateSystem` and refuses unknown versions loudly rather than guessing.

**Water and islands.** Ocean and lake cells take zero capacity and are excluded from land migration, which makes islands unreachable under adjacency-only movement. This is an honest Phase 1 limitation to be **measured and recorded**, not patched. Maritime migration, when it comes, is a generic engine mechanic built on coastline, distance, and technology — never special handling for Azhora's archipelagos.

**Regions are metadata, not simulation units.** Clio simulates hexes and aggregates upward into World Builder regions. Simulating regions directly would discard exactly the detail that isolation, migration paths, terrain barriers, and river crossings depend on. Regions earn their keep in reporting and chronicle: "population entered Marosh" is worth far more to Brief 6 than "cell 11,83 settled."

**3. Population & Expansion (full model)** — activate the invariant's reserved factors: technology and infrastructure multipliers on capacity, and disease as a factor of the survival product `Λ` (INVARIANTS.md §2–3). Note **ecological capacity over terrain × climate arrives with the importer in Brief 2**, not here; Brief 3 adds the multipliers that sit on top of it. The growth formula itself is already authored — this brief switches reserved terms on, it does not replace math. Still one undifferentiated population; this is about mechanics fidelity, not cultural structure.

⚠ Disease needs care: sustained mortality of 1% a year extinguishes a population growing at 1% a year (INVARIANTS.md §1). Endemic levels sit very close to 1; epidemics are brief excursions.

**4. Cultural Divergence** — *the Phase 1 centerpiece.* One founding culture; daughter cultures emerge from it. **Culture only — the political layer is Brief 5 and must not be started here.**

Formula drafted in **INVARIANTS.md §6, which is not yet settled.** Culture is a **continuous 8-dimensional field carried per cell**, not a set of named group objects. Each cell's vector drifts slightly each year at a rate calibrated to Swadesh retention; cells in contact pull toward each other; distance and terrain cost break contact so separated regions wander apart. Peoples are then **detected from the evolving field rather than announced by the engine** — because no year exists in which Latin became French, and a simulation that emits a founding event is inventing one. Detection runs over 25-year snapshots in post-run analysis, at the same cadence as the chronicle chapters. A lineage tree layers on top for naming and ancestry, with population-weighted parentage.

Two refinements worth carrying into the formula. Drift scaled by inverse population, which is mechanistically real and gives frontier settlements fast divergence while a crowded homeland stays conservative — for free, without a pressure. And cohesion that weakens with cultural distance, so contact does not blur everything into homogeneity and stable sharp borders can form.

Isolation must be a function of geography — distance, terrain cost, river and mountain barriers — so that the map does real work rather than decorating a distance calculation. The drift-to-cohesion ratio sets the characteristic size of cultural regions and is where nearly all calibration time will go.

Clustering is unstable near thresholds: report culture count as a curve over threshold rather than a single number, and require a cluster to persist across consecutive snapshots before it earns a name, so the lineage tree does not flicker. Names are assigned retrospectively with a founding *window*, never a founding year.

**This machinery is engine-level, not scenario-specific.** Drift, cohesion, and clustering logic belong to Clio; `σ`, `μ`, the contact-weight table `wᵢⱼ`, and the clustering threshold `τ` are scenario data.

**Brief 4 begins with calibration runs.** Several things §6 needs — the σ-to-μ ratio, whether a threshold plateau appears at all, map-resolution dependence — can only be answered by running the model, so the formula is implemented and exercised before it is settled. Those results are calibration evidence, never runs of record. INVARIANTS.md §6 lists what is outstanding, split by whether it can be authored at the desk or requires runs.

The variance test has teeth here for the first time, and this brief must convert its own terms into metrics before it can be tested. Definitions to pin down during the brief, not left as prose: what "in contact" means numerically — the contact-weight function `wᵢⱼ` over travel cost between neighbouring **cells**, since runtime culture groups do not exist; what counts as a distinct culture (a cultural-distance threshold); and what band of outcomes counts as acceptable (an expected range for culture count and for the size of the largest culture's territory). Across N seeds those measures should fall in the defined band **and differ between seeds**. Identical culture counts and identical boundaries in every run means divergence is being driven by something deterministic rather than by accumulated contingency, and should be hunted down.

**5. Political Layer** — ⚠ *the dangerous brief.* Culture and political control are **two separate layers over the same map**, not one thing viewed two ways. A state can hold many cultures; a culture can span many states. Austria-Hungary over a dozen peoples; the Kurds across four states; Powhatan forging district chiefdoms into one paramountcy over an Algonquian culture older and wider than his reach.

The two layers want opposite treatments, and both are right:

| | Culture (Brief 4) | Polity (Brief 5) |
|---|---|---|
| Representation | continuous field per cell | discrete named entity owning cells |
| Boundaries | gradients, no hard edge | hard borders |
| Change | slow, no announcement | fast, dated, discrete events |
| Named by | detection after the fact | at founding |

This also resolves the cost of detecting cultures after the fact rather than announcing them: **chronicles get their dateable events from the political layer.** "In year 412 Marosh took the river forts" is what a chronicler writes; cultural drift is the slow background. Sharp on politics, vague on everything else — which is how real annals read.

**Minimum scope.** Polities own sets of cells. They form when a population cluster reaches a threshold. They expand into neighbouring cells by relative strength. They lose cells and collapse when stability fails. Stability is a function of cultural homogeneity within the polity, total size, and distance from the core — which produces natural maximum extents that vary by terrain, rather than a hardcoded size cap.

**Two feedback loops close here, and they are the point of the brief:**

- **Politics → growth and mortality.** Stability activates *both* reserved terms in INVARIANTS.md §3: `S` on the rate, so unstable territory grows more slowly, and `H_political` in the survival product `Λ`, so instability can also kill. Both are pinned at 1 until this brief; Brief 5 decides how much of each it needs.
- **Politics → culture.** Shared political membership counts toward the contact term that suppresses cultural drift. Long-lived states make their subjects more alike — how "Roman" became a culture and not merely a citizenship. Political borders begin *creating* cultural ones.

**Explicitly NOT in this brief.** Each of these is excluded because interesting history is reachable without it, and each one multiplies the state space:

- diplomacy, alliances, treaties
- named rulers, succession, dynasties
- economy, trade, taxation
- armies as units, or resolved battles
- religion
- internal factions or civil war

This is the brief where simulation projects drown — the previous project (Clashvergence) died of exactly this complexity creep. Anything on the list above that seems necessary mid-implementation should be written down as a limitation and measured, not added.

**The tripwire that matters most:** political borders must *sometimes* cut across cultural boundaries rather than coinciding with them. If every polity turns out to be exactly one culture in every run, the two layers are secretly one layer and the separation has failed. That is a defect, and it is the specific thing this brief exists to get right.

Cross-seed variance also applies to polity count, maximum extent, and lifespan — all should fall in a defined band and differ between seeds.

**6. Narrative Probe (plumbing only)** — point a chronicle generator at a Phase 1 run. Purpose is validating the engine/narrative interface, not writing quality. Answerable here: does the event log contain what an in-world chronicler needs (contact events, settlement and abandonment, notable transitions — not just population per cell). Note **"what a culture knew in year X" is a post-run derived artifact**, reconstructed from logged contact events during analysis; it is not runtime state and the engine never computes it; does the append mechanic survive dozens of successive entries without style collapse; does chronicler personality derive correctly from sim state; does the tripwire catch a deliberately planted invented event. Explicitly NOT answerable: whether the output is any good — narrative prose makes almost any event sequence read as meaningful, so a good-looking chronicle from an emergent run proves nothing. Runs before Phase 2 because log format is the one artifact here that's expensive to retrofit.

---

## Phase 2 briefs (directed simulation — not written yet)

**7. Authored Event Schema** — exogenous shocks on a timeline, declared as data: a plague arrives, a climate shift lands, a resource is exhausted. **Not "a people appears"** — that would contradict the one-founder premise, under which every people descends from the Boueni rather than arriving from outside. Note that the founding settlement in Phase 1 is *initial state*, not an authored event; this brief introduces the event system proper.

**8. Pressure Layer (first instance)** — the pressure schema, calibrated toward a single biased outcome before attempting a full multi-culture history. Last spine brief; what follows is balancing.

---

## World Builder integration

Clio stays **outside** the Electron app through Briefs 1–2. World Builder's existing simulation bridge is specialized to Clashvergence and Claudevergence — the simulation type union admits only those two, and the bridge assumes a translator plus an HTTP simulation server. Its SimulationPanel is faction- and region-oriented, so it is not a natural early Clio surface.

Pipeline:

```
.azmap  →  validate & normalize  →  Clio map adapter
        →  flat NumPy arrays + adjacency + region metadata
        →  simulation
        →  .npz state history + JSONL events
        →  World Builder replay overlay
```

**Replay first, live server later.** A replay overlay — load Clio output and color hexes by year — is simple and matches the multi-seed experimental workflow, where the interesting object is a finished run rather than a live game. A live `/api/world` + `/api/advance` adapter is worth building around Briefs 4–5, once runs are worth watching unfold.

**View modes** on the replay, added as the layers come online:

| View | Shows | From |
|---|---|---|
| Population | density and settlement frontier | Brief 1–3 |
| Cultural | culture as color; gradients, no hard edges | Brief 4 |
| Political | named polities and their borders | Brief 5 |

These are diagnostic, not decorative. Flipping between the cultural and political views is the fastest way to see whether borders follow cultures or cut across them — the tripwire in Brief 5 above — and whether a run produced structured history or mush.

Sizing is not a concern: 3,334 land hexes × 1,000 ticks is small for NumPy, and the array architecture buys roughly 30–50× over per-cell Python, which matters mainly when running many seeds.

## Deferred extensions (parked, not discarded)

Excluded from Brief 5 to keep the political layer implementable — **not rejected**. Listed in the order they earn their complexity, to be revisited once Brief 5 produces runs worth reading.

Note first what is *not* on this list: **conquest is already in Brief 5.** Polities take cells from each other by relative strength. What Brief 5 lacks is tactical resolution, not war.

| | Extension | Why it's worth adding | Cost |
|---|---|---|---|
| 1 | **Diplomacy (minimal)** | Alliance and hostility between polities, affecting whether they expand into each other. Largest behavioral change for the least new state — polities currently can only fight or ignore. | Low |
| 2 | **Rulers and succession** | Named individuals with reigns and deaths. Higher value than it looks: the Brief 6 chronicler badly wants names and dated reigns, and a chronicle without them reads thin. | Low–medium |
| 3 | **Trade and economy** | Feeds infrastructure, and trade routes become contact channels that slow cultural drift along them — a second geography of connection layered over terrain. | Medium |
| 4 | **Religion** | Effectively a *second* cultural field with different rules: spreads by contact like culture, but converts discontinuously rather than drifting. Reuses Brief 4 machinery. | Medium–high |
| 5 | **Internal factions and civil war** | Partly covered already by polity collapse and fission in Brief 5; full treatment means modelling politics *within* a polity. | High |
| 6 | **Armies and battles** | Lowest value for this project. A millennium-scale history does not need tactical resolution, and the chronicle layer would discard it. | High |

**The reason to defer is diagnostic, not aesthetic.** Add diplomacy now and, if the history comes out wrong, there is no way to tell whether the fault is diplomacy, divergence, capacity, or drift rates. Each layer added before the one beneath it is understood costs the ability to attribute a failure. Same logic as the Phase 1 / Phase 2 split.

## Balancing (not pre-written)

Once a full scenario is authored and pressures are on, work becomes iterative: run N seeds, check whether the intended arc emerges in most-not-all runs, adjust pressure weights, re-run. Logged as dated balancing sessions with DEFECTS.md entries whenever an invariant, or a hidden deterministic outcome, is suspected — not written as briefs ahead of time.

---

## Narrative Generation

**The plumbing probe is Brief 6** (above), run on emergent Phase 1 output.

**The full narrative layer is deferred** until the simulation runs consistently. Design direction, recorded so the log format can accommodate it, not to be built yet: an in-world chronicle as a single continuous manuscript appended every 20–30 years by successive chroniclers, each with a personality derived from sim state rather than rolled. Chroniclers read the manuscript so far plus their own era's events plus what their people actually know, never the full log. Divergence between manuscript and log is acceptable only where something logged explains it (no contact, hostile source, motive to flatter); unexplained divergence is a defect. Prove one convincing manuscript before interleaving several.

### Historical models

The appended-manuscript form is not invented. Each document below licenses a specific mechanic rather than supplying atmosphere, and is the reason that mechanic is in the design at all.

**Anglo-Saxon Chronicle — divergent manuscripts from a common core.** Copies were distributed to different religious houses in the late 9th century and thereafter continued independently, so the surviving versions disagree with one another about the same years. Direct precedent for generating two peoples' accounts of a single logged event. Also licenses wildly uneven entry length — some years get a clause, some get pages — and termination mid-stream: the Peterborough manuscript simply stops in 1154, and the silence carries information no sentence would.

**Rus' Primary Chronicle — retroactive reframing.** Compiled in early 12th-century Kiev out of older material (traditionally credited to Nestor, though the attribution is disputed), it does not merely append; the compiler reorganizes what came before to serve a present-day dynastic argument. Precedent for letting a chronicler revise the reading of earlier entries rather than only adding to them.

**Irish annals — terse register, common ancestry.** The Annals of Ulster, Inisfallen, Tigernach and others are thought to descend from a shared lost source (the reconstructed "Chronicle of Ireland"), and their early entries are clipped and formulaic: a death, a battle, a portent, a line each. Precedent for a chronicle voice that is not literary, and for several documents derivable from one ancestor text.

**Chinese dynastic histories — bias with a logged cause.** By convention each dynasty compiled the official history of the one it replaced, so a fallen regime's account is written by its conquerors. Enormous bias, entirely traceable origin — exactly the shape the two-stage tripwire needs.

**Cradle of the Empire — the editorial frame.** The user's own *A Big History of Virginia* (2026), analyzed in `CRADLE.md`. Retrospective and system-aware in a way no in-world contemporary chronicler can be, so it slots in as the framing layer around the annal registers rather than competing with them. Its measured chapter cadence — 20–31 years, mean 25.0 — is the empirical basis for the generational chapter length above.

### The comparison test

**Worth running once both phases exist:** generate chronicles from a Phase 1 emergent run and from a Phase 2 authored, pressured run. If a reader can't tell which history was authored, that is a real finding about the project's premise — it would suggest the pressure layer does mechanical rather than narrative work, and that what makes the history feel like history is the prose rather than the simulation under it.
