# Brief 1: Core Simulation Loop (Single Population, Locked Mechanics)

Project: **Clio** — see `B0_project_brief.md`, `ROADMAP.md`, `AGENTS.md`.

Teaser scope, first brief of Phase 1. It proves the loop and the invariant lock on the simplest possible case, before cultural divergence, contact, or anything from Phase 2 exists. Runs on a small reference toy map, not on Azhora.

**INVARIANTS.md §§1–5 are locked** — growth and survival, carrying capacity, shock terms, extinction floor, and the complete frontier-expansion rule. Any edit to them is an invariant change, must be logged in DEFECTS.md, and invalidates earlier runs as evidence about the mechanics. The **reference cohort** — `clio.toymap/1` and the scenario parameters below — is frozen on the same terms.

## Objective

Build a tick-based simulation loop in which one population, seeded at one location on a small toy map, grows and expands across turns under two locked mechanics: a population growth formula and a terrain carrying-capacity function. Prove that expansion emerges from population pressure against carrying capacity, not from any scripted rule about where the population "should" go.

## Why this scope

Clio's central claim is that a continent's worth of distinct peoples can descend from one founding culture given a map and a thousand years — divergence doing the work that authored arrivals would otherwise do. That claim isn't testable yet. Divergence needs populations that have already spread far enough apart to lose contact, which needs expansion, which needs this loop. Brief 1 sits two steps upstream of the interesting question.

What it verifies is that the two locked-mechanics primitives compose into believable expansion at all, and that the multi-seed harness exists from the start rather than being retrofitted once results start mattering. Everything after this brief builds on a loop already known to behave.

This is also the brief written up as the teaser post. It should stand alone as "a population grows and spreads across a map, believably, under rules that don't know where it's supposed to end up."

## Scope

**In scope:**

- Tick loop over discrete time steps — one year per tick, matching the eventual 1000-year timeline
- A small synthetic toy map (not World Builder output): grid-based, a handful of terrain types with distinct carrying capacities, large enough to give expansion somewhere to go and some terrain worth avoiding
- One population, one seed cell, one starting population value — this is initial state, not an authored event
- Project bootstrap: packaging, lockfile, package layout, test layout, lint config
- Locked invariants implemented as authored in INVARIANTS.md, with the Brief 1 reference-cohort coefficients
- Expansion by probabilistic weighted selection (see below)
- Two output logs per the formats pinned in AGENTS.md
- A multi-seed run harness with the summary statistics defined under Acceptance Criteria
- Unit tests on the locked invariants directly, plus the order-independence and no-stalling assertions below

**Out of scope (deferred to later briefs):**

- Real map / World Builder integration (Brief 2)
- The reserved capacity and rate factors — technology, infrastructure, disease (Brief 3). The growth formula itself is already authored and locked; Brief 3 switches terms on rather than replacing math
- Cultural state, drift, or divergence of any kind (Brief 4). The population here has no culture attribute at all; it is a number moving across cells
- Cultural cohesion between neighbouring cells, and settled-to-settled migration — both begin at **Brief 4**, not Brief 5
- Polities, borders, conquest, and political stability (Brief 5)
- Chronicle or narrative output (Brief 6)
- Authored events and any pressure layer or biasing (Phase 2)
- Any visualization/UI beyond the log files
- A scenario editor or settings UI

## World representation

A small **hexagonal** grid using axial `q, r` coordinates with six-neighbor adjacency — roughly 200–400 cells. Each cell has a terrain type and a climate from short fixed lists, and a carrying capacity derived from both via the invariant function.

**Hex, not square, deliberately.** World Builder produces axial hex maps with six neighbors, and Azhora has 18,700 hexes of which 3,334 are land. Building Brief 1 on a square grid would mean Brief 2 changes the adjacency model rather than swapping a data source — which is precisely the failure Brief 2 is designed to detect. Matching the real topology now costs nothing and removes that trap.

Likewise the toy map carries both terrain **and** climate, because capacity is a function of both (INVARIANTS.md §2). A toy map with terrain alone would bake in the exact modeling error Brief 2 would then have to unwind.

Per AGENTS.md, world state is **NumPy arrays over cells**, not objects per cell. This is architectural and decided here.

| Array | Shape | Type | Note |
|---|---|---|---|
| `population` | (cells,) | float64 | |
| `settled` | (cells,) | bool | |
| `terrain`, `climate` | (cells,) | int8 | |
| `K_eco` | (cells,) | float64 | `K_base[terrain, climate] × W(∅)`, and `W(∅) = 1` here |
| `passable` | (cells,) | bool | |
| `neighbor` | (cells, 6) | int32 | target cell per direction; **−1** where the map has no neighbour |
| `M_base` | (cells, 6) | float64 | movement accessibility from the map; **all ones** on land–land edges here, 0 elsewhere |

**Edge convention, decided:** `M ∈ [0, 1]`, and `M_base = 0` means the edge does not exist for migration. A fixed `(cells, 6)` array beats ragged adjacency for vectorization, and it unifies three cases that would otherwise need separate handling — off-map edges, water crossings, and (from Brief 2) impassable terrain all carry `M_base = 0`. Direction order is fixed and part of the fixture: `(+1,0), (+1,−1), (0,−1), (−1,0), (−1,+1), (0,+1)`.

**Two layers, and which rule reads which is part of the invariant** (INVARIANTS.md §5):

| Field | Definition | Read by |
|---|---|---|
| `M_base` | from the map, never changes | **eligibility** |
| `M_eff` | `clip(M_base × Π modifiers, 0, 1)` | **weighting** |

Eligibility keys on `M_base` rather than `M_eff`, so the candidate set is fixed by geography and cannot be widened *or narrowed* by any later layer. Modifiers must be **strictly positive on live edges**, and `M_eff` is clipped to `[0, 1]` — modifiers may exceed 1, since political integration genuinely improves passage, but accessibility relative to free movement cannot.

In Brief 1 there are no modifiers, so `M_eff = M_base`. The loop still reads `M_eff` in the weighting path so later layers fill a value rather than introduce a concept.

**Validation at load**, all asserted:

- `M_base` finite and within `[0, 1]` everywhere
- `neighbor[i, d] == -1` ⟹ `M_base[i, d] == 0` — off-map slots carry no accessibility
- `M_base[i, d] > 0` ⟹ `neighbor[i, d]` is a valid cell index
- every modifier (none in Brief 1) is strictly positive wherever `M_base > 0`
- `M_base` symmetric on land–land edges in Brief 1 (`M_base[i,d] == M_base[j,d']` for the reverse direction). Not required in general — Brief 2 may make river crossings directional — but asserted here because the toy map has no reason to be asymmetric, so a violation means a construction bug

The toy map is hand-authored for this brief only. It is not meant to resemble any real map and should not be built with reuse by Brief 2 in mind — building it "properly" now is scope creep against Brief 2's job.

**One constraint on its design:** it must contain regions where several adjacent cells have similar (not necessarily identical) carrying capacity, so that weighted selection has genuinely competitive choices. A map with one dominant direction everywhere would suppress the variance this brief is meant to observe.

## Time and entities

One tick = one year. World state per tick: population per cell and a settled flag per cell. The population has no cultural, linguistic, or group identity — it is a number moving across cells.

## Locked invariants

**The formulas are specified in `INVARIANTS.md` and are not this brief's to design.** Implement them exactly as written. They are authored, locked, and off-limits to adjustment — including adjustment that would make a run "look right."

What Brief 1 implements, at Phase 1 neutral settings:

`grow(population, capacity, r_max, S=1.0, survival=1.0) -> (next_population, raw, clamped)` — `[P × (1 + r_max·S·(1 − P/K))] × survival`, returning the **clamped result, the unclamped raw value, and a boolean clamp mask**. The `clamp_negative` event needs the raw value; returning it here stops the loop from recomputing invariant math to obtain it, which would duplicate the formula outside the invariants module and let the two drift apart. **The reserved slots are in the signature from the start**, defaulted to their neutral values, so Briefs 3 and 5 fill arguments rather than change an interface that later code already depends on. With both at 1 this reduces to discrete logistic growth. Note the **growth-rate component `g` is negative above K** — the population itself stays positive and merely declines. The tick semantics below depend on that.

`carrying_capacity(terrain, climate, water_context, params, A=1.0, I=1.0, Y=1.0) -> capacity` — `K_base[terrain, climate] × W(water_context) × A × I × Y`, per INVARIANTS.md §2. **All four reserved factors are in the signature from the start**, defaulted to neutral: `W` fills at Brief 2, `A` and `I` at Brief 3, `Y` whenever a transient supply shock is first modelled. The toy map carries no rivers, so `water_context` is `∅` throughout and `W(∅) = 1`, but the parameter is passed rather than assumed.

`emigration(population, capacity, θ, φ) -> emigrants` — fixed fraction φ leaves when population exceeds θ × K. The rule is invariant; θ and φ are scenario parameters.

Extinction floor applies after the growth step: a cell below `P_min` is zeroed and marked unsettled.

Assert `r_max < 0.5` at startup. See the chaos bound in INVARIANTS.md §1 — the discrete logistic destabilizes at r = 2 and this margin exists so nobody rediscovers that during balancing.

These are the **Brief 1 reference-cohort coefficients**, not throwaway values. Changing any of them opens a new run cohort rather than editing a scratch number (INVARIANTS.md, Run cohorts). If expansion looks wrong, the loop is the suspect, never the formula.

## Tick semantics

A tick is a chain of **explicit stage buffers**. Each stage reads the previous stage's buffer in full and writes a new one; no cell ever observes another cell's update within the same stage.

```
state_t
  → [A] growth + negative clamp        (cells with K > 0 only)
  → [B] extinction floor
  → [C] migration intentions, frozen
  → [D] simultaneous resolution
  → state_t+1
```

Reads come from the **immediately preceding stage**, not from `state_t` — migration works on post-growth, post-extinction populations, so a blanket "everything reads tick *t*" would be false.

| Stage | Reads | Writes | Notes |
|---|---|---|---|
| A — growth | `state_t` | buffer A | Cells with `K = 0` bypass growth entirely; `D = 1 − P/K` is undefined there |
| B — extinction | buffer A | buffer B | Cells below `P_min` zeroed and unsettled; emits `cell_abandoned` |
| C — intentions | buffer B populations, **`state_t` settlement mask** | intention list, aggregated by destination | Selection, **aggregation, and the founding-viability check with refunds** all happen here. Stage D applies the surviving intentions and does no filtering |
| D — resolution | buffer B + intentions | `state_t+1` | All flows applied simultaneously |

**Recolonization rule: a cell abandoned during stage B cannot be resettled in the same tick.** Candidates are drawn against the **tick-*t*** settlement mask, so anything settled at the start of the tick is excluded regardless of what stage B did to it. It becomes eligible from the next tick onward. This is one rule rather than a special case, and it avoids a log in which a cell is abandoned and resettled at the same tick.

**Candidate eligibility**, stated once and used identically by the mechanic and by the no-stalling test:

```
eligible(i → j)  ≡  ¬settled_at_t(j) ∧ passable(j) ∧ K_eco(j) > 0 ∧ M_base(i → j) > 0
```

The `M_base > 0` clause is what makes off-map directions, water crossings, and later impassable edges a single case rather than three — and keying it to `M_base` rather than `M_eff` is what keeps the candidate set immutable.

Rules, all of which must be explicit in code:

- **Migration conserves population exactly.** Emigrants are subtracted from the source and added to the destination. No transit mortality in Brief 1.
- **Several sources may target one destination.** All arrivals are summed. This is expected behavior, not a collision to be resolved.
- **Newly settled cells cannot expand in the same tick.** This falls out of double-buffering — expansion reads tick-*t* settlement state, in which the new cell does not yet exist.
- **Migration may push a destination above its carrying capacity.** This is permitted and transient: `g` is negative above K, so the next growth step pulls the population back down. What is asserted after the growth step is the set of **bounded growth properties** in INVARIANTS.md §1 — monotone correction, no crossing within `K < P < K/b`, non-negativity — never a flat "population ≤ K", which is unsatisfiable from above.

## Numerical contract

Reproducibility is load-bearing for this project, and floating-point addition is not associative — summing the same arrivals in a different order changes low bits even when the algorithm is perfectly synchronous. These rules are what make "identical output for the same seed" achievable rather than aspirational.

**Precision**

- **All computation in float64.** float32 is used *only* when writing the stored grid, never internally.
- Culture vectors (Brief 4 onward) likewise float64 internally.

**Canonical ordering — required wherever values are combined**

| Operation | Canonical order |
|---|---|
| Migration intentions | sorted by source cell index |
| Destination reductions (summing arrivals) | arrivals sorted by source cell index before summation |
| Event log records | sorted by `(tick, event, cell)` — `event` is the JSON field name |
| An event's `arrivals` list | sorted by source cell index |

**Tolerances**

| Check | Definition | Tolerance |
|---|---|---|
| **Migration conservation** | `\|Σ P(stage D) − Σ P(stage B)\| / Σ P(stage B)`, both sums taken over the **float64** buffers before serialization. If `Σ P(stage B) = 0` the run is extinct: skip the check and record the extinction tick | ≤ 1e-12 per tick |
| **Convergence to K** | `\|P − K\| / K`, migration disabled, on the convergence fixture below | ≤ 1e-6 within horizon |
| **Internal reproducibility** | float64 state arrays, same run identity | **exact** equality |
| **Serialized reproducibility** | float32 `.npz` contents and parsed JSONL | **exact** equality |

The last two are separate claims. float32 serialization is lossy, so identical float32 output does **not** prove identical float64 computation — two runs could agree on disk while diverging internally in bits that later amplify. Assert both.

Convergence is tested against a **finite horizon with a tolerance**, never as a limit — `P → K` is not a computable assertion.

**Input validation at startup**

- `P₀` finite and `≥ P_min`; seed cell passable with `capacity > 0`
- all capacities finite and `≥ 0`
- horizon `N` a positive integer
- parameter domains per the table under Acceptance Criteria

## Expansion mechanic

When a settled cell's post-growth population exceeds θ × K, a fraction φ of that population emigrates to one adjacent cell. **The trigger rule is a locked invariant** (INVARIANTS.md §5); θ and φ are scenario parameters, global rather than per-region.

**The complete rule is INVARIANTS.md §5.** Summarized here; the invariant is authoritative.

**The candidate set is: unsettled at *t*, passable, positive-capacity neighbours only.** Settled cells are not destinations in Brief 1.

Deliberate. Allowing migration into settled cells lets emigrants shuffle between occupied cells while open frontier sits untouched, and it accomplishes nothing observable yet — with no cultural layer, moving people between two settled cells changes only population numbers.

**Settled-to-settled flow is a separate mechanic introduced at Brief 4**, with its own rule and its own lock. It does not amend this one: Brief 1's frontier-expansion rule stays exactly as locked, and Brief 4 adds a parallel rule beside it. A later brief appearing to widen a locked rule's eligibility would be an invariant change in disguise.

**If an eligible source has no candidate** — every neighbour settled, impassable, or zero-capacity — **no emigration occurs and nothing is subtracted.** The population stays put and continues to grow against its own capacity.

**Founding viability.** Because the extinction floor runs at stage B and migration resolves at stage D, arrivals could otherwise found a cell already below `P_min`, which would be unsettled again on the next tick — producing settle/abandon churn in the log and a frontier that flickers.

Rule: **at stage C, after intentions are aggregated by destination, any unsettled destination whose total incoming population is below `P_min` has its intentions dropped.** Those emigrants never leave; their sources keep them. Aggregation happens before the check, so several small parcels can jointly found a cell that none could found alone.

This applies only to *founding*. **Settled destinations do not occur in Brief 1** — candidates are unsettled by definition — so behaviour for arrivals into an already-settled cell is not specified here and is reserved for Brief 4 along with the settled-to-settled mechanic.

The destination is chosen from the candidate set by **probabilistic weighted selection** with `weight(i→j) = K_eco(j) × M_eff(i→j)` per INVARIANTS.md §5. `M_eff` is all ones on live edges in Brief 1, so weighting is effectively capacity-driven here — but the term is present in the code path, not implied, so Brief 2's river and terrain costs fill it without touching the rule.

**The candidate list is sorted by cell index before sampling.** Canonical ordering of *sources* is not sufficient — a weighted draw maps a uniform variate onto a cumulative-weight array, so if the same candidates are presented in a different order the identical draw selects a different destination. Sort the neighbours, then sample.

This replaces an earlier, contradictory description in which candidates were strictly ranked with randomness only breaking exact ties. Weighted selection is chosen deliberately: strict ranking makes runs nearly deterministic on any map without exact ties, which would leave later cultural divergence depending on engineered map symmetry rather than on accumulated contingency. This draw is the only source of path dependence in Brief 1, and everything downstream inherits from it.

Nothing else influences direction. There is no pressure layer and no scenario knowledge in this choice.

## Outputs

Two logs per AGENTS.md, because the dense grid and the sparse event stream have different shapes and different consumers.

**Dense per-tick grid** → `.npz`, minimum contents:

| Array | Shape | Type | Meaning |
|---|---|---|---|
| `population` | (T+1, cells) | float32 | population per cell per snapshot |
| `settled` | (T+1, cells) | bool | settlement state per cell per snapshot |
| `terrain` | (cells,) | int8 | terrain type index, static |
| `climate` | (cells,) | int8 | climate index, static |
| `K_eco` | (cells,) | float32 | ecological capacity, static. **Named `K_eco`, not `capacity`** — from Brief 3 the effective capacity is `K_eco × A(tech) × I(infra)` and becomes time-dependent, so the static array must not claim the general name |
| `passable` | (cells,) | bool | land/water mask, static — core world state, not derivable from capacity alone |
| `coords` | (cells, 2) | int16 | axial `q, r` per cell index |

Snapshot arrays are **(T+1, cells)**, not (T, cells): index 0 is the initial state before any update.

Plus a **`run_identity` entry: one UTF-8 JSON string**, saved with `allow_pickle=False`, matching the run identity locked in INVARIANTS.md (Run cohorts):

```json
{
  "identity_schema": "clio.run_identity/1",
  "seed": 0,
  "commit": "<git rev-parse HEAD>",
  "working_tree": "clean",
  "dirty_fingerprint": null,
  "scenario": { "r_max": 0.03, "theta": 0.6, "phi": 0.30,
                "P_min": 10.0, "P0": 60.0, "seed_cell": 189, "horizon_T": 1000 },
  "fixture": { "schema": "clio.toymap/1", "sha256": "<hash of the .npz fixture>" },
  "dependency_lock_sha256": "<hash of uv.lock>",
  "terrain_map": { "plains": 0, "hills": 1, "mountains": 2, "marsh": 3, "water": 4 },
  "climate_map": { "temperate": 0, "arid": 1, "cold": 2 }
}
```

JSON as a string, not a pickled dict: `.npz` with `allow_pickle=True` is both a security hazard and a portability one. A run whose identity cannot be reconstructed from its own file is not evidence.

**Runs of record require a clean working tree.** `commit` is meaningless if uncommitted changes were in play, so the harness checks `git status --porcelain` and writes `"working_tree": "clean"` with `"dirty_fingerprint": null`.

A dirty run is still permitted — exploration is the normal case — and records `"working_tree": "dirty"` with **`dirty_fingerprint`**: SHA-256 over `git diff HEAD` **concatenated with the contents of every untracked non-ignored file**, in `git status --porcelain` order. Untracked content must be in the fingerprint, or a newly added source file changes behaviour while leaving the fingerprint identical. **A dirty run may never be cited as a run of record.**

The repository must carry at least one commit before any run: `git rev-parse HEAD` does not resolve in an empty repository. A committed `.gitignore` is part of this, since without it `.venv/` and caches make a clean tree unreachable.

**`coords` is the authoritative cell-index mapping and must be treated as part of the contract.** Cell index → (q, r) has to be stable and explicit; if a later importer changes ordering, arrays from different runs silently stop being comparable and every cross-seed result is quietly wrong. From Brief 2 onward the metadata also records a hash of the source map and its schema version, so a run can prove which map produced it.

**Sparse event log** → JSONL, one object per line, minimum schema:

```json
{"tick": 12, "event": "cell_settled",   "cell": 47, "arrivals": [{"source": 46, "population": 71.2}, {"source": 52, "population": 47.2}], "population": 118.4}
{"tick": 42, "event": "cell_abandoned", "cell": 17, "population_before": 22.1}
{"tick": 88, "event": "clamp_negative", "cell": 31, "population_before": 4150.0, "raw_population": -73.5, "capacity": 40.0}
```

Brief 1 emits `run_start`, `cell_settled`, `cell_abandoned`, `clamp_negative`, and `run_end`.

| Event | Fires | Fields |
|---|---|---|
| `run_start` | once, tick 0, before any update | `run_identity` echoed |
| `cell_settled` | on unsettled → settled | `cell`, `arrivals[]`, `population` |
| `cell_abandoned` | **only on a settled → unsettled transition** at stage B — never for a cell already unsettled, and never for one that merely lost population | `cell`, `population_before` |
| `clamp_negative` | diagnostic; raw growth result was negative | `cell`, `population_before`, `raw_population`, `capacity` |
| `run_end` | once, after tick T | terminal summary statistics |

**Sort key: `(tick, event, cell)`**, using the JSON field names exactly as written, with `event` compared as a string. Events with no natural cell — `run_start`, `run_end` — carry **`cell = -1`** so that every record has a defined value in every key position and **no two records tie**, which is what makes the ordering canonical. Note this does *not* place them first within a tick: `event` is compared before `cell`, so ordering within a tick is alphabetical by event name.

**`cell_settled` carries an `arrivals` list, not a single `from_cell`.** Several sources may found the same cell in one tick, and a lone `from_cell` cannot represent that — it would silently attribute a settlement to whichever source happened to be processed first. The same correction applies to culture in Brief 4: a founded cell's culture is the population-weighted mean of all arrivals (INVARIANTS.md §6), not one source's vector.

`cell_abandoned` fires when the extinction floor unsettles a cell. Without it the log records territory being gained but never lost, and abandonment is the only way a frontier can retreat.

`clamp_negative` is a **diagnostic**, not a normal event. It fires when the growth multiplier goes negative from extreme migration inflow (INVARIANTS.md §1) and the result is clamped at zero. It should essentially never appear; if it does, migration inflow is pathological and wants investigating rather than silencing.

The schema must tolerate added fields without restructuring, because Brief 4 will attach cultural state and Brief 6 will need to reconstruct what a chronicler could have known.

Neither log is rendered or plotted in this brief.

## Acceptance criteria

Every criterion below is a computed check, not a judgment call.

**Invariants**

- Unit tests pass for `grow` and `carrying_capacity` against hand-computed expected values.
- Growth-step assertions, **each restricted to its valid domain** per INVARIANTS.md §1. In Brief 1 `S = 1` and `Λ = 1`, so the density-free coefficient is `b = r_max`:
  - `K > 0` precondition — cells with `K = 0` bypass growth and are never passed to `grow()`
  - if `0 < P < K` then `P < P_next ≤ K` (approach from below, no overshoot)
  - if `P > K` then `P_next < P` (overshoot declines)
  - if `K < P < K/b` then `P_next ≥ K` (does not cross below)
  - `P_next ≥ 0` after clamp, always
  - with migration disabled and `0 < P₀ < K/b`, `P → K` within the convergence fixture's tolerance and horizon
- **Do not assert that population is at or below K after a growth step** — decline from above is asymptotic within the safe band. And **do not assert no-crossing or convergence without the domain bound**: above `K/b` a single step can undershoot, and above `(1 + 1/b)·K` the raw update is negative and the cell clamps to extinction.

**Parameter domains**, asserted at startup:

| | Bound |
|---|---|
| `r_max` | `0 < r_max < 0.5` (chaos margin, INVARIANTS.md §1) |
| `θ` | `0 < θ < 1` |
| `φ` | `0 < φ < 1` |
| `P_min` | `P_min > 0` |
| seed cell | `capacity > 0` and `passable` |
| horizon | finite tick count `N`, declared in config |

**Determinism and order-independence**

- Two runs with the same seed produce identical output: compare **loaded arrays and parsed JSONL records**, not archive bytes. `.npz` is a ZIP and embeds timestamps, so byte comparison is brittle.
- **All random draws are consumed in canonical cell-index order**, never in processing order. Destination draws for every eligible cell are sampled as one vectorized operation before any resolution happens.
- Order-independence test: with the destination draws fixed, processing cells in forward versus reversed order produces identical output. This isolates the property actually being tested — that the update is synchronous and no cell sees another's mid-tick state — from RNG consumption order, which a naive shuffle test would conflate.

  **The test must span stages C and D, not D alone.** Stage C aggregates parcels by destination and applies the founding-viability filter with refunds, and both are order-sensitive: float64 summation is non-associative, and a viability decision computed mid-aggregation could depend on which parcels had arrived so far. Reversing source order must leave the aggregated totals, the set of refused destinations, and the refunded amounts bit-identical.

**Behavioral checks**

- **Weighted sampler correctness — one unit test, fully specified.**

  ```python
  weights = [120.0, 80.0, 50.0, 45.0, 20.0, 8.0]      # sums to 323.0
  rng     = np.random.default_rng(20260721)
  draws   = 100_000
  ```

  Compute Pearson's chi-square of observed counts against `draws × wᵢ / Σw` and assert **χ² < 20.515** (5 degrees of freedom, α = 0.001). One test, one seed, one threshold, deterministic — it either passes or reveals a real defect. A correct sampler can pick poor terrain repeatedly by chance, which is why this tests the sampler's distribution rather than a run's sampled mean.
- **No stalling.** Assertion: no tick passes in which a settled cell is above the emigration threshold, has at least one **eligible** neighbour, **its parcel is not refused for founding viability**, and yet contributes no emigrants.

  Both qualifiers are required. Testing "any unsettled neighbour" fires spuriously on impassable and zero-capacity cells; omitting the viability clause fires on every legitimately refused sub-floor parcel, which is the *correct* behaviour of the founding rule. An assertion that fires on correct behaviour is worse than no assertion.

  Report separately, as diagnostics rather than failures. **The refusal metric is the count of rejected *source intentions*** — not ticks, which undercounts when several intentions are rejected in the same tick, and not destinations, which undercounts when several sources targeted one refused cell. Optionally also report rejected destination groups, but the primary figure is intentions.
- **Three separate measures, not one.** Requiring that no cell be first-settled after `t` would make the criterion unreachable: with a founding-viability floor and marginal terrain, a handful of poor cells keep being founded by rare high-population neighbours right up to the horizon. The frontier asymptotes but never closes — verified across seeds at T = 1000 and T = 2000.

  | Measure | Definition |
  |---|---|
  | **Plateau tick** | smallest `t` with `t ≤ T − 50` such that `max over s ∈ [t, T) of \|P_total(s+1) − P_total(s)\| / P_total(s) < 0.001`. A *population* criterion only; reported tick is `t`, the window start. `None` if never reached. **The `t ≤ T − 50` bound is required** — without a minimum suffix the final one-tick or empty window satisfies a max-over-nothing vacuously, and every run would report a plateau at `T`. |
  | **Settlement fraction (reachable)** | settled ÷ cells reachable from the seed over positive-`M` edges. The primary figure |
  | **Settlement fraction (passable)** | settled ÷ all passable cells. Differs from the above whenever the map has unreachable land, and the gap is itself the islands limitation made numeric |
  | **Last frontier-advance tick** | tick of the most recent first-settlement. Not a closure measure — on this fixture it typically equals `T`, because marginal cells keep trickling in |
  | **Extinction tick** | first tick at which `P_total = 0`, else `None` |

  Clause 1 of the old definition also duplicated clause 3; both concerned first-ever settlement. If a future version wants to forbid recolonization churn it must say **settlement transition** (settle *or* abandon), which is a different quantity.

  **If `P_total = 0`** the relative-change formula is undefined: the run is extinct. Report an extinction tick and no plateau.

**Direct mechanism tests** (unit level, not run assertions):

- **Emigration** — a cell just above `θ·K` emits exactly `φ·P`; a cell just below emits nothing.
- **Extinction timing** — a cell driven below `P_min` is zeroed and unsettled in stage B of that same tick, and emits `cell_abandoned`.
- **Collision summation** — three sources targeting one destination produce a destination population equal to the exact sum of arrivals, and one `cell_settled` event carrying all three in `arrivals`.
- **No candidate, no subtraction** — a source above threshold whose neighbours are all settled/impassable/zero-`M` retains its full population; nothing is silently lost.
- **Recolonization bar** — a cell abandoned in stage B is not a valid destination in the same tick, and becomes one on the next.

**Capacity-composition tests** — `W` is locked with all coefficients neutral, so its composition rule is untested by the reference map alone:

- **Empty context** — `W(∅) = 1`, so `K_eco = K_base[terrain, climate]` exactly. This is the only case the reference map exercises.
- **Max composition** — with synthetic `W_base = {river: 1.3, coast: 1.2}` and no override, a hex carrying both yields `1.3`, **not** `1.56`. Guards against a product creeping in.
- **Override precedence** — with an override entry for `{river, coast} = 1.4`, that hex yields `1.4`. The override is consulted before the max rule, not blended with it.

**Movement-accessibility tests** — the reference map is all ones on live edges, so a implementation that silently dropped the `× M` term would pass every other test in this brief. These exist to catch exactly that:

- **Fractional M shifts the distribution** — synthetic 2-candidate case with equal `K_eco` and `M = [1.0, 0.25]`; over 100,000 draws with `np.random.default_rng(20260722)`, selection frequencies must match 0.8 / 0.2 with **χ² < 10.828** — **1** degree of freedom at α = 0.001, not the 20.515 of the six-candidate test, which has 5. Equal capacities mean *only* `M` can produce the split.
- **Zero M excludes** — a candidate with `M = 0` is never selected and never appears in the candidate set, even when its `K_eco` is the highest available.
- **All-zero row** — a source whose six `M` entries are all zero has no candidates, so no emigration and no subtraction (the no-candidate case).

**Founding-viability tests** — the mechanic with the most edge cases, and the one most likely to be implemented as a plain per-source check:

- **Rejected parcel** — a single sub-floor parcel targeting an unsettled cell founds nothing.
- **Source retention** — after that rejection the source's population is *exactly* what it was before stage C. Refund, not loss.
- **Joint founding** — three parcels of `0.4 × P_min` each, individually sub-floor, targeting the same unsettled cell, **do** found it, with total `1.2 × P_min`. This is the case a per-source check gets wrong.
- **Boundary equality** — a total of exactly `P_min` **founds** the cell. The rule is "below `P_min` is refused," so the comparison is `total < P_min`, not `≤`.

**Cross-seed variance**

Run **R = 20** seeds over a horizon of **T = 1000** ticks. The comparison object is the **first-settlement-tick vector** — for each cell, the tick at which it was first settled, or null.

- Expansion occurs in **20 of 20** runs.
- **At least 19 unique first-settlement-tick vectors among the R = 20 runs.** A count of distinct vectors across the set, not comparisons against seed 0 — there are only 19 other seeds, so "19 of 20 pairs against seed 0" was malformed.
- **Do not compare terminal settled sets.** If every run eventually fills the whole reachable map — which a small toy map makes likely — those sets are legitimately identical and the test would fail on correct behaviour. The route taken differs even when the destination does not.
- All 20 first-settlement-tick vectors being identical means the weighted draw is not wired up.

**Run and log conventions**, so comparisons are well defined:

- A run of **T ticks stores T+1 grid snapshots**: index 0 is the initial state before any update, indices 1…T are the states after each tick.
- **An event's `tick` is the index of the resulting state** — an event stamped 12 describes a transition observable in snapshot 12, produced by the update from snapshot 11.

**Harness summary statistics**, reported per seed:

final total population · cells settled · settlement fraction (reachable) · settlement fraction (passable) · tick of first expansion · **last frontier-advance tick** · plateau tick or `None` · **extinction tick or `None`** · count of `cell_abandoned` · count of `clamp_negative` (expected 0) · **count of rejected source intentions** · **count of sources unable to found alone from equilibrium**

**Symbols:** `T` is the tick horizon, `R` is the number of seeds. They are distinct letters deliberately — a single symbol for both makes "across N seeds over N ticks" ambiguous.

## Deliverables

- **Project bootstrap** (see below): `pyproject.toml`, `uv.lock`, `src/clio/`, `tests/`, ruff config
- Frozen fixture artifacts `fixtures/brief1_toymap_v1.npz` + `.json`, and the convergence fixture
- Invariants module implementing `grow`, `carrying_capacity`, `emigration`, and the extinction floor exactly as authored in INVARIANTS.md, plus unit tests. **The formulas are not placeholders** — only the coefficients are provisional, and they live in scenario config, not in the module
- Simulation loop module implementing the tick semantics above
- Multi-seed run harness producing the summary statistics above
- Both log writers, with one example run's output committed for reference

## Reference cohort — frozen fixtures

These are the **Brief 1 reference cohort**. They are committed as artifacts, not regenerated from a description, so that a run's identity resolves to bytes.

### Toy map — `fixtures/brief1_toymap_v1.npz`

Generated once by the procedure below and then **committed**; the generator is recorded for provenance, not re-run at test time.

```python
cells   = [(q, r) for q in range(20) for r in range(18)]        # 360; index = this order
terrain = np.random.default_rng(11).choice(
              [PLAINS, HILLS, MOUNTAINS, MARSH, WATER],
              size=360, p=[0.42, 0.24, 0.12, 0.10, 0.12])
climate = [COLD if r < 3 else (ARID if q > 15 else TEMPERATE) for q, r in cells]
```

Climate precedence is ordered and matters: `r < 3` and `q > 15` overlap in one corner, and cold wins there.

| | |
|---|---|
| Fixture schema | `clio.toymap/1` |
| Arrays | `coords`, `terrain`, `climate`, `K_eco`, `passable`, `neighbor` (cells,6), **`M_base`** (cells,6, all ones on live edges), `reachable` |
| Integer maps | plains 0, hills 1, mountains 2, marsh 3, water 4 · temperate 0, arid 1, cold 2 |
| Direction order | `(+1,0), (+1,−1), (0,−1), (−1,0), (−1,+1), (0,+1)` |
| Realized composition | plains 165, hills 83, mountains 39, marsh 35, **water 38 (10.6%)** → **322 passable** |
| Climate composition | temperate 240, arid 60, cold 60 |
| Seed cell | index **189** = `(10, 9)`, hills/temperate, `K_eco = 80`, **6 eligible neighbours** |
| Reachability | **322 of 322** passable cells reachable from the seed — no unreachable land in this fixture, so both settlement fractions coincide here and diverge only on maps with islands |
| `K_base` table | temperate/arid/cold — plains 120/60/45, hills 80/40/30, mountains 20/10/8, marsh 50/25/18, water 0/0/0 |

Sidecar `fixtures/brief1_toymap_v1.json` carries the schema id, integer maps, direction order, seed cell, and the fixture **SHA-256**, which the run identity references.

### Scenario parameters

`r_max = 0.03`, `θ = 0.6`, `φ = 0.30`, `P_min = 10`, `P₀ = 60` at cell 189, horizon `T = 1000`, `R = 20` seeds.

### Convergence fixture — a frozen test case, not a file

Five scalars; serializing them to an `.npz` would add a hash to verify without adding any information. They live as constants in the test module, frozen exactly as the map fixture is.

```
single cell, migration disabled
P₀ = 10, K = 100, r_max = 0.03, S = 1, Λ = 1
horizon 1000, assert |P − K| / K ≤ 1e-6
```

Verified: tolerance is reached at **tick 529**, comfortably inside the horizon.

### Cohort properties, measured not asserted

Per INVARIANTS.md §5, `φ·K > P_min` is a bounded claim about *isolated* sources, so it is a property of this fixture rather than an engine startup requirement.

- Seed cell: `φ·K = 24 > 10` ✓, and `φ·θ·K = 14.4 ≥ 10`, so it can found immediately at the trigger rather than after further growth.
- **59 of 322 passable cells (18.3%) are unable to found alone from equilibrium** (`φ·K ≤ P_min`). They can still be entered, be founded jointly, and contribute to a joint founding — they simply cannot reach the floor unaided.

Observed across 5 seeds at `T = 1000`: settlement fraction 96–98%, all first-settlement-tick vectors unique, last frontier-advance at or near the horizon, total population ≈ 22,000 against `Σ K_land = 23,632`.

## Project bootstrap — also this brief's deliverable

Brief 0 deliberately builds nothing, so Brief 1 starts from a documentation-only directory and owns standing the project up:

- `pyproject.toml` — Python 3.13, NumPy, pytest, ruff, per AGENTS.md
- `uv.lock`, committed; its SHA-256 goes in every run identity
- `src/clio/` package: `invariants.py`, `loop.py`, `logging.py`, `harness.py`
- `tests/` mirroring that layout, plus `ruff.toml` or the `[tool.ruff]` table
- `fixtures/` holding the two frozen fixtures above and their sidecars


**Settled in AGENTS.md, do not re-litigate:** Python 3.13, uv, pytest, ruff. World state as NumPy arrays over cells. One `numpy.random.Generator` per run from a single recorded seed, spawned into per-subsystem streams, no global random state. Output as `.npz` plus JSONL per the schemas above.
