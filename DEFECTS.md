# Clio — Defect Log

Entry format:

```
## [YYYY-MM-DD] Short title
Phase/Brief: <which phase and brief surfaced this>
Symptom: what was observed, stated as an observation rather than a diagnosis
         (e.g. "settlement order identical across all 20 seeds")
Cause: invariant edited / scenario logic leaked into engine / unintended determinism /
       Phase 2 mechanic used in Phase 1 / spec gap in the brief / model error / other
Resolution and verification: what changed, and what check now confirms it
```

Log an entry whenever:

- an invariant is suspected of having been edited to make a run work
- an outcome is deterministic across every seed where variance was expected
- scenario-specific logic has leaked into the engine
- a Phase 2 mechanic (authored event, pressure, biasing) has crept into Phase 1 work
- the brief itself was underspecified and the implementer had to invent a mechanic
- (Phase 2, later) a pressure change forces an outcome instead of biasing it

The last of these is worth noting explicitly: **a gap in the brief is a defect**, logged the same as a code fault. On the previous project most defects traced to specification gaps rather than to the model's output, and that pattern only became visible because they were counted.

Entries below, most recent first.

---

(none yet)
