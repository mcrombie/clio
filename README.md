# Clio — The living atlas

**A historical strategy game about guiding a people, beginning with a band of fifty.**

Clio is an experimental Windows desktop game written in C#. Explore a generated hex world, maintain food and salt supplies, gather wood for fires and camps, form new bands, and watch related peoples separate into independent polities. Languages, place names, encounters and decisions become part of the recorded story.

The current source corresponds to **bestiary-35**: a hand-drawn paper campaign map with compact edge controls, ink animal sketches and paper adviser portraits. It uses a native Windows Forms interface and an engine-independent simulation. A separate Unity presentation starter is included for future development.

## Play on Windows

Clone this repository, then double-click **Launch Clio.cmd**. On the first launch, it builds the game using the Windows .NET Framework compiler and opens it. Later launches follow the current completed build.

After pulling source updates into an existing clone, run **build.ps1** before launching to compile the new version. The launcher builds automatically only when it cannot find a valid completed build.

The native build requires Windows and the .NET Framework C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. It does not require a browser, account, server or Unity installation. The .NET SDK is not needed for this build path.

To build explicitly from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
& ".\Launch Clio.cmd"
```

The executable and simulation library are written to `build/bestiary-35/`. Keep them together. Build output and saved games are not included in the repository.

Clio opens fullscreen. **F11** or **Alt+Enter** returns to a window; fullscreen is also available in the upper-left campaign menu. Save before closing an older build, then launch the updated version and load the story. Automation always loads stopped.

New saves use the named **CLIO-STORY-15** header. Earlier V1–V14 stories retain their original readers and command replay, including restoration of an active tactical battle. Compatible older stories receive recorded rules upgrades after replay, including releasing any formerly domestic deer alive under the livestock rules. V15 saves require the updated application.

## Three ways to play

| Mode | What you do |
|---|---|
| **Manual** | Select a band and choose its actions. |
| **Semiautomatic** | Choose between situation-based story options; the bands carry out your priorities. |
| **Automatic** | Watch the computer handle the bands and turns without story decisions. |

Choose a mode from the bottom dock. Automatic starts immediately; **P** or **Take control** returns to Manual. In Semiautomatic mode, clicking a choice applies it immediately, and **P** pauses or continues. **Watch settings** controls pace, camera following, event pauses and adviser frequency.

## Reading the interface

The campaign map fills the window behind compact edge controls. People, food, salt and wood sit at the top center; page navigation, advisers and events sit at the upper right. Orders and turn controls occupy opposite lower corners, while the upper-left menu holds Save, Load, New story and fullscreen. Other pages retain the fuller tribal status strip. Hover a symbol for its meaning and click for its account. Resource totals cover the tribe; warning marks identify individual-band shortages even when total reserves look healthy.

Warm parchment remains visible in unexplored space and between the sparse pen-drawn trees, hatched mountains and inked watercourses. Muted washes describe terrain; paper counters, notes and controls use restrained accents. Animal groups have naturalist ink sketches, food markers use leafy berry sprigs, salt uses cubic crystals beside water strokes, and exploration uses a compass rose. The markers sit directly on the chart, with existing crowding limits and hover explanations.

Scroll over the map to zoom. Click the selected band's name to open its details; map selections show a short card with **More details** available. Economy opens to six summary cards, with complete ledgers behind them. Advisers appear as pencil-and-ink portraits on paper. Their advice and routine events share one compact cue at a time, retaining expanded explanations, competing views and the existing frequency setting. [Interface guide](docs/INTERFACE.md).

## The early game

- **Food and salt:** essential supplies held separately by each band. Economy explains consumption, gathering and shortages.
- **Wood:** gathered with **W** or the wood-bundle icon. A supplied fire reduces food upkeep and protects against cold exposure; building a camp costs food and wood. Read the exact prototype rules in [Wood, fire and camps](docs/WOOD.md).
- **Exploration and movement:** discover and name places in your people's language. Right-click adjacent known land to move the selected band. Mountains and river crossings cost more actions.
- **Regions and battles:** geographic regions contain connected hexes. The Regions map view outlines them. Enter combat to deploy temporary formations on the real local terrain, then move, strike, defend or retreat through tactical rounds. Losses persist; interrupted campaign turns resume afterward. Automatic and Semiautomatic use the same battle rules. [Battle guide](docs/BATTLES.md).
- **People and animals:** roaming groups can meet, fight or attempt peaceful contact. Cattle and goats provide milk proportional to herd size. Slaughter gives meat but removes animals and reduces later milk. Dogs improve hunting without producing food or boosting gathering; deer cannot be domesticated. Select an owned livestock herd for production details and its Slaughter action. [Livestock rules](docs/LIVESTOCK.md).
- **Tribes and diplomacy:** daughter bands initially belong to the same tribe. Personality, travel and lost contact can lead to independent peoples. Known splinters unlock the first diplomatic relationships and gatherings.
- **Advisers:** Economic, Military, Cultural and Social advisers explain developments from different perspectives. Frequency defaults to High; Moderate, Low and None are available.
- **Culture and history:** the default founders are the Zholhen, speaking the custom Zhol language. Historical language inspirations are experimental alternatives. The journal records actions, events and semiautomatic decisions.

Use **F** to gather food, **M** to move, **A** to review an attack, **B** to review a peaceful animal approach, **W** to gather wood, and **Space** to end a Manual turn. Hover action icons for costs and explanations. **N** selects another band with actions remaining; **C** opens advisers.

## Project layout

| Location | Contents |
|---|---|
| `src/Clio.Simulation/` | World generation, resources, bands, encounters, languages and simulation rules |
| `src/Clio.Desktop/` | Windows interface, procedural map art, advisers, story decisions and save/replay |
| `src/Clio.Desktop/Assets/Advisers/` | Original generated portraits and their provenance |
| `tests/` | Existing simulation checks |
| `docs/` | Current mechanics, design notes and deferred development |
| `unity/` | Unity presentation starter and import instructions |

Start with [Regions and battles](docs/BATTLES.md), [Gameplay modes](docs/STORY_MODE.md), [Tribes](docs/TRIBES.md), [Salt](docs/SALT.md), [Wood](docs/WOOD.md), [Livestock](docs/LIVESTOCK.md), [Languages](docs/LANGUAGES.md), or the [consolidated future development notes](docs/FUTURE_DEVELOPMENT.md). The broader [game design](docs/GAME_DESIGN.md) includes proposed features; it is not a list of implemented mechanics.

## Development status

This is a working design prototype, with balance and interface behavior still changing. **bestiary-35 compiled successfully in the primary local project.** No new tests, screenshot renders or playthrough checks accompanied these design changes, and compilation was not repeated in this GitHub checkout. Existing checks remain available through `build.ps1 -Test`; optional native rendering is available through `build.ps1 -Render`. These switches are opt-in. See [Validation status](docs/VALIDATION.md).

The Unity files are a starter, not the desktop game or a finished Unity release. Follow [Unity setup](unity/README.md) to explore that path. Full 3D production, expanded institutions, agriculture and later eras remain future work.

This C# game replaces the earlier Python experiment at the repository's main branch. That project's files and commits remain available in [the previous history](https://github.com/mcrombie/clio/tree/6cdf60b).
