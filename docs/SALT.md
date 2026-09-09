# Salt and survival

Salt is a separate reserve carried by each band. These are abstract game units, scaled to the turn system.

- Demand is one salt per ten living people each turn. Fifty people need 5 salt.
- Founders receive four turns of salt at their initial population: 20 for fifty people.
- Coastal land has salt deposits; sparse inland springs provide sources away from the sea. A safe source is available at or beside each founding band. Source geography is deterministic and does not change when the map is drawn.
- Move onto a source and choose **Orders → Gather salt**, or use the action in its map card or **Economy → Resources → Salt**. Collection spends one action and yields five turns of salt at the band's current population.
- Salt travels with the band. Forming a daughter band shares the existing reserve in proportion to population; it does not create more salt.
- Sources are renewable in this prototype. Collection does not deplete food gathering, and salt has no preservation or trade bonus yet.

The close-of-turn forecast consumes the salt demanded by the population entering household resolution. Any deficient close advances a consecutive-shortage counter. After one, two and three deficient turns, gathering operates at 90%, 80% and 70% respectively. Cohesion also suffers and births pause. The fourth deficient close, and each further one, causes losses of up to 2% of the surviving population, rounded upward with a minimum of one. Food, exposure and salt deaths are accounted for separately and never exceed the living population.

Collecting salt restores the reserve immediately. A full supply at the following turn's close clears the shortage and restores normal gathering; cohesion recovers over subsequent supplied turns. Population growth increases the next turn's demand.

The map ribbon shows reserve coverage and links to the detailed ledger. Crystal markers, hover notes and source cards identify only sources in remembered land, including when Atlas view is open. The ledger lists known sources, current demand, projected consumption, remaining salt and shortage consequences. Low reserves, shortages, losses and recovery produce readable events; **Inspect** opens the salt ledger.

Autoplay and independent bands seek remembered sources, collect salt, and balance collection against food needs. Route choice avoids unknown or hostile ground. Source lookup, drawing, forecasts and policy choice do not consume simulation randomness.

## Save compatibility

V1–V5 saves replay their original rules before any salt upgrade. Normal Load introduces salt afterward as a recorded `enable-salt` command, with a four-turn reserve for each living band. Repeated enabling cannot refill supplies. Direct replay of an old file remains unchanged.

V6 records the initial naming and salt rules independently. `SaltEconomy` enables salt from the founding; `LegacySalt` retains the original rules until the recorded upgrade. The `salt` command records ordinary collection. Salt sources, stores, deficits, forecasts and events are reconstructed deterministically from the seed and command history. Invalid or incomplete headers are rejected without replacing the current game.
