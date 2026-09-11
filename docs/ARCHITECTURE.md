# Clio: executable foundation and Unity production architecture

This document separates the source that runs today from the intended production architecture. The game design is an original proposal, not a calibrated historical model.

## The decision

Use a C# simulation library with no Unity dependencies. Give Unity a snapshot to render and commands to submit. Keep topology independent of rendering: the world is a graph on a sphere, visually projected onto an oblate ellipsoid. Preserve stable IDs for cells, bands, language profiles, beasts and future households.

Unity 6 supports .NET Standard 2.1 as its default API compatibility profile. Target that profile for a separately built production simulation library; .NET Core libraries are not supported Unity managed plug-ins. See [Unity's compatibility documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html). The included SDK project targets netstandard2.1, but that target could not be built here because the .NET SDK is absent. The tested build uses the installed Windows .NET Framework compiler and a conservative C#5/BCL subset. Importing the source with `noEngineReferences` is the supplied Unity integration route; the Windows desktop DLL is not presented as a verified Standard 2.1 plug-in.

HDRP is a reasonable Windows visual target with a meaningful hardware cost. Use the matching HDRP version supplied with the chosen Unity editor/template and commit the resolved package lock after import. HDRP 17.0 documents its compute-shader requirement and supported platforms in [the official system requirements](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/System-Requirements.html). Measure the target GPU before choosing foliage density, volumetrics or shadow quality. No HDRP performance claim has been measured in this workspace.

## Current source boundaries

| File | Authority |
|---|---|
| `World.cs` | Deterministic dual-icosphere topology, ten biomes, climate, contiguous natural regions |
| `Language.cs` | Phonotactics, stable inherited roots, systematic sound change, name composition, optional borrowing |
| `HistoricalCultures.cs` | Frozen experimental founding lexicons and template provenance |
| `HistoryTime.cs` | Relative calendars and aggregate historical conditions; original legacy seasons |
| `BandEconomy.cs` | Shared domestic assistance, care, output, hunt probability and exact player chapter forecast |
| `Simulation.cs` | Two-priority chapter loop, autonomous descendants, animals, eight lived-practice milestones |
| `Encounters.cs` | Versioned mobile-unit rules, pure profiles/outlooks, targeted commands, persistent conditions/feuds and immutable outcome records |
| `Autoplay.cs` | Deterministic, read-only player-band decision policy |
| `MapRenderer.cs`, `MapRenderer.Units.cs`, `UnitArt.cs` | Windows atlas, individual unit counters/picking and renderer-only movement interpolation; no game rules |
| `GameForm.cs` and partial files | Native shell, command dispatch, People ledger, inspectors, event reading, camera state and versioned replay |
| `StoryJournal.cs` | Detached before/after observations, reconciled receipts, notices, cumulative totals and population timeline |
| `unity/Assets/Clio/Presentation` | Unity presentation starter that consumes the same core source; unvalidated in Unity |

The Windows renderer uses System.Drawing. That dependency stays out of the simulation and Unity presentation. The Windows application is a development/playtest harness, not an HDRP build or a web application.

## Executable turn contract

Commands change state only if accepted. Move/forage/hunt/befriend/camp/split spend one of two chapter priorities. Closing a chapter resolves care, camp and cattle output, community needs, demographic consequences, capacity retention, wildlife movement and ecological recovery. Invalid actions return an explanation and spend nothing. Death closes player commands; the chronicle remains readable.

`Game` takes a single `GameSettings`. Its defaults are the original Classic rules, so settings that name only the world and its founders reproduce the earliest stories; new desktop stories use MobileUnits and every later rule. Targeted `AttackAnimal(id)`, `BefriendAnimal(id)` and `AttackBand(id)` validate a living known target, range, remaining priorities, cooldown and costs before mutation. A valid adjacent encounter includes approach movement in its single priority. `EncounterRules.Animal`, `Band` and `Outlook` calculate read-only profiles without creating condition state. Persistent unit IDs key wounds, recent encounters and hostility; trust remains attached to the animal group. [Player-facing rules](UNIT_ENCOUNTERS.md).

Mobile chapter resolution heals surviving wounds, lets independent bands and wild animals act, then captures and applies household economy forecasts from the resulting state. Combat can precede hunger, exposure and births, so the journal uses the recorded post-encounter player economy receipt rather than assuming the initial forecast still describes the outcome. Ecological recovery, companion release/growth and the next calendar state follow. Companion groups follow household moves and retreats; ownerless survivors rejoin wild ecology with their identity intact.

New stories use `HistoryPace.Generations` (25 years), `Centuries` (100 years), or `Abstract` (undated chapters). Dated stories start at relative Year 0. One chapter is one aggregate resolution, not a loop over annual ticks. Provision capacity represents support across the interval. `LegacySeasons` preserves original V1/V2 outcomes; older constructor overloads keep that default for replay compatibility. The New Story dialog defaults to Generations. [Calendar and economy semantics](HISTORY_TIME.md).

Two explicit random streams separate player encounters from ecology. Language/world generation are deterministic from their seeds. There is no wall-clock random source, network service or generative-model call in play. The same initial settings and command list reproduce the tested state on this build/runtime. Production cross-platform determinism is an additional validation gate: floating-point math and a changed implementation are not guaranteed to preserve old replays.

The one-band setup contains exactly one polity and one founding language. Nearby wolves and aurochs are explicit tutorial initial conditions. Four-band setup creates one band per ancestry with independent languages. Fission transfers real people and provisions and initially preserves language identity. A provisional distance check at an 18-turn boundary can branch an autonomous band's speech; the accumulated contact/isolation model in LANGUAGES.md is future work.

## Save and replay

The prototype `.clio` file is UTF-8 text with a format header, initial seed/palette/ancestry/scenario/name, then commands. Names are Base64-encoded UTF-8 so newlines cannot become commands. Loads accept only known commands and settings, cap file size/command count, and replace the current game only after replay succeeds. Files are not executable code and use no object deserializer.

Classic stories retain `CLIO-STORY-1` for generated legacy cultures, `CLIO-STORY-2` for historical legacy templates, or `CLIO-STORY-3` for historical-time stories. V2 adds the founding-template ID after the encoded name; V3 also records `HistoryPace`. Mobile stories use `CLIO-STORY-4`, adding the initial `SimulationRules`. All four load in encounters-06. An existing Classic story can record `enable-encounters` at its current point; its V4 header still says Classic, so earlier commands replay under their original rules before the transition. New mobile stories have MobileUnits in that header. Older executables cannot read V4. Released template roots, inventories and naming data remain frozen replay content; intentional changes require a content/version migration.

Stories are now saved as `CLIO-STORY-15`: the version line, then one named `key=value` line each for `seed`, `style`, `ancestry`, `bands`, `name`, `culture`, `pace`, `rules`, `places`, `salt`, `tribes`, `terrain`, `personalities`, `gatherings` and `decisions`, then a blank line and the commands. Every key is required; unknown or repeated keys are rejected. Versions 1–14 stored the same values by position, each version appending to the last, so one frozen key order in `StoryHeader.cs` reads all of them. Older stories still load and are saved as version 15. A key added later must be optional and default to its earlier behavior. Builds before this change cannot open version 15 stories.

Autoplay is a presentation controller around a pure simulation-library policy. Each UI timer tick chooses, executes, and records one ordinary player command; the controller never adds autoplay commands to the save. Manual actions and explicit takeover stop the timer first. Save/Load/New Story pause before any modal message loop. Loaded stories are manual. Controller speed, camera-follow preference, event-flow preference and read/unread state are presentation choices, not simulation state.

The journal is rebuilt from normal commands during load. It observes a detached pre-command snapshot and the realized result, without consuming RNG or changing the game. Population history contains the initial count and successful chapter closes. Births, hunger/exposure losses and economy totals use the shared forecast only after reconciliation with actual closing population and capacity. Fission departures are tracked separately from deaths; unclassified changes are recorded as unresolved rather than invented. Replay rebuilds the archive without opening event sheets.

Mobile `EncounterRecord` entries freeze actor/target IDs, observed names, before/after counts and wounds, damage, food/travel/offering effects, trust and visibility at the event. Journal totals separate combat deaths and food from household resolution; released domestic groups are independent survivors, not casualties. Hidden movement destinations and unseen household names are excluded from player-facing notices. A selected target's current location can be inspected only while visible under the chosen discovery mode.

Compatibility for the released V1/V2 rules is protected by regression fingerprints. This small prototype format does not promise compatibility with arbitrary future gameplay changes. Camera and selected tab are not saved. Long campaigns need snapshots rather than replaying every command on load.

Production save schema:

1. Header: schema version, engine rules version, content manifest hash, world hash and full configuration.
2. State: arrays, entity tables, entity-ID allocator, turn phase, all subsystem RNG states, pending orders.
3. Knowledge: evidence records and practice bearers, with semantic identifiers independent of translated UI labels.
4. History: sparse event records, language genealogy, population ancestry and name attestations.
5. Integrity checksum and explicitly tested migration routes; reject unsupported rule versions rather than silently replaying them differently.

The writer uses a temporary file in the destination directory and an atomic replace for existing saves; writer and reader apply matching size/command limits. Actual interrupted-write and disk-full recovery tests remain gates before distributing a larger playtest.

## Scaling the polity instead of multiplying unit clicks

The prototype deliberately uses object entities and 2,562 cells. This is enough to prove globe adjacency and the survival interface. It does not establish performance for billion-person empires.

Production stores ecological and climate fields in contiguous arrays keyed by cell ID. A billion people are population quantities carried by cohorts, not a billion agents. Use 64-bit population counts and fixed-point resource quantities with checked operations. Households can be explicit locally in the early era, aggregated into cohorts when off-screen or numerous, preserving weighted practices, speech, ancestry, age and work. Keep political entities relatively sparse and represent memberships separately from residence.

No additional action points should appear merely because a polity is split into more units. Replace per-band AP with population-backed work budgets before multi-band management becomes central. The current autonomous band policy moves toward food, gathers and can attack nearby enemies. The feud and damage model is intentionally small; negotiated rights, peace treaties, conquest, coalition decisions and voluntary reunification remain future systems.

A production turn should build a read-only state snapshot, validate all orders against it, reserve stocks, resolve deterministic conflict groups, and apply a delta. This prevents command ordering from deciding who gets shared forage or wins a disputed crossing. Choose and document tie-breaking; seed it when equal-priority outcomes should vary.

## Rendering plan

The encounters-06 native shell retains the compact band-status ribbon, right-hand Place/Band/Animals inspector and persistent orders introduced in chapters-05. World, People, Knowledge, Languages and Chronicle are separate pages. The People ledger combines observed population history, cumulative receipts, household forecasts, domestic lineages and known-band links. Known-land inspectors return before reading hidden place or inhabitant details.

Every visible living animal group has an individual counter and target. Dense stacks cycle by persistent ID; selected groups show condition and strength in the inspector. The map tracks previously drawn band and animal positions independently of the simulation, easing witnessed adjacent steps over approximately 580 ms. Only endpoints visible in both frames animate; new worlds and discovery-mode changes reset tracking. The UI requests frames every 40 ms while movement remains unfinished. Targets follow the drawn position, and geometry plus hit areas are clipped to the map and visible cell mask. Pausing or speeding rendering cannot change ecology, combat, discovery or RNG.

Transient event cards preserve observation flow. A full reading sheet blocks background input and pauses autoplay; an explicit continuation resumes it when appropriate. Major events can pause automatically, while flow mode keeps decisions moving and leaves every event available in the archive. These are presentation behaviors around the journal, not new simulation commands.

Render terrain in chunked meshes; never create a GameObject per tile, tree, household or person. Maintain separate visual detail levels for globe, region and camp. The visual LOD hierarchy is separate from simulation identity. A river remains the same geographical feature as it becomes a blue stroke, a valley channel or animated water.

Use Shader Graph for biome blending, vegetation response and the main terrain material. Use HLSL only when profiling justifies a specialized cell-ID outline, fog lookup or terrain-edge solution. Blender produces a coherent family of trees, exposed rock, shelters, herd silhouettes and landmarks. Instances share meshes/materials. Fog reveals observations, not the current contents of places no one can see. The prototype's Atlas view deliberately reveals the board for design inspection; Known land mode hides unexplored cells but is not a remembered last-observation model.

Keep the world readable without relying on hue alone: mountain silhouette, canopy texture, shoreline shape, water value, cold cover, line styles and glyphs all carry information. Scale labels by screen space. Measure mouse-target size, text contrast, 125–200% Windows scaling and keyboard access in the Unity UI; the canvas-based desktop harness is not an accessibility-complete production UI.

## Gates for the next build

These are targets, not reported measurements:

| Gate | Reproducible check |
|---|---|
| Unity integration | Open pinned Unity/HDRP project cleanly; no errors; play and Windows build use the same command stream |
| Shared simulation | Compare serialized state between native test runner, Unity editor and Windows player over ten seeds/100 turns |
| Geographic fidelity | Reciprocal connected graph, 12 pentagons, seam and pole traversal, correct ellipsoid picking |
| Ecology | Biomass and herd abundance never negative; no unlimited yield from overlapping bands; multi-seed survival differences measured |
| Rendering budget | On a recorded target PC at 1920x1080, 60 fps target; measure p95 frame time, memory and turn latency at each world size |
| UI | No clipped essential controls at 1280x720/100% and 1920x1080/150%; keyboard controls and readable non-color cues |
| Save/load | All mutable state and RNG restored; interrupted writes preserve last valid save; compatibility failure explained |
| Player clarity | Five testers predict food balance and explain one knowledge unlock without reading source code |

Do not add tactical battles, global tech eras or distant future economies before this first survival-to-settlement loop is legible and worth replaying.
