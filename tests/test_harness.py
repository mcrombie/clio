from __future__ import annotations

from dataclasses import replace

import numpy as np
import pytest

from clio.harness import EnsembleResult, assert_reference_acceptance, run_reference_ensemble
from clio.loop import _plateau_tick


def test_plateau_requires_a_fifty_tick_suffix() -> None:
    constant = np.full(101, 100.0)
    assert _plateau_tick(constant, 100) == 0

    late_only = np.concatenate((np.arange(61, dtype=np.float64) + 100.0, np.full(40, 160.0)))
    assert _plateau_tick(late_only, 100) is None


@pytest.fixture(scope="module")
def reference_ensemble() -> EnsembleResult:
    return run_reference_ensemble()


def test_reference_ensemble_clears_brief_one_acceptance(
    reference_ensemble: EnsembleResult,
) -> None:
    ensemble = reference_ensemble
    assert_reference_acceptance(ensemble)
    assert ensemble.expansion_count == 20
    assert ensemble.unique_first_settlement_vectors >= 19
    assert all(run.summary["clamp_negative_count"] == 0 for run in ensemble.runs)
    assert all(
        run.summary["sources_unable_to_found_alone_from_equilibrium_count"] == 59
        for run in ensemble.runs
    )


def test_acceptance_rejects_wrong_run_count_or_horizon(
    reference_ensemble: EnsembleResult,
) -> None:
    one_run = EnsembleResult(
        seeds=reference_ensemble.seeds[:1],
        runs=reference_ensemble.runs[:1],
        expansion_count=1,
        unique_first_settlement_vectors=1,
    )
    with pytest.raises(AssertionError, match="exactly 20"):
        assert_reference_acceptance(one_run)

    wrong_horizon_runs = list(reference_ensemble.runs)
    wrong_horizon_runs[0] = replace(
        wrong_horizon_runs[0],
        scenario=replace(wrong_horizon_runs[0].scenario, horizon_t=999),
    )
    wrong_horizon = replace(reference_ensemble, runs=tuple(wrong_horizon_runs))
    with pytest.raises(AssertionError, match="T = 1000"):
        assert_reference_acceptance(wrong_horizon)
