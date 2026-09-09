# Validation status

The latest design iterations, including wood, story decisions and the three-mode selector, were published without new verification runs at the project owner's request. This repository import also adds no test or playthrough claim.

The existing simulation checks are included in `tests/`. To compile and run them on Windows, use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Optional native smoke rendering is available with `-Render`. It writes generated output to `artifacts/`, which is ignored by Git. These switches are opt-in; a normal build compiles the game without running them.

Earlier local development used additional isolated review harnesses, screenshots and logs. Those outputs, player saves and recordings are not distributed in this repository. Some detailed design documents retain references to that historical work; they do not establish that the current source has passed those checks. Use current results when assessing a change.

The native game uses Windows Forms and System.Drawing. The Unity presentation starter is a separate integration path and has not been established as a finished, tested Unity player.