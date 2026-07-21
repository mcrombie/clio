from __future__ import annotations

from collections.abc import Iterable

import numpy as np
import pytest

from clio.harness import load_reference_world
from clio.loop import World


@pytest.fixture(scope="session")
def reference_world() -> World:
    return load_reference_world()


def graph_world(
    capacities: Iterable[float],
    edges: Iterable[tuple[int, int, float]],
) -> World:
    k_eco = np.asarray(tuple(capacities), dtype=np.float64)
    cells = k_eco.size
    neighbor = np.full((cells, 6), -1, dtype=np.int32)
    m_base = np.zeros((cells, 6), dtype=np.float64)
    used = np.zeros(cells, dtype=np.int8)
    for left, right, accessibility in edges:
        left_slot = int(used[left])
        right_slot = int(used[right])
        if left_slot >= 6 or right_slot >= 6:
            raise ValueError("test graph exceeds six neighbors")
        neighbor[left, left_slot] = right
        neighbor[right, right_slot] = left
        m_base[left, left_slot] = accessibility
        m_base[right, right_slot] = accessibility
        used[left] += 1
        used[right] += 1
    passable = k_eco > 0.0
    return World(
        coords=np.column_stack((np.arange(cells), np.zeros(cells))).astype(np.int16),
        terrain=np.zeros(cells, dtype=np.int8),
        climate=np.zeros(cells, dtype=np.int8),
        k_eco=k_eco,
        passable=passable,
        neighbor=neighbor,
        m_base=m_base,
        reachable=passable.copy(),
    )
