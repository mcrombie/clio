# Clio — pre-bronze design, v0.1

**Status:** original design target, not a claim that every system below is implemented. The repository's runnable prototype demonstrates a deliberately smaller slice; consult the README for the current boundary. Values marked **v0.1 tuning** are proposed gameplay parameters, not claims about historical populations, animal biology, or archaeological chronology.

Clio is a desktop game about keeping a community alive long enough for its choices to become history. Start with fifty people, a language, practical knowledge, and relationships. Survive a place, learn how to live there, and decide what obligations bind increasingly different communities together. The player directs a **polity**; a civilization, species, language, or fixed culture is never the player character.

The name fits particularly well: Clio is conventionally the Muse of history; Calliope is the Muse of epic poetry. The game's intended feeling combines both—historical consequences with the emotional scale of an epic.

## 1. The promise and the boundary

An ideal early decision is: “The river will feed more families, but our seed stores and sick relatives will anchor us here. The ridge households want to follow the herds. Can we remain one people without living in one place?”

The answer should emerge from stocks, relationships, routes, and political arrangements. It should not be a scripted choice between +10% growth and +10% movement.

Design pillars:

1. **Survival creates history.** Winter preparations, a dangerous migration, a rescued household, and a failed tame attempt should leave persistent consequences.
2. **Capability grows through practice.** Communities learn by doing, observing, teaching, and keeping skilled people and material conditions alive.
3. **More people create both strength and obligations.** Growth requires food; food alone does not solve crowding, trust, transport, or political consent.
4. **Several ways of life remain competitive.** Mobile, settled, and mixed communities have distinct opportunities and costs. No button permanently changes a civilization from “tribal” to “civilized.”
5. **World geography matters at the scale of a journey.** Water crossings, winter routes, mountain passes, animal ranges, and seasonal return sites make the map readable and consequential.
6. **Identity has several layers.** Political allegiance, cultural practice, speech, household kinship, and biological ancestry can change independently.

The first substantial game ends before bronze production becomes a dominant system. It supports camps, managed gathering landscapes, herding circuits, gardens, villages, small towns, raiding, assemblies, alliances, and confederacies. It does not need industrial economies, dynastic courts, tactical battle scenes, or an entire planet simulated at household resolution to prove its appeal.

The engine-independent C# simulation is authoritative. Unity 6 presents a globe, commands, readable seasonal changes, overlays, audio, and animation. HDRP is the visual target, but simulation rules and save data must not reference Unity objects or renderer types. Map topology, rendering, and language generation have their own companion design documents; this document specifies what those systems must accomplish for play.

## 2. Starting conditions and campaign shape

### One founding band — the default

The player names a band of **50 humans** and chooses or randomizes its founding language. It starts with ordinary survival skills, kin relationships, tools, and a fire. “Blank slate” means no preset national identity or historical destiny; it does not mean people begin without language or knowledge.

**Executable slice defaults:** 210 provisions, two action points per turn, and a twelve-turn repeating seasonal cycle. The proposed larger design preserves those as initial balance anchors while adding richer stocks and household labor. A provision feeds one person for one turn under ordinary conditions. The season cycle is a game rhythm, not a twelve-year calendar.

There are no hidden foreign human polities in the one-band scenario. Later autonomous bands come from actual members leaving, negotiated expeditions, distant communities becoming self-governing, or descendants of earlier departures. Newly revealed human populations must have an explainable ancestry and migration record.

The early opposition is environmental: food shortfalls, exposure, dangerous animals, disease events, and uncertainty. Rivalry develops when a population that once fit around one fire can no longer agree on how to share places and obligations. Do **not** guarantee fission on a turn number or at a hard population threshold. A skilled player may sustain a dispersed kin network for a long time.

### Four founding bands — alternate setup

Start four independent bands of 50: human, elf, dwarf, and goblin. Each begins in a viable, appropriately favored but nonexclusive habitat, with reachable opportunities beyond it. The player selects one; the other three use the same simulation rules. Every band receives food security for initial exploration, rather than one ancestry getting a guaranteed peaceful start.

Each founding band has its own generated proto-language by default. “Related founder languages” is an optional world setting; biological ancestry never implies a specific language. Support a later custom setup with any technically comfortable band count, but build and balance one and four first.

### Success and defeat

The first playable's guided objective is to survive one full seasonal circuit and establish a sustainable second source of security: a known return route, a reliable cache, a managed animal relationship, or an anchored settlement. A longer scenario evaluates a **chronicle**, including surviving descendants, connected communities, resilient food systems, lost practices recovered, and lasting agreements.

Do not award victory solely for territorial coverage or population. Expansion that destroys the household network is a comprehensible failure even if the map has become one color. Campaign defeat occurs when the player's political community has no viable members or the player accepts absorption without a successor polity. A configurable succession option can continue as a descendant polity; it must explicitly transfer the player role and preserve the earlier polity's history.

## 3. The turn: commitments, consequences, learning

Turns represent meaningful episodes. One turn may compress a difficult journey, a season of tending, or a period of family growth. Never display a fixed Gregorian year count implying that every episode lasted the same number of days. A season wheel forecasts local environmental pressure; animal reproduction and demographics use explicit episode counters, not a hidden promise that one turn equals one year.

The initial command model grants **two action points per active band**. A neighboring move, forage, hunt, animal contact, camp improvement, or deliberate split takes an action. Waiting does not create food. Movement changes where the next action happens. Preview the immediate food balance and the next two seasonal steps before ending the turn.

The fuller design replaces a large stack of two-action units with a compact **work allocation panel**. Households contribute effective labor after care, illness, and travel needs. Tasks show expected yields and uncertainty. A settled population can gather, repair shelter, and teach simultaneously; creating tiny bands must not multiply the population's available labor.

Recommended deterministic resolution order:

1. Commit orders and reserve inputs; reject unaffordable or geometrically invalid orders without consuming them.
2. Move groups, determine contact, and resolve contested access. Rotate equal-priority ties with a seeded rule instead of permanently favoring the first player.
3. Resolve work, animal interactions, conflict, and resource depletion from the resulting positions.
4. Update stores: transfers, preservation, spoilage, consumption, exposure costs, and ration policy.
5. Apply household health, births, deaths, confidence, grievances, and migration proposals.
6. Apply ecology renewal and wildlife movement; record evidence and practice transmission.
7. Advance the season, publish the chronicle, and update forecasts.

All randomness belongs to the simulation seed. Forecasts distinguish **known amount**, **estimated range**, and **unobserved risk**. The UI must never advertise a guaranteed tame outcome when a random failure remains possible.

## 4. Food, population, and anticipation

Treat food as physical supplies located with bands, camps, stores, and herds. An isolated village cannot eat the polity's distant food total. Shared political allegiance does not teleport supplies.

Proposed economy:

| Rule | v0.1 tuning / target behavior |
| --- | --- |
| Ordinary consumption | 1 provision per resident per turn, dependents included |
| Starting population and provisions | 50 people, 210 provisions |
| Birth eligibility | At least two consecutive secure turns; after the proposed growth, projected provisions and accessible income still cover two ordinary consumption turns |
| Growth limit | No more than 4% of population per eligible two-turn growth event; round consistently and show the result |
| Food quality | First slice has one provision type; later, prolonged single-source diets cause a visible health liability rather than immediate surprise death |
| Portable capacity | Initial target: roughly two consumption turns in ordinary equipment; departure previews show what must be cached, carried, or abandoned |
| Stationary buffer | A functioning prepared store can hold several consumption turns, with maintenance and access costs |
| Harsh season | Local forage opportunity falls before exposed groups suffer attrition; warnings precede both |
| Starvation | Spend available food, report the deficit, then apply graduated health loss; avoid killing an arbitrary entire band after one missed provision |

The starting 210 provisions are a tutorial and exploration buffer. Portable-capacity limits belong to the larger inventory system; do not retroactively discard the prototype's starting surplus. When transport is implemented, allocate the initial buffer across band loads and the founding camp with a clearly visible retrieval option.

Births are a game abstraction for household reproduction and effective population growth over compressed time. In the fuller model, care burdens and dependents delay the labor benefit. The first slice's immediate population increments must not be mistaken for a finished demographic model.

A strong location supports more people; more people consume the slack that made it attractive. Permanent structures, seed stocks, dependencies, and neighboring claims can make departure costly. This is the **sedentary dependence** the game should model. It is a state of commitments and carrying capacity, never a one-way technology unlock. A village can return to mobility by preparing routes, portable stores, suitable equipment, and agreements—and accepting that some households may remain.

Scarcity should be legible. The player sees “next turn: mild; in two turns: river freezes,” estimated local renewal, and the provisions needed to reach known alternatives. Forecast accuracy improves through observation and local teachers. Tropical and arid regions use drought, flood, or migration rhythms rather than a temperate winter with different artwork.

## 5. Keeping several ways of life competitive

Mobility is an actual distribution of residence and work, not an ideological slider with a permanent bonus.

| Strategy | Distinct strength | Persistent cost | Failure pressure | Pre-bronze ambition |
| --- | --- | --- | --- | --- |
| Mobile gathering / hunting | Follow patch renewal; escape local disaster; maintain distant knowledge | Transport limits, route uncertainty, no constant access to heavy stores | Lost corridors or repeated overhunting | Wide kin networks, trusted guides, seasonal exchange centers |
| Mobile herding | Move stored subsistence on its own feet; exploit dispersed pasture | Herd feed and water, offspring vulnerability, competing grazing claims | Drought, disease, interrupted routes | Pasture leagues and powerful assembly networks |
| Settled gathering / fishing | Repeated access to exceptionally productive places without requiring crops | Reliance on runs, floods, nearby extraction radius | Ecological shift or access conflict | Substantial seasonal villages and durable communities |
| Gardening / settled cultivation | Invest labor for more predictable local supply and larger reserves | Seasonal labor peaks, seed protection, soil and water demands | Bad harvest plus high resident consumption | Villages, shared works, small towns |
| Mixed / seasonal residence | Combine a durable store with moving work groups | Coordination, transfer costs, divided priorities | Untrusted storekeepers or disconnected dependents | Confederacies of camps, villages, and circuits |

Never give settlement a global output multiplier or mobility a blanket combat bonus. Benefits arise from mechanics: a cache avoids spoilage, a moving herd avoids exhausted pasture, and a permanent landing makes a fish run easier to harvest. No pre-bronze strategy promises mounted steppe conquest; future horse riding and imperial logistics would be later, independently designed capabilities.

Balance should compare **security, recovery, reach, and influence** as well as total output. A mobile population may be smaller but harder to starve and better connected. A dense town may employ specialists but need outside partners more urgently. Repeatedly migrating across untouched tiles should eventually incur travel, information, and replenishment constraints rather than become unlimited free loot.

## 6. People, polity, and political change

Use separate records rather than a single civilization object:

| Entity | Owns / describes | Must not silently determine |
| --- | --- | --- |
| Person or household cohort | Members, ancestry, kin links, care needs, skills, affiliations | The entire polity's language or loyalty |
| Local community | Residents, daily practices, repertoire of languages, access arrangements | Exclusive ownership of every surrounding cell |
| Band / work group | Position, carried assets, orders, household membership | A separate action economy without population cost |
| Polity | Offices, decision rules, obligations, member communities, agreements | One homogeneous culture or one biological lineage |
| Confederacy | Explicit delegated powers and contributions from member polities | Automatic annexation of those polities |
| Culture / tradition network | Shared practices, stories, inheritance and teaching relationships | A universal superiority score |
| Language variety | Shared forms and historical relationships among speakers | A biological race |

For the first slice, a band can aggregate households. Preserve stable IDs and provenance so later household detail can replace aggregate fields without rewriting the political model.

### Voluntary fission

The player may equip a daughter band, appoint or recognize a leader, choose its departing households, and negotiate expectations. Split food, tools, animals, and dependents explicitly; keep population and inventories conserved. A ten-person offshoot cannot create another fifty-person founder. Record why it left and which knowledge carriers went with it.

The offshoot can remain a subordinate community, become an equal confederacy member, become an ally with a kin tie, or seek complete independence. These are different arrangements. Distant descendants may renegotiate them later. The player should understand whether the new band is still directly controllable before confirming the action.

### Involuntary fission

Build pressure from specific grievances: repeated unsafe orders, unequal rationing, exclusion from decisions, unpaid communal obligations, distance from a store, or rival household leadership. Each grievance names affected households and an achievable response. Continued strain produces a publicly visible secession proposal, a refusal to contribute, or a departing group—not an unexplained random revolt.

Initial target: give at least two turns of actionable warning before ordinary secession. Sudden leader death or immediate violence can shorten the warning, but those exception causes must appear in the chronicle. Loyal members do not automatically leave because total population crossed 100.

### Confederacies and kinship

A confederacy is a network of commitments: river access, annual assembly, food relief, agreed defense, exchange hosts, and limits on intervention. Members retain offices and local identity. Power comes from trust and enforceable benefits; missed contributions and incompatible needs create negotiations.

Kin relations improve an initial willingness to listen, not permanent obedience. A descendant rival can have the same language and ancestry while disputing fishing rights. A household of a different ancestry can become central to a confederacy through teaching, adoption, friendship, or service.

### Absorption and composite cultures

Joining a polity changes political membership first. It does not instantly replace language, customary law, or techniques. Retain local carriers, provenance, and disputes. A newcomer community may introduce marsh boatbuilding while retaining its preferred marriage customs and home language. The polity may become multilingual, teach selected techniques, and negotiate conflicting access rules.

There is no “culture upgrade” that consumes the smaller group's identity and awards all its bonuses. A shared store law may spread broadly while local food traditions persist. A formerly subordinate language may become prestigious in a trade niche. Composite culture means a changing distribution of practices and relationships, not a new painted label that erases its sources.

The prototype's small cultural practice vector is a diagnostic summary of repeated actions. It must not become the authoritative identity model. Final UI summaries should be derived from local distributions, with a visible minority or disagreement where one exists.

## 7. Wildlife, fantasy ancestry, and domestication

### Ecological actors

Large herds, predator packs, solitary megafauna, and dragons are map actors with home ranges, feeding needs, fear, habituation, reproductive capacity, and remembered encounters. Small fauna and plants remain habitat resources. Display the economically important actors; do not instantiate every rabbit.

Herds seek food and water, avoid recent danger, and change routes when pressured. Predators follow prey and vulnerable opportunities but retreat from costly fights. Kills reduce an actual herd stock, carcasses decay, and overharvesting can damage future supply. Repeatedly drawing the same herd across a border must not respawn its population.

Interactions include observe, avoid, drive away, hunt, scavenge, offer food, habituate, protect, capture, and managed breeding. Each names its purpose and expected risk. Cooperative encounters can fail: injury, stolen food, offspring loss, or an animal returning to the wild. A failed encounter should not secretly reset ten turns of progress without explaining why.

### From contact to a domestic lineage

Ten positive contacts are a **relationship requirement**, not a magic replacement for breeding, ecological suitability, or maintenance. The full target requires:

- Evidence credited at most once per species and community per turn, across at least twelve observation episodes.
- At least ten successful, materially costly contacts; repeated zero-cost feeding commands cannot advance the count.
- Species suitability, enough healthy founder animals, suitable care, and two recorded successful managed reproduction episodes.
- A credible food, water, containment, and handler plan through a difficult local phase.
- Local knowledgeable carriers who can teach the practice.

For wolves, trusted companions may provide an earlier, limited watch or tracking benefit before a reproducible dog lineage exists. For cattle-like animals, a modest managed founder group precedes a large herd. **Do not grant 100 cattle from an encounter counter.** A herd reaches 100 through reproduction, exchange, or acquisition with explicit conservation and costs.

The executable slice can approximate lineage formation with ten contacts, observation duration, and food spending. Label that as an abstraction; successful breeding and carrier continuity remain full-game requirements.

A lineage records founders, habitat history, selected traits, handling practice, related lineages, and current population. Candidate traits include cold tolerance, heat burden, size, feed demand, docility, vigilance, and travel endurance. Use tradeoffs and bounded change: a large cold-adapted animal may consume more and struggle in heat. Trade transfers animals and, if agreed, instruction; it never awards the same animal stock to both parties. Population collapse can extinguish a lineage while records of it remain in the chronicle.

### Fantasy without fixed destiny

Human, elf, dwarf, and goblin describe biological ancestry. The first version can use small, transparent adjustments to habitat work and exposure. Proposed maximum routine advantage is about 15–20% in a favored habitat; this is a balance hypothesis, not a scientific model. Humans receive broad baseline viability; elves excel in forests; dwarves in mountains and somewhat hills; goblins in mountains, barrens, and hills. All need food, shelter, knowledge, and relationships.

Do not encode intelligence, morality, preferred politics, professions, or language in ancestry. Environmental learning can outweigh an unfamiliar ancestry's innate affinity. Mixed communities retain their members' affinities locally instead of assigning the polity a majority-species bonus. Household formation and biological inheritance can follow a configurable fantasy lore rule; adoption and political belonging work regardless of that rule.

Dragons should reshape local decisions rather than roll a random campaign-ending attack. Start with rare territorial actors, visible signs, a discoverable range, and avoidance routes. A dragon's presence might protect a valley from grazers, create scavenging opportunities, and make a pass dangerous. No unavoidable lethal attack in a new band's initial safe radius. Taming dragons is outside the first pre-bronze release; observation, avoidance, tribute-like feeding, and coordinated deterrence are sufficient.

## 8. Knowledge as a maintained practice web

The [advancement web](ADVANCEMENT_WEB.md) defines 28 proposed nodes. Each has explicit all/any prerequisites, credited evidence, a commitment, an effect, and maintenance. Neither research points nor repeated button presses substitute for those conditions.

Practice progression is **unfamiliar → observed → practiced → reproducible → transmitted**, with **dormant** and **locally lost** as possible outcomes. A practice is located among carriers in communities. It can survive political defeat, spread without conquest, or disappear while the polity remains large.

An attempt is not useless if it fails to unlock a node: it can still feed people, establish a relationship, reveal risk, or teach why the method failed. Experiments show likely requirements without revealing exact hidden future outcomes. The UI should tell the player “needs another successful cold-season return” rather than “93/100 research.”

Local variants inherit provenance and environmental tradeoffs. A forest dog tracking tradition differs from an open-plains alert tradition; neither provides the other's benefits automatically. Imported practice requires local testing if materials, climate, or animals differ. Theory and oral memory can outlive active practice, allowing recovery at less cost than first development.

Chapters of an age are descriptive summaries. Track subsistence diversity, coordination reach, permanence of investment, and transmission resilience independently. A mobile league can enter an era of interwoven communities alongside a town society without constructing houses. Material labels such as “pre-bronze” define scenario scope; they should not imply a universal ladder of better cultures.

## 9. Language and living names

The language system serves three game functions: recognizable historical descent, regional identity, and the cost and opportunity of communication. It does not need fully generated grammar or a complete dictionary.

In the one-band world, every newly formed human language descends from the configured founding proto-language. Split communities initially speak varieties of that language. Separation, continued contact, prestige, borrowing, and bilingual households determine later divergence. A new polity does not automatically get a new language, and a political reunion does not immediately merge languages.

Store underlying meanings and stable place IDs separately from surface names. River names, ancestor names, kin terms, animal names, and settlement terms can change through regular sound changes and borrowing. Show an old name and a local name when necessary so players can still navigate. Preserve player-selected spellings as optional display names while recording internally evolved forms.

Setup offers pronounceable presets and a seedable randomizer, with deeper controls for sound inventory, permitted syllables, stress, and spelling style. These are aesthetics and history settings, not bonuses to technology or moral character. A bounded vocabulary and transparent sound-change history should make related names feel related. Detailed generation and testing belong to the language design document.

## 10. Fifteen turns in a founding band's history

**Illustrative narrative, not a simulator trace or a balance guarantee.** This example uses two orders per turn and a temperate cycle: thaw 1–3, warm 4–6, harvest 7–9, lean 10–12, then repeat. Stocks are intentionally qualitative because weather, terrain, and hunting outcomes change the arithmetic. Population figures illustrate gated growth; they are not historical demographic rates.

| Turn | Orders and observation | Consequence / emerging history |
| --- | --- | --- |
| 1 | Forage a river margin; observe a nearby wolf pack | Fifty people identify rich food and risky neighbors. The founding river receives a name from the proto-language. |
| 2 | Forage; improve the founding camp | A second secure episode permits growth to 52. Dependence on this good location begins as a choice, not an era reward. |
| 3 | Move to the ridge; forage | A pass and a sheltered basin enter local route memory. Leaving the rich cell costs an opportunity but gains future options. |
| 4 | Hunt a migratory herd; preserve part of the catch | Success and secure stores permit growth to 54. The same hunt also depletes the herd; repeating it is not free progress. |
| 5 | Return to the river; offer food to the wolves | One credited positive contact costs real provisions. No dogs or domestic herd appear. |
| 6 | Forage; prepare a cache | Workers spend the apparent surplus on insurance. A household argues for staying permanently; growth is deferred if the new reserve test fails. |
| 7 | Inspect the return basin; forage | The basin has renewed; a seasonal-route practice gains evidence from a genuinely repeated visit. The forecast now makes winter preparations urgent. |
| 8 | Return to the cache; preserve gathered food | Sustained security permits growth to 56. A remembered route and a maintained store support different future paths. |
| 9 | Improve shelter; offer food to the wolves | A failed contact injures a handler and consumes food. Trust falls locally; earlier observations remain recorded. |
| 10 | Ration and forage; maintain the camp | Lean-season forage is low. The band draws down its cache; births stop. The UI explains the deficit and remaining buffer. |
| 11 | Forage; teach firekeeping and shelter repair | A second carrier learns critical skills while travel is unattractive. The lesson protects knowledge from the injured handler's absence. |
| 12 | Forage; repair the cache | The band survives with an adequate reserve because it prepared. A winter story names the ridge and its shelter practice. |
| 13 | Move to the renewed river margin; forage | Actual renewal confirms part of the annual route. A household tending useful plants sees a reason to return rather than follow the distant herd. |
| 14 | Forage; hold a household assembly | Renewed security can permit growth to 58. Two groups negotiate how a river community and a mobile group could share the cache. |
| 15 | Provision and authorize a split; send the mobile group toward the pass | Twenty-two people depart and 36 stay. Their political agreement, food division, inherited practices, and common language are explicit. Neither side automatically becomes a rival. |

The point of the example is the accumulating causal chain. A location led to a store; a store made winter survivable and permanence attractive; a route made departure viable; an assembly turned conflicting preferences into a negotiated political arrangement. Three wolf contacts are a relationship story, not a domestic lineage. If the player had pursued a whole-band migration instead, the campaign would still have advanced.

## 11. Information and visual experience

The globe should communicate a habitable place before it communicates a spreadsheet. Terrain forms, water, canopy, snow, animal silhouettes, and visible movement provide orientation. Detailed map topology and the Unity production plan are specified separately.

The always-visible interface needs population, local food stock, expected consumption, next-season risk, and current orders. Selecting a place shows access, renewal, known occupants, and names used by relevant communities. Selecting a band shows its carried assets and responsibilities; selecting a polity shows member communities and agreements.

Essential overlays: food renewal and depletion; seasonal exposure; household routes and stores; wildlife ranges; political commitments; local cultural practices; language varieties and contact. Overlays can coexist in pairs where meaningful—such as stores plus routes—but never require seven saturated colors on one tile. Culture and language maps must support blended or patterned distributions rather than imply every cell is homogeneous.

The chronicle is a causal interface. Entries link an event to its people, place, practice, and later consequences: “Twelve households retained the river store; the ridge travelers kept access in exchange for winter meat.” Generated prose must be grounded in actual records and never invent a treaty, death, or lineage origin.

## 12. Playable scope and acceptance

The runnable slice should prove that the simulation can support the premise. A polished desktop globe comes in a subsequent Unity integration milestone; a working headless simulation is not evidence that graphics are finished.

### First executable slice — quantitative acceptance

- A new game contains exactly 50 people in the player band and 210 provisions; the four-founder setting creates exactly four 50-person founding bands with distinct IDs.
- A turn accepts at most two successful AP-costing orders per band; rejected orders do not alter food, position, population, or evidence.
- Seasonal state repeats deterministically after twelve turns; ending the turn consumes food for every resident, including in an idle band.
- Secure surplus can produce growth only after two qualifying turns and no more than the configured 4% event cap; a shortfall prevents growth.
- Splitting conserves population and provisions, creates a distinct band/polity relation, and requires enough viable members and supplies; no tiny-band command generates extra people.
- Ten credited successful contacts are necessary for the simplified lineage milestone, but not sufficient without observation and food-cost requirements; repeated contacts in one turn cannot bypass the observation gate.
- The same seed and command sequence produce identical state and chronicle events. A failed command leaves the reproducible state unchanged.
- A scripted 15-turn scenario runs without negative populations, negative food, invalid neighbor transitions, or duplicate entity IDs.

These criteria describe required verification targets. The README must name which are actually covered by implementation and automated checks.

### First Unity vertical slice — quantitative acceptance

- A Windows player can launch, configure one or four founding bands, read the globe, move, forage, hunt, interact with animals, end turns, and inspect consequences without an editor.
- At least three viable opening situations support respectively mobile, settled-gathering, and mixed play for 36 turns. Each has a documented route through a lean phase without an unavoidable death event.
- At least one voluntary split, one avertible secession dispute, one confederacy agreement, and one absorption with retained local practice can be demonstrated from recorded commands.
- At least eight advancement nodes are playable, including a mobile route, storage or gathering path, and animal-contact path. The other documented nodes are visibly identified as planned.
- A practice can be taught, become dormant when its carriers or materials are absent, and be recovered; political annexation alone does not grant it instantly.
- Two related language varieties show traceable regular sound changes and persistent old/new place names. The four-founder mode has four independent language roots unless configured otherwise.
- On a named reference Windows PC at 1080p, target 60 FPS in ordinary globe navigation, at least 30 FPS during peak visual effects, a turn under one second for the benchmark population, and a save/load round trip preserving deterministic state. Record hardware, world size, populations, and p95 measurements; do not advertise these targets as measured performance.
- In five observed new-player sessions, at least four players identify the next lean phase, their local food deficit, and a plausible preparation action without developer instruction. Use this as a small usability gate, not a statistically general claim.

### Larger design validation

Run a controlled scenario matrix covering four ancestries, several starting ecologies, and the five strategy families above. Compare survival, recovery from a missed harvest, social reach, and political cohesion. Investigate any strategy that dominates all outcomes or requires fewer meaningful tradeoffs in every environment. Do not balance solely by equalizing maximum population.

For heterogeneous absorption, seed two communities with different languages and practiced skills, combine their political allegiance, and verify that the local records survive unchanged until actual contact and teaching events modify them. For ecology, prove that hunts, trading, splitting, and animal reproduction conserve stocks except for explicit births, deaths, spoilage, and resource renewal.

## 13. Roadmap and difficult choices

| Milestone | Deliverable | Exit condition |
| --- | --- | --- |
| 0 — rules laboratory | C# simulation, deterministic commands, seedable world, food/growth/animal/split loops | Reproducible 15-turn example and conservation checks; implemented limits documented |
| 1 — navigable history | Unity desktop globe, input, seasonal readability, readable camps and fauna, save/load | A person can play a full twelve-turn cycle and predict its largest risks |
| 2 — local practice | First eight robust web nodes, carriers, evidence provenance, teaching, local variants | A new capability can be explained through actual actions and can survive a political split |
| 3 — communities and speech | Household cohorts, negotiated fission, limited AI, language descent and contact | Solo founders produce an explainable descendant network; a four-founder map remains intelligible |
| 4 — ecological commitments | Herd feeding, breeding gates, three domestication candidates, mixed residence, managed landscapes | Mobility and permanence both sustain populations through a 36-turn stress scenario |
| 5 — early polities | Confederacy obligations, heterogeneous absorption, villages and small towns, complete pre-bronze web | Distinct political forms emerge without forcing a linear settlement path |

Major tradeoffs to preserve during implementation:

- **Detail versus control:** households preserve social difference; direct control stays at band and community level. Add person-level simulation only when a decision needs it.
- **Simulation versus legibility:** wildlife and political actors have goals, but forecastable causes matter more than hidden cleverness. Model fewer causes well before adding hundreds of traits.
- **Agency versus historical pressure:** resource dependence and distance create strong incentives; avoid predetermined collapse or fission because the narrative “needs rivals.”
- **Rich identity versus busywork:** retain local differences in the model while summarizing important disagreements and opportunities in the UI. Do not ask the player to tune everyone's culture sliders.
- **Large planet versus early meaningful play:** the pre-bronze community sees a small, consequential region of a continuous world. Render and simulate distant areas at appropriate detail; do not dilute the opening with enormous empty traversal.
- **Visual ambition versus implementation order:** build an original, convincing terrain and atmosphere prototype early, but keep rules playable without it. Use performance measurements to determine terrain density, shadow range, and HDRP feature cost.

The design succeeds when a player remembers why their communities became different—and can point to the places, decisions, and relationships that made it happen.
