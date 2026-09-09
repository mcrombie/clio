# Remembered place names

New stories give each people its own remembered map. A hex has a stable cell ID; its name belongs to the people who know it. The implementation is in `src/Clio.Simulation/PlaceNames.cs`, with discovery and contact hooks in `Simulation.cs` and `Encounters.cs`.

## Discovery and contact

- **Firsthand discovery:** a band observes its current hex and two rings of neighbors, including water. Every previously unknown hex receives a name in that band's current language: a terrain-related vocabulary root followed by a local epithet using its sound inventory. Locally coined names are distinct within that band's map, including hexes with the same terrain and region.
- **Independent discovery:** another people's earlier knowledge has no effect. A band that independently discovers the hex coins its own term. Band identity contributes to the naming seed, including when the discoverers speak the same language.
- **Learning from guides:** living, peaceful bands occupying the same or adjacent hexes exchange their complete known maps. Each recipient adopts the donor's exact name for every hex it does not already know. Existing names are never overwritten. A later firsthand visit keeps the adopted foreign name.
- **Relayed knowledge:** an adopted record retains the original naming people and language, as well as the immediate donor. A guide can pass on a name that it originally learned from somebody else.

Founding bands observe their surroundings independently before any initial contact exchange. Contact is checked at the start of turn resolution and after ordinary band movement. An attacking approach or retreat does not exchange maps; hostile bands do not share knowledge. Sharing requires no separate action and consumes no provisions. Newly shared land enters the recipient's known map. Player-facing history records and notices explain new borrowed names.

A daughter band inherits its parent's exact remembered names and original provenance, then observes the neighborhood of its new position. Subsequent language branching affects newly coined names; remembered proper names retain their recorded spelling.

## Viewing names

Map labels and the selected-place title use the player's remembered name. Select a hex to open its compact map card and see how and when the name entered the people's memory. The card distinguishes independent naming, contact and inheritance; it never exposes an unknown original people's identity merely because a name was relayed.

The atlas toggle remains a viewing aid. Inspecting or rendering an unknown hex does not discover it, coin a name, or consult an NPC's label on the player's behalf. Its card is marked **Atlas only** until the player learns about that hex through observation or contact; fogged hover previews remain generic.

## State and compatibility

`PlaceKnowledge` stores the cell ID, exact name, original band and language IDs, immediate donor ID, acquisition turn, and acquisition type (`Discovered`, `Shared`, or `Inherited`). `KnownPlace` is a read-only lookup; `KnownPlaces` returns a read-only snapshot sorted by cell ID. Naming uses a separate stable hash of the seed and naming identity. It draws from neither gameplay RNG stream.

The appended game-constructor flag enables cultural names for new stories. Earlier constructor overloads retain legacy naming rules. V5 saves record whether cultural names were enabled at the founding; command replay reconstructs later discoveries and exchanges deterministically.

Loading an older story through the normal UI first replays its original rules exactly, then records an `enable-place-names` command. The upgrade names only the player's already explored hexes, initializes other living bands' present sight, and gives identifiable existing daughters the parent's map. It performs no contact exchange or extra player revelation, advances no turn, spends no action, and changes no gameplay RNG state. Existing stories did not record naming provenance, so the upgrade coins their initial remembered labels in the current tongue. Internal `ReadStory` itself retains exact legacy replay without an implicit upgrade. Enabling names again is a no-op.

## Current scope and checks

A band currently represents one people with one remembered map. Peaceful neighbors exchange their entire known maps; there are no partial rumors, translation costs, map-selection negotiations, or later renaming rules. Foreign names are preserved exactly, even when their sounds fall outside the recipient's native inventory. The original root-composition helper remains in use for people names and other existing naming recipes.

The feature passed **5,391 focused assertions** within **448,825 core assertions**. Coverage includes all 2,562 hexes receiving distinct local names, independent same-language discoveries, direct and relayed borrowing, later arrival, peaceful movement and turn contacts, attack exclusion (including a neutral people's companions and nearby bystanders), daughter inheritance, language branching, read-only queries, legacy upgrade boundaries, and deterministic replay. Desktop save and presentation checks are recorded separately in [VALIDATION.md](VALIDATION.md).
