# Validation status

**Current source: bestiary-35.** The primary local project compiled this release successfully using the installed Windows .NET Framework compiler. Compilation was not repeated in this GitHub checkout during integration.

The release includes the compact full-window campaign layout, parchment terrain and watercourses, paper adviser portraits, ink animal sketches and drawn resource/exploration markers. It also carries the settings refactor and named V15 story header, with older V1–V14 readers and tactical command replay retained. These descriptions identify the implemented changes; compilation does not establish their gameplay, visual or save-compatibility correctness.

No new regression tests, screenshot renders or playthrough checks were run for these design changes or this repository integration, at the project owner's request. Earlier wood, story-decision and gameplay-mode iterations likewise do not acquire a new verification claim through this update.

The existing simulation checks are included in `tests/`. To compile and run them on Windows, use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Optional native smoke rendering is available with `-Render`. It writes generated output to `artifacts/`, which is ignored by Git. These switches are opt-in; a normal build compiles the game without running them.

Earlier local development used additional isolated review harnesses, screenshots and logs. Those outputs, player saves and recordings are not distributed in this repository. Some detailed design documents retain references to that historical work; they do not establish that the current source has passed those checks. Use current results when assessing a change.

The native game uses Windows Forms and System.Drawing. The Unity presentation starter is a separate integration path and has not been established as a finished, tested Unity player.
