# From a band to a tribe

Forming a daughter band now creates another **controlled household within the same tribe**. It transfers people, provisions and salt without removing them from the tribe's totals. The new band inherits language and place knowledge, carries its own supplies, and can begin acting on the following turn.

Every living controlled band has **two actions per turn**. Select its banner or its row in **Units**, then give orders. Right-click movement, gathering, salt collection, camps and encounters use that particular band's position, supplies and action budget. The command source stays visible in the map dock. Inspecting an animal or another people preserves the selected actor; an encounter review retains the actor it was opened for. Companions follow and support their own household.

## Reunion and separation

The leader's current hex is the tribe's reunion place. A camp can anchor the leader there, or the leader can travel. A band renews contact by occupying that same hex; merely being nearby does not count. The meeting takes effect immediately after movement, or when co-located bands close a turn together.

New bands have a lasting, semirandom **disposition**, reproducible from the story seed and band identity. The current prototype distributes them as 25% Loyal, 25% Restless, 30% Adventurous and 20% Separatist. These are tendencies, not new orders or political alignments.

| Disposition | Drift starts | Independence, adjacent to leader | Independence, 2+ hexes away | Tendency |
|---|---:|---:|---:|---|
| Loyal | 8 missed closes | 18 | 18 | Usually stays close; seeks reunion after two missed closes. |
| Restless | 4 | 9 | 8 | Wanders, then seeks reunion as drift begins. |
| Adventurous | 2 | 6 | 5 | Favors room to explore away from the leader. |
| Separatist | 1 | 4 | 3 | Strongly favors a separate path. |

The distance column uses graph adjacency, not a movement-time estimate. Mountain and river costs still apply to the return journey. **Units** and the selected-band card show the actual current deadline; it can shorten when a daughter moves farther from the leader. Meeting on the leader's hex resets separation before a secession can occur. Legacy stories retain their original 8-turn drift and 16-turn secession until the new rules are enabled.

An untouched daughter with actions and adequate food and salt may use **one ordinary move at turn close**. It considers known, passable, productive terrain and avoids observed hostile ground. Loyal bands have a 10% roaming tendency, Restless 40%, Adventurous 75%, and Separatist 95%; reunion and survival priorities can override a roaming choice. It pays normal movement/provision costs and carries its companions. The leader never moves voluntarily. This limited autonomy does not gather, attack, tame or split bands.

**Any successful order** prevents that household from wandering voluntarily for the rest of the turn. **Hold** on the band card or **Hold this turn** in Units explicitly spends its remaining actions without moving. Normal food and salt upkeep still applies. Hold does not reset contact or stop the separation clock: reunite to keep a distant daughter in the tribe.

**Units** shows the leader, each band's actions, its contact state and a link to the reunion hex. Shared heraldry distinguishes tribe membership while each household keeps its own name and selection target.

A seceding band keeps its living people and supplies. Its language branches from the parent speech and its cultural profile begins to differ modestly. Separation alone does not start a war. History and Demographics record a living departure, not deaths. A completed reunion resets the counter before separation occurs.

## Continuing after a leader dies

The founder's identity remains stable in the simulation and historical record. If the leader dies, a surviving member band becomes leader deterministically, without changing band IDs or merging stores. Play continues until no living band remains in the player's tribe.

The final account then shows the recorded immediate cause, final turn, population peak, births, deaths, explored places, learned practices and bands founded. Only observed surviving peoples appear in its survivor summary. You can save the history, read it, survey the map, load a story or begin again. The account can be reopened after dismissal.

## Accounting and compatibility

Each household has its own food and salt, and the selected band's ledger exposes its actual forecast. Cumulative population and resource accounts cover the controlled tribe. Internal splits do not create spending or population departures; secession transfers living people and supplies out of those totals. Combat, hunger, exposure and salt losses remain separate causes.

V7 introduced the initial `TribalBands` / `LegacyBands` rule marker and explicit commands such as `band:2:forage` and `band:1:move:623`. A global `end` closes the turn for all bands. Autoplay supplies each household and uses its disposition to choose between reunion and exploring farther away. `wait` spends that band's remaining actions before the tribe closes its turn.

V9 records `BandPersonalities` or `LegacyPersonalities` after the terrain marker. A recorded `enable-personalities` command introduces the rules after a legacy replay without rewriting earlier decisions. Voluntary moves are deterministic consequences of `end`, with normal encounter movement records and linked receipts; their provision costs are included exactly once in the tribal ledger. Dispositions do not consume either simulation random stream.

Older saves replay their original rules first. The normal Load flow then records the new rule upgrades for future play. There is no reliable historical parentage for old independent bands, so migration keeps those peoples independent; only future splits establish controlled tribal membership. Shared ancestry is never treated as proof of political membership.
