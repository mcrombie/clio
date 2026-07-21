from __future__ import annotations

import json
import subprocess
from dataclasses import replace
from pathlib import Path

import numpy as np
import pytest

from clio.harness import project_root
from clio.logging import (
    build_run_identity,
    read_event_jsonl,
    working_tree_identity,
    write_run_outputs,
)
from clio.loop import REFERENCE_SCENARIO, run_simulation


def _git(root: Path, *arguments: str) -> None:
    subprocess.run(("git", *arguments), cwd=root, check=True, capture_output=True)


def _temporary_repository(path: Path) -> None:
    path.mkdir()
    _git(path, "init")
    (path / ".gitignore").write_text("ignored.txt\n", encoding="utf-8")
    (path / "tracked.txt").write_text("base\n", encoding="utf-8")
    _git(path, "add", ".")
    _git(
        path,
        "-c",
        "user.name=Clio Tests",
        "-c",
        "user.email=clio-tests@example.invalid",
        "commit",
        "-m",
        "base",
    )


def test_working_tree_identity_tracks_all_relevant_states(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    _temporary_repository(repository)
    assert working_tree_identity(repository) == ("clean", None)

    (repository / "tracked.txt").write_text("changed\n", encoding="utf-8")
    dirty_tracked = working_tree_identity(repository)
    assert dirty_tracked[0] == "dirty"
    _git(repository, "add", "tracked.txt")
    dirty_staged = working_tree_identity(repository)
    assert dirty_staged[0] == "dirty"
    assert dirty_staged[1] == dirty_tracked[1]

    (repository / "new.txt").write_text("first\n", encoding="utf-8")
    dirty_untracked = working_tree_identity(repository)
    assert dirty_untracked[1] != dirty_staged[1]
    (repository / "new.txt").write_text("second\n", encoding="utf-8")
    assert working_tree_identity(repository)[1] != dirty_untracked[1]

    identity_before_ignored_file = working_tree_identity(repository)
    (repository / "ignored.txt").write_text("ignored\n", encoding="utf-8")
    assert working_tree_identity(repository) == identity_before_ignored_file


def test_run_identity_and_outputs_round_trip(reference_world, tmp_path: Path) -> None:
    root = project_root()
    scenario = replace(REFERENCE_SCENARIO, horizon_t=20)
    identity = build_run_identity(root, reference_world, scenario, seed=7)
    assert identity["identity_schema"] == "clio.run_identity/1"
    assert identity["fixture"]["sha256"] == reference_world.fixture_sha256
    assert identity["working_tree"] in {"clean", "dirty"}
    result = run_simulation(reference_world, scenario, seed=7, run_identity=identity)
    dense_path, event_path = write_run_outputs(tmp_path, "run", result, reference_world)

    with np.load(dense_path, allow_pickle=False) as dense:
        assert set(dense.files) == {
            "population",
            "settled",
            "terrain",
            "climate",
            "K_eco",
            "passable",
            "coords",
            "run_identity",
        }
        assert dense["population"].dtype == np.float32
        assert dense["settled"].dtype == np.bool_
        assert dense["population"].shape == (21, reference_world.cells)
        assert json.loads(str(dense["run_identity"])) == identity

    parsed_events = read_event_jsonl(event_path)
    assert parsed_events == list(result.events)
    assert parsed_events == sorted(
        parsed_events,
        key=lambda event: (event["tick"], event["event"], event["cell"]),
    )


def test_run_rejects_incomplete_or_mismatched_identity(reference_world) -> None:
    scenario = replace(REFERENCE_SCENARIO, horizon_t=20)
    with pytest.raises(ValueError, match="missing required fields"):
        run_simulation(
            reference_world,
            scenario,
            seed=7,
            run_identity={"identity_schema": "clio.run_identity/1", "seed": 7},
        )

    identity = build_run_identity(project_root(), reference_world, scenario, seed=7)
    identity["scenario"]["horizon_T"] = 21
    with pytest.raises(ValueError, match="scenario does not match"):
        run_simulation(reference_world, scenario, seed=7, run_identity=identity)


def test_writer_revalidates_result_identity(reference_world, tmp_path: Path) -> None:
    scenario = replace(REFERENCE_SCENARIO, horizon_t=2)
    identity = build_run_identity(project_root(), reference_world, scenario, seed=7)
    result = run_simulation(reference_world, scenario, seed=7, run_identity=identity)
    result.run_identity["fixture"]["sha256"] = "0" * 64
    with pytest.raises(ValueError, match="fixture does not match"):
        write_run_outputs(tmp_path, "invalid", result, reference_world)


def test_serialized_reproducibility(reference_world, tmp_path: Path) -> None:
    scenario = replace(REFERENCE_SCENARIO, horizon_t=50)
    identity = build_run_identity(project_root(), reference_world, scenario, seed=11)
    first = run_simulation(reference_world, scenario, seed=11, run_identity=identity)
    second = run_simulation(reference_world, scenario, seed=11, run_identity=identity)
    first_npz, first_jsonl = write_run_outputs(tmp_path / "first", "run", first, reference_world)
    second_npz, second_jsonl = write_run_outputs(
        tmp_path / "second",
        "run",
        second,
        reference_world,
    )
    with (
        np.load(first_npz, allow_pickle=False) as left,
        np.load(
            second_npz,
            allow_pickle=False,
        ) as right,
    ):
        assert left.files == right.files
        for name in left.files:
            np.testing.assert_array_equal(left[name], right[name])
    assert read_event_jsonl(first_jsonl) == read_event_jsonl(second_jsonl)
