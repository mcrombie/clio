# Clio — Locked Mechanics (Invariants)

The experimental control. These formulas are **authored by the project owner and forbidden to the implementer**, including during balancing. Changing anything here invalidates every prior run and must be logged in DEFECTS.md as an invariant change.

This file specifies the *machinery*. Coefficients and thresholds are scenario data and live elsewhere — see "Parameters" at the end.

---

## 1. Population growth

**Locked form — discrete exponential:**

```
P(t+1) = P(t) × (1 + g)
```

which over t ticks at an unchanging rate `g` is the closed form `P(t) = P₀ × (1 + g)^t`.

One tick is one year. Population is stored as float; see the extinction floor in §4.

### How constraints enter

Constraints act on **the rate or on survival — never as a hard clamp to capacity**. Clamping P to a ceiling would produce a corner and unphysical dynamics; modifying growth and mortality is both more realistic and how actual demographic models behave.

```
P(t+1) = [ P(t) × (1 + g) ] × Λ          where  g = r_max × D × S
                                               Λ = Π survival factors
```

**Two coefficients, deliberately distinguished.** They do different work and conflating them makes the domain analysis ambiguous:

| Symbol | Definition | Use |
|---|---|---|
| `g` | `r_max · D · S` | the **actual growth rate** this tick; varies with P through D |
| `b` | `r_max · S` | the **density-free coefficient**; constant while S is, and the term all domain bounds are expressed in |

Two positions, and which one a factor occupies is semantic, not cosmetic:

| Position | Factors | Effect |
|---|---|---|
| **Rate** — inside `(1 + g)` | `r_max`, `D`, `S` | changes how fast a population grows |
| **Survival** — the product `Λ` | `H_disease`, `H_political`, … | kills a proportion outright |

| Term | Name | Range | Position | Implementing brief | Neutral |
|---|---|---|---|---|---|
| `r_max` | intrinsic rate | scenario param | rate | Brief 1 | — |
| `D` | density term | (−∞, 1] | rate | Brief 1 | — |
| `S` | stability term | [0, 1] | rate | Brief 5 | 1 |
| `H_disease` | disease survival | [0, 1] | survival | Brief 3 | 1 |
| `H_political` | political survival | [0, 1] | survival | Brief 5 | 1 |

**`Λ` is a product of independent survival factors, and that is the extension point.** Adding a new source of mortality means adding a factor to `Λ`, never restructuring the equation. This closes what would otherwise have been a scheduled invariant change: political instability plausibly both suppresses growth *and* kills, so it gets a slot in each position, both neutral until Brief 5 decides how much of each it needs. Nothing about the formula's shape changes when it does.

A combined effective rate, when one is wanted for reporting:

```
r_eff_total = Λ·(1 + r_max·D·S) − 1
```

**Density term:**

```
D = 1 − P / K
```

### ⚠ Why mortality is a survivor multiplier, never a rate multiplier

Mortality must not multiply the *signed* growth rate. Doing so fails in both directions:

- **Below K**, a mortality factor on the rate only slows growth. At zero it gives stasis, not deaths.
- **Above K**, the rate is negative, so reducing it makes the rate *less* negative — an epidemic would **slow** the decline of an overcrowded population.

Neither matches what a plague does. Applied as a survivor multiplier *after* growth, `Λ` removes a fixed proportion regardless of which side of K the population sits on.

`Λ = 1` throughout Brief 1 and until Brief 3, so this does not affect current work.

**Settled, so that Brief 5 contains no scheduled invariant change:** political instability gets a slot in *both* positions — `S` on the rate and `H_political` in `Λ` — both neutral at 1 until Brief 5. Brief 5 decides how much of each to use by filling reserved slots, not by moving a term.

K is the cell's carrying capacity (§2). D goes negative when P exceeds K, which makes `g` negative and the population decline — overshoot corrects itself smoothly rather than being clamped.

### ⚠ Growth behaviour, with domains stated

**Every identity and property below assumes `Λ = 1` (no mortality), `K` fixed, and `b = r_max·S > 0`.** They are the *disease-free* dynamics. Sustained mortality changes them qualitatively — see the next subsection.

**Growth is defined only for `K > 0`.** Zero-capacity cells — ocean, lake — are excluded from the growth step entirely; `D = 1 − P/K` is undefined there and would divide by zero. This is a hard precondition, not a convention.

Writing `x = P/K`, the update satisfies the exact identity

```
x_next − 1 = (x − 1)(1 − b·x)
```

which makes every regime visible. Verified numerically against the closed form:

| Domain | Behaviour |
|---|---|
| `0 < x < 1` | monotone increase toward K, **never overshooting** |
| `1 < x < 1/b` | monotone decrease toward K, **never crossing below** |
| `x > 1/b` | single-step undershoot possible — `x_next` can land below 1 |
| `x > 1 + 1/b` | raw update **negative**; clamped to 0 and the cell goes extinct |

At `b = 0.01` the safe overshoot band runs to 100 K and extinction begins above 101 K; at `b = 0.03`, 33 K and 34 K. Reaching those requires pathological migration inflow into a tiny-capacity cell, but the domains must be stated rather than assumed away.

**Assertable properties, each with its domain:**

| Property | Statement | Valid domain |
|---|---|---|
| Monotone approach | `P < P_next ≤ K` | `0 < P < K`, `K > 0` |
| Monotone correction | `P_next < P` | `P > K` |
| No crossing | `P_next ≥ K` | `K < P < K/b` |
| Non-negative | `P_next ≥ 0` after clamp | always |
| Convergence | `P → K` with migration disabled | `0 < P₀ < K/b` |

Each property holds only inside its stated band. "Decline never crosses K" and "any positive P₀ converges to K" are both false in general and true only where the table says so.

**Required guard.** Clamp `P_next` at zero and emit a `clamp_negative` diagnostic. A clamp firing means migration inflow is pathological and wants investigating, not silencing.

### ⚠ Sustained mortality moves the equilibrium — and can erase it

With `Λ < 1` held constant, the equilibrium sits below K (`b = r_max·S`):

```
P*/K  =  1 + (1 − 1/Λ) / b
```

This holds with `K`, `b`, and `Λ` all **fixed**; a moving `K` or `Λ` moves the equilibrium with it.

Population settles **below** carrying capacity, and below a critical mortality the positive equilibrium disappears entirely:

```
positive equilibrium lost when   Λ ≤ 1 / (1 + b)          [floor-free recurrence]
```

**⚠ That is not the engine's extinction threshold.** It describes the recurrence *without* the `P_min` floor of §4. In the full engine a population whose positive equilibrium merely sits **below `P_min`** is extinguished by the floor, at a distinctly milder mortality:

```
extinction in the full engine when   Λ  <  1 / (1 + b·(1 − P_min/K))
```

Verified: at `b = 0.03`, `K = 1000`, `P_min = 100`, the floor-free threshold is `Λ ≤ 0.9709` but the engine extinguishes at `Λ < 0.9737`. At `Λ = 0.9737` the equilibrium is `P* = 99.7`, above zero and below the floor — the floor-free formula predicts survival and simulation gives extinction. Reducing to the floor-free form when `P_min = 0`, as it must.

Verified numerically against 20,000-tick simulations (predicted and simulated agree to 4 decimal places):

| `b = r_max·S` | `Λ = 0.999` | `Λ = 0.995` | `Λ = 0.99` | floor-free threshold |
|---|---|---|---|---|
| 0.01 | P* = 0.90 K | 0.50 K | **P\* ≤ 0** | Λ ≤ 0.9901 |
| 0.03 | 0.97 K | 0.83 K | 0.66 K | Λ ≤ 0.9709 |

The last column is the *floor-free* threshold. With a floor the engine extinguishes earlier, per the formula above.

**A sustained 1% annual mortality wipes out a population growing at 1% a year.** Obvious in hindsight and easy to trip over in Brief 3: disease is not a gentle damper, and a plausible-looking endemic burden can be lethal at the intrinsic rates this project uses. Endemic `Λ` should sit very close to 1, with epidemics as brief excursions rather than a permanent floor. Log any cell where `Λ < 1/(1 + b·(1 − P_min/K))` persists — the engine threshold, not the floor-free one.

### This generalizes logistic growth rather than replacing it

With `Λ = 1` and `S = 1`, the above is exactly `P(t+1) = P(t)(1 + r_max(1 − P/K))` — the discrete logistic equation. Logistic growth is the neutral special case of this formula, not a different model.

### ⚠ Bound on r_max — stay far below 2.0

The discrete logistic is the logistic map in disguise. Substituting x = P/K yields the map with parameter λ = 1 + b, which:

- has a **stable** fixed point for `b < 2`
- reaches its **period-doubling bifurcation at exactly `b = 2`** — where the fixed point is only marginally stable and convergence becomes arbitrarily slow. The stable two-cycle appears for `b` just *above* 2, not at it
- becomes **chaotic** above `b ≈ 2.57`

Realistic annual human growth rates are 0.001–0.03, roughly two orders of magnitude below the danger zone, so this is not a practical constraint — but it **must** be documented, because a balancing session that pushes r_max upward looking for faster settlement could cross it and produce oscillation that looks exactly like a bug. Assert `r_max < 0.5` in code with a comment pointing here. Anything approaching that is already unphysical.

### Suggested starting range (scenario parameter, not invariant)

`r_max` between **0.005 and 0.03** (0.5%–3% per year). Long-run pre-industrial growth was far below 1%; frontier populations with abundant land historically reached ~3%.

Note the division of labor: **r_max sets how fast the continent fills; K sets what it fills to.** Terminal population is governed by the sum of carrying capacities over settled cells, near-independently of r_max. Tuning r_max to change the endgame population is a category error.

---

## 2. Carrying capacity

**Locked form — multiplicative composition:**

```
K(cell, t)  = K_eco(cell) × A(tech) × I(infrastructure) × Y(t)

K_eco(cell) = K_base[terrain, climate] × W(water_context)
```

`Y(t)` is the **transient supply multiplier**, neutral at 1 and reserved now. It exists because §2 otherwise promises behaviour it has no slot for: famine, a trade shipment, or a stored surplus temporarily changes how many people the land currently feeds without altering its long-run capacity. Division of labour among the slots:

| Effect | Slot |
|---|---|
| permanent capability gain — irrigation, ploughs, roads | `A`, `I` |
| **transient** supply shock — famine year, grain convoy, drawn-down store | `Y(t)` |
| people dying — plague, violence | `Λ` (§1), never capacity |

Without `Y`, the first implementation of a famine would have had to edit a locked §2.

`W` is the **water-context multiplier**, reserved and locked in shape now. `W(∅) = 1` throughout Brief 1; Brief 2 supplies river, coast, and lake coefficients by filling the table, not by amending the formula.

**Composition is part of the locked shape; only the coefficients wait for Brief 2.** A hex may carry several water contexts at once — river *and* coast, say — and how those combine is a modelling decision, not a coefficient:

```
W(contexts) = max over c ∈ contexts of W_base[c]        (∅ → 1)
```

with an optional **override table for named combinations**, consulted first when an exact combination is listed.

Max rather than product, deliberately. A product compounds without bound — river × coast × lake would multiply three bonuses onto one hex and grow with every context added later. A river-mouth hex on the coast is not twice as habitable as either; it is about as good as the better of the two, perhaps a little more, and the override table exists for exactly that "a little more" without opening the door to unbounded compounding.

Without this slot, §2 would lock and then require an invariant edit at Brief 2 immediately — the same trap the `M` slot avoids for movement.

This is where **land, food, technology, and infrastructure** enter. They are not four independent rate modifiers; they are all channels into how many people a place can feed.

| Factor | Enters as | Implementing brief | Phase 1 neutral value |
|---|---|---|---|
| **Land** | `K_eco(cell)` — static per cell, from map data | Brief 1 (lookup), Brief 2 (real map) | active from Brief 1 |
| **Food** | *not a separate term* — it is what K measures. Food per capita is implicit in `P/K`; scarcity appears as D shrinking | — | implicit throughout |
| **Technology** | `A(tech)`, multiplier ≥ 1 | Brief 3 | `A = 1` |
| **Infrastructure** | `I(infrastructure)`, multiplier ≥ 1 | Brief 3 | `I = 1` |

### "Land" is a composite, not a terrain enum

World Builder describes a hex with **terrain and climate as separate fields**. A desert is typically plains or hills carrying an arid climate; cold country is likewise expressed through climate rather than terrain. Reading terrain alone would give an arid plain the same capacity as a temperate one, which is wrong by a wide margin.

`ecological_capacity` therefore takes terrain **and** climate **and** water context (river edge, lake or coast adjacency) as inputs. The conceptual claim is unchanged — land determines base capacity — but "land" means World Builder's full description of the hex.

**Inputs are fixed here; the value table is scenario data.** The importer computes `K_eco` from this function and nothing else. An importer that invents its own capacity adjustments — "rivers add 20%" decided at load time — is an invariant edit smuggled through the map loader, and must be logged as one.

**Rivers: authored effect required before Brief 2.** Rivers plausibly act through two mechanically distinct channels, and the choice belongs here rather than to an implementer:

1. **Capacity** — a river hex supports more people (irrigation, fish, transport).
2. **Movement** — travel along a river is cheap, across it is expensive. Affects migration adjacency and, later, the contact term that drives cultural divergence.

Both are historically real and they are not substitutes. Recommend enabling both with separate parameters, since a river that raises capacity but does not shape movement will fail to produce river-valley cultures, and one that only shapes movement will fail to concentrate population along water.

**On food:** it is deliberately not given its own multiplier. Carrying capacity *is* the food ceiling expressed as people; adding a separate food term would double-count it. If food ever needs to be independently visible — a famine, a trade shipment, a stored surplus — it enters as a temporary modifier on K, not as a new factor in the product.

---

## 3. Shock and order terms

| Factor | Enters as | Implementing brief | Phase 1 neutral value |
|---|---|---|---|
| **Disease** | `H_disease` ∈ [0,1], a factor of the **survival product `Λ`** | Brief 3 | `H_disease = 1` |
| **Political stability (rate)** | `S` ∈ [0,1], a **rate** multiplier | **Brief 5** | `S = 1` |
| **Political stability (mortality)** | `H_political` ∈ [0,1], a factor of `Λ` | **Brief 5** | `H_political = 1` |

**Disease is mortality, not a rate shock.** An epidemic kills people without reducing how many the land could support, so it belongs in `Λ` and not on the rate — see §1 for why a rate multiplier cannot kill anyone and would perversely slow the decline of an overcrowded population. It should be at least partly **density-dependent**, since crowding raises epidemic probability, which gives a sharper check on runaway population than the density term alone.

Note the extinction bound in §1 when specifying it: sustained `Λ ≤ 1/(1 + b)` is fatal, which at `r_max = 0.01` means an endemic burden of 1% a year wipes the population out. Endemic levels must sit very close to 1, with epidemics as brief excursions.

**Political stability** stays pinned at 1 until **Brief 5**, which introduces the political layer that can compute it. Stability there is a function of cultural homogeneity within a polity, its total size, and distance from its core; unstable territory grows more slowly. Its slot is reserved here so the formula is complete and fixed from the start, and the term is simply neutral until something can fill it.

Note this is a layer distinct from culture. Culture is a continuous field with no hard edges (Brief 4); a polity is a discrete named entity with a border (Brief 5). One state may hold many cultures. They interact — shared political membership counts toward the contact term that suppresses cultural drift — but they are never the same field.

**This is the intended pattern for all six factors:** the formula is locked now, in full; the factors switch on across briefs; anything unimplemented sits at its neutral value. The invariant never changes shape — only which terms are live.

---

## 4. Extinction floor

A cell with a positive but negligible population is meaningless and will otherwise persist forever at fractions of a person.

```
if P(cell) < P_min:  P(cell) = 0;  settled(cell) = False
```

Applied after the growth step, before migration. `P_min` is a scenario parameter; something on the order of a single viable band (tens of people) is sensible.

This matters more than it looks. It is the only mechanism by which settled territory can be *lost*, and without it no run can ever produce abandonment, retreat, or a collapsed frontier — all of which the history is supposed to be able to contain.

---

## 5. Emigration

**Locked form:**

```
if P(cell) > θ × K(cell):   emigrants = φ × P(cell)
```

The *rule* — that emigration is triggered by population crossing a fraction of local carrying capacity, and that a fixed fraction leaves — is an invariant. The values θ (threshold) and φ (emigrant fraction) are scenario parameters.

This is a deliberate change from Brief 1's earlier framing, which called the emigration threshold "loop logic, not part of the locked invariant." Left tunable, it becomes a pressure knob by the back door: quietly raising θ in one region to hold a population in place would bias outcomes exactly the way the pressure layer is supposed to, but without being visible as pressure. The rule is locked; only its coefficients are scenario data, and they are global rather than per-region in Phase 1.

### Frontier expansion — the complete rule

Everything below is one locked rule. Eligibility, weighting, selection, the no-candidate case, and founding viability are all part of it: a rule that referred to terms defined elsewhere would leave later briefs appearing free to alter them.

**1. Eligibility.** A neighbour `j` is a candidate for source `i` when

```
eligible(i → j)  ≡  ¬settled_at_t(j) ∧ passable(j) ∧ K_eco(j) > 0 ∧ M_base(i → j) > 0
```

**`M` has two layers, and which one each rule reads is part of the invariant.**

| | Definition | Mutable | Read by |
|---|---|---|---|
| `M_base(i→j)` | derived from the map at import; locked at Brief 2 | no | **eligibility** |
| `M_eff(i→j, t)` | `clip( M_base × Π modifiers, 0, 1 )` | yes | **weighting** |

Both are `(cells, 6)` fields in `[0, 1]`. Constraints, all asserted:

- **Eligibility tests `M_base > 0`, never `M_eff`.** Immutable by construction, so the candidate set is fixed by geography and cannot be widened *or narrowed* by any later layer.
- **Modifiers are strictly positive on live edges** — `modifier > 0` wherever `M_base > 0`. A zero modifier is forbidden, since it would silently remove an edge through the weighting path that eligibility still admits.
- **`M_eff` is clipped to `[0, 1]`.** Modifiers may exceed 1 — political integration genuinely improves passage — but `M_eff` cannot: `M` is accessibility relative to free movement, and nothing is better than free.
- **`M_base = 0` ⟹ `M_eff = 0`**, trivially, since the product is zero.

Keying eligibility to `M_base` makes candidate-set stability **structural rather than derived**. Deriving it instead from `M_base = 0 ⟹ M_eff = 0` would not survive contact with a zero modifier, and a modifier above 1 would break the declared `[0,1]` range; keying to the immutable layer holds regardless of what modifiers later do.

`M_base = 0` unifies off-map directions, water crossings, and impassable terrain into one case rather than three.

`settled_at_t` is the mask at the *start* of the tick, which is also the recolonization bar: a cell unsettled by the extinction floor this tick was settled at *t*, so it cannot be resettled until the next tick.

**2. Weighting.** Candidates are sorted by cell index, then weighted:

```
weight(i → j)  =  K_eco(j) × M_eff(i → j, t)
```

`M_eff` is the **movement-accessibility factor** on the edge, in `[0, 1]`. It is **1 for every live edge in Brief 1** and is where Brief 2 puts river-crossing and terrain movement cost. Capacity-only weighting had nowhere to put those, which would have forced a rule change at Brief 2; the slot exists from the start so it does not.

**3. Selection.** One destination is drawn from that distribution using the expansion subsystem's Generator.

**4. No candidate.** If `eligible` is empty, **no emigration occurs and nothing is subtracted.** The source keeps its population and continues growing.

**5. Founding viability and refund.** Intentions are aggregated by destination *before* resolution. If an unsettled destination's total incoming population is below `P_min`, **every intention targeting it is dropped and its emigrants are refunded to their sources** — nothing leaves. Aggregation precedes the check, so several small parcels may jointly found a cell none could found alone.

Settled destinations do not arise in Brief 1 — §5 governs frontier expansion, whose candidates are unsettled by definition. **Behaviour for arrivals into an already-settled cell is reserved for Brief 4**, with the settled-to-settled mechanic, and is deliberately unspecified here.

**Feasibility — a bounded claim, not a general theorem.**

> **Under isolated-source assumptions**, a source can eventually found a cell **alone** iff `φ · K_source > P_min`.

Here `K` is the **total** carrying capacity `K_eco × A × I`, which coincides with `K_eco` only while `A = I = 1`. Every assumption is load-bearing:

| Assumption | Why it is needed |
|---|---|
| source acts **alone** | the claim is about founding unaided |
| `K` **fixed** | a rising `K` moves the limit the parcel tends to |
| source **starts below `K`** | above it, decline applies instead |
| mortality **neutral**, `Λ = 1` | sustained mortality lowers the equilibrium below `K` (§1), so the parcel tends to `φ·P*` not `φ·K` |
| **persistent positive growth**, `b > 0` | with `b = 0` nothing approaches anything |
| `0 < θ < 1` | at `θ ≥ 1` the trigger is never reached; at `θ ≤ 0` it fires below any floor |
| an **eligible target remains available** | with none, no founding occurs at any parcel size |

Given those, a refused parcel is not subtracted, the source grows toward `K`, and its parcel tends to `φ·K`.

**What the claim does not cover.** Several individually sub-floor sources can *jointly* found a cell, since aggregation precedes the viability check. This does not contradict the claim — a claim about founding **alone** says nothing about participation — but it does mean a source below the bound is never barred from contributing to a founding. Earlier phrasing called this "sufficiency failing downward," which mislabelled a scope limit as a counterexample.

**The actual counterexample, outside the assumptions.** A source may temporarily exceed `K` through migration inflow, making `φ·P > φ·K` while the excess lasts. That breaks *necessity* — but only by violating the isolation assumption, since inflow means the source is not acting alone.

`φ·θ·K_source ≥ P_min` is a *stronger* condition still, and means only that founding is possible immediately at the emigration trigger rather than after further growth.

**This is therefore a property of a scenario, not an engine precondition.** It is checked when a reference fixture is built and reported as a cohort statistic — never asserted at engine startup, which would wrongly reject valid scenarios that rely on joint founding.

**Strictness differs at the boundary.** A source *approaching* `K` from below never attains `K`, so its parcel stays strictly below `φ·K` and the requirement is the strict `φ·K > P_min`. At **literal equilibrium** `P = K` the parcel is exactly `φ·K`, and since the founding rule refuses only totals **below** `P_min`, equality *succeeds*:

| Situation | Founds alone when |
|---|---|
| approaching `K` from below (the normal case) | `φ·K > P_min` |
| exactly at `K` | `φ·K ≥ P_min` |

Cells with `φ·K < P_min` are therefore **unable to found alone while approaching equilibrium from below**. They can be entered, can be founded jointly, and can contribute to a joint founding; they simply cannot reach the floor unaided. They are not dead ends and not permanently barred — both would overstate it.

`K` here is total capacity `K_eco × A × I × Y`, equal to `K_eco` only at Brief 1 neutral settings.

**Scope: this rule governs frontier expansion only** — movement into *unsettled* destinations. **Settled-to-settled movement is a separate mechanic introduced at Brief 4**, with its own rule and its own lock. It does not modify frontier eligibility, and Brief 4 must not be allowed to look like an amendment to this rule.

**What Phase 2 may do.** Pressure supplies modifiers to `M_eff` and the weights fed into selection — never eligibility, viability, the refund rule, or the sampling method itself. Biasing where migrants go is legitimate pressure; biasing whether they leave is not.

---

## 6. Cultural divergence

**This section is not yet settled.** Unlike §§1–5, several of its choices — the σ-to-μ ratio, whether a threshold plateau appears at all, map-resolution dependence — can only be answered by running the model. Until those runs exist the formula below may be revised in response to what they show.

Code written against it during that period is **calibration work**: it lives under a clearly marked path, its results are labelled as calibration evidence, and no such run is cited as evidence about the simulation's behaviour. The point is to earn the numbers this section depends on, not to start the real work early.

### Chosen basis: Swadesh / glottochronology

Shared core vocabulary between two separated lineages decays exponentially with time apart:

```
c = r^(2t)          t in millennia, r ≈ 0.86 (100-word Swadesh list)
```

The exponent's 2 is there because *both* lineages change independently. Rewritten as a per-year accumulation, with `k = −2·ln(r) / 1000`:

```
k = 3.0165e-4 per year
c(t) = exp(−k·t)
```

Sanity: lineages split at year 0 share 74.0% at year 1000; split at year 500, 86.0%. Boueni therefore stays visibly ancestral to every daughter across the full run, which is the intended outcome.

**Known objection, and why it is acceptable here.** Glottochronology is largely rejected by historical linguists as a *dating* method — Bergsland & Vogt (1962) is the standard critique — because retention rates are not constant. Icelandic changed slowly, English quickly, and contact is the reason. That failure mode is exactly what the cohesion term below restores. Clio uses the decay curve as a calibration target for isolated lineages, not as a claim that real divergence is clock-like.

### Representation

Each cell carries a culture vector `v ∈ R^n`. Divergence and similarity between cells i and j:

```
D_ij = ‖v_i − v_j‖²           (squared Euclidean distance)
c_ij = exp(−D_ij)
```

Squared distance, not distance, because a random walk's squared displacement grows linearly in time — which is what matches Swadesh's linear-in-t exponent. Plain Euclidean distance would grow as √t and diverge too slowly.

**Drift.** Each year every settled cell adds an independent Gaussian step per dimension:

```
v ← v + ε,     ε ~ Normal(0, σ²) per dimension
σ = sqrt(k / 2n)
```

### ⚠ Which mean Swadesh calibrates: the geometric one

`σ² = k/(2n)` gives `E[D] = k·t` **exactly, for every n**. Since `c = exp(−D)`, that means `exp(E[log c]) = exp(−k·t)` — the **geometric mean** of similarity hits the Swadesh curve exactly by construction.

The **arithmetic** mean does not. `D = 2σ²t·χ²ₙ`, so

```
E[c] = (1 + 2kt/n)^(−n/2)
```

which always sits *above* `exp(−k·t)` and approaches it only as n → ∞. The gap is structural, not approximation error — closed form and simulation agree to three decimals at every n.

**The calibration target is therefore the geometric mean**, which is the right choice on its own merits: Swadesh's `c` is a proportion measured between two specific languages, and the arithmetic mean over an ensemble is skewed upward by the exponential transform.

**Consequence for n.** Because calibration is exact at any n, **n is chosen purely on spurious re-convergence** — not on calibration accuracy, which was never the real criterion.

**Dimension count: n = 8, σ = 0.004342 per year.** Reproduce with **`calib.py`**, deterministic under its fixed seed, re-run required if n or σ changes:

| n | `E[D]/kt` | geometric mean c | arithmetic E[c] (diagnostic) | pairs falsely near-identical |
|---|---|---|---|---|
| 1 | 0.998 | 0.740 | 0.790 | 32.0% |
| 4 | 1.000 | 0.740 | 0.755 | 4.6% |
| **8** | **1.001** | **0.739** | **0.747** | **0.5%** |
| 16 | 1.000 | 0.740 | 0.744 | — |

"Falsely near-identical" means **c > 0.95 after 1000 years of complete isolation** — two lineages that never met, ending up looking like dialects of each other by chance alone. At n = 1 that happens to a third of all pairs, which would be nonsense; at n = 8 it is negligible.

### ⚠ Open problem: map-resolution dependence

Drift is independent per cell, so the *mean* culture of a region spanning 100 cells drifts more slowly than one spanning 10 — independent noise averages out. Divergence behaviour therefore depends on hex resolution, and the same geography at a finer resolution would produce different history.

Not solved here. **Brief 4 must test the same geography at two or more hex resolutions and measure the difference**, and the result goes in the Phase 1 limitations record either way. Partial mitigation: the population-scaled drift refinement below ties drift to people rather than to cells, which should reduce but not eliminate the dependence — a claim that itself needs testing rather than assuming.

Low dimension counts fail on re-convergence: they let lineages that never met drift back into apparent identity by chance — a third of all pairs at n = 1, against 0.5% at n = 8. They do **not** fail on calibration, which is exact at every n under the geometric target.

Dimensions are **abstract and unnamed** in Phase 1. Naming them ("language," "kinship," "worship") invites hand-tuning a specific axis to produce a desired result, which is pressure entering through the back door. If the narrative layer later needs "they share a language but differ in worship," that is a Brief 6 interpretation question, not a change here. Language identity is carried by the lineage tree, not by an axis.

### Cohesion (contact)

Cells in contact pull toward one another:

```
v_i  ←  v_i  +  μ · Σⱼ wᵢⱼ (v_j − v_i)          [UNNORMALIZED — see below]
```

**⚠ Do not normalize by Σw.** Dividing by `Σⱼ wᵢⱼ` cancels absolute contact strength and destroys the mechanic this model depends on. With a single neighbour, a mountain-pass weight of 0.001 and an open-plain weight of 1.0 both reduce to `0.001/0.001 = 1` and `1.0/1.0 = 1` — **identical pull**. Terrain would stop isolating anything and the map would do no work at all.

The unnormalized form preserves absolute strength: a cell reachable only through one bad pass gets a weak pull, exactly as intended.

**Isolated cells take a cohesion delta of exactly zero.** With the unnormalized form this is automatic — an empty or all-zero-weight neighbour set gives an empty sum — but assert it rather than relying on the sum being empty.

**Stability bound: `μ · maxᵢ(Σⱼ wᵢⱼ) < 1`.** The update rewrites as `v_i ← (1 − μΣw)·v_i + μ·Σⱼ wᵢⱼ v_j`, a convex combination only while `μΣw < 1`. Six neighbours with weights up to 1 give `Σw ≤ 6`, so `μ < 1/6` at maximum contact.

**Weights are not static, so a load-time check is insufficient.** From Brief 5 shared political membership adds to `wᵢⱼ`, and row sums rise during the run. Two requirements, both needed:

1. **Bound against the worst case at load:** `μ · (max geographic row sum + max political bonus × 6) < 1`, using the largest political bonus the scenario config permits. This is what makes the bound sound rather than optimistic.
2. **Assert actual row sums every tick** that weights change — a single vectorized `max` over the weight field, negligible cost, and it catches a mis-specified political bonus immediately rather than as mysterious oscillation a century later.

Note that `μ ≤ 1` is **not** sufficient even in the normalized form: at exactly μ = 1 two double-buffered cells simply exchange values and oscillate forever rather than converging. The bound is strict.

`wᵢⱼ` is contact strength between neighbouring cells, derived from travel cost — low across mountains, low across rivers, high along them, zero across impassable water. This is where the map does its real work. From Brief 5, shared political membership adds to `wᵢⱼ`. **The contact-weight function itself is still to be authored**, and §6 cannot be settled without it.

**The σ-to-μ ratio is the master parameter.** It sets the characteristic size of cultural regions: cohesion-dominant gives one culture at year 1000, drift-dominant gives every valley its own. Expect nearly all Brief 4 calibration time to go here.

### Update order within a tick

Culture updates in three sub-steps, after population growth and migration have resolved:

1. **Migration blending** — arrivals mix into destination cultures. Local to each cell; no ordering hazard.
2. **Cohesion** — each cell pulls toward its neighbours. **Reads neighbours, so it must be double-buffered exactly like population:** all cells read the post-blending snapshot, all write to a fresh buffer. Without this, cell iteration order changes the result and the order-independence test fails.
3. **Drift** — each settled cell adds its Gaussian step. Local to each cell; no ordering hazard.

**Cohesion precedes drift** so that each year's new variation survives into the next tick instead of being smoothed away in the step that created it — drift-then-cohesion would smooth the fresh noise immediately. Calibration is unaffected either way, since isolated lineages take no cohesion at all, but the connected case gets more effective divergence per unit of σ this way.

### Migration and founding

Several sources may target the same destination in one tick, so **there is only one rule**, applied identically whether the destination was already settled or is being founded:

```
              P_res·v_res  +  Σₛ P_arr,ₛ · v_src,ₛ
v_dest  ←  ─────────────────────────────────────────
                   P_res  +  Σₛ P_arr,ₛ
```

All arrivals in a tick are blended simultaneously, not sequentially, so the result does not depend on which source is processed first.

**A newly founded cell is the same formula with `P_res = 0`** — its culture is the population-weighted mean of everyone who arrived, not the vector of whichever single source happened to be listed first. A cell inherits *its arrivals'* culture, which coincides with a single source's only when exactly one source arrives.

### Detecting cultures

Peoples are **detected from the evolving culture field, never announced by the engine.** No year exists in which Latin became French; a simulation that emits a "culture founded" event is inventing one.

Detection is nonetheless a *time series*, not a single pass over the final state. Persistence can only be assessed against history, so:

- The engine records a **culture-field snapshot every 25 years** — deliberately the same cadence as the chronicle chapters, so cultural periodisation and narrative periodisation share a clock.
- Clustering runs over those snapshots **in post-run analysis, not during the tick.** The simulation stores the field; the analysis finds the peoples. No mechanical threshold event is ever emitted mid-run.
**Clustering algorithm: thresholded spatial connected components.** Two *adjacent* settled cells belong to the same culture when `c_ij > τ`; cultures are the connected components of that graph. Specified because the choice is not neutral — hierarchical clustering and all-pairs similarity graphs would produce different peoples from identical data, and leaving it open means the answer depends on whoever implements it.

Chosen because it is spatially contiguous by construction, runs in O(cells) on adjacency already in hand rather than O(cells²), and reproduces dialect-continuum behaviour honestly: a chain of cells each similar to its neighbour forms one culture even when its two ends differ substantially. That chaining is realistic but must be visible, so **report an intra-culture diameter alongside culture count.** A culture whose extremes are wildly dissimilar is a finding about τ, not a bug.

**Diameter is computed by double sweep, not all-pairs.** Exact maximum pairwise distance is O(cells²) within each culture, which would silently reintroduce the quadratic cost the clustering choice avoids. Instead: pick any cell, find the farthest cell from it, then find the farthest cell from *that* — two O(cells) passes. The result is a **lower bound** on true diameter and is reported as such. Adequate for spotting runaway chaining, which is what it is for.

**Threshold selection — τ is a frozen global constant, not a per-run result.**

Deriving τ separately from each run's terminal snapshot would break cross-seed comparability outright: "culture count varies across seeds" could then be measuring threshold variation rather than divergence. It would also contradict AGENTS.md, B0, and ROADMAP, which all correctly list τ as scenario data.

**The plateau procedure is the calibration method, run once — not a per-run selector.** Sweep τ across calibration runs, pick a value by the rule below, then **freeze it in scenario config**. Every run of record uses that same τ.

Selection rule, applied during calibration:

- **Admissible range only:** τ values yielding `2 ≤ count ≤ cells_settled / 10`. This excludes the trivial plateaus at both ends — one culture covering everything, and one culture per cell — which are otherwise the widest plateaus on the curve and would always win.
- **Widest plateau within that range**, taking its midpoint. Minimum plateau width **5% of the admissible τ range**; narrower means no natural scale exists.
- **Aggregated across the provisional ensemble, not one run.** For each candidate τ, compute culture count in every provisional seed; a τ qualifies only where the count is stable across the *window* **and** its across-seed interquartile range is at most 1. Choose the midpoint of the widest τ interval satisfying both. A τ that is stable within seeds but scatters across them is measuring the seed, not the field.

Afterwards the plateau procedure stays on as a **diagnostic**: every run of record reports the count-vs-τ curve, the plateau containing the frozen τ, and its width. If runs of record stop showing a plateau near the frozen τ, that is a finding worth investigating — not a licence to quietly re-derive τ.

**Changing τ is not an invariant edit; it opens a new run cohort.** τ is scenario data, and no formula changes when it moves. What it does invalidate is comparison: see "Run cohorts" below.

**Persistence and lineage.** A cluster earns a name after persisting across a set number of consecutive snapshots; the name is assigned retrospectively with a founding *window*, never a founding year.

Matching a cluster to its parent needs a computable rule, and the obvious one does not work: **after migration, "population contributed by a parent" cannot be recovered from spatial overlap.** People have moved; a cell's occupants may descend from a cluster nowhere near it.

**Phase 1 rule — labelled an approximation.** For a cluster `C` at snapshot *t+1*, compute each prior cluster's **population-weighted overlap share**: the fraction of `C`'s population sitting in cells that belonged to that prior cluster at *t*. Let `m` be the largest such share.

| Condition | Outcome | Identity | Persistence counter |
|---|---|---|---|
| `m ≥ 50%`, prior is sole ≥50% parent of `C` and of no other cluster | **continuation** | keeps parent's name | **carries forward** |
| `m ≥ 50%` but the prior is ≥50% parent to two or more clusters | **split** | see rule below | see rule below |
| `20% ≤ m < 50%`, exactly one prior ≥ 20% | **derivation** | new identity, parent recorded | resets to 0 |
| Two or more priors ≥ 20%, none ≥ 50% | **merger** | new identity, all recorded as parents with shares | resets to 0 |
| No prior ≥ 20% | **unmatched** | new identity, proximate parent unresolved | resets to 0 |

**Derivation** covers the middle case. Under half of `C`'s people came from the dominant prior, so calling it the same culture overstates continuity — but one ancestor is still identifiable, which is more than "unmatched" implies.

**Split rule.** The child holding the **largest share of the parent's population** retains the parent's name and its persistence counter; every other child is a new name with the parent recorded and its counter reset. Exact ties broken by lowest minimum cell index, so the outcome is deterministic.

**"Unmatched" never means "no ancestry."** Every population on the map descends from the founding culture, so the lineage tree is always connected at the root. Unmatched means only that *the matching rule could not resolve a proximate parent at this snapshot spacing* — typically because the cluster's people arrived from many scattered sources. Record it as unresolved, with the founder as ultimate ancestor. A chronicle must never render this as a people of unknown origin.

**Persistence follows identity.** A continuation inherits its parent's counter, which is what lets a long-lived culture accumulate the persistence needed for naming. Every other outcome creates a new identity and starts at zero — a merger is a new thing, not a continuation of its largest input.

The 50% and 20% floors stop a single-cell coincidence from carrying an identity forward. Both are scenario parameters and both need checking during provisional calibration.

**Exact alternative, if the approximation proves inadequate.** The obvious version — tracking each cell's ancestry fractions by *named culture* — is circular, since names are assigned post-run and don't exist while the run executes. The non-circular form is a **reference rerun**, defined as:

1. **First pass** runs normally and produces named cultures, each with the snapshot at which it was named and the cell set it occupied then.
2. **Labels are frozen.** No further naming occurs.
3. **Rerun with the identical seed.** Each cell carries an ancestry vector over the frozen labels, summing to 1. **Label injection happens at the snapshot where the first pass named that culture**, into exactly the cells it occupied: those cells' ancestry vectors are set to 1.0 for that label.
4. **Propagation.** Ancestry fractions are **invariant under growth and mortality** — both scale a cell's population uniformly and affect every ancestry equally, so the fractions do not move. They change **only through migration**, blended by the same population-weighted rule as the culture vector:
   `f_dest ← (P_res·f_res + Σₛ P_arr,ₛ·f_src,ₛ) / (P_res + Σₛ P_arr,ₛ)`

That growth and mortality leave the fractions untouched is what makes this cheap: one blend per migration event, nothing per tick.

Deterministic, exact, and needed for validation rather than production. One such rerun is required before §6 is settled.

The approximation must be labelled as such wherever lineage output is reported, so a chronicle never presents an inferred ancestry as a recorded one.

### Optional refinements (not in the base formula)

Reclassified deliberately: these are **not alternatives to Swadesh.** Swadesh sets the baseline decay rate; each of these modifies a different sub-decision, and the base model is complete without them. Both are worth exploring once the base model runs.

**1. Population-scaled drift — Wright (1943), isolation by distance.** Drift variance scales inversely with effective population size in the Wright–Fisher model. Applied here, `σ_eff = σ · sqrt(N_ref / P)`: small frontier settlements diverge fast while a crowded homeland stays conservative. Mechanistically real, and it would produce the intended Azhora pattern — peripheral peoples becoming distinct while Boueni persists at the core — with no pressure at all. *Default: off, constant σ.* The main cost is that Swadesh calibration then holds only at `P = N_ref`.

**2. Bounded-confidence cohesion — Deffuant (2000), Hegselmann–Krause (2002).** Cohesion applies only when `D_ij` is below a threshold; beyond it, neighbours stop blending. Produces stable sharp cultural borders instead of contact grinding everything toward homogeneity. *Default: off, cohesion always applies.* Worth turning on if base runs show cultures blurring together rather than holding boundaries.

### Parameters (scenario data)

| Symbol | Meaning | Proposed |
|---|---|---|
| `r` | Swadesh retention per millennium | 0.86 |
| `n` | culture vector dimensions | 8 |
| `σ` | drift per dimension per year | 0.004342 (derived from r, n) |
| `μ` | cohesion rate | to calibrate |
| `wᵢⱼ` | contact weight from travel cost | to author |
| — | naming persistence window | to calibrate |

---

## 7. Political state-transition rules

**Not yet authored — reserved.** Brief 5 requires rules for polity formation, expansion, conquest, and collapse, plus the stability function feeding `S` and `H_political` in §1.

Reserved here because **every Phase 1 state-transition rule is a locked invariant once settled**. There is no category of Phase 1 mechanic that stays permanently adjustable; leaving the polity rules outside this document would have created exactly that gap. They follow the same path as §6: authored by the project owner, calibrated against runs, then locked.

---

## Parameters (scenario data, not invariants)

These live in the scenario config and may be tuned freely in Phase 1 — they are not pressure, since they are global rather than regional or temporal.

| Symbol | Meaning | Note |
|---|---|---|
| `r_max` | intrinsic growth rate | 0.005–0.03 suggested; hard bound < 0.5 |
| `K_base[terrain, climate]` | base capacity table | terrain × climate, not terrain alone |
| `W_base[context]` | water-context multipliers | river / coast / lake; `W(∅) = 1` |
| `W_override[combination]` | named combination overrides | consulted before the max rule |
| river movement costs | along-river and across-river costs, feeding `M_base` | |
| `P_min` | extinction floor | |
| `θ` | emigration threshold, fraction of K | |
| `φ` | emigrant fraction | |
| `P₀`, seed cell | founding population and location | initial state |

**A parameter becomes pressure when its variation is *authored*, not when it merely varies.** Technology, infrastructure, disease, stability, culture and capacity all differ by cell and year because the simulation computed them — that is the model working. A coefficient typed in with one value for the north and another for the south, or changed by hand at year 500, is pressure. The test is provenance, not variance. See AGENTS.md for the full statement.

**Machinery is code; coefficients are data.** The formulas in this document are implemented in an engine module, not loaded from a file — an invariant is not a data file. What lives in scenario configuration is the numbers: the table below, the `K_base` and `W_base` tables, the contact-weight mapping.

---

## Run cohorts

Two different things can invalidate a comparison, and conflating them makes both records useless.

| | What changed | Consequence | Recorded in |
|---|---|---|---|
| **Invariant change** | a formula in this document | every earlier run is invalid as evidence about the mechanics | DEFECTS.md |
| **New cohort** | scenario data — `τ`, `r_max`, `θ`, `φ`, `P_min`, the `K_eco` table, the map | earlier runs remain valid; they simply belong to a different cohort | cohort registry |

**Runs are comparable within a cohort, never across cohorts.** A cohort is identified by the full run identity below, minus the seed. Changing τ opens a new cohort; it is *not* an invariant edit, because no formula moved.

### Run identity — what reproducibility actually requires

A seed alone does not identify a run. Reproduction requires **all** of:

- seed
- code version (commit hash)
- scenario configuration (all coefficients, including τ)
- source-map hash and schema version
- **dependency lock** (`uv.lock`)
- **`working_tree`** — `"clean"`, or `"dirty"` plus **`dirty_fingerprint`**: SHA-256 over `git diff HEAD` **concatenated with the contents of every untracked non-ignored file**, in `git status --porcelain` order. Untracked content must be included or a dirty fingerprint misses newly added source files entirely.

**A dirty run may never be cited as a run of record.** Exploration is expected and permitted; citation is not. The repository must also carry at least one commit before any run — `git rev-parse HEAD` does not resolve in an empty repository.

The dependency lock is load-bearing, not hygiene. **NumPy's `Generator` carries no stream-compatibility guarantee across NumPy versions** — NEP 19 deliberately dropped it so distribution algorithms could be improved, and the documented advice is that anyone needing exact reproduction should use the same NumPy version. A NumPy upgrade can therefore silently change every result while every seed stays the same. `BitGenerator` classes carry stronger guarantees than `Generator` methods, which is worth knowing if this ever becomes a real problem.

Every run writes its full identity into the `.npz` metadata. A run whose identity cannot be reconstructed from its own output is not evidence.

## What is locked

| Sections | State |
|---|---|
| §§1–5 growth and survival, capacity, shocks, extinction, frontier expansion | **locked** |
| §6 cultural divergence | not settled — see the section itself, and the outstanding items below |
| §7 political state transitions | reserved, not authored |

Any edit to a locked section is an invariant change: log it in DEFECTS.md, state what changed, and treat every earlier run as invalid for comparison. This document describes the design as it stands, not how it got here.

### §6 — outstanding, authorable now

No runs needed.

- [ ] Contact-weight function `wᵢⱼ` authored (travel cost → weight, including river along/across behaviour)
- [ ] River effect on capacity and movement decided (§2)
- [ ] Maximum political contact bonus declared, so the worst-case μ bound can be computed
- [ ] Naming persistence window (how many consecutive 25-year snapshots)

### §6 — outstanding, requires runs

Each can only be answered by calibration runs.

- [ ] `μ` calibrated, satisfying the worst-case bound
- [ ] σ-to-μ ratio producing a qualifying threshold plateau in the admissible range
- [ ] **Map-resolution dependence measured** — same geography at ≥2 hex resolutions, difference recorded
- [ ] Lineage approximation checked against at least one ancestry-flow reference run

### §§2–3 — outstanding before the reserved factors switch on

- [ ] Disease model authored: how `H` is computed, and whether it is density-dependent (crowding raising epidemic probability)
- [ ] Technology and infrastructure accumulation rules
