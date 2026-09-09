# Languages in Clio

The purpose of language is to make history leave recognizable traces. A river named by the founding band should still be recognizable after its valley contains five rival polities. Its name may have several pronunciations, an older layer preserved by newcomers, and a name used by distant traders. Players should be able to discover these relationships without learning an invented language.

Languages are learned practices carried by people. A language is not a species trait, a political allegiance, a personality score, or a rung on an advancement ladder. The most mobile band can possess a complex oral tradition; a large state can conduct its affairs in several languages. Human, elven, dwarven, and goblin communities can learn and transmit one another's languages. Fantasy ancestry changes environmental adaptations elsewhere in the simulation; it does not assign an immutable language or intelligence.

## Working implementation and its limits

`src/Clio.Simulation/Language.cs` is an engine-independent C# subsystem using only the base class library. It provides:

- Seeded founding lexicons with twelve distinct roots, readable spelling, three sound palettes, and an editable inventory/settings API.
- Branches that transform inherited words through one documented, simultaneous sound law. They preserve the parent snapshot, original family ID, etymon identities, and immediate ancestor ID.
- A vocabulary-root composition helper with a stable feature/salt recipe. It remains in use for people names and legacy place labels; reusing a recipe in a descendant language gives recognizably related forms.
- Explicit borrowing with donor provenance and adaptation to the recipient's sound inventory. A loan preserves the recipient's genealogical identity.
- A bounded, weighted lexical intelligibility estimate with a separate listener-exposure input.
- Deterministic checks covering multiple seeds, all palettes, deep-copy behavior, systematic cognates, contact, naming, and custom inventories.

The game also implements [remembered hex names](PLACE_NAMES.md) in `PlaceNames.cs`. Every people coins distinct local names when it independently observes unknown hexes. Peaceful neighboring bands exchange exact foreign names for places the recipient does not yet know. Records preserve the original coinage, immediate guide, and acquisition turn; daughters inherit them, and later visits or language changes do not silently rewrite them. The Names inspector explains the selected hex's remembered name. Atlas inspection alone creates no knowledge.

This is a naming and genealogy prototype, not a complete constructed language or a historical linguistics model. It has no grammar, sentence generation, sound recording, tone, stress, writing-system simulation, or semantic change. Its sound changes are deliberately broad, one-letter substitutions. They are sufficient to establish reproducible family resemblance, not to reconstruct natural languages. The language module does not itself schedule divergence or track populations' proficiency; the simulation has a basic distance-and-turn divergence gate and a band-level speech overlay. The broader community, fluency, and historical-variant systems proposed below remain design work. The implemented per-hex naming behavior and save compatibility are documented in [PLACE_NAMES.md](PLACE_NAMES.md).

## Starting a world

The native game's default starting language is **Zhol**, a custom Mandarin–Spanish sound fusion authored for Michael. Its twelve fixed roots, naming choices and future word-making rule are recorded in [ZHOL.md](ZHOL.md). Zhol's proper name is independent of its word for people. Earlier stories retain their recorded languages, and the original generated palettes and three historical inspirations remain selectable.

**One founding band.** Begin with fifty people sharing a single generated or customized proto-language. Newly formed bands initially speak that same language. Their descendants share roots even if their later cultures, political systems, and fantasy ancestry composition differ. A political split is not a call to create a fresh language.

**Several founding bands.** The world settings should separate the number of bands from the language-origin model:

| Origin setting | Initial languages | Historical consequence |
| --- | --- | --- |
| Shared origin | All founders share one profile | Later diversification produces one genealogical family. |
| Related founders | Founders use branches of one archived proto-language | Early contact reveals recognizable cognates; the earlier separation is scenario backstory. |
| Independent origins | Each founding community receives its own root profile | Contact creates loans and learned fluency across distinct families. |

The four-band human/elf/dwarf/goblin scenario can use any of these. Independent origins is an interesting scenario option, not a claim that each ancestry must possess its own language. Assign language IDs through a world-level counter or registry when creating several roots; `Create(seed, style, id)` makes that assignment explicit. The convenience `Create(seed, style)` hashes a seed and palette into a stable nonnegative ID; it is not a world-wide uniqueness guarantee.

Expose a short initial-language settings panel: generated sample names, sound palette, seed/randomize, and an optional advanced drawer for consonants, vowels, syllable length, and final consonants. Changing the language preview before starting changes names. It does not secretly change yields, intelligence, diplomacy, or cultural temperament. The polity's player-chosen name remains a separate label.

## Sound palettes and spelling

The initial generator uses syllables of the form **CV**, with an optional consonant only at the end of the complete word: `CV.CV(C)` or `CV.CV.CV(C)` by default. Every word has a vowel. Hyphens separate roots in compound place names. ASCII orthography keeps labels readable and searchable; each letter is a spelling token for the prototype, not a promise of exact international phonetic notation.

| Palette | Initial consonants | Final consonants | Length | Final-consonant chance | Intended texture |
| --- | --- | --- | --- | --- | --- |
| Flowing | m n l r s v h w y | n l r | 2–3 syllables | 15% | Open, vowel-rich names |
| Crisp | p t k s n r l d g | k n r s t | 2 syllables | 55% | Shorter names with frequent stops |
| Resonant | b d g m n l r v | m n l r | 2–3 syllables | 40% | Voiced consonants and sonorant endings |

All presets begin with `a e i o u`. The labels describe aesthetic choices, with no ancestry assignment or gameplay bonus. Custom palettes can use one to four syllables, a subset of lowercase ASCII consonants, and a subset of these vowels. Set `CodaChance = 0` when `Codas` is empty. Repeated letters and inventories too small to provide twelve distinct roots are rejected explicitly. A deterministic fallback enumerates available forms when random generation encounters collisions in a small valid inventory.

```csharp
LanguageSettings sounds = new LanguageSettings {
    Onsets = "mnlrsh", Vowels = "aeou", Codas = "nr",
    MinSyllables = 2, MaxSyllables = 3, CodaChance = 0.2
};
LanguageProfile founding = LanguageGenerator.Create(734, sounds, 0);
```

The twelve founding concepts are water, river, mountain, forest, people, home, wolf, cattle, fire, sun, stone, and grass. These are naming roots, not proof that a founding band has met or domesticated everything it can name. In particular, the cattle root identifies a broad animal concept; it does not grant husbandry. A later semantic layer can distinguish unfamiliar large grazers, specific wild species, and named domestic breeds.

## Descent leaves evidence

`Branch(parent, id, seed)` chooses a productive rule, copies the parent, and applies that same rule to every inherited word. It also updates the child's sound inventory. A rule is simultaneous: under `p > f; t > s; k > h`, a former `t` becomes `s` once. It is not processed again during that event. Rules that affect none of the parent's words are skipped. A final vowel transformation ensures even unusual valid palettes can change.

The child's `ParentId` records the immediate predecessor. `RootId` records the founding family. Each concept also has an etymon such as `100:river`, preserved through sound changes. Two words can merge into the same spelling after sound change; those homophones retain different etymons. The generator does not reroll them to make every descendant word unique.

The following is actual output from the current implementation, not an illustrative replacement lexicon:

```csharp
LanguageProfile proto = LanguageGenerator.Create(734, LanguageStyle.Crisp, 100);
LanguageProfile west = LanguageGenerator.Branch(proto, 101, 1);
LanguageProfile east = LanguageGenerator.Branch(proto, 102, 5);
LanguageProfile coast = LanguageGenerator.Branch(west, 103, 9);
```

| Community label | ID / parent | Autonym | Change from parent |
| --- | --- | --- | --- |
| Founders | 100 / none | Sulo | Founding vocabulary |
| Western community | 101 / 100 | Sulo | p → b, t → d, k → g |
| Eastern community | 102 / 100 | Hulo | s → h, v → w |
| Coastal western descendants | 103 / 101 | Sala | a → o, o → a, e → i, i → e, u → a |

| Concept | Founders | Western | Eastern | Coastal western |
| --- | --- | --- | --- | --- |
| water | rorun | rorun | rorun | raran |
| river | kugun | gugun | kugun | gagan |
| mountain | desu | desu | dehu | disa |
| forest | pelet | beled | pelet | bilid |
| people | sulo | sulo | hulo | sala |
| home | kede | gede | kede | gidi |
| wolf | nurus | nurus | nuruh | naras |
| cattle | rasus | rasus | rahuh | rosas |
| fire | ladu | ladu | ladu | loda |
| sun | desa | desa | deha | diso |
| stone | nini | nini | nini | nene |
| grass | luto | ludo | luto | lada |

The western language still calls its people *Sulo*. That is legitimate: its sound change does not affect that word. Interface labels can say “Sulo · Western valley” and “Sulo · Founding record,” with different IDs, without forcing an invented new autonym.

For `PlaceName(language, "river", 551)`, the same “wolf + river” recipe produces:

| Founders | Western | Eastern | Coastal western |
| --- | --- | --- | --- |
| Nurus-kugun | Nurus-gugun | Nuruh-kugun | Naras-gagan |

Those names tell a miniature history. The coastal name changed twice, while the eastern name follows another path. A family comparison view should highlight the changed letters and show the ordinary-language gloss “Wolf River.”

## Divergence belongs to communities, not borders

The next simulation layer should attach speech-community membership and proficiency to population cohorts. A polity aggregates its populations; a culture stores overlapping practices, memories, and institutions. Neither is the language's owner. A community can cross several political borders, and a polity can contain several communities.

Track three separate quantities:

| Quantity | Meaning | Sources of change |
| --- | --- | --- |
| Shared exposure | How often communities understand and use one another's speech | Visiting, seasonal gatherings, household migration, shared herding, trade, ritual, interpreters |
| Dialect distance | Accumulated differences in pronunciation and vocabulary | Time in relative isolation, repeated local innovations, differential contact |
| Identification | Whether people treat their speech as a named community practice | Oral traditions, migration memories, local institutions, player-supported gathering places |

A proposed update is `distance += innovationPressure × isolation × historicalPace`, while repeated contact reduces effective distance and builds listener exposure. All factors are continuous and bounded. `historicalPace` belongs to the game's abstract time progression; one tactical movement turn must not equal a fixed number of linguistic years. The coefficients require playtesting.

Initially, separated bands retain the same language profile while their dialect-distance meter rises. Crossing a visible threshold after sustained isolation creates a named descendant profile and records a sound-change event. The current `Branch` function implements that event, not the meter or the scheduling decision. Do not call it whenever a band splits, changes allegiance, crosses a mountain hex, or acquires a new cultural trait.

Contact does not necessarily erase difference. Nearby communities can preserve distinct identities while becoming highly mutually fluent. Conversely, two distant communities can share a cultural tradition while their everyday speech diverges. Population size affects how widely a variety is encountered, not its inherent quality.

An illustrative campaign sequence:

1. A fifty-person founding band shares one language and annual stories about a river crossing.
2. A larger band divides between a wooded valley and a highland route. Both initially use the same language ID.
3. The valley becomes more sedentary. The highlanders remain mobile and regularly meet distant camps. Exposure and borrowed vocabulary now differ.
4. Several historical intervals of weak contact produce distinct valley and highland varieties; both preserve founding roots.
5. A confederacy forms. It increases meetings and translation capacity. It does not merge all languages on the day of formation.
6. A larger composite culture emerges around shared routes, rituals, and mutual defense. Several languages can continue carrying it.

## Borrowing and knowledge travel together

Words can cross genealogical boundaries through contact. `Borrow(recipient, donor, concept)` makes a new snapshot with the recipient's existing ID and parent. It replaces one concept with an adapted donor form, copies the donor etymon, and records the donor ID, original spelling, and adapted spelling in `Loans`. Neither input is modified. Historical loan records retain the form at the contact event; a descendant's current word may subsequently change.

The adaptation helper substitutes sounds into the recipient's inventory using broad consonant/vowel groups. If a borrowed final consonant is not allowed, it adds a supporting vowel. This is a transparent gameplay approximation, not a universal linguistic rule. It does not simulate prestige, mixed grammars, creoles, or detailed phonetic distance.

A cattle-herding community might transmit a herd, handling practices, and an animal word. These are three linked but separate transfers. Buying cattle does not immediately grant every husbandry practice; learning a word does not domesticate an animal. Successful work with knowledgeable visitors can transfer practical knowledge and improve exposure. A culture's knowledge web can record that lineage of learning even after its language changes.

The same structure should support breed names. A breed has a stable biological lineage ID, while each community can have a name for it. The original breeders' term may become a widespread loan; its etymology can survive after the founding polity disappears. Breed survival and language survival need not coincide.

## Intelligibility and useful choices

`Intelligibility(listener, speaker, exposure)` returns a value from zero to one. Its baseline averages normalized spelling-edit similarity across the twelve concepts. Shared etymons receive full resemblance weight; unrelated nonidentical roots receive 45% of that resemblance. Identical forms receive full lexical credit. Water, people, home, and fire have somewhat greater weights than the other naming roots. Exposure adds learned comprehension:

`result = baseline + (1 - baseline) × exposure`

The lexical baseline is symmetric. Passing different exposure values for each direction makes learned comprehension asymmetric. Exposure is clamped to 0–1, and nonfinite values are rejected. This score is explicitly a **game abstraction**. Twelve written roots cannot measure real mutual intelligibility; the score must not be presented as a scientific percentage or used as a hidden determinant of diplomacy.

Use bands such as “familiar,” “needs mediation,” and “learned fluency” in the interface. Hovering can reveal the underlying game score and the factors that changed it. The next model can combine vocabulary familiarity, learned proficiency, established interpreters, and domain-specific knowledge rather than pretending spelling alone is sufficient.

Communication should create choices with visible costs and benefits:

| Player action | Cost to test | Benefit to test |
| --- | --- | --- |
| Assign a bilingual envoy | One specialist unavailable for gathering or another mission | Faster agreements, more reliable exchange of detailed practices |
| Host a seasonal gathering | A declared food commitment and travel effort | Broad exposure, shared stories, relationships across communities |
| Maintain a multilingual council | Ongoing coordination labor | More communities represented; fewer administrative misunderstandings |
| Support local oral traditions | Storyteller time or a small ceremony commitment | Preserved community memory and cultural knowledge; stronger local participation |

These are tunable proposals, not implemented resource charges. Basic trade, vital warnings, and every player-facing decision remain understandable. A lack of interpreters may reduce throughput or delay a complex negotiation, but must not hide a food shortage, make an essential button unreadable, or spring a random treaty violation on the player. Show the cost before commitment. Let interpreters, repeated contact, and multilingual institutions solve the problem through play.

Do not reward erasing languages as an automatic path to a stronger empire. Shared administrative speech can reduce coordination work while local languages continue to transmit knowledge and identity. The player can govern a successful composite culture with several languages. No sound palette receives a diplomacy bonus or penalty.

## Places are stable; their names are historical

Separate a referent from every name attached to it. A river, settlement, mountain route, herd lineage, or polity needs a permanent simulation ID. A name record should point to that ID and contain a language ID, naming recipe or attested form, creation event, meaning, and use category:

| Name category | Example | Persistence rule |
| --- | --- | --- |
| Endonym | Residents' contemporary name for Wolf River | Can evolve with local speech; previous forms remain searchable. |
| Exonym | Distant traders' term for the same river | Coexists with the endonym; does not create a second river. |
| Substrate name | An earlier community's river name retained by newcomers | Keeps its older etymology even when its literal meaning is no longer understood. |
| Player alias | “Northern crossing” | Remains stable until the player changes it. |

`PlaceName` supplies a deterministic composition helper. It does not implement this historical name registry. Its `salt` is an opaque stable recipe seed, ideally saved with the referent; do not derive it from current population, mutable coordinates, or the active turn. The feature chooses the head root and the salt chooses a modifier. “Woods” and “forest” both use the forest root; mountains and hills share the mountain root in this small vocabulary.

Do not regenerate every map label every turn. Store an attested name when a feature is named. At an explicit sound-change event, generate a new local form from its saved recipe or transform the attested form, and preserve the previous one. Borrowing a generic animal word must not automatically rename all older places containing that animal's former name. A substrate name should remain its own attestation even when newcomers would compose a different name for the feature.

Default labels should prioritize recognition: the current local or player-selected name, a small feature icon, and a plain gloss in the detail panel. Searching any historical form or alias selects the same feature. A language overlay should show population shares and mixed communities, with political borders drawn lightly above it. Avoid painting every polity as a single uniform language block.

## Next implementation increments

1. **Speech-community state:** membership, exposure edges, migration, and historical-pace input; political fission copies current language membership.
2. **Historical registry:** immutable archived profiles and name attestations keyed by stable IDs. Profile fields are public for serialization; game code should treat published snapshots as immutable.
3. **Divergence events:** accumulated local differences, visible thresholds, a short event explanation, and side-by-side cognates. Preserve old language IDs in the archive.
4. **Language map and names panel:** mixed-population overlays, endonym/exonym preference, aliases, and a small family diagram.
5. **Knowledge-bearing contact:** interpreters and repeated practical exchanges tied to the advancement web; domain vocabulary and breed names come after that loop works.

The acceptance test for this system is a recognizable history: a player can inspect a name, discover who used it first, see why neighboring people say it differently, and act on communication needs without having to memorize vocabulary.
