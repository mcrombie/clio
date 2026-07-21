from __future__ import annotations

from dataclasses import replace

import numpy as np
import pytest

from clio.harness import project_root
from clio.logging import build_run_identity
from clio.loop import (
    REFERENCE_SCENARIO,
    Intention,
    Scenario,
    SimulationState,
    World,
    filter_viable_intentions,
    initial_state,
    plan_migration,
    reachable_from_seed,
    resolve_migration,
    run_simulation,
    step,
    summarize_run,
    weighted_sample,
)
from conftest import graph_world


def _chi_square(observed: np.ndarray, expected: np.ndarray) -> float:
    return float(np.sum((observed - expected) ** 2 / expected))


def test_frozen_fixture_contract(reference_world) -> None:
    world = reference_world
    assert world.fixture_schema == "clio.toymap/1"
    assert (
        world.fixture_sha256 == "65e82fb5eea2507dc28bc4630e400c2215cb5887f59b6b98693845d86a1af77b"
    )
    assert world.cells == 360
    np.testing.assert_array_equal(np.bincount(world.terrain), [165, 83, 39, 35, 38])
    np.testing.assert_array_equal(np.bincount(world.climate), [240, 60, 60])
    assert np.count_nonzero(world.passable) == 322
    assert np.count_nonzero(world.reachable) == 322
    assert float(np.sum(world.k_eco)) == 23632.0
    np.testing.assert_array_equal(world.coords[189], [10, 9])
    assert world.k_eco[189] == 80.0
    assert np.count_nonzero(world.m_base[189]) == 6
    with np.load(project_root() / "fixtures" / "brief1_toymap_v1.npz", allow_pickle=False) as raw:
        assert "M" in raw.files
        assert "M_base" not in raw.files
        np.testing.assert_array_equal(world.m_base, raw["M"].astype(np.float64))


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("r_max", 0.0),
        ("r_max", 0.5),
        ("theta", 0.0),
        ("theta", 1.0),
        ("phi", 0.0),
        ("phi", 1.0),
        ("p_min", 0.0),
        ("p0", 9.0),
        ("seed_cell", True),
        ("horizon_t", 0),
        ("horizon_t", True),
    ],
)
def test_scenario_rejects_invalid_startup_domains(field: str, value: object) -> None:
    values = {
        "r_max": 0.03,
        "theta": 0.6,
        "phi": 0.3,
        "p_min": 10.0,
        "p0": 60.0,
        "seed_cell": 0,
        "horizon_t": 1000,
    }
    values[field] = value
    with pytest.raises((TypeError, ValueError)):
        Scenario(**values)


def test_initial_state_requires_a_passable_positive_capacity_seed() -> None:
    world = graph_world([0.0], [])
    scenario = Scenario(0.03, 0.6, 0.3, 10.0, 60.0, 0, 1)
    with pytest.raises(ValueError, match="passable with positive capacity"):
        initial_state(world, scenario)


def test_weighted_sampler_matches_six_weight_fixture() -> None:
    weights = np.array([120.0, 80.0, 50.0, 45.0, 20.0, 8.0])
    draws = 100_000
    sampled = weighted_sample(weights, np.random.default_rng(20260721), draws)
    observed = np.bincount(sampled, minlength=weights.size)
    expected = draws * weights / np.sum(weights)
    assert _chi_square(observed, expected) < 20.515


def test_fractional_m_distribution() -> None:
    draws = 100_000
    cells = draws + 2
    first_destination = draws
    neighbor = np.full((cells, 6), -1, dtype=np.int32)
    neighbor[:draws, 0] = first_destination
    neighbor[:draws, 1] = first_destination + 1
    m_base = np.zeros((cells, 6), dtype=np.float64)
    m_base[:draws, 0] = 1.0
    m_base[:draws, 1] = 0.25
    world = World(
        coords=np.zeros((cells, 2), dtype=np.int16),
        terrain=np.zeros(cells, dtype=np.int8),
        climate=np.zeros(cells, dtype=np.int8),
        k_eco=np.full(cells, 100.0),
        passable=np.ones(cells, dtype=np.bool_),
        neighbor=neighbor,
        m_base=m_base,
        reachable=np.ones(cells, dtype=np.bool_),
        require_symmetric_movement=False,
    )
    population = np.zeros(cells, dtype=np.float64)
    population[:draws] = 100.0
    settled = np.zeros(cells, dtype=np.bool_)
    settled[:draws] = True
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=1.0,
        rng=np.random.default_rng(20260722),
    )
    selected = np.fromiter(
        (intention.destination - first_destination for intention in plan.intentions),
        dtype=np.int64,
        count=draws,
    )
    observed = np.bincount(selected, minlength=2)
    expected = np.array([80_000.0, 20_000.0])
    assert _chi_square(observed, expected) < 10.828


def test_joint_founding_aggregates_before_floor() -> None:
    world = graph_world([60.0, 60.0, 60.0, 60.0], [(0, 3, 1.0), (1, 3, 1.0), (2, 3, 1.0)])
    population = np.array([40.0, 40.0, 40.0, 0.0])
    settled = np.array([True, True, True, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=25.0,
        rng=np.random.default_rng(1),
    )
    state, events = resolve_migration(population, settled, plan, tick=1)
    np.testing.assert_array_equal(state.population, np.array([30.0, 30.0, 30.0, 30.0]))
    np.testing.assert_array_equal(state.settled, np.ones(4, dtype=np.bool_))
    assert len(events) == 1
    assert [arrival["source"] for arrival in events[0]["arrivals"]] == [0, 1, 2]
    assert events[0]["population"] == 30.0


def test_rejected_group_refunds_every_source_exactly() -> None:
    world = graph_world([60.0, 60.0, 60.0], [(0, 2, 1.0), (1, 2, 1.0)])
    population = np.array([40.0, 40.0, 0.0])
    settled = np.array([True, True, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=25.0,
        rng=np.random.default_rng(1),
    )
    state, events = resolve_migration(population, settled, plan, tick=1)
    np.testing.assert_array_equal(state.population, population)
    np.testing.assert_array_equal(state.settled, settled)
    assert not events
    assert len(plan.rejected) == 2
    assert plan.refused_destinations == (2,)


def test_boundary_equality_founds() -> None:
    accepted, rejected, groups, refused = filter_viable_intentions(
        [Intention(source=0, destination=1, population=20.0)],
        p_min=20.0,
    )
    assert len(accepted) == 1
    assert not rejected
    assert groups[0].total == 20.0
    assert not refused


def test_no_candidate_means_no_subtraction() -> None:
    world = graph_world([100.0, 100.0], [(0, 1, 0.0)])
    population = np.array([100.0, 0.0])
    settled = np.array([True, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=10.0,
        rng=np.random.default_rng(1),
    )
    state, events = resolve_migration(population, settled, plan, tick=1)
    np.testing.assert_array_equal(state.population, population)
    assert not plan.intentions
    assert not events


def test_zero_m_excludes_a_higher_capacity_candidate() -> None:
    world = graph_world(
        [100.0, 1.0, 10_000.0],
        [(0, 1, 1.0), (0, 2, 0.0)],
    )
    population = np.array([100.0, 0.0, 0.0])
    settled = np.array([True, False, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=1.0,
        rng=np.random.default_rng(8),
    )
    assert [(item.source, item.destination) for item in plan.intentions] == [(0, 1)]


def test_single_rejected_parcel_is_refunded_exactly() -> None:
    world = graph_world([60.0, 60.0], [(0, 1, 1.0)])
    population = np.array([40.0, 0.0])
    settled = np.array([True, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.25,
        p_min=11.0,
        rng=np.random.default_rng(8),
    )
    state, events = resolve_migration(population, settled, plan, tick=1)
    assert len(plan.rejected) == 1
    np.testing.assert_array_equal(state.population, population)
    np.testing.assert_array_equal(state.settled, settled)
    assert not events


def test_abandoned_cell_cannot_be_recolonized_until_next_tick() -> None:
    world = graph_world([100.0, 100.0], [(0, 1, 1.0)])
    scenario = Scenario(0.01, 0.5, 0.2, 10.0, 20.0, 0, 2)
    state = SimulationState(
        population=np.array([100.0, 5.0]),
        settled=np.array([True, True]),
    )
    rng = np.random.default_rng(3)
    first = step(state, world, scenario, rng, tick=1)
    assert not first.state.settled[1]
    assert [event["event"] for event in first.events] == ["cell_abandoned"]
    second = step(first.state, world, scenario, rng, tick=2)
    assert second.state.settled[1]
    assert any(event["event"] == "cell_settled" for event in second.events)


def test_newly_founded_cell_does_not_expand_in_same_tick() -> None:
    world = graph_world([100.0, 100.0, 100.0], [(0, 1, 1.0), (1, 2, 1.0)])
    scenario = Scenario(0.01, 0.5, 0.2, 10.0, 60.0, 0, 1)
    state = SimulationState(
        population=np.array([60.0, 0.0, 0.0]),
        settled=np.array([True, False, False]),
    )
    result = step(state, world, scenario, np.random.default_rng(4), tick=1)
    np.testing.assert_array_equal(result.state.settled, np.array([True, True, False]))


def test_no_post_migration_extinction_floor() -> None:
    world = graph_world([40.0, 40.0], [(0, 1, 1.0)])
    population = np.array([30.0, 0.0])
    settled = np.array([True, False])
    plan = plan_migration(
        population,
        settled,
        world,
        world.k_eco,
        theta=0.5,
        phi=0.75,
        p_min=20.0,
        rng=np.random.default_rng(1),
    )
    state, _events = resolve_migration(population, settled, plan, tick=1)
    np.testing.assert_array_equal(state.population, np.array([7.5, 22.5]))
    np.testing.assert_array_equal(state.settled, np.array([True, True]))


def test_clamp_and_abandonment_events_share_the_tick() -> None:
    world = graph_world([80.0], [])
    scenario = Scenario(0.25, 0.5, 0.25, 10.0, 10.0, 0, 1)
    state = SimulationState(np.array([440.0]), np.array([True]))
    result = step(state, world, scenario, np.random.default_rng(1), tick=1)
    assert result.state.population[0] == 0.0
    assert not result.state.settled[0]
    assert [event["event"] for event in result.events] == [
        "cell_abandoned",
        "clamp_negative",
    ]


def test_stage_c_and_d_are_source_order_independent() -> None:
    edges = [edge for source in range(6) for edge in ((source, 7, 1.0), (source, 6, 1.0))]
    world = graph_world([1.0] * 6 + [100.0, 100.0], edges)
    population = np.array([10.0, 10.0, 1e17, 10.0, 10.0, 10.0, 0.0, 0.0])
    settled = np.array([True] * 6 + [False, False])
    arguments = (population, settled, world, world.k_eco, 0.5, 0.1, 10.0)
    forward = plan_migration(
        *arguments,
        rng=np.random.default_rng(55),
        source_order=[0, 1, 2, 3, 4, 5],
    )
    reverse = plan_migration(
        *arguments,
        rng=np.random.default_rng(55),
        source_order=[5, 4, 3, 2, 1, 0],
    )
    assert forward == reverse
    assert [(item.source, item.destination) for item in forward.intentions] == [
        (0, 7),
        (1, 7),
        (2, 6),
        (3, 6),
        (4, 6),
        (5, 7),
    ]
    assert forward.refused_destinations == (7,)
    assert [item.source for item in forward.rejected] == [0, 1, 5]
    forward_state, forward_events = resolve_migration(population, settled, forward, tick=1)
    reverse_state, reverse_events = resolve_migration(population, settled, reverse, tick=1)
    np.testing.assert_array_equal(forward_state.population, reverse_state.population)
    np.testing.assert_array_equal(forward_state.settled, reverse_state.settled)
    np.testing.assert_array_equal(forward_state.population[[0, 1, 5]], population[[0, 1, 5]])
    assert forward_state.population[7] == 0.0
    assert forward_events == reverse_events


def test_same_seed_is_bit_exact_internally(reference_world) -> None:
    scenario = replace(REFERENCE_SCENARIO, horizon_t=100)
    identity = build_run_identity(project_root(), reference_world, scenario, seed=99)
    first = run_simulation(reference_world, scenario, seed=99, run_identity=identity)
    second = run_simulation(reference_world, scenario, seed=99, run_identity=identity)
    np.testing.assert_array_equal(first.population, second.population)
    np.testing.assert_array_equal(first.settled, second.settled)
    assert first.events == second.events
    assert np.max(first.conservation_errors) <= 1e-12


def test_reachable_fraction_uses_the_active_scenario_seed() -> None:
    world = graph_world([100.0, 100.0, 100.0], [(0, 1, 1.0)])
    np.testing.assert_array_equal(
        reachable_from_seed(world, 0),
        np.array([True, True, False]),
    )
    scenario = Scenario(0.01, 0.5, 0.2, 10.0, 20.0, 0, 1)
    population = np.array([[20.0, 0.0, 0.0], [20.0, 20.0, 0.0]])
    settled = population > 0.0
    summary = summarize_run(
        population,
        settled,
        np.array([0, 1, -1], dtype=np.int32),
        events=(),
        rejected_intentions=0,
        world=world,
        scenario=scenario,
    )
    assert summary["settlement_fraction_reachable"] == 1.0
    assert summary["settlement_fraction_passable"] == 2.0 / 3.0
