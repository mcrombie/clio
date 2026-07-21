"""Reference-cohort runner and multi-seed Brief 1 acceptance harness."""

from __future__ import annotations

import argparse
import json
from collections.abc import Iterable, Sequence
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any

from clio.logging import build_run_identity, canonical_json, write_run_outputs
from clio.loop import (
    REFERENCE_SCENARIO,
    Scenario,
    SimulationResult,
    World,
    load_world_fixture,
    run_simulation,
)


@dataclass(frozen=True, slots=True)
class EnsembleResult:
    seeds: tuple[int, ...]
    runs: tuple[SimulationResult, ...]
    expansion_count: int
    unique_first_settlement_vectors: int

    @property
    def summaries(self) -> tuple[dict[str, Any], ...]:
        return tuple(dict(run.summary) for run in self.runs)


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def load_reference_world(root: Path | str | None = None) -> World:
    base = Path(root).resolve() if root is not None else project_root()
    return load_world_fixture(
        base / "fixtures" / "brief1_toymap_v1.npz",
        base / "fixtures" / "brief1_toymap_v1.json",
    )


def run_reference(
    seed: int,
    *,
    root: Path | str | None = None,
    scenario: Scenario = REFERENCE_SCENARIO,
    world: World | None = None,
) -> SimulationResult:
    base = Path(root).resolve() if root is not None else project_root()
    loaded_world = world if world is not None else load_reference_world(base)
    identity = build_run_identity(base, loaded_world, scenario, seed)
    return run_simulation(loaded_world, scenario, seed, identity)


def run_reference_ensemble(
    seeds: Iterable[int] = range(20),
    *,
    root: Path | str | None = None,
    scenario: Scenario = REFERENCE_SCENARIO,
    write_directory: Path | str | None = None,
) -> EnsembleResult:
    base = Path(root).resolve() if root is not None else project_root()
    world = load_reference_world(base)
    seed_values = tuple(int(seed) for seed in seeds)
    if not seed_values:
        raise ValueError("the ensemble requires at least one seed")
    runs: list[SimulationResult] = []
    for seed in seed_values:
        result = run_reference(seed, root=base, scenario=scenario, world=world)
        runs.append(result)
        if write_directory is not None:
            write_run_outputs(write_directory, f"brief1_seed_{seed}", result, world)

    expansion_count = sum(run.summary["tick_of_first_expansion"] is not None for run in runs)
    vectors = {tuple(int(value) for value in run.first_settlement_tick.tolist()) for run in runs}
    return EnsembleResult(
        seeds=seed_values,
        runs=tuple(runs),
        expansion_count=expansion_count,
        unique_first_settlement_vectors=len(vectors),
    )


def assert_reference_acceptance(ensemble: EnsembleResult) -> None:
    expected_runs = 20
    if len(ensemble.seeds) != expected_runs or len(ensemble.runs) != expected_runs:
        raise AssertionError("Brief 1 acceptance requires exactly 20 runs")
    if len(set(ensemble.seeds)) != expected_runs:
        raise AssertionError("Brief 1 acceptance requires 20 distinct seeds")
    if any(run.scenario.horizon_t != 1000 for run in ensemble.runs):
        raise AssertionError("Brief 1 acceptance requires horizon T = 1000")
    if ensemble.expansion_count != expected_runs:
        raise AssertionError(
            f"expansion occurred in {ensemble.expansion_count}/{expected_runs} runs"
        )
    minimum_unique = 19
    if ensemble.unique_first_settlement_vectors < minimum_unique:
        raise AssertionError(
            "only "
            f"{ensemble.unique_first_settlement_vectors} unique first-settlement vectors; "
            f"expected at least {minimum_unique}"
        )


def ensemble_report(ensemble: EnsembleResult) -> dict[str, Any]:
    return {
        "seeds": list(ensemble.seeds),
        "expansion_count": ensemble.expansion_count,
        "unique_first_settlement_vectors": ensemble.unique_first_settlement_vectors,
        "runs": [
            {"seed": seed, **dict(run.summary)}
            for seed, run in zip(ensemble.seeds, ensemble.runs, strict=True)
        ],
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seeds", type=int, default=20, help="number of seeds, starting at 0")
    parser.add_argument("--horizon", type=int, default=REFERENCE_SCENARIO.horizon_t)
    parser.add_argument("--write-runs", action="store_true", help="write NPZ and JSONL per seed")
    parser.add_argument("--output-dir", type=Path, default=Path("runs") / "brief1")
    arguments = parser.parse_args(argv)
    scenario = replace(REFERENCE_SCENARIO, horizon_t=arguments.horizon)
    output = arguments.output_dir if arguments.write_runs else None
    ensemble = run_reference_ensemble(
        range(arguments.seeds), scenario=scenario, write_directory=output
    )
    if arguments.seeds == 20 and arguments.horizon == 1000:
        assert_reference_acceptance(ensemble)
    report = ensemble_report(ensemble)
    if arguments.write_runs:
        arguments.output_dir.mkdir(parents=True, exist_ok=True)
        (arguments.output_dir / "summary.json").write_text(
            canonical_json(report) + "\n",
            encoding="utf-8",
        )
    print(json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
