# The First Adviser: experimental guided opening

New desktop stories start with a small, guided set of decisions. This is an experimental rules mode with a regional scale and a shared influence budget. The older Manual, Semiautomatic and Automatic rules remain the rules of existing stories; loading one does not convert it into this experiment.

## Opening the game

Turn 1 is an evening introduction from the **First Adviser**, an old sage with practical advice. The only way to advance is **End turn**. Administrative decisions begin the following morning; the opening gives the player time to read before any orders are required. This first night does not consume supplies or change the population.

The `sage-37` opening begins at zoom **14**, twice the previous starting zoom of 7, centered on the tribe. The player can still pan and change scale after the first introduction. Aged paper and faint frontier rumors frame the known landscape; dense haze hides more distant terrain.

On turn 2, the adviser explains **influence**: the chief gains **1 influence per turn** beginning that morning. Unused influence carries forward. The experiment presents two uses for it:

- **Gather** spends 1 influence to collect food, salt and wood together. The yields reflect the abundance of the current region; salt requires an actual coastal or spring source in that region. The player does not have to order separate salt or wood gathering.
- **Move** spends 1 influence to take the whole tribe to the nearest land entrance of a neighboring region, revealing the area around it. Its resources and position provide a new setting for the next decisions. Moving does not impose an additional travel-food charge in this experiment.

From turn 2 onward, ending a turn advances population growth, food and salt consumption, wood fuel use, land recovery and, when the recorded wildlife upgrade is active, peaceful animal movement and growth. The First Adviser explains the current situation as these controls become relevant. This opening has one founding band and leaves autonomous combat, band splitting, camping, separate resource orders and the larger collection of band commands outside the experiment.

## Controls appear gradually

Turn 1 offers only End turn as a gameplay decision, alongside a readily available Voice toggle. Turn 2 introduces Gather and Move through the First Adviser's morning lesson. The Supplies panel, First Adviser button and compact menu become available from turn 3, so the initial decisions arrive before the extra interface.

| Control | Available from | Action |
|---|---|---|
| Space | Turn 1 | End the turn |
| Voice / V | Turn 1 | Mute or unmute narration; the choice is remembered |
| F | Turn 2 | Gather food, wood and available salt for 1 influence |
| M | Turn 2 | Open neighboring regions; click a destination to move for 1 influence |
| Ctrl+S / Ctrl+O | Turn 2 | Save / load a story |
| C | Turn 3 | Open or close the First Adviser's counsel |
| Supplies and menu buttons | Turn 3 | Inspect resources or access save, load and other map options |

## Choosing a destination on the map

Move opens a compact drawer on the left, leaving the map visible. Its numbered destinations correspond to numbered arrival pins. Hovering either a row or a pin previews the tribe's path and destination before influence is spent. Known route segments use solid ink; uncharted segments use dashes. This indication does not reveal hidden terrain, resources or occupants.

Click the destination row or its arrival pin to move immediately for one influence; there is no additional Confirm step. The tribe arrives at the region's actual entrance cell. Canceling closes the drawer and restores the camera position and zoom from before the preview.

## Animal observations

Animal groups appear from turn 2, using the same paper counters and compact stack badges as the larger game. Click an animal counter or group badge to read an observation card with the groups present and their actual populations. Inspection gives no orders and spends no influence.

With guided wildlife enabled, wild groups can move to neighboring land at turn end, with species-specific movement rates and a modest preference for forage. Timid grazers favor unoccupied ground. Population growth uses the existing animal rules. The first night remains quiet, and this peaceful loop does not start attacks against the tribe or advance independent peoples. Hunting, taming and combat are not yet introduced as guided orders.

Only animals on known land contribute to observations. An animal that wanders away can leave the visible map; wildlife is not kept near the tribe merely to supply an adviser update.

## Reports that follow the situation

The First Adviser uses the actual command and turn results: food changes, upkeep and spoilage; salt and fuel used; population changes; and newly observed animal groups. Later reports no longer repeat the generic Gather-or-Move paragraph every turn. Routine results remain in the bottom summary, and opening First Adviser gives the latest available account.

Automatic reports are reserved for lessons and meaningful developments such as newly observed wildlife, new or worsening shortages, recovery and population changes. A continuing concern can be repeated after four turns instead of interrupting every turn. Reports remain dismissible, and Explain more retains the basic influence and supply explanation.

## First Adviser voice

Voice defaults to on. A visible Voice control is available from turn 1, including over the introduction and travel panels; **V** toggles mute. The mute preference is stored between sessions. Closing counsel, switching away from it or issuing a new order stops the current speech.

The adviser uses a synthetic Northern British male voice with a slower, measured delivery. `opening.wav` and `influence.wav` are embedded recordings. Live reports narrate the current report text through a local Piper engine when available. No real person's or fictional character's voice was cloned, and dialogue is not sent to a speech service.

The optional setup script `tools/prepare-adviser-voice.ps1` downloads approximately 97 MB of engine and model files into `build/narration/runtime`. A subsequent build copies that directory into `build/sage-37/voice`. Without the runtime, the two embedded recordings remain available and live reports fall back to an installed Windows voice, whose sound depends on the machine. See the [voice asset README and licenses](../src/Clio.Desktop/Assets/Advisers/Voice/README.md) and [portrait provenance](../src/Clio.Desktop/Assets/Advisers/FIRST_ADVISER.md).

New Story checks **Guided beginning** by default and uses one founding band. Uncheck it to start with the older Manual rules, whose Mode selector also offers Semiautomatic and Automatic. Loading an older save likewise keeps those earlier rules.

## Saved stories

The `CLIO-STORY-16` header records the founding `GameSettings.GuidedOpening` flag as `guided=GuidedOpening` or `guided=LegacyOpening`. The field is optional when reading version 16; its absence means the older rules. Version 15 named headers and versions 1–14 positional headers always restore their original mode. Saving an older story writes the new header with `LegacyOpening`; it does not change the story's rules.

Influence and gathered resources are simulation state reconstructed by replay. The saved command record uses `end`, `guided-gather` and `guided-move:<cellId>`, with the same seeded world and founding settings. The move's cell ID identifies its destination in that world. No new presentation metadata is required for the opening; the adviser presentation can be restored from the mode and current turn.

`sage-37` retains V16 and adds the recorded `enable-guided-wildlife` command. New guided stories record it; older guided stories first replay their existing commands with their original stationary-wildlife behavior, then record the upgrade for future turns. Enabling wildlife does not advance time or move any animal itself. Applications released before this command cannot read a story containing it, even though its header remains V16. Original save files are not overwritten by loading.

The separate Semiautomatic decision-record schema is unchanged. Loading never upgrades an existing story into guided mode, and it does not start an autoplay timer.

## Development scope

This opening deliberately concentrates on learning the chief's smallest useful decisions. It is an experimental branch of the early game, rather than a claim that the larger economy, household organization or combat systems have been replaced everywhere. Historical flavor and additional advisers can follow once the influence loop is understandable.

Tests, screenshot renders and automated playthroughs remain paused at the user's request. Build status is recorded in the release notes; this document does not claim runtime verification.
