# Manual, Semiautomatic and Automatic gameplay

`automatic-28` presents all three ways to play directly in the Mode selector. Story cards still apply their choice immediately without a confirmation button. This iteration is built without verification runs at the user's request: no new regression suite, screenshot rendering or automated playthrough is claimed.

## The direct action row

Manual is the default and retains individual control of each band. The bottom dock places all eight actions together, without an Orders submenu:

| Icon | Action | Tooltip explains |
|---|---|---|
| Leaf | Gather food (`F`) | The current place, one-action cost and ground depletion |
| Route | Move / meet (`M`) | The selected destination, travel cost and encounter review |
| Bow | Attack (`A`) | Target review, action cost and danger to lives |
| Heart | Befriend (`B`) | Peaceful approach, offerings, risk and group-specific trust |
| Tent | Make camp | One action, 30 food, 10 wood and the effect of moving away |
| Branching kinship | Form a tribal band | Population and food requirements and the sharing of supplies |
| Salt crystals | Gather salt | The present source and yield; away from a source, the button opens the salt ledger |
| Wood bundle | Gather wood (`W`) | Carried wood, gathering yield, fire fuel and camp cost |

Unavailable actions retain explanatory tooltips. Selecting terrain or inspecting another group does not itself spend an action.

## Choosing how to play

Use the bottom dock's **Mode** button to choose one of three cards:

| Mode | Your role |
|---|---|
| Manual | Choose each band's actions yourself. |
| Semiautomatic | Choose story decisions; the bands carry out the selected priorities. |
| Automatic | Watch the computer handle the bands and turns without story choices. |

Choosing **Automatic** starts the existing autoplay immediately. The dock shows **Automatic** while it runs. **P** or **Take control** returns you to Manual. **Watch settings** retains speed, camera following and major-event pauses.

Switching mode does not create a new game or refill supplies. Manual remains available whenever you want direct control again. Automatic is a temporary watch control: loading never restarts it by itself.

## A story grounded in the current situation

Selecting Semiautomatic offers a situation with a title, narrative, adviser portrait and two or three choice cards. The situation uses current needs and observed circumstances, such as food, salt, wood, nearby danger, household contact or possibilities for discovery. Advice has a perspective; the player decides which direction to favor.

Each card explains a proposed direction and its intended consequences. Click a card or press its number (**1 / 2 / 3**) to commit that choice and continue immediately. There is no confirmation step. Reading or hovering does not order the bands; choosing sets their priorities and starts them acting through the existing rules.

Choices normally guide the next **three turns**. Different priorities favor different real actions: gathering and finding supplies, exploration and travel, keeping households together, or responses to danger. They do not award an invented package of free resources. Movement still costs actions, supplies are still consumed, and outcomes depend on what the bands encounter. A new urgent situation can interrupt the current direction with another decision sooner.

The bottom story dock shows the chosen direction, whether the story is paused, and the turns remaining until the next decision. It also gives access to decisions already made. Where available, the next event summarizes actual changes in population, food and known places since the last choice.

## Pause, continue and return to Manual

- Clicking a choice card, or pressing **1 / 2 / 3**, immediately starts the bands with that choice.
- **Pause story / Continue story**, or **P** outside a blocking sheet, pauses or resumes the run.
- Closing a story event with **Esc** or its close button pauses it without choosing. **Continue story** returns to the decision.
- **Return to Manual**, or the Mode selector, restores individual actions.
- In Automatic, **P** or **Take control** stops autoplay and returns to Manual. In Manual, **P** starts Automatic.
- Opening Save, Load, New Story or a council discussion pauses automation. It does not silently resume after the player finishes reading.

Semiautomatic does not require clicking Gather, Move or End turn for every band. Those individual order controls belong to Manual. Speed, camera following and major-event pause preferences remain in Watch settings.

## Your decisions and saves

**History → Your decisions** retains the chosen event, turn, option and its intended consequences. **Full history** keeps the actual band actions and their results. The decision archive holds the most recent 256 choices.

A wood-enabled story saves as **V12**, retaining the mode and decision metadata introduced in V11. Metadata remembers Manual/Semiautomatic, the active priority and expiry turn, the previous event key and decision records. Automatic uses the existing autoplay controller and adds no save format or persistent third mode.

Loading validates metadata separately before assigning the restored game. Earlier files remain readable. A saved Semiautomatic story remembers its direction but **always loads paused**; use Continue story to resume. A story saved during Automatic loads in Manual, with automation stopped. An unanswered event is reconstructed from the restored situation when needed; saving does not choose an option.

V12 requires `wood-27` or later. Older Clio binaries may not understand newer formats. Loading an older save does not overwrite it; use a new filename if you want to retain a copy in its original format.

## Scope of this first version

This is an experimental situation-and-priority system. Its meaningful effects come from changing which existing actions the bands pursue. It does not yet implement permanent ideological axes, hereditary institutions, agricultural social changes or an authored multi-generation event campaign. The current goal is to make leadership choices playable before expanding those deeper arcs.
