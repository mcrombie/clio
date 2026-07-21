# AGENTS.md — Clio Standing Rules

These rules apply across every brief in this project, independent of any single brief's scope. Check them before starting implementation on any brief.

## What Clio is

Clio is a **historical simulation engine** that consumes maps produced by World Builder. It is not a simulation of Azhora; Azhora is the first map it is tested against. Anything that would break on a different World Builder map is a bug.

## Which phase are you in

The project runs in two phases (see ROADMAP.md). **Phase 1 is the emergent simulation: one founding culture, everything else descending from it, no authored timeline events and no pressure layer.** Phase 2 introduces authored events and pressure.

Nearly all current work is Phase 1. If a brief doesn't say otherwise, assume Phase 1 rules.

The gate to Phase 2 is **not** "Phase 1 produces good history." It is: Phase 1 is reproducible, understood, and its limitations measured and written down.

## The layers

1. **Locked mechanics (invariants).** Specified in **`INVARIANTS.md`** — growth, carrying capacity, emigration, extinction floor, cultural divergence (once authored), and the political state-transition rules (once authored).

   **Every Phase 1 state-transition rule is a locked invariant once settled.** §§1–5 — growth and survival, capacity, shocks, extinction, frontier expansion — are locked. §6 cultural divergence and §7 political state transitions are not yet settled and are locked once calibration runs have answered what only runs can answer. There is no category of Phase 1 mechanic that remains permanently adjustable; Phase 1 is locked machinery whose only free variables are global coefficients.

   Implement them exactly as written; never edit them to make a run come out better. Only the project owner may change them, and any change must be logged in DEFECTS.md as an invariant change and treated as invalidating earlier runs for comparison. This is the experimental control; if a result only holds because an invariant moved, the result is worthless.

   The invariants define a fixed formula shape with slots for factors that switch on across briefs — technology, infrastructure, disease, political stability. Unimplemented factors sit at a neutral value of 1. **The formula never changes shape; only which terms are live.** Adding a factor is filling a reserved slot, not amending the invariant.

2. **Authored events (Phase 2 only).** Exogenous shocks declared on a timeline as data. Does not exist yet. See the note on initial conditions below.

3. **Pressure layer (Phase 2 only).** Soft modifiers biasing outcomes via probabilities and weights, never setting results directly. Does not exist yet.

## Initial conditions are not authored events

The founding culture's settlement is **initial state** — where the simulation starts, like a starting population value or a map. It is not an authored event and does not require the event system.

This distinction matters because it's the seam Phase 1 work is most likely to erode. Adding "just one more" seeded population, or a hardcoded date when something appears, is building the Phase 2 event system early and without a schema. Don't. If a run seems to need an exogenous shock, that's a finding about Phase 1, not a licence to add one.

## No biasing in Phase 1

Phase 1 has no pressure layer, which makes this a hard rule rather than a soft one: **do not nudge outcomes.** No terrain weights tuned because a population "should" reach the south, no adjusted thresholds because a run looked wrong, no special-casing a region.

If you find yourself wanting to bias an outcome, that impulse is Phase 2 work and the wanting is itself data — write it down. Note what you wanted to bias and why in the brief's completion notes, and if you suspect something has already crept in, log it in DEFECTS.md. That list is the raw material for designing the pressure layer later: it records where the unaided simulation actually falls short, rather than where it was assumed it would.

## Engine/scenario boundary

The engine contains no scenario-specific logic — no branching on a named people or a named place.

**Invariant machinery is engine code, not data.** The formulas in INVARIANTS.md are implemented in an engine module; they are not loaded from a file. What lives in scenario data files is the *numbers* — coefficients, the `K_eco` table, contact weights, the map — plus (later) authored events and pressure. Saying "invariants are data files" conflates the formula with its parameters and is wrong.

**The line runs between machinery and parameters.** Divergence drift, cohesion, and clustering logic are engine mechanics; the drift rate `σ`, cohesion rate `μ`, clustering threshold `τ`, and the **global travel-cost → weight mapping** are scenario data. Growth and capacity work the same way: the functions are engine, their coefficients are scenario. When in doubt, ask whether the thing would still make sense on a completely different map — if yes, it's engine.

**Contact weights and movement accessibility are derived, never authored per edge.** Scenario data supplies a *global mapping* from travel cost to weight, plus its coefficients. The realized per-edge fields — `M_base(i→j)` for migration, `wᵢⱼ` for cultural cohesion — are computed from the map. From Brief 5 politics modifies the *effective* field `M_eff = clip(M_base × modifiers, 0, 1)`. **Eligibility reads `M_base`, weighting reads `M_eff`** — so the candidate set is fixed by geography and cannot be widened or narrowed by a later layer. Modifiers must be strictly positive on live edges; they may exceed 1, but `M_eff` is clipped there. **An authored per-edge table is regional pressure in disguise** and is forbidden in Phase 1. The provenance test below applies here as sharply as anywhere.

**A parameter becomes pressure when its variation is *authored*, not when it merely varies.** The distinction matters because much of the simulation is *supposed* to vary across space and time:

| | Example | Verdict |
|---|---|---|
| **Endogenous variation** | technology, infrastructure, disease, stability, culture, capacity after tech growth — all differ by cell and year because the simulation computed them | Fine. This is the model working. |
| **Authored variation** | a coefficient given one value in the north and another in the south, or changed by hand at year 500, because a run "should" go a certain way | Pressure. Phase 2 only; log it if it appears earlier. |

The test is provenance, not variance: did the simulation derive this value, or did someone type it in per-region? A global coefficient typed once is configuration and may be tuned freely in Phase 1. A per-region table of the same coefficient is the pressure layer wearing a disguise.

## Define what you test

Every brief must convert the terms it tests into explicit, computable metrics before it can claim to pass. Prose like "believable," "stable," "plausible," "in contact," or "roughly right" is not an acceptance criterion.

If a brief tests a behavior, it states the number: the window and tolerance for a plateau, the ratio that constitutes preferring good terrain, the distance cutoff that constitutes contact, the band that constitutes a plausible culture count. A criterion that can't be computed can't fail, and a criterion that can't fail isn't testing anything.

## No UI, not yet

Do not build a scenario editor, settings UI, or any user-facing configuration surface. The only requirement is keeping the engine/scenario file boundary clean enough that one is possible later.

## Testing discipline

Every brief that touches simulation behavior includes or extends the multi-seed run harness. A result is trusted only after being checked across seeds, never eyeballed from a single run.

From Brief 4 onward the cross-seed check has real content: divergence outcomes should vary between seeds. Identical results across every seed indicate something deterministic where contingency was intended.

## Run identity and cohorts

A seed does not identify a run. Every run records, and reproduction requires, all of:

**seed · commit hash · `working_tree` · `dirty_fingerprint` · complete scenario configuration · source-map hash and schema · dependency-lock hash**

Runs of record require a **clean working tree**. A dirty run is permitted and records `"working_tree": "dirty"` plus `dirty_fingerprint` — SHA-256 over `git diff HEAD` concatenated with the contents of every untracked non-ignored file — and **may never be cited as a run of record**. Untracked content belongs in the fingerprint because a new source file otherwise changes behaviour while leaving the fingerprint unchanged. The repository must carry at least one commit before any run.

Stored in the `.npz` as one versioned JSON string with `allow_pickle=False`. A run whose identity cannot be reconstructed from its own output is not evidence.

**The dependency lock is load-bearing.** NumPy's `Generator` carries no stream-compatibility guarantee across NumPy versions — NEP 19 dropped it deliberately so distribution algorithms could improve. A NumPy upgrade can change every result while every seed stays identical.

**Two different things invalidate a comparison, and they are not the same:**

| | Changed | Consequence |
|---|---|---|
| **Invariant change** | a formula in INVARIANTS.md | earlier runs invalid as evidence about the mechanics; log in DEFECTS.md |
| **New cohort** | scenario data — `τ`, `r_max`, `θ`, `φ`, `P_min`, the `K_eco` table, the map | earlier runs stay valid; they belong to a different cohort |

Runs are comparable **within** a cohort, never across. Changing τ opens a cohort; it is not an invariant edit, because no formula moved.

## Defect log

Log to `DEFECTS.md` whenever:

- an invariant is suspected of having been edited to make a run work
- an outcome is deterministic across every seed where variance was expected
- scenario-specific logic has leaked into the engine
- a Phase 2 mechanic (authored event, pressure, biasing) has crept into Phase 1 work
- (Phase 2, later) a pressure change forces an outcome instead of biasing it

Use the entry format at the top of that file.

## Language and tooling

| | Pinned | Why |
|---|---|---|
| Language | **Python 3.13** | A year of ecosystem maturity; supported by every relevant package and by SPEC 0 into late 2027. 3.14 is also viable (Numba added support in 0.63, Dec 2025). Do **not** pin 3.12 — NumPy 2.5 already dropped 3.11 and 3.12 is next in line. |
| Deps & venv | **uv** | One tool for Python install, virtualenv, dependency resolution, and lockfile. Removes the activation dance. Commit `uv.lock`. |
| Tests | **pytest** | |
| Lint & format | **ruff** | Replaces black, flake8, and isort with one fast tool. |
| Array layer | **NumPy** | See below — this is an architectural requirement, not a convenience. |
| Type checking | optional | Type hints on invariant signatures are worth it as a validation signal on AI-written code; a checker in CI is optional. |

### NumPy is architectural, not optional

World state is **arrays over cells** — not an object per cell. A tick is a handful of vectorized operations over whole arrays.

- Flat per-cell arrays: `population` (float), `settled` (bool), `terrain` (int), `climate` (int), `capacity` (float), `passable` (bool)
- Culture (Brief 4) is a **`(cells, 8)` continuous float field**, not a culture-ID label per cell. There are no culture *objects* and no group-level entities — peoples are found by clustering that field in post-run analysis, never announced by the engine.
- Polities (Brief 5) *are* discrete named entities owning sets of cells. Culture and polity are separate layers with deliberately opposite representations; see ROADMAP Brief 5.

This is roughly a 100× difference on continent-scale runs (10⁴ cells × 1000 ticks × 20–100 seeds), and it decides whether a multi-seed experiment takes seconds or hours. It is a **Brief 1 decision**: cheap now, a rewrite later. Group-level logic in Briefs 4–5 is graph-shaped rather than grid-shaped, but group counts stay in the dozens, so a plain Python loop over groups wrapping NumPy per-cell work is correct there.

Do not reach for Numba, Cython, or a second language until the multi-seed harness demonstrates an actual wall. When that happens the fix is `@njit` on the hot function, not a rewrite.

### Randomness: reproducibility rules

The project's success criterion is a cross-seed comparison, so seeded reproducibility is load-bearing rather than housekeeping. These rules are not stylistic.

- **One `numpy.random.Generator` per run, created from a single integer seed**, passed explicitly to whatever needs it. Never `np.random.seed()`, never `random`, never module-level global state.
- **Record the seed in the run output.** A result that can't be reproduced from its own log isn't evidence.
- **Give each subsystem its own stream** via `rng.spawn()` — expansion, divergence, contact, and later pressure each draw from an independent child generator.

That last rule prevents a trap that will otherwise bite hard during balancing. With a single shared stream, adding one new random draw anywhere shifts every subsequent draw in the run. A seed that used to produce a given outcome now produces a different one, and it looks like a behavior change when it's only stream displacement. Independent per-subsystem streams mean a change in one subsystem leaves the others' draws untouched, so before/after comparisons across code versions stay meaningful.

### Output formats

Two logs, not one — they have different shapes and different consumers.

- **Dense per-tick cell grid** → NumPy `.npz` (or Parquet). Consumed by analysis and plotting.
- **Sparse event log** → JSONL, one record per line, schema-flexible. Consumed by the Brief 6 chronicler.

Conflating them will hurt at Brief 6.
