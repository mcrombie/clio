# Clio — consolidated future development notes

Updated 2026-09-09. **Status: research and proposals, except the first gathering slice and the wood system described in [Gatherings](GATHERINGS.md) and [Wood](WOOD.md).** This is the consolidated place for new future-development ideas. Existing detailed designs remain in [GAME_DESIGN.md](GAME_DESIGN.md) and [ADVANCEMENT_WEB.md](ADVANCEMENT_WEB.md); the [README](../README.md) and [validation record](VALIDATION.md) describe what runs.

## Direction recorded from Michael

Deepen the early game around the needs and choices of real early humans. Give nonagricultural peoples substantial histories and several ways to organize beyond the current tribe. Eventually return to the roots of civilization, the attraction and dependence of sedentism, agriculture, and the large historical questions associated with *Guns, Germs, and Steel*. Park that Agricultural Revolution work here for now.

Retain turn iterations without years. Keep the map readable: avoid adding more permanent hex symbols or compulsory survival chores. Advisers should argue from distinct interests, explain consequences, and leave the decision to the player.

Introduce resources one at a time. Food and salt remain the essential supplies; add a new resource only when its benefits and choices are understandable. Use plain adviser roles and clear consequences first. Personal names and further flavor can come later.

## Wood: first slice and later possibilities

**Implemented in wood-27:** carried wood, one-action gathering, automatically supplied fire and a wood cost for camps. Fuel reduces food upkeep by 10% with the result rounded up and prevents cold-exposure deaths for that turn. Fuel demand is `max(1, population × 0.04)` per turn; living bands receive six turns of fuel when wood is introduced. A camp costs one action, 30 food and 10 wood. These numbers are prototype choices, not historical measurements. The [wood account](WOOD.md) describes the current limits.

Historical anchors: cooking changes how people can use food, and wooden spears are part of the archaeological record. [Smithsonian: Tools & Food](https://humanorigins.si.edu/human-characteristics/tools-food). Hearths and shelters also support eating and social life. [Smithsonian: Hearths & Shelters](https://humanorigins.si.edu/evidence/behavior/hearths-shelters). Worked logs from Kalambo Falls show structural use of wood at least 476,000 years ago. [Barham and colleagues, Nature (2023)](https://www.nature.com/articles/s41586-023-06557-9).

Later possibilities, **not implemented**:

- **Tools and spear shafts:** invest wood and work in equipment that improves gathering or hunting. Decide first how maintenance, breakage and lost gear affect the benefit. Do not give every weapon the same effect merely because it contains wood.
- **Larger structures:** frames, raised floors, storage supports and shared shelters could improve a returning camp. Construction should require work and a reason to stay or return. A large structure must not automatically imply farming, hereditary chiefs or a state.
- **Boats:** watercraft could provide access to fishing and new routes. Choose a specific craft and environment before assigning wood costs or river-crossing benefits; suitable timber, construction skill and repair should matter. The historical sources above do not establish a date for boats.
- **Fuel access and recovery:** only if the current system proves worth deepening, consider local deadwood, longer collection journeys and woodland recovery. Avoid another permanent map-marker layer or compulsory action every turn.

Keep these extensions separate. Add and discuss one effect at a time; do not bundle tools, boats, construction chains and new resources into the first wood release.

## Historical framing

Foraging includes fishing, shellfish collection and plant gathering as well as hunting. Recent foraging peoples are diverse societies with their own histories; they cannot be treated as unchanged examples of the distant past. Use archaeology and carefully bounded comparisons together. [HRAF research synthesis](https://hraf.yale.edu/ehc/summaries/hunter-gatherers).

Track settlement permanence, food production, political authority, social rank and geographic reach separately. Large settlements, collective construction and durable authority should each require their own explanation. A farming unlock must not automatically create chiefs, cities or a state.

Four useful comparative anchors:

| Evidence | What it establishes | Possible Clio use |
| --- | --- | --- |
| [Northern Japanese Jomon sites](https://whc.unesco.org/en/list/1632/) | Long-lived sedentary hunter-fisher-gatherer settlements and substantial ritual places before an agrarian way of life. | Returning villages supported by aquatic resources and nut-bearing woodland. |
| [Northwest Coast peoples, Smithsonian NMAI](https://americanindian.si.edu/exhibitions/infinityofnations/northwest-coast.html) | Maritime exchange, social ranking, and chiefly generosity expressed through feasts. | Houses competing through hosting, access and reputation. Preserve cultural specificity rather than copying a ceremony as a generic bonus. |
| [Calusa research, Florida Museum](https://www.floridamuseum.ufl.edu/science/watercourts-stored-live-fish-fueling-floridas-calusa/) | Fishing supported regional power, tribute, specialists and major construction. Watercourts are interpreted as short-term fish holding before use or preservation. | A substantial fishing economy can support political scale without cereal fields. This later case is a comparison, not an Ice Age template. |
| [Poverty Point research, Washington University](https://source.washu.edu/2021/09/new-evidence-supports-idea-that-americas-first-civilization-was-made-up-of-sophisticated-engineers/) | Hunter-gatherers coordinated sophisticated monumental earthworks. | Large gatherings and shared projects without assuming hereditary rulers. Monument size alone does not identify the government. |

## Tribe, chiefdom and paramount chiefdom

In older comparative typologies, a chiefdom differs through enduring political office, recognized authority and succession rules. A paramount chief can stand above subordinate chiefs. These describe arrangements of power, not fixed population brackets. [Davenport's historical account, Penn Museum](https://www.penn.museum/sites/expedition/states-chiefdoms-and-tribes/).

The term tribe also identifies peoples with very different institutions and scales, and its use as a universal evolutionary stage is contested. Shared peoplehood must remain distinct from government. [Sneath, Open Encyclopedia of Anthropology](https://www.anthroencyclopedia.com/entry/tribe).

Proposed game vocabulary:

| Arrangement | What would change in play |
| --- | --- |
| Connected bands / current tribe | Households maintain relationships, knowledge and a recognized meeting place. |
| Assembly or confederacy | Several communities accept limited common obligations while retaining local command. Councils negotiate access, relief and defense. |
| Chiefdom | A continuing office has accepted rights and duties across communities: allocating access, arbitrating disputes, calling contributions, maintaining reserves or organizing defense. Succession matters. |
| Paramount chiefdom | Local chiefs mediate obligations to a senior chief. The player deals with subordinate authorities and their commitments rather than ordering every household. |

A confederacy is a viable alternative, not a failed chiefdom. A charismatic hunt leader need not hold permanent civil authority. Chieftainship can consolidate, fragment or lose powers. Calling a figure a chief must not grant an institution that has never been established. "Paramount" is not a universally separate historical stage, though it can be a useful game label for nested chiefly authority.

Keep peoplehood, language, government and membership separate. Zholhen identity could span several governments; one government could include communities with different speech and ancestry. Joining a league must not silently annex its members or erase their names.

## Nonagricultural arcs to develop

### 1. A dependable circuit

Turn exploration into a remembered pattern of viable places: fresh water, shelter, fuel, gathering patches, fish runs, game crossings and tool material. A place matters because of what it supplies and when it can support a return. Foresee changing availability through conditions measured in turns, without restoring a year calendar.

Choices: range farther, let ground recover, maintain a return camp, prepare a cache, or negotiate passage through another people's customary ground. A powerful mobile community might possess few permanent structures but many reliable routes and hosts. Its failure pressure is the loss of alternatives, not simply insufficient territorial area.

Builds on proposed P01/P05/P07–P10. Basic competence exists at founding; new practices improve reliability rather than making the starting people ignorant of fire or shelter.

### 2. The hearth that makes journeys possible

Make care, repair and teaching visible parts of subsistence. An injured hunter, a knowledgeable elder, children and carers affect how much work a group can undertake and which skills it retains. Use household cohorts or a few named skill carriers, not a mandatory simulation of every individual.

Choices: carry an injured household, provision a protected return camp, take a skilled guide on a risky journey, or train another carrier. Hospitality can rescue a group and create a lasting relationship. Hearths, cooking, shelter and extended care are grounded in human-origins evidence; the exact costs and cohort system are game proposals. [Smithsonian Human Origins](https://humanorigins.si.edu/evidence/behavior/hearths-shelters).

### 3. Abundance that must be organized

A fish run or productive woodland offers more than one band can process immediately. Boats, nets, drying racks, smokehouses, containers and protected stores let people use that opportunity. Labor, fuel, repair and transport limit the benefit. Rivers gain an economic role as well as their current movement cost.

Choices: invest in a landing, invite other bands to help, control access, share the catch, or remain mobile. The attraction of staying develops before crop cultivation. A disrupted run, exhausted fuel supply or contested landing exposes dependence on the site.

Builds on P16/P17/P21–P24. Research plant tending, burning and fish management in specific settings; a sharp untouched-wilderness/farming binary would hide important intermediate practices.

### 4. The first great gathering

Independent relatives and neighbors meet at a known place for exchange, hospitality, teaching, ritual and dispute settlement. Hosting costs actual supplies. Visitors must travel and have reasons to attend. Successful gatherings can establish a recurring assembly without everyone accepting one ruler.

Choices: preserve food for a lean period or host generously; include a rival; share an uncommon technique; negotiate refuge or return rights. Kinship can grow through household affiliation, adoption and voluntary relationships as well as shared descent. Avoid a genetic-purity meter or treating people as transferable commodities.

A study of two contemporary hunter-gatherer groups connects multilevel social organization with food-sharing and cooperative partners. This supports examining networks as a survival resource, without prescribing one universal prehistoric institution. [Dyble and colleagues, research manuscript](https://discovery.ucl.ac.uk/id/eprint/1506404/).

### 5. Roads, gifts and indispensable neighbors

Useful stone, cordage, containers, boats, prepared foods and salt can make another people's work valuable. Start with explicit exchanges and guest teaching; prices, currency and market simulation can wait. Spoken place names, remembered routes and teacher provenance make exchange part of Clio's cultural history.

Choices: give supplies now for future refuge, escort a visitor, share a scarce tool material, or demand payment for a crossing. Promises create expectations and grievances; mere proximity creates neither a treaty nor a shared inventory. A seceded daughter can become a valued partner while remaining politically independent.

Builds on P03/P04/P08/P25–P27. Reciprocal aid should buffer different local failures; a drought affecting every partner tests the whole network.

### 6. Whose authority survives the crisis?

A shared work, feud, raid, failed harvest of wild food or refuge crisis gives someone a reason to coordinate several communities. Authority can remain temporary, be limited by an assembly, or become an enduring office. A storekeeper may gain followers through dependable relief; a war leader through protection; a ritual host through participation and legitimacy.

Choices: renew the leader's mandate, bind it to duties, recognize succession, refuse a contribution, arbitrate a grievance, or leave. Contributions, inheritance and coercion should have different consequences. Do not award permanent obedience simply for building a store or winning a fight.

At paramount scale, local chiefs retain households, interests and bargaining power. Mobilization needs local compliance and real routes. A failed succession or failure to protect members can break regional authority while communities survive. Success includes a resilient league as well as a powerful chiefdom.

## Recommended first playable arc: a gathering after separation

Build a small chain around the peoples that current play already produces:

1. A known, peaceful splinter polity can receive a rendezvous proposal at a mutually known, reachable place.
2. Each party decides whether attendance is worth the travel and supplies. Movement and encounters still obey normal rules.
3. At the meeting, permit one explicit food or salt transfer and one return-visit commitment. Show the actual donor cost, recipient gain and agreed date in turns.
4. Record fulfillment, refusal, inability and a missed meeting separately. A successful relationship preserves local command and language.
5. Later, extend repeated useful cooperation into access rights, guest teaching, a shared project and an assembly. Requirements should reflect fulfilled work and consent, not a generic research bar.

The historical mechanism being explored is cooperation that makes a larger network worth maintaining. The first implementation now covers paid invitations, ordinary travel, an explicit donation, a return window and recorded outcomes. Its concrete rules are in [Gatherings](GATHERINGS.md). Access rights, teaching, shared projects, assemblies and political promotion remain proposals.

Illustrative adviser disagreement over hosting after a poor run:

- **Sula:** keep a minimum reserve before inviting more mouths.
- **Tavo:** use the gathering to secure the crossing and learn who will help defend it.
- **Yara:** invite the skilled visitors; their knowledge may be worth more than the feast.
- **Lian:** honor the invitation, or our former households may stop seeing us as dependable kin.

Each position should name a real cost or opportunity. The advisers cannot invent a treaty, hidden threat or benefit merely to create an argument.

## Deferred Agricultural Revolution and roots of civilization

Preserve these questions for a later design pass. This section does not authorize agricultural implementation now.

- **The attraction of staying:** reliable local abundance, heavy stores, houses, landing places, managed stands, burials, ritual and social obligations can encourage return or permanence.
- **The dependence of staying:** more residents, immovable investment, depleted nearby alternatives and access disputes can raise the cost of departure. Treat the "trap of sedentism" as an emerging situation, not an inevitable punishment. Plan for partial departure, abandonment, mixed residence and renewed mobility.
- **Agriculture as several commitments:** plant tending, harvesting wild stands, seed selection, sowing, soil/water work and staple dependence should be distinguishable. Allow learning through contact and repeated trials. Domestication is a process rather than one discovery button.
- **Labor and demography:** distinguish additional residents from immediately productive workers. Compare dependable yield, total work, care burdens, dietary variety, risk and household bargaining rather than raw food output alone.
- **Stored surplus and power:** who contributes, guards, distributes and inherits the reserve? Does a feast create prestige, an obligation, a tax precedent, a dispute or none of those? Collective storage need not automatically become elite ownership.
- **Crowding, animals and disease:** design specific transmission routes, environmental conditions and susceptibility. Do not encode a universal farming disease penalty or effortless, permanent "civilization immunity." Detailed epidemiology requires its own research.
- **Uneven opportunities and diffusion:** suitable species, habitats, routes, barriers and communication can influence which practices spread. Geography creates opportunities and constraints while institutions and choices determine their use.
- **States as a further question:** taxation, accounting, enforcement, captivity, administrative specialization and resistance need explicit institutions. Do not equate agriculture, a chiefdom and a state.
- **Nonfarmers remain part of the story:** mobile, fishing, gathering and later herding peoples can exchange, compete, intermarry, resist incorporation or join mixed systems. Pastoralism is a later food-producing path dependent on domestic herds; it is not synonymous with foraging.

Research lenses to revisit, not mechanics accepted on authority:

- Jared Diamond's *Guns, Germs, and Steel*: suitable domesticates, geography, diffusion and uneven historical trajectories. [Author's account](https://jareddiamond.org/Jared_Diamond/Guns%2C_Germs%2C_and_Steel.html).
- James C. Scott's *Against the Grain*: early agrarian states, staple dependence, mobility, extraction and the costs of incorporation. [Yale University Press synopsis](https://yalebooks.yale.edu/book/9780300231687/against-the-grain/).
- Compare those broad arguments with regional archaeology and counterexamples, including settled nonfarmers and large collective projects. These notes draw on the linked summaries; they do not claim a complete reading or scholarly assessment of either book.

## Boundaries to settle before implementation

1. **Food accounting:** current UI describes food reserves carried by each band; the larger design proposes distinct carried and stored goods. Define the relationship before adding preservation, caches and transfers. Preserve conservation and make transport explicit.
2. **Nutrition and salt:** retain salt as a strategic resource, but research dietary sodium versus separately collected salt before claiming that every foraging community must replenish mineral stores. Explore preservation and exchange uses. Current shortage timings are game tuning, not human physiological measurements.
3. **Labor:** the current two-actions-per-band model must not let repeated splitting manufacture effective workers. Favor recurring arrangements, delegation and exception alerts over more mandatory clicks.
4. **Contact and politics:** current reunion/secession deadlines are a prototype. Future contact can involve several bands, shared meeting places and fulfilled obligations, while authority and cultural difference develop separately. A messenger must not magically transport food or erase grievances.
5. **Timing and language:** continue undated turns. Ecological pulses can be forecast without a year calendar. New political agreements should neither merge languages nor erase independent names; longer-term divergence needs contact history.
6. **Map load:** sites, resources, units and obligations should appear contextually. Put cumulative needs and labor in Economy, agreements and regional authority in Diplomacy, carriers and practices in Culture, and exceptions in adviser cards.
7. **Scale and success:** advance from directly guiding a few bands to choosing duties, projects and negotiated contributions. Measure reliable access, partners, knowledge continuity, resilience and political reach alongside population. Large political entities can fragment without erasing their peoples' histories.
