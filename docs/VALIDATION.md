# Validation status

**Current source: sage-37.** The primary local project and this GitHub checkout compiled the simulation library and desktop executable successfully with `build.ps1` (no switches) on September 14, 2026. The corresponding source is the September 13 local Clio revision `e9dab30aff51bbb33bb117c93017e854b6f91d36`.

The release adds the guided influence opening, First Adviser portrait and embedded narration, closer starting view, numbered map destinations and route previews, restored wildlife, situation-based reports and a persistent Voice toggle. Named V16 stories record the guided mode, with older readers and explicit wildlife upgrade commands retained. These descriptions identify the implemented changes; compilation does not establish their gameplay, visual or save-compatibility correctness.

The Cromblog download contains the compiled game and simulation library, credits, and optional voice setup. Archive contents and SHA-256 hashes were checked against the release files. The additional Piper runtime is not redistributed in the ZIP; live narration uses installed Windows speech until the player runs the optional pinned downloader. No download occurs when launching Clio.

No new regression tests, screenshot renders, playthrough checks, save/replay roundtrips or audible playback checks were run for these design changes or this repository integration, at the project owner's request. Earlier wood, story-decision and gameplay-mode iterations likewise do not acquire a new verification claim through this update.

The existing simulation checks are included in `tests/`. To compile and run them on Windows, use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Optional native smoke rendering is available with `-Render`. It writes generated output to `artifacts/`, which is ignored by Git. These switches are opt-in; a normal build compiles the game without running them.

Earlier local development used additional isolated review harnesses, screenshots and logs. Those outputs, player saves and recordings are not distributed in this repository. Some detailed design documents retain references to that historical work; they do not establish that the current source has passed those checks. Use current results when assessing a change.

The native game uses Windows Forms and System.Drawing. The Unity presentation starter is a separate integration path and has not been established as a finished, tested Unity player.
