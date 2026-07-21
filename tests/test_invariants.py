from __future__ import annotations

import numpy as np
import pytest

from clio.invariants import (
    CapacityParameters,
    carrying_capacity,
    ecological_capacity,
    effective_movement,
    emigration,
    extinction_floor,
    grow,
)


@pytest.mark.parametrize(
    ("population", "expected_raw", "expected_next", "expected_clamped"),
    [
        (40.0, 45.0, 45.0, False),
        (80.0, 80.0, 80.0, False),
        (120.0, 105.0, 105.0, False),
        (320.0, 80.0, 80.0, False),
        (360.0, 45.0, 45.0, False),
        (400.0, 0.0, 0.0, False),
        (440.0, -55.0, 0.0, True),
    ],
)
def test_growth_hand_oracles(
    population: float,
    expected_raw: float,
    expected_next: float,
    expected_clamped: bool,
) -> None:
    next_population, raw, clamped = grow(population, 80.0, 0.25)
    assert float(raw) == pytest.approx(expected_raw)
    assert float(next_population) == pytest.approx(expected_next)
    assert bool(clamped) is expected_clamped


def test_survival_multiplies_after_growth() -> None:
    next_population, raw, clamped = grow(40.0, 80.0, 0.25, S=0.5, survival=0.8)
    assert float(raw) == pytest.approx(34.0)
    assert float(next_population) == pytest.approx(34.0)
    assert not bool(clamped)


def test_growth_properties_hold_only_in_their_domains() -> None:
    population = np.array([20.0, 100.0, 200.0, 4000.0])
    capacity = np.full(4, 80.0)
    next_population, _raw, _clamped = grow(population, capacity, 0.03)
    assert population[0] < next_population[0] <= capacity[0]
    assert next_population[1] < population[1]
    assert next_population[2] >= capacity[2]
    assert np.all(next_population >= 0.0)


def test_growth_rejects_zero_capacity() -> None:
    with pytest.raises(ValueError, match="K > 0"):
        grow(np.array([10.0]), np.array([0.0]), 0.03)


def test_convergence_fixture_crosses_tolerance_at_tick_529() -> None:
    population = np.array([10.0])
    capacity = np.array([100.0])
    errors: list[float] = []
    for _tick in range(529):
        population, _raw, _clamped = grow(population, capacity, 0.03)
        errors.append(float(abs(population[0] - 100.0) / 100.0))
    assert errors[527] > 1e-6
    assert errors[528] <= 1e-6


@pytest.fixture
def capacity_params() -> CapacityParameters:
    return CapacityParameters(
        k_base=np.array([[80.0, 40.0], [20.0, 10.0]]),
        w_base={"river": 1.3, "coast": 1.2, "lake": 1.1},
        w_override={frozenset(("river", "coast")): 1.4},
    )


def test_empty_water_context_is_neutral(capacity_params: CapacityParameters) -> None:
    result = ecological_capacity(0, 0, frozenset(), capacity_params)
    assert float(result) == 80.0


def test_water_context_uses_max_not_product(capacity_params: CapacityParameters) -> None:
    no_override = CapacityParameters(
        capacity_params.k_base,
        w_base=capacity_params.w_base,
    )
    result = ecological_capacity(0, 0, frozenset(("river", "coast")), no_override)
    assert float(result) == pytest.approx(104.0)


def test_exact_water_override_precedes_max(capacity_params: CapacityParameters) -> None:
    result = ecological_capacity(0, 0, frozenset(("river", "coast")), capacity_params)
    assert float(result) == pytest.approx(112.0)


def test_exact_water_override_does_not_require_base_entries() -> None:
    params = CapacityParameters(
        np.array([[80.0]]),
        w_override={frozenset(("canal", "estuary")): 1.5},
    )
    result = ecological_capacity(0, 0, frozenset(("canal", "estuary")), params)
    assert float(result) == pytest.approx(120.0)


def test_single_cell_accepts_one_context_set_per_cell(
    capacity_params: CapacityParameters,
) -> None:
    result = ecological_capacity(
        np.array([0]),
        np.array([0]),
        [frozenset(("river", "coast"))],
        capacity_params,
    )
    np.testing.assert_array_equal(result, np.array([112.0]))


def test_water_override_does_not_match_a_superset(capacity_params: CapacityParameters) -> None:
    result = ecological_capacity(
        0,
        0,
        frozenset(("river", "coast", "lake")),
        capacity_params,
    )
    assert float(result) == pytest.approx(104.0)


def test_total_capacity_applies_reserved_factors(capacity_params: CapacityParameters) -> None:
    result = carrying_capacity(
        0,
        0,
        frozenset(("river", "coast")),
        capacity_params,
        A=1.5,
        I=1.25,
        Y=0.5,
    )
    assert float(result) == pytest.approx(105.0)


def test_unknown_water_context_is_rejected(capacity_params: CapacityParameters) -> None:
    with pytest.raises(ValueError, match="unknown water contexts"):
        ecological_capacity(0, 0, frozenset(("oasis",)), capacity_params)


def test_emigration_trigger_is_strict_and_parcel_is_phi_times_population() -> None:
    population = np.array([59.0, 60.0, 64.0])
    result = emigration(population, np.full(3, 80.0), theta=0.75, phi=0.25)
    np.testing.assert_array_equal(result, np.array([0.0, 0.0, 16.0]))


def test_extinction_floor_preserves_equality() -> None:
    population, settled, abandoned = extinction_floor(
        np.array([9.0, 10.0, 11.0]),
        np.ones(3, dtype=np.bool_),
        10.0,
    )
    np.testing.assert_array_equal(population, np.array([0.0, 10.0, 11.0]))
    np.testing.assert_array_equal(settled, np.array([False, True, True]))
    np.testing.assert_array_equal(abandoned, np.array([True, False, False]))


def test_effective_movement_clips_boosts_and_preserves_closed_edges() -> None:
    base = np.array([[0.0, 0.4, 1.0]])
    result = effective_movement(base, modifiers=(np.array([[0.0, 3.0, 2.0]]),))
    np.testing.assert_array_equal(result, np.array([[0.0, 1.0, 1.0]]))


def test_effective_movement_rejects_zero_modifier_on_live_edge() -> None:
    with pytest.raises(ValueError, match="strictly positive"):
        effective_movement(np.array([[1.0]]), modifiers=(np.array([[0.0]]),))
