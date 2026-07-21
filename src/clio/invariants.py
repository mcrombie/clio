"""Locked mechanics from INVARIANTS.md sections 1-5.

The functions in this module are engine machinery. Scenario coefficients are
arguments or :class:`CapacityParameters`; no scenario-specific branch belongs
here.
"""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from types import MappingProxyType

import numpy as np
from numpy.typing import ArrayLike, NDArray

FloatArray = NDArray[np.float64]
BoolArray = NDArray[np.bool_]
Context = frozenset[str]


@dataclass(frozen=True, slots=True)
class CapacityParameters:
    """Scenario data used by the locked carrying-capacity formula."""

    k_base: ArrayLike
    w_base: Mapping[str, float] = field(default_factory=dict)
    w_override: Mapping[Context, float] = field(default_factory=dict)

    def __post_init__(self) -> None:
        k_base = np.asarray(self.k_base, dtype=np.float64)
        if k_base.ndim != 2 or k_base.size == 0:
            raise ValueError("k_base must be a non-empty terrain x climate table")
        if not np.all(np.isfinite(k_base)) or np.any(k_base < 0.0):
            raise ValueError("k_base values must be finite and non-negative")

        w_base = {str(name): float(value) for name, value in self.w_base.items()}
        if any(not np.isfinite(value) or value < 0.0 for value in w_base.values()):
            raise ValueError("water-context multipliers must be finite and non-negative")

        overrides: dict[Context, float] = {}
        for raw_contexts, raw_value in self.w_override.items():
            contexts = frozenset(raw_contexts)
            value = float(raw_value)
            if not contexts:
                raise ValueError("the empty water context is fixed at 1 and cannot be overridden")
            if not np.isfinite(value) or value < 0.0:
                raise ValueError("water-context overrides must be finite and non-negative")
            overrides[contexts] = value

        object.__setattr__(self, "k_base", k_base.copy())
        object.__setattr__(self, "w_base", MappingProxyType(w_base))
        object.__setattr__(self, "w_override", MappingProxyType(overrides))


def grow(
    population: ArrayLike,
    capacity: ArrayLike,
    r_max: float,
    S: ArrayLike = 1.0,
    survival: ArrayLike = 1.0,
) -> tuple[FloatArray, FloatArray, BoolArray]:
    """Apply one locked population-growth step on strictly positive capacities."""

    population_array = np.asarray(population, dtype=np.float64)
    capacity_array = np.asarray(capacity, dtype=np.float64)
    if population_array.shape != capacity_array.shape:
        raise ValueError("population and capacity must have identical shapes")
    if not np.all(np.isfinite(population_array)) or np.any(population_array < 0.0):
        raise ValueError("population must be finite and non-negative")
    if not np.all(np.isfinite(capacity_array)) or np.any(capacity_array <= 0.0):
        raise ValueError("grow is defined only for finite K > 0")
    if not np.isfinite(r_max) or not 0.0 < r_max < 0.5:
        raise ValueError("r_max must satisfy 0 < r_max < 0.5")

    stability = _broadcast_factor(S, population_array.shape, "S", minimum=0.0, maximum=1.0)
    survivor = _broadcast_factor(
        survival,
        population_array.shape,
        "survival",
        minimum=0.0,
        maximum=1.0,
    )

    with np.errstate(over="ignore", invalid="ignore"):
        density = 1.0 - population_array / capacity_array
        raw = population_array * (1.0 + r_max * density * stability) * survivor
    if not np.all(np.isfinite(raw)):
        raise FloatingPointError("growth produced a non-finite raw population")
    clamped = raw < 0.0
    next_population = np.maximum(raw, 0.0)
    return next_population, raw, clamped


def ecological_capacity(
    terrain: ArrayLike,
    climate: ArrayLike,
    water_context: object,
    params: CapacityParameters,
) -> FloatArray:
    """Compute static ecological capacity, including locked water composition."""

    terrain_array = np.asarray(terrain)
    climate_array = np.asarray(climate)
    if terrain_array.shape != climate_array.shape:
        raise ValueError("terrain and climate must have identical shapes")
    if not np.issubdtype(terrain_array.dtype, np.integer):
        raise TypeError("terrain indices must be integers")
    if not np.issubdtype(climate_array.dtype, np.integer):
        raise TypeError("climate indices must be integers")
    if np.any(terrain_array < 0) or np.any(terrain_array >= params.k_base.shape[0]):
        raise ValueError("terrain index is outside k_base")
    if np.any(climate_array < 0) or np.any(climate_array >= params.k_base.shape[1]):
        raise ValueError("climate index is outside k_base")

    contexts = _normalise_contexts(water_context, terrain_array.shape)
    water = np.fromiter(
        (_water_multiplier(context, params) for context in contexts),
        dtype=np.float64,
        count=terrain_array.size,
    ).reshape(terrain_array.shape)
    with np.errstate(over="ignore", invalid="ignore"):
        result = np.asarray(params.k_base[terrain_array, climate_array] * water, dtype=np.float64)
    if not np.all(np.isfinite(result)) or np.any(result < 0.0):
        raise ValueError("ecological capacity must be finite and non-negative")
    return result


def carrying_capacity(
    terrain: ArrayLike,
    climate: ArrayLike,
    water_context: object,
    params: CapacityParameters,
    A: ArrayLike = 1.0,
    I: ArrayLike = 1.0,  # noqa: E741 - invariant notation uses I for infrastructure
    Y: ArrayLike = 1.0,
) -> FloatArray:
    """Compute total K = K_eco * A * I * Y."""

    k_eco = ecological_capacity(terrain, climate, water_context, params)
    technology = _broadcast_factor(A, k_eco.shape, "A", minimum=1.0)
    infrastructure = _broadcast_factor(I, k_eco.shape, "I", minimum=1.0)
    supply = _broadcast_factor(Y, k_eco.shape, "Y", minimum=0.0)
    with np.errstate(over="ignore", invalid="ignore"):
        result = np.asarray(k_eco * technology * infrastructure * supply, dtype=np.float64)
    if not np.all(np.isfinite(result)) or np.any(result < 0.0):
        raise ValueError("total capacity must be finite and non-negative")
    return result


def emigration(
    population: ArrayLike,
    capacity: ArrayLike,
    theta: float,
    phi: float,
) -> FloatArray:
    """Return potential emigrant parcels without subtracting them from sources."""

    population_array = np.asarray(population, dtype=np.float64)
    capacity_array = np.asarray(capacity, dtype=np.float64)
    if population_array.shape != capacity_array.shape:
        raise ValueError("population and capacity must have identical shapes")
    if not np.all(np.isfinite(population_array)) or np.any(population_array < 0.0):
        raise ValueError("population must be finite and non-negative")
    if not np.all(np.isfinite(capacity_array)) or np.any(capacity_array <= 0.0):
        raise ValueError("emigration requires finite K > 0")
    _validate_fraction(theta, "theta")
    _validate_fraction(phi, "phi")
    return np.where(population_array > theta * capacity_array, phi * population_array, 0.0)


def extinction_floor(
    population: ArrayLike,
    settled: ArrayLike,
    p_min: float,
) -> tuple[FloatArray, BoolArray, BoolArray]:
    """Apply the strict extinction floor and report settled-to-unsettled cells."""

    population_array = np.asarray(population, dtype=np.float64)
    settled_array = np.asarray(settled, dtype=np.bool_)
    if population_array.shape != settled_array.shape:
        raise ValueError("population and settled must have identical shapes")
    if not np.all(np.isfinite(population_array)) or np.any(population_array < 0.0):
        raise ValueError("population must be finite and non-negative")
    if not np.isfinite(p_min) or p_min <= 0.0:
        raise ValueError("P_min must be finite and positive")

    below = population_array < p_min
    abandoned = settled_array & below
    next_population = population_array.copy()
    next_population[below] = 0.0
    next_settled = settled_array.copy()
    next_settled[below] = False
    return next_population, next_settled, abandoned


def effective_movement(m_base: ArrayLike, modifiers: Sequence[ArrayLike] = ()) -> FloatArray:
    """Build M_eff while preserving geographic eligibility in M_base."""

    base = np.asarray(m_base, dtype=np.float64)
    if not np.all(np.isfinite(base)) or np.any((base < 0.0) | (base > 1.0)):
        raise ValueError("M_base must be finite and within [0, 1]")

    product = np.ones_like(base, dtype=np.float64)
    live = base > 0.0
    for index, raw_modifier in enumerate(modifiers):
        modifier = _broadcast_factor(raw_modifier, base.shape, f"modifier[{index}]", minimum=0.0)
        if np.any(modifier[live] <= 0.0):
            raise ValueError("movement modifiers must be strictly positive on live edges")
        with np.errstate(over="ignore", invalid="ignore"):
            product *= modifier

    effective = np.clip(base * product, 0.0, 1.0)
    effective[~live] = 0.0
    return np.asarray(effective, dtype=np.float64)


def _water_multiplier(contexts: Context, params: CapacityParameters) -> float:
    if not contexts:
        return 1.0
    if contexts in params.w_override:
        return params.w_override[contexts]
    unknown = contexts.difference(params.w_base)
    if unknown:
        raise ValueError(f"unknown water contexts: {sorted(unknown)!r}")
    return max(params.w_base[context] for context in contexts)


def _normalise_contexts(water_context: object, shape: tuple[int, ...]) -> list[Context]:
    size = int(np.prod(shape, dtype=np.int64)) if shape else 1
    if water_context is None:
        return [frozenset()] * size

    if isinstance(water_context, (str, set, frozenset)):
        return [_as_context(water_context)] * size

    try:
        raw_contexts = list(water_context)  # type: ignore[arg-type]
    except TypeError as error:
        raise TypeError(
            "water_context must be a context set or one context set per cell"
        ) from error
    if len(raw_contexts) != size:
        raise ValueError("water_context must provide exactly one context set per cell")
    return [_as_context(context) for context in raw_contexts]


def _as_context(raw_context: object) -> Context:
    if raw_context is None:
        return frozenset()
    if isinstance(raw_context, str):
        return frozenset((raw_context,))
    try:
        return frozenset(str(item) for item in raw_context)  # type: ignore[union-attr]
    except TypeError as error:
        raise TypeError("each water context must be an iterable of names") from error


def _broadcast_factor(
    value: ArrayLike,
    shape: tuple[int, ...],
    name: str,
    *,
    minimum: float,
    maximum: float | None = None,
) -> FloatArray:
    array = np.asarray(value, dtype=np.float64)
    try:
        result = np.asarray(np.broadcast_to(array, shape), dtype=np.float64)
    except ValueError as error:
        raise ValueError(f"{name} is not broadcastable to the state shape") from error
    if not np.all(np.isfinite(result)):
        raise ValueError(f"{name} must be finite")
    if np.any(result < minimum) or (maximum is not None and np.any(result > maximum)):
        interval = f"[{minimum}, {maximum}]" if maximum is not None else f">= {minimum}"
        raise ValueError(f"{name} must be {interval}")
    return result


def _validate_fraction(value: float, name: str) -> None:
    if not np.isfinite(value) or not 0.0 < value < 1.0:
        raise ValueError(f"{name} must satisfy 0 < {name} < 1")
