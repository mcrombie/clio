# Reading and commanding the map

The map fills the central canvas beneath Clio's library-style navigation. The top ribbon summarizes the whole controlled tribe: identity, total people and provisioning capacity, households ready to act, a salt watch and unread events. The salt watch counts households with less than two turns of supply; reserves remain separate. Its links open the relevant ledger, Units roster or archive.

The compact **Selected band** card at the lower left shows the household receiving orders: name, actions remaining out of two, people, food coverage including companion care, salt coverage and reunion status. With dispositions enabled it also shows personality, distance from the leader and the current separation deadline. **Hold** finishes that band's remaining actions and prevents a voluntary journey at this close; it does not stop upkeep or renew tribal contact. Values are read from the living household. Death or separation cannot leave an uncontrolled band displayed as the actor. Resource forecasts follow the selected household, while cumulative population and resource accounts cover the tribe.

Hover briefly over a hex or unit for a short note; click to open its record. Place records show terrain, gathering potential, ground recovery, known salt deposits, naming provenance and observed danger. Band records show population, condition and actions; animal records show temperament, health, trust and household effects. Hovering, inspecting and changing views do not discover terrain, coin names, spend actions or consume simulation randomness. Whole atlas is a debug view and may display counters outside explored land; it does not discover those places or permit their inhabitants roster, resource records or movement orders.

## Choose the household receiving orders

Every living controlled tribal band has its own supplies and two actions per turn. A newly formed band starts acting on the following turn. Select a tribal banner or a household in **Units** to give it orders. Its gold ground ring, name and two action diamonds in the dock identify the actor. **Find band**, **Home**, or the dock's band summary centers that selected household; if no valid actor remains, they find the living leader.

**Next band / N** selects and centers the next living controlled household with actions left, cycling in band-ID order. It returns control from autoplay without spending an action. Exhausted and newly founded households are skipped; if nobody is ready, it leaves the turn for you to end.

**Reunion route** on the selected-band card previews a route through remembered safe land to the current leader. It shows action cost and the number of turn windows used along that route, including the current turn and its remaining actions. It minimizes action cost, not necessarily elapsed turns. The preview updates as bands move or conditions change and disappears when no valid route remains. It never queues or issues movement: give each step yourself. The card normally shows the leader's reunion place or the selected household's countdown to separation.

The actor is separate from the place or unit being inspected. In tribal play, inspecting terrain, wildlife or another people preserves the commanded band. An encounter review retains the actor it was opened for and rechecks both identities before commitment. New and loaded stories select a living controlled band. Older stories whose tribal rules have not been enabled retain their original single-band selection behavior.

Companions follow and support their owning household. They have no separate action pool or independent movement orders. Wild groups and independent peoples cannot receive player commands. Future daughter bands belong to your tribe; old independent bands remain independent when an older story is upgraded. [Tribe membership, reunion and succession](TRIBES.md).

## Crowded hexes

Crowded places use a group-count badge instead of drawing every band and herd over one another. Left-click the badge to open **Together in this place**, a list of all observed living bands and animal groups on that hex. Seven rows fit at once; **Previous** and **Next** reach the remaining pages. Controlled households appear first, with their band IDs and remaining actions; animal groups retain distinct IDs even when their names match.

Choosing a controlled household makes it the actor and opens its record. Choosing a foreign people or animal opens its individual record while preserving the current actor. The list rechecks the group when clicked: dead groups and groups that have moved away cannot be selected through an old row. Right-clicking a badge refers to the hex represented by that badge, applying the same movement and encounter checks as clicking the ground. An unexplored Atlas badge cannot open this roster or authorize a journey.

## Movement and encounters

Right-click adjacent, explored land to order one step. The destination outline and arrow preview eligible steps; the place note gives the action cost. Unknown or atlas-only ground, open water, distant hexes, insufficient actions and insufficient ice provisions give a reason without recording a failed right-click order. Right mouse-up never repeats movement. There is no manual route queue.

| Step or approach | Actions |
|---|---|
| Ordinary adjacent land | 1 |
| Enter mountains or cross a river | 2 |
| Enter mountains and cross a river together | 2 |
| Encounter on the actor's current hex | 1 |

Mountains are passable. A band with one action left must wait for its next turn before a two-action step. Adjacent encounter approaches use the same terrain cost, rather than adding another action for the encounter. Travel still consumes provisions, and polar ice still requires reserves equal to three times the band's food needs. Earlier stories using legacy travel keep one-action steps until the terrain rule is introduced. [Landscape and travel rules](LANDSCAPE.md).

Destinations occupied by wildlife or independent peoples open the target-specific encounter sheet. **Review encounter**, **Attack** and **Befriend** let you read the costs and chances before commitment. Clicking a unit or choosing a roster row opens information; committing to an encounter remains a separate action. Moving into a place occupied only by your own tribal households does not require a foreign encounter review.

## Useful places and salt

Click the **Food / Salt / Paths** key beside Map views to open **Reading the landscape**. Gold food markers identify strong gathering opportunities: the selected band's current yield is at least twice its food needs. Crystal markers identify known coastal salt or inland springs. Pale blue frontier markers identify explored places beside uncharted land. These markers suggest useful destinations; movement awards no food or salt.

The guide's **Useful glows** toggle is visual only. Food highlights appear on Terrain and Food, frontier highlights on Terrain; salt source markers remain available when glows are off. Only a small selection of useful places is highlighted at once. Food and frontier highlights accompany the new terrain rules. The guide explains legacy costs and offers **Enable terrain travel** when those rules are absent.

Gather food at the selected band's location with **Gather / F**. To collect salt, bring that household onto a known source and use **Orders → Gather salt**, the source or band card, or **Economy → Resources → Salt**. Salt collection spends one action and adds five turns of demand at the band's current population. The selected card's salt measure and the ribbon's salt watch open that household's stock, demand, projected consumption, shortage effects and known source locations; adjacent source links show the actual one- or two-action approach. [Salt and survival](SALT.md).

| Control | Contents |
|---|---|
| Map views | Terrain, Food, Regions, Speech, Polities, place-name labels, grid, known land / whole atlas |
| Food / Salt / Paths | Useful-place guide, glow toggle, movement costs and salt ledger |
| Find band / Home | Center the commanded household and open its record |
| Next band / N | Take manual control and select the next household with actions left |
| Reunion route | Show or hide a manual route preview to the leader |
| Group-count badge | Open the paged inhabitants list for an explored hex |
| Gather / F | Gather food at that household's current place |
| Move / meet / M | Use the inspected destination or target with the selected actor |
| Right-click a hex | Order one adjacent known step or review an encounter there |
| Orders | Attack / A, Befriend / B, Make camp, Form a tribal band, Gather salt; legacy rule introductions where applicable |
| Autoplay / P | Guide all controlled households or take control |
| Watch settings | Decision speed, major-event pauses, camera follow |
| End turn / Space | Close the turn for the whole tribe, including households with actions left |

## Observation and input

Menus close with their close button, Escape or a background click. Visible toolbar controls work directly even while another menu is open. The persistent selected-band card, paged roster, other records, menus and event sheets shield the underlying map: their empty backgrounds do not begin a drag or select terrain, and their wheel input does not zoom it. Letterbox margins cannot issue movement. Gameplay right-clicks take control from autoplay. Escape closes the open reading or encounter sheet first, then transient map cards or menus; with none open it stops autoplay and leaves fullscreen. **P** toggles playback outside blocking sheets.

Dragging or zooming dismisses hover previews and transient inspection cards, and briefly holds autoplay decisions until the gesture settles. The selected-band card remains available. **Follow your band** follows the household autoplay is currently directing; turn it off to observe a fixed area. Tooltips clear on mouse leave, page changes, dialogs, resize and fullscreen transitions; transient roster state is also cleared when its record closes or a new story is loaded. Ledger, roster and event links reopen the relevant record or guide.

Map layers, glow choices and other presentation settings are transient. Gameplay orders and rule introductions are recorded separately: V9 preserves personality, terrain, tribe, salt and naming rules, including upgrades made after exact replay of an older story. The normal Load flow introduces current rules for surviving stories when command capacity allows. [Save and playback behavior](AUTOPLAY.md).
