# Wood, fire and camps

Introduced in **wood-27**. Food and salt remain essential supplies. Wood adds a useful choice: spend an action gathering fuel, use wood to build a camp, or accept the loss of fire benefits while doing something else.

## Current rules

- Each band carries its own wood. A new band receives its share when people split from an existing band.
- New stories begin with six turns of fuel at each band's starting population. Loading an older story replays its original history, then records the wood upgrade with the same initial allowance for living bands. Saving writes the upgraded story; the original file is unchanged.
- **Gather wood [W]** uses one action. Forests provide more than bare terrain. The tooltip and resource page show the current band's exact yield.
- At turn end, a living band's fire needs `max(1, population × 0.04)` wood: two wood for fifty people, with a minimum of one.
- If the band can supply the full fuel amount, it burns that amount automatically. People's food upkeep becomes `ceil(base upkeep × 0.90)`, and the fire prevents the existing cold-exposure deaths for that turn. A camp is not required to use fire.
- If the full fuel amount is unavailable, no partial fuel is spent and neither fire benefit applies. There is no separate wood-shortage death penalty. Hunger, salt shortages and other dangers still follow their own rules.
- **Make camp** costs one action, **30 food and 10 wood**. Camp construction and recurring fire fuel are separate expenses. The camp produces food while the band remains there; moving abandons it.

For example, fifty people start with twelve wood. Their fire uses two per turn. If their base food upkeep is fifty, a supplied fire reduces it to forty-five. Building a camp uses ten wood, leaving just one turn of fuel; gathering more wood becomes a useful next choice.

## Finding the information

**Economy → Resources → Wood** shows carried reserves, gathering per action, fuel needed, food saved, remaining wood and exposure protection. **Economy → Overview → Wood and fire** opens the same account.

The Manual action row adds a wood-bundle icon after Salt. Hover for the stock, gathering yield, fuel expense and camp cost. The map adds no new permanent wood beacon: the terrain and resource account provide the information.

In Semiautomatic mode, bands manage their supplies. Situation-based choices can prioritize gathering firewood or preparing a camp, while alternatives direct their work elsewhere. The story card explains the intended benefits and costs before the player chooses.

## Adviser names

Advice uses clear role labels: **Economic adviser**, **Military adviser**, **Cultural adviser** and **Social adviser**. They keep different priorities, but players do not need to learn personal names to understand who is speaking. The existing frequency choices remain High, Moderate, Low and None.

## Historical basis and limits

Cooking can make foods easier to digest and release nutrients; wooden spears also formed part of early human hunting equipment. These uses support making wood valuable beyond a construction counter. [Smithsonian: Tools & Food](https://humanorigins.si.edu/human-characteristics/tools-food).

Hearths and shelters were places for eating, care and social life. Clio currently represents only a small part of those benefits through fuel and camps. [Smithsonian: Hearths & Shelters](https://humanorigins.si.edu/evidence/behavior/hearths-shelters).

Worked, interlocking logs at Kalambo Falls provide evidence for structural use of wood at least 476,000 years ago. That supports treating construction as an early possibility, without assuming every mobile people built the same structures. [Barham and colleagues, Nature (2023)](https://www.nature.com/articles/s41586-023-06557-9).

**The resource units, six-turn allowance, costs, 10% benefit and full exposure protection are prototype design choices, not historical measurements.** They make fuel's advantage visible and keep the first wood system simple. Tools, spear shafts, larger structures and boats remain proposals in [Future development](FUTURE_DEVELOPMENT.md); this release does not add them.
