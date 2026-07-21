"""Run identity, dense NPZ output, and canonical JSONL event output."""

from __future__ import annotations

import hashlib
import json
import subprocess
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

import numpy as np

from clio.loop import Event, Scenario, SimulationResult, World, validate_run_identity


def build_run_identity(
    project_root: Path | str,
    world: World,
    scenario: Scenario,
    seed: int,
) -> dict[str, Any]:
    """Build the complete versioned identity required for reproducibility."""

    if isinstance(seed, bool) or not isinstance(seed, int):
        raise TypeError("seed must be an integer")
    root = Path(project_root).resolve()
    commit = _git(root, "rev-parse", "HEAD").decode("ascii").strip()
    working_tree, dirty_fingerprint = working_tree_identity(root)
    dependency_lock = root / "uv.lock"
    if not dependency_lock.is_file():
        raise FileNotFoundError("uv.lock is required before any run")
    if not world.fixture_sha256:
        raise ValueError("a run identity requires a hashed source fixture")
    identity = {
        "identity_schema": "clio.run_identity/1",
        "seed": int(seed),
        "commit": commit,
        "working_tree": working_tree,
        "dirty_fingerprint": dirty_fingerprint,
        "scenario": scenario.identity_values(),
        "fixture": {
            "schema": world.fixture_schema,
            "sha256": world.fixture_sha256,
        },
        "dependency_lock_sha256": file_sha256(dependency_lock),
        "terrain_map": dict(world.terrain_map),
        "climate_map": dict(world.climate_map),
    }
    return validate_run_identity(identity, world, scenario, seed)


def working_tree_identity(project_root: Path | str) -> tuple[str, str | None]:
    """Return clean/dirty state and the approved dirty fingerprint."""

    root = Path(project_root).resolve()
    status = _git(root, "status", "--porcelain=v1", "-z", "--untracked-files=all")
    if not status:
        return "clean", None

    digest = hashlib.sha256()
    digest.update(_git(root, "diff", "HEAD"))
    records = [record for record in status.split(b"\0") if record]
    for record in records:
        if not record.startswith(b"?? "):
            continue
        relative = record[3:].decode("utf-8", errors="surrogateescape")
        path = root / relative
        if not path.is_file():
            raise RuntimeError(f"untracked path disappeared while fingerprinting: {relative}")
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
    return "dirty", digest.hexdigest()


def write_dense_npz(path: Path | str, result: SimulationResult, world: World) -> Path:
    """Write the dense Brief 1 grid with a non-pickled JSON identity scalar."""

    validate_run_identity(result.run_identity, world, result.scenario, result.seed)
    expected_shape = (result.scenario.horizon_t + 1, world.cells)
    if result.population.shape != expected_shape or result.settled.shape != expected_shape:
        raise ValueError("run histories do not match the identified horizon and world")
    if result.population.dtype != np.float64:
        raise TypeError("internal population history must be float64")
    if result.settled.dtype != np.bool_:
        raise TypeError("settled history must be boolean")
    if not np.all(np.isfinite(result.population)) or np.any(result.population < 0.0):
        raise ValueError("population history must be finite and non-negative")
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    identity = np.asarray(canonical_json(result.run_identity), dtype=np.str_)
    np.savez(
        destination,
        population=result.population.astype(np.float32),
        settled=result.settled,
        terrain=world.terrain,
        climate=world.climate,
        K_eco=world.k_eco.astype(np.float32),
        passable=world.passable,
        coords=world.coords,
        run_identity=identity,
        allow_pickle=False,
    )
    return destination


def write_event_jsonl(path: Path | str, events: Sequence[Event]) -> Path:
    """Write events in the canonical `(tick, event, cell)` order."""

    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    ordered = sorted(events, key=lambda event: (event["tick"], event["event"], event["cell"]))
    with destination.open("w", encoding="utf-8", newline="\n") as stream:
        for event in ordered:
            stream.write(canonical_json(event))
            stream.write("\n")
    return destination


def write_run_outputs(
    output_directory: Path | str,
    stem: str,
    result: SimulationResult,
    world: World,
) -> tuple[Path, Path]:
    directory = Path(output_directory)
    dense = write_dense_npz(directory / f"{stem}.npz", result, world)
    events = write_event_jsonl(directory / f"{stem}.jsonl", result.events)
    return dense, events


def read_event_jsonl(path: Path | str) -> list[dict[str, Any]]:
    with Path(path).open(encoding="utf-8") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def canonical_json(value: Any) -> str:
    return json.dumps(
        value,
        default=_json_default,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def file_sha256(path: Path | str) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _git(root: Path, *arguments: str) -> bytes:
    try:
        completed = subprocess.run(
            ("git", *arguments),
            cwd=root,
            check=True,
            capture_output=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        raise RuntimeError(f"git {' '.join(arguments)} failed in {root}") from error
    return completed.stdout


def _json_default(value: Any) -> Any:
    if isinstance(value, Mapping):
        return dict(value)
    if isinstance(value, np.integer):
        return int(value)
    if isinstance(value, np.floating):
        return float(value)
    if isinstance(value, np.ndarray):
        return value.tolist()
    raise TypeError(f"{type(value).__name__} is not JSON serializable")
