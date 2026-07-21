"""Brief 1's synchronous population-growth and frontier-expansion loop."""

from __future__ import annotations

import hashlib
import json
from collections import defaultdict
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from types import MappingProxyType
from typing import Any

import numpy as np
from numpy.typing import ArrayLike, NDArray

from clio.invariants import effective_movement, emigration, extinction_floor, grow

FloatArray = NDArray[np.float64]
BoolArray = NDArray[np.bool_]
IntArray = NDArray[np.int32]
Event = dict[str, Any]


@dataclass(frozen=True, slots=True)
class World:
    """Static cell arrays consumed by the simulation loop."""

    coords: ArrayLike
    terrain: ArrayLike
    climate: ArrayLike
    k_eco: ArrayLike
    passable: ArrayLike
    neighbor: ArrayLike
    m_base: ArrayLike
    reachable: ArrayLike
    fixture_schema: str = "synthetic/1"
    fixture_sha256: str = ""
    terrain_map: Mapping[str, int] = field(default_factory=dict)
    climate_map: Mapping[str, int] = field(default_factory=dict)
    require_symmetric_movement: bool = True

    def __post_init__(self) -> None:
        arrays: dict[str, np.ndarray[Any, Any]] = {
            "coords": np.asarray(self.coords, dtype=np.int16).copy(),
            "terrain": np.asarray(self.terrain, dtype=np.int8).copy(),
            "climate": np.asarray(self.climate, dtype=np.int8).copy(),
            "k_eco": np.asarray(self.k_eco, dtype=np.float64).copy(),
            "passable": np.asarray(self.passable, dtype=np.bool_).copy(),
            "neighbor": np.asarray(self.neighbor, dtype=np.int32).copy(),
            "m_base": np.asarray(self.m_base, dtype=np.float64).copy(),
            "reachable": np.asarray(self.reachable, dtype=np.bool_).copy(),
        }
        _validate_world_arrays(arrays, require_symmetric=self.require_symmetric_movement)
        for name, array in arrays.items():
            array.flags.writeable = False
            object.__setattr__(self, name, array)
        object.__setattr__(self, "terrain_map", MappingProxyType(dict(self.terrain_map)))
        object.__setattr__(self, "climate_map", MappingProxyType(dict(self.climate_map)))

    @property
    def cells(self) -> int:
        return int(self.terrain.shape[0])


@dataclass(frozen=True, slots=True)
class Scenario:
    """Global scenario coefficients and initial state for one run cohort."""

    r_max: float
    theta: float
    phi: float
    p_min: float
    p0: float
    seed_cell: int
    horizon_t: int

    def __post_init__(self) -> None:
        if not np.isfinite(self.r_max) or not 0.0 < self.r_max < 0.5:
            raise ValueError("r_max must satisfy 0 < r_max < 0.5")
        if not np.isfinite(self.theta) or not 0.0 < self.theta < 1.0:
            raise ValueError("theta must satisfy 0 < theta < 1")
        if not np.isfinite(self.phi) or not 0.0 < self.phi < 1.0:
            raise ValueError("phi must satisfy 0 < phi < 1")
        if not np.isfinite(self.p_min) or self.p_min <= 0.0:
            raise ValueError("P_min must be finite and positive")
        if not np.isfinite(self.p0) or self.p0 < self.p_min:
            raise ValueError("P0 must be finite and at least P_min")
        if isinstance(self.seed_cell, bool) or not isinstance(self.seed_cell, int):
            raise TypeError("seed_cell must be an integer")
        if isinstance(self.horizon_t, bool) or not isinstance(self.horizon_t, int):
            raise TypeError("horizon_t must be an integer")
        if self.horizon_t <= 0:
            raise ValueError("horizon_t must be positive")

    def identity_values(self) -> dict[str, float | int]:
        return {
            "r_max": self.r_max,
            "theta": self.theta,
            "phi": self.phi,
            "P_min": self.p_min,
            "P0": self.p0,
            "seed_cell": self.seed_cell,
            "horizon_T": self.horizon_t,
        }


REFERENCE_SCENARIO = Scenario(
    r_max=0.03,
    theta=0.6,
    phi=0.30,
    p_min=10.0,
    p0=60.0,
    seed_cell=189,
    horizon_t=1000,
)


@dataclass(frozen=True, slots=True)
class SimulationState:
    population: FloatArray
    settled: BoolArray


@dataclass(frozen=True, slots=True, order=True)
class Intention:
    source: int
    destination: int
    population: float


@dataclass(frozen=True, slots=True)
class DestinationArrivals:
    destination: int
    arrivals: tuple[Intention, ...]
    total: float


@dataclass(frozen=True, slots=True)
class MigrationPlan:
    eligible_sources: tuple[int, ...]
    intentions: tuple[Intention, ...]
    accepted: tuple[Intention, ...]
    rejected: tuple[Intention, ...]
    accepted_groups: tuple[DestinationArrivals, ...]
    refused_destinations: tuple[int, ...]


@dataclass(frozen=True, slots=True)
class TickResult:
    state: SimulationState
    events: tuple[Event, ...]
    migration: MigrationPlan
    conservation_error: float | None


@dataclass(frozen=True, slots=True)
class SimulationResult:
    seed: int
    scenario: Scenario
    population: FloatArray
    settled: BoolArray
    first_settlement_tick: IntArray
    events: tuple[Event, ...]
    summary: Mapping[str, Any]
    run_identity: Mapping[str, Any]
    conservation_errors: FloatArray


def load_world_fixture(npz_path: Path | str, sidecar_path: Path | str) -> World:
    """Load and validate a frozen toy-map fixture without permitting pickle."""

    npz_path = Path(npz_path)
    sidecar_path = Path(sidecar_path)
    sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
    digest = _sha256_file(npz_path)
    if digest != sidecar.get("sha256"):
        raise ValueError("fixture SHA-256 does not match its sidecar")
    if sidecar.get("fixture_schema") != "clio.toymap/1":
        raise ValueError("unsupported fixture schema")

    with np.load(npz_path, allow_pickle=False) as archive:
        members = set(archive.files)
        # The frozen v1 bytes predate the runtime field-name lock and contain
        # ``M``.  Preserve their hash and normalize that sole compatibility
        # spelling to ``m_base`` at this loader boundary.
        movement_keys = members.intersection({"M", "M_base"})
        if len(movement_keys) != 1:
            raise ValueError("fixture must contain exactly one of M or M_base")
        movement_key = movement_keys.pop()
        required = {
            "coords",
            "terrain",
            "climate",
            "K_eco",
            "passable",
            "neighbor",
            "reachable",
            movement_key,
        }
        if members != required:
            missing = sorted(required - members)
            extra = sorted(members - required)
            raise ValueError(
                f"fixture members differ from schema: missing={missing}, extra={extra}"
            )
        _validate_fixture_dtypes(archive, movement_key)
        arrays = {name: archive[name].copy() for name in archive.files}

    world = World(
        coords=arrays["coords"],
        terrain=arrays["terrain"],
        climate=arrays["climate"],
        k_eco=arrays["K_eco"],
        passable=arrays["passable"],
        neighbor=arrays["neighbor"],
        m_base=arrays[movement_key],
        reachable=arrays["reachable"],
        fixture_schema=sidecar["fixture_schema"],
        fixture_sha256=digest,
        terrain_map=sidecar["terrain_map"],
        climate_map=sidecar["climate_map"],
        require_symmetric_movement=True,
    )
    if world.cells != int(sidecar["cells"]):
        raise ValueError("fixture cell count does not match sidecar")
    _validate_fixture_geometry(world, sidecar)
    fixture_seed = sidecar.get("seed_cell")
    if isinstance(fixture_seed, bool) or not isinstance(fixture_seed, int):
        raise ValueError("fixture seed_cell must be an integer")
    if not 0 <= fixture_seed < world.cells or not world.passable[fixture_seed]:
        raise ValueError("fixture seed_cell must identify a passable cell")
    if not np.array_equal(world.reachable, reachable_from_seed(world, fixture_seed)):
        raise ValueError("fixture reachable mask does not match its seed and M_base graph")
    return world


def initial_state(world: World, scenario: Scenario) -> SimulationState:
    _validate_scenario_for_world(world, scenario)
    population = np.zeros(world.cells, dtype=np.float64)
    settled = np.zeros(world.cells, dtype=np.bool_)
    population[scenario.seed_cell] = scenario.p0
    settled[scenario.seed_cell] = True
    return SimulationState(population=population, settled=settled)


def weighted_sample(
    weights: ArrayLike,
    rng: np.random.Generator,
    draws: int,
) -> NDArray[np.int64]:
    """Draw indices from positive finite weights with one uniform vector."""

    if isinstance(draws, bool) or not isinstance(draws, int) or draws < 0:
        raise ValueError("draws must be a non-negative integer")
    weight_array = _validate_weights(weights)
    uniforms = rng.random(draws)
    cumulative = np.cumsum(weight_array, dtype=np.float64)
    return np.searchsorted(cumulative, uniforms * cumulative[-1], side="right")


def plan_migration(
    population: ArrayLike,
    settled_at_t: ArrayLike,
    world: World,
    capacity: ArrayLike,
    theta: float,
    phi: float,
    p_min: float,
    rng: np.random.Generator,
    *,
    m_eff: ArrayLike | None = None,
    source_order: Sequence[int] | None = None,
) -> MigrationPlan:
    """Create, aggregate, and viability-filter frozen migration intentions."""

    population_array = np.asarray(population, dtype=np.float64)
    settled_array = np.asarray(settled_at_t, dtype=np.bool_)
    capacity_array = np.asarray(capacity, dtype=np.float64)
    _validate_state_shapes(world, population_array, settled_array)
    if capacity_array.shape != (world.cells,):
        raise ValueError("capacity must have shape (cells,)")
    if not np.all(np.isfinite(capacity_array)) or np.any(capacity_array < 0.0):
        raise ValueError("capacity must be finite and non-negative")
    if not np.isfinite(theta) or not 0.0 < theta < 1.0:
        raise ValueError("theta must satisfy 0 < theta < 1")
    if not np.isfinite(phi) or not 0.0 < phi < 1.0:
        raise ValueError("phi must satisfy 0 < phi < 1")
    if not np.isfinite(p_min) or p_min <= 0.0:
        raise ValueError("P_min must be finite and positive")

    effective = world.m_base if m_eff is None else np.asarray(m_eff, dtype=np.float64)
    if effective.shape != world.m_base.shape:
        raise ValueError("M_eff must have shape (cells, 6)")
    if not np.all(np.isfinite(effective)) or np.any((effective < 0.0) | (effective > 1.0)):
        raise ValueError("M_eff must be finite and within [0, 1]")

    eligible = _eligible_matrix(world, settled_array)
    source_mask = settled_array & (capacity_array > 0.0)
    source_mask &= population_array > theta * capacity_array
    source_mask &= np.any(eligible, axis=1)
    canonical_sources = np.flatnonzero(source_mask).astype(np.int64, copy=False)
    uniforms = rng.random(canonical_sources.size)
    uniform_by_source = dict(zip(canonical_sources.tolist(), uniforms.tolist(), strict=True))

    if source_order is None:
        processing_sources = canonical_sources.tolist()
    else:
        processing_sources = [int(source) for source in source_order]
        if sorted(processing_sources) != canonical_sources.tolist():
            raise ValueError("source_order must be a permutation of eligible source cells")

    amounts = np.zeros(world.cells, dtype=np.float64)
    if canonical_sources.size:
        amounts[canonical_sources] = emigration(
            population_array[canonical_sources],
            capacity_array[canonical_sources],
            theta,
            phi,
        )

    intentions: list[Intention] = []
    for source in processing_sources:
        directions = np.flatnonzero(eligible[source])
        candidates = world.neighbor[source, directions]
        order = np.argsort(candidates, kind="stable")
        candidates = candidates[order]
        directions = directions[order]
        weights = world.k_eco[candidates] * effective[source, directions]
        weight_array = _validate_weights(weights)
        cumulative = np.cumsum(weight_array, dtype=np.float64)
        draw = uniform_by_source[source] * cumulative[-1]
        chosen = int(np.searchsorted(cumulative, draw, side="right"))
        intentions.append(
            Intention(
                source=source,
                destination=int(candidates[chosen]),
                population=float(amounts[source]),
            )
        )

    canonical_intentions = tuple(sorted(intentions, key=lambda item: item.source))
    accepted, rejected, groups, refused = filter_viable_intentions(canonical_intentions, p_min)
    eligible_sources = tuple(int(source) for source in canonical_sources)
    classified_sources = {item.source for item in accepted}.union(item.source for item in rejected)
    if classified_sources != set(eligible_sources):
        raise AssertionError("an eligible, viable source stalled without classification")
    return MigrationPlan(
        eligible_sources=eligible_sources,
        intentions=canonical_intentions,
        accepted=accepted,
        rejected=rejected,
        accepted_groups=groups,
        refused_destinations=refused,
    )


def filter_viable_intentions(
    intentions: Iterable[Intention],
    p_min: float,
) -> tuple[
    tuple[Intention, ...],
    tuple[Intention, ...],
    tuple[DestinationArrivals, ...],
    tuple[int, ...],
]:
    """Aggregate in canonical order, then reject sub-floor destination groups."""

    if not np.isfinite(p_min) or p_min <= 0.0:
        raise ValueError("P_min must be finite and positive")
    by_destination: dict[int, list[Intention]] = defaultdict(list)
    for intention in intentions:
        if not np.isfinite(intention.population) or intention.population <= 0.0:
            raise ValueError("intention populations must be finite and positive")
        by_destination[intention.destination].append(intention)

    accepted: list[Intention] = []
    rejected: list[Intention] = []
    groups: list[DestinationArrivals] = []
    refused: list[int] = []
    for destination in sorted(by_destination):
        arrivals = tuple(sorted(by_destination[destination], key=lambda item: item.source))
        total = 0.0
        for arrival in arrivals:
            total += arrival.population
        if total < p_min:
            rejected.extend(arrivals)
            refused.append(destination)
        else:
            accepted.extend(arrivals)
            groups.append(
                DestinationArrivals(
                    destination=destination,
                    arrivals=arrivals,
                    total=total,
                )
            )
    return (
        tuple(sorted(accepted, key=lambda item: item.source)),
        tuple(sorted(rejected, key=lambda item: item.source)),
        tuple(groups),
        tuple(refused),
    )


def resolve_migration(
    population: ArrayLike,
    settled: ArrayLike,
    plan: MigrationPlan,
    tick: int,
) -> tuple[SimulationState, tuple[Event, ...]]:
    """Apply accepted flows simultaneously and emit settlement events."""

    next_population = np.asarray(population, dtype=np.float64).copy()
    next_settled = np.asarray(settled, dtype=np.bool_).copy()
    if next_population.shape != next_settled.shape:
        raise ValueError("population and settled must have identical shapes")

    for intention in plan.accepted:
        next_population[intention.source] -= intention.population
        if next_population[intention.source] < 0.0:
            raise AssertionError("migration subtracted more than the source population")

    events: list[Event] = []
    for group in plan.accepted_groups:
        destination = group.destination
        if next_settled[destination]:
            raise AssertionError("frontier migration targeted a settled destination")
        next_population[destination] += group.total
        next_settled[destination] = True
        events.append(
            {
                "tick": tick,
                "event": "cell_settled",
                "cell": destination,
                "arrivals": [
                    {"source": arrival.source, "population": arrival.population}
                    for arrival in group.arrivals
                ],
                "population": float(next_population[destination]),
            }
        )
    return SimulationState(next_population, next_settled), tuple(events)


def step(
    state: SimulationState,
    world: World,
    scenario: Scenario,
    expansion_rng: np.random.Generator,
    tick: int,
    *,
    source_order: Sequence[int] | None = None,
) -> TickResult:
    """Advance one tick through stages A-D."""

    population = np.asarray(state.population, dtype=np.float64)
    settled = np.asarray(state.settled, dtype=np.bool_)
    _validate_state_shapes(world, population, settled)
    capacity = world.k_eco

    population_a = population.copy()
    clamp_events: list[Event] = []
    positive_capacity = capacity > 0.0
    if np.any(positive_capacity):
        grown, raw, clamped = grow(
            population[positive_capacity],
            capacity[positive_capacity],
            scenario.r_max,
        )
        population_a[positive_capacity] = grown
        cells = np.flatnonzero(positive_capacity)
        for local_index in np.flatnonzero(clamped):
            cell = int(cells[local_index])
            clamp_events.append(
                {
                    "tick": tick,
                    "event": "clamp_negative",
                    "cell": cell,
                    "population_before": float(population[cell]),
                    "raw_population": float(raw[local_index]),
                    "capacity": float(capacity[cell]),
                }
            )

    population_b, settled_b, abandoned = extinction_floor(
        population_a,
        settled,
        scenario.p_min,
    )
    abandonment_events = [
        {
            "tick": tick,
            "event": "cell_abandoned",
            "cell": int(cell),
            "population_before": float(population_a[cell]),
        }
        for cell in np.flatnonzero(abandoned)
    ]

    m_eff = effective_movement(world.m_base)
    migration = plan_migration(
        population_b,
        settled,
        world,
        capacity,
        scenario.theta,
        scenario.phi,
        scenario.p_min,
        expansion_rng,
        m_eff=m_eff,
        source_order=source_order,
    )
    next_state, settlement_events = resolve_migration(
        population_b,
        settled_b,
        migration,
        tick,
    )

    before_total = float(np.sum(population_b, dtype=np.float64))
    after_total = float(np.sum(next_state.population, dtype=np.float64))
    conservation_error: float | None
    if before_total == 0.0:
        conservation_error = None
    else:
        conservation_error = abs(after_total - before_total) / before_total
        if conservation_error > 1e-12:
            raise AssertionError(f"migration conservation error {conservation_error} exceeds 1e-12")

    events = clamp_events + abandonment_events + list(settlement_events)
    events.sort(key=_event_key)
    return TickResult(
        state=next_state,
        events=tuple(events),
        migration=migration,
        conservation_error=conservation_error,
    )


def run_simulation(
    world: World,
    scenario: Scenario,
    seed: int,
    run_identity: Mapping[str, Any],
) -> SimulationResult:
    """Run the complete Brief 1 loop and retain float64 internal snapshots."""

    _validate_scenario_for_world(world, scenario)
    if isinstance(seed, bool) or not isinstance(seed, int):
        raise TypeError("seed must be an integer")
    identity = validate_run_identity(run_identity, world, scenario, seed)

    root_rng = np.random.default_rng(seed)
    expansion_rng = root_rng.spawn(1)[0]
    state = initial_state(world, scenario)
    population_history = np.empty((scenario.horizon_t + 1, world.cells), dtype=np.float64)
    settled_history = np.empty((scenario.horizon_t + 1, world.cells), dtype=np.bool_)
    population_history[0] = state.population
    settled_history[0] = state.settled
    first_settlement = np.full(world.cells, -1, dtype=np.int32)
    first_settlement[state.settled] = 0
    events: list[Event] = [
        {
            "tick": 0,
            "event": "run_start",
            "cell": -1,
            "run_identity": identity,
        }
    ]
    rejected_intentions = 0
    conservation_errors = np.zeros(scenario.horizon_t, dtype=np.float64)

    for tick in range(1, scenario.horizon_t + 1):
        tick_result = step(state, world, scenario, expansion_rng, tick)
        state = tick_result.state
        population_history[tick] = state.population
        settled_history[tick] = state.settled
        rejected_intentions += len(tick_result.migration.rejected)
        conservation_errors[tick - 1] = tick_result.conservation_error or 0.0
        for event in tick_result.events:
            if event["event"] == "cell_settled" and first_settlement[event["cell"]] < 0:
                first_settlement[event["cell"]] = tick
        events.extend(tick_result.events)

    summary = summarize_run(
        population_history,
        settled_history,
        first_settlement,
        events,
        rejected_intentions,
        world,
        scenario,
    )
    events.append(
        {
            "tick": scenario.horizon_t,
            "event": "run_end",
            "cell": -1,
            **summary,
        }
    )
    events.sort(key=_event_key)
    return SimulationResult(
        seed=seed,
        scenario=scenario,
        population=population_history,
        settled=settled_history,
        first_settlement_tick=first_settlement,
        events=tuple(events),
        summary=MappingProxyType(summary),
        run_identity=MappingProxyType(identity),
        conservation_errors=conservation_errors,
    )


def validate_run_identity(
    run_identity: Mapping[str, Any],
    world: World,
    scenario: Scenario,
    seed: int,
) -> dict[str, Any]:
    """Validate and detach the complete identity bound to a run."""

    if not isinstance(run_identity, Mapping):
        raise TypeError("run_identity must be a mapping")
    try:
        encoded = json.dumps(
            dict(run_identity),
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    except (TypeError, ValueError) as error:
        raise ValueError("run_identity must be finite, JSON-serializable data") from error
    identity = json.loads(encoded)

    required = {
        "identity_schema",
        "seed",
        "commit",
        "working_tree",
        "dirty_fingerprint",
        "scenario",
        "fixture",
        "dependency_lock_sha256",
        "terrain_map",
        "climate_map",
    }
    missing = sorted(required.difference(identity))
    if missing:
        raise ValueError(f"run_identity is missing required fields: {missing}")
    if identity["identity_schema"] != "clio.run_identity/1":
        raise ValueError("unsupported run identity schema")
    if (
        isinstance(identity["seed"], bool)
        or not isinstance(identity["seed"], int)
        or identity["seed"] != seed
    ):
        raise ValueError("run identity seed does not match the run seed")
    if identity["scenario"] != scenario.identity_values():
        raise ValueError("run identity scenario does not match the run scenario")
    expected_fixture = {"schema": world.fixture_schema, "sha256": world.fixture_sha256}
    if identity["fixture"] != expected_fixture:
        raise ValueError("run identity fixture does not match the run world")
    if identity["terrain_map"] != dict(world.terrain_map):
        raise ValueError("run identity terrain map does not match the run world")
    if identity["climate_map"] != dict(world.climate_map):
        raise ValueError("run identity climate map does not match the run world")

    _validate_hex_digest(identity["commit"], "commit", lengths=(40, 64))
    _validate_hex_digest(identity["dependency_lock_sha256"], "dependency lock hash")
    _validate_hex_digest(world.fixture_sha256, "fixture hash")
    working_tree = identity["working_tree"]
    fingerprint = identity["dirty_fingerprint"]
    if working_tree == "clean":
        if fingerprint is not None:
            raise ValueError("a clean run identity must have a null dirty fingerprint")
    elif working_tree == "dirty":
        _validate_hex_digest(fingerprint, "dirty fingerprint")
    else:
        raise ValueError("working_tree must be 'clean' or 'dirty'")
    return identity


def summarize_run(
    population: FloatArray,
    settled: BoolArray,
    first_settlement_tick: IntArray,
    events: Sequence[Event],
    rejected_intentions: int,
    world: World,
    scenario: Scenario,
) -> dict[str, Any]:
    """Compute every per-seed statistic required by Brief 1."""

    final_settled = settled[-1]
    first_expansions = first_settlement_tick[first_settlement_tick > 0]
    totals = np.sum(population, axis=1, dtype=np.float64)
    extinction_indices = np.flatnonzero(totals == 0.0)
    extinction_tick = int(extinction_indices[0]) if extinction_indices.size else None
    plateau_tick = (
        None if extinction_tick is not None else _plateau_tick(totals, scenario.horizon_t)
    )
    reachable = reachable_from_seed(world, scenario.seed_cell)
    reachable_count = int(np.count_nonzero(reachable))
    passable_count = int(np.count_nonzero(world.passable))
    event_names = [str(event["event"]) for event in events]
    unable = world.passable & (scenario.phi * world.k_eco < scenario.p_min)

    return {
        "final_total_population": float(totals[-1]),
        "cells_settled": int(np.count_nonzero(final_settled)),
        "settlement_fraction_reachable": (
            float(np.count_nonzero(final_settled & reachable) / reachable_count)
            if reachable_count
            else 0.0
        ),
        "settlement_fraction_passable": (
            float(np.count_nonzero(final_settled & world.passable) / passable_count)
            if passable_count
            else 0.0
        ),
        "tick_of_first_expansion": (
            int(np.min(first_expansions)) if first_expansions.size else None
        ),
        "last_frontier_advance_tick": (
            int(np.max(first_expansions)) if first_expansions.size else None
        ),
        "plateau_tick": plateau_tick,
        "extinction_tick": extinction_tick,
        "cell_abandoned_count": event_names.count("cell_abandoned"),
        "clamp_negative_count": event_names.count("clamp_negative"),
        "rejected_source_intentions_count": int(rejected_intentions),
        "sources_unable_to_found_alone_from_equilibrium_count": int(np.count_nonzero(unable)),
    }


def reachable_from_seed(world: World, seed_cell: int) -> BoolArray:
    """Compute directed geographic reachability over positive M_base edges."""

    if isinstance(seed_cell, bool) or not isinstance(seed_cell, int):
        raise TypeError("seed_cell must be an integer")
    if not 0 <= seed_cell < world.cells:
        raise ValueError("seed_cell is outside the world")
    reachable = np.zeros(world.cells, dtype=np.bool_)
    if not world.passable[seed_cell]:
        return reachable
    reachable[seed_cell] = True
    frontier = [seed_cell]
    while frontier:
        source = frontier.pop()
        directions = np.flatnonzero(world.m_base[source] > 0.0)
        for target in world.neighbor[source, directions]:
            cell = int(target)
            if not reachable[cell]:
                reachable[cell] = True
                frontier.append(cell)
    return reachable


def _plateau_tick(totals: FloatArray, horizon_t: int) -> int | None:
    if horizon_t < 50 or np.any(totals[:-1] == 0.0):
        return None
    relative = np.abs(np.diff(totals)) / totals[:-1]
    suffix_max = np.maximum.accumulate(relative[::-1])[::-1]
    candidates = np.flatnonzero(suffix_max[: horizon_t - 49] < 0.001)
    return int(candidates[0]) if candidates.size else None


def _eligible_matrix(world: World, settled_at_t: BoolArray) -> BoolArray:
    valid = world.neighbor >= 0
    targets = np.where(valid, world.neighbor, 0)
    return np.asarray(
        valid
        & (~settled_at_t[targets])
        & world.passable[targets]
        & (world.k_eco[targets] > 0.0)
        & (world.m_base > 0.0),
        dtype=np.bool_,
    )


def _validate_world_arrays(
    arrays: Mapping[str, np.ndarray[Any, Any]],
    *,
    require_symmetric: bool,
) -> None:
    cells = int(arrays["terrain"].shape[0]) if arrays["terrain"].ndim == 1 else -1
    if cells <= 0:
        raise ValueError("world must contain at least one cell")
    one_dimensional = ("terrain", "climate", "k_eco", "passable", "reachable")
    if any(arrays[name].shape != (cells,) for name in one_dimensional):
        raise ValueError("per-cell arrays must all have shape (cells,)")
    if arrays["coords"].shape != (cells, 2):
        raise ValueError("coords must have shape (cells, 2)")
    if arrays["neighbor"].shape != (cells, 6) or arrays["m_base"].shape != (cells, 6):
        raise ValueError("neighbor and M_base must have shape (cells, 6)")
    if not np.all(np.isfinite(arrays["k_eco"])) or np.any(arrays["k_eco"] < 0.0):
        raise ValueError("K_eco must be finite and non-negative")
    if np.any(arrays["reachable"] & ~arrays["passable"]):
        raise ValueError("reachable cells must be passable")
    m_base = arrays["m_base"]
    if not np.all(np.isfinite(m_base)) or np.any((m_base < 0.0) | (m_base > 1.0)):
        raise ValueError("M_base must be finite and within [0, 1]")
    neighbor = arrays["neighbor"]
    if np.any((neighbor < -1) | (neighbor >= cells)):
        raise ValueError("neighbor contains an invalid cell index")
    if np.any((neighbor == -1) & (m_base != 0.0)):
        raise ValueError("off-map neighbor slots must have M_base = 0")
    if np.any((m_base > 0.0) & (neighbor < 0)):
        raise ValueError("positive M_base must point to a valid neighbor")
    for cell in range(cells):
        live_neighbors = neighbor[cell, neighbor[cell] >= 0]
        if np.unique(live_neighbors).size != live_neighbors.size:
            raise ValueError("a neighbor row contains duplicate cell indices")
        for direction in np.flatnonzero(m_base[cell] > 0.0):
            target = int(neighbor[cell, direction])
            if not arrays["passable"][cell] or not arrays["passable"][target]:
                raise ValueError("positive M_base is allowed only on passable-to-passable edges")
            if require_symmetric:
                reverse = np.flatnonzero(neighbor[target] == cell)
                if reverse.size != 1 or m_base[target, reverse[0]] != m_base[cell, direction]:
                    raise ValueError("Brief 1 movement accessibility must be symmetric")


def _validate_fixture_dtypes(archive: np.lib.npyio.NpzFile, movement_key: str) -> None:
    expected = {
        "coords": np.dtype(np.int16),
        "terrain": np.dtype(np.int8),
        "climate": np.dtype(np.int8),
        "K_eco": np.dtype(np.float32),
        "passable": np.dtype(np.bool_),
        "neighbor": np.dtype(np.int32),
        movement_key: np.dtype(np.float32),
        "reachable": np.dtype(np.bool_),
    }
    for name, dtype in expected.items():
        if archive[name].dtype != dtype:
            raise ValueError(
                f"fixture array {name} has dtype {archive[name].dtype}, expected {dtype}"
            )


def _validate_fixture_geometry(world: World, sidecar: Mapping[str, Any]) -> None:
    if list(sidecar["shape"]) != [20, 18]:
        raise ValueError("clio.toymap/1 must have shape [20, 18]")
    directions = np.asarray(sidecar["direction_order"], dtype=np.int16)
    if directions.shape != (6, 2):
        raise ValueError("fixture direction order must contain six axial offsets")
    coord_to_cell = {tuple(coord): cell for cell, coord in enumerate(world.coords.tolist())}
    if len(coord_to_cell) != world.cells:
        raise ValueError("fixture coordinates are not unique")
    for cell, coord in enumerate(world.coords):
        for direction, offset in enumerate(directions):
            expected = coord_to_cell.get(tuple((coord + offset).tolist()), -1)
            if int(world.neighbor[cell, direction]) != expected:
                raise ValueError("neighbor array does not match coordinates and direction order")


def _validate_state_shapes(world: World, population: FloatArray, settled: BoolArray) -> None:
    if population.shape != (world.cells,) or settled.shape != (world.cells,):
        raise ValueError("state arrays must have shape (cells,)")
    if not np.all(np.isfinite(population)) or np.any(population < 0.0):
        raise ValueError("population must be finite and non-negative")


def _validate_scenario_for_world(world: World, scenario: Scenario) -> None:
    if not 0 <= scenario.seed_cell < world.cells:
        raise ValueError("seed_cell is outside the world")
    if not world.passable[scenario.seed_cell] or world.k_eco[scenario.seed_cell] <= 0.0:
        raise ValueError("seed_cell must be passable with positive capacity")


def _validate_weights(weights: ArrayLike) -> FloatArray:
    weight_array = np.asarray(weights, dtype=np.float64)
    if weight_array.ndim != 1 or weight_array.size == 0:
        raise ValueError("weights must be a non-empty one-dimensional array")
    if not np.all(np.isfinite(weight_array)) or np.any(weight_array < 0.0):
        raise ValueError("weights must be finite and non-negative")
    if not np.any(weight_array > 0.0):
        raise ValueError("at least one weight must be positive")
    total = float(np.sum(weight_array, dtype=np.float64))
    if not np.isfinite(total) or total <= 0.0:
        raise ValueError("weight sum must be finite and positive")
    return weight_array


def _validate_hex_digest(
    value: object,
    name: str,
    *,
    lengths: tuple[int, ...] = (64,),
) -> None:
    if (
        not isinstance(value, str)
        or len(value) not in lengths
        or any(character not in "0123456789abcdef" for character in value.lower())
    ):
        expected = " or ".join(str(length) for length in lengths)
        raise ValueError(f"{name} must be a {expected}-character hexadecimal digest")


def _event_key(event: Mapping[str, Any]) -> tuple[int, str, int]:
    return int(event["tick"]), str(event["event"]), int(event["cell"])


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()
