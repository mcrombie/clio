# Council of advisers

An illustrated adviser appears with a brief explanation, **Tell me more**, and a dismiss control. Cards remain visible while inspecting the map and reading other tabs. Open **Advisers**, or press **C**, to review the council. It watches every living controlled household and explains important developments as well as urgent economic and military concerns.

Each new story opens with a prominent parchment briefing: your people, language, first place, actual provisions and salt, three early priorities, and the four advisers. **Begin our story** dismisses it; **Meet advisers** opens their council profiles. Enter or Escape begins play. Reading or closing the announcement spends no action, and loading an existing story does not repeat it.

Four named advisers have original painted portraits and distinct priorities:

| Adviser | Office | Bias |
| --- | --- | --- |
| **Sula** | Keeper of stores | Secure provisions before spending people and supplies on ambition. |
| **Tavo** | Warden of paths | Protect households and safe approaches before taking chances. |
| **Yara** | Keeper of memory | Seek discoveries and preserve a cultural legacy, even when caution would keep the people home. |
| **Lian** | Voice of kinship | Keep households connected and preserve relationships before accepting separation. |

They read the same evidence, but advocate different priorities. The full council separates observed facts or a recorded development from the selected adviser's speech, argument, the cost of that view, and an attributed answer from another adviser. Their disagreements are opinions about existing mechanics, not extra simulation effects. Switch among the four portraits to weigh their counsel. [Design references and interpretation](COUNCIL_INSPIRATION.md).

**Favor Sula / Tavo / Yara / Lian** makes that adviser speak first for ordinary concerns and optional guidance. Urgent and critical economic or military warnings retain their specialist. Favoring a voice does not suppress reports, change urgency, spend an action, issue an order, or change autoplay. The preference lasts for the application session, including loading or starting another story; it is not saved with a campaign. **Meet the council** lets you read their priorities even when no concerns are active.

Current economic and military concerns are:

| Concern | When it appears | Urgency |
| --- | --- | --- |
| Low salt | Reserves cover at most two turns at the band's current population | Warning |
| Salt deficiency | The current forecast cannot supply the next turn's full salt need | Urgent; Critical when the forecast includes salt deaths |
| Narrowing food reserves | The current forecast is losing food and ends with less than one turn's upkeep remaining | Warning |
| Hunger | The current forecast includes hunger deaths | Critical |
| Hostile people nearby | An already hostile, observed band is within one adjacent hex of a controlled household | Warning; Urgent if they share a hex |

Food advice uses the same forecast as Economy, including camp and herd output, companion care, upkeep and spoilage. It describes current conditions; other units' movement and encounters can change the eventual outcome. Military advice reports observed proximity and existing hostility, not a prediction that an attack will occur. Neutral people and wild animals do not trigger this first military adviser.

## Reading and acting

One side card presents the highest-priority unread concern or development. Economic and military alerts take priority over optional guidance. Within guidance, independence and drift take priority over routine wandering and the introduction. Use the council's Economic, Military and Guidance filters, and page through longer lists, to review individual reports.

Reading a concern or dismissing its side card acknowledges it. The concern stays in the council while its condition remains active. A higher urgency makes it unread again. Resolved concerns disappear; a later recurrence can notify you anew. High and Moderate also remind you of unresolved concerns after their stated intervals. An old dismiss control cannot acknowledge a later, unseen escalation, reminder or recurrence.

Guidance introduces play and explains daughter households, dispositions, voluntary journeys, drift, independence, discoveries and new domestic lineages. Each adviser can interpret these developments through their own priorities. A development appears once; voluntary wandering is explained only once per household. The reading shelf keeps up to 32 guidance entries. Obsolete guidance about commanding a daughter is retired when that household dies or becomes independent, while the event remains in History. New developments cannot be buried under the original introduction.

Choose **Adviser frequency** in **Watch settings** or at the bottom of the council:

| Frequency | Automatic advice |
| --- | --- |
| **High (default)** | Beginner lessons, developments and all warnings. Unresolved warnings return after three turns. |
| **Moderate** | Contextual lessons and developments, at most one routine reading every three turns. All warnings, with unresolved reminders after eight turns. |
| **Low** | Urgent/critical warnings and major developments (drift, independence and major gathering changes). Developments at most once every eight turns; no beginner lessons or routine reminders. |
| **None** | No automatic adviser cards, including urgent warnings. The council remains available with C or Advisers. |

Frequency is saved separately from campaigns in `%LOCALAPPDATA%\Clio\advisers.txt`. It survives loading, new stories and reopening Clio. A missing or invalid preference defaults to High; selecting None is respected in later stories. This setting changes presentation only. The opening story announcement and actual encounter/ending decisions remain separate from adviser frequency.

High teaches food reserves and upkeep, salt sources, per-band actions, travel costs, camps, animal companionship, reunions, landscape markers, diplomacy when available, and cultural practices. Lessons use the selected controlled household's current supplies and observed surroundings; learning a topic acknowledges it for that story session. Loaded games can receive these lessons immediately without replaying old developments. Important developments and warnings take priority over general teaching. Moderate introduces topics when they become relevant. All available lessons remain readable manually at any frequency.

Each view offers an inspection link appropriate to its argument: a household forecast, salt sources, its Units record, the known map, culture, companions, or a relationship. Household links select the affected controlled household; foreign relationship and location records preserve your commanded household. Reading or inspecting advice does not gather resources, attack, move a band, or spend an action. The player still chooses any order. Links re-evaluate the current situation before opening a record; an obsolete warning or unavailable target cannot silently select an unrelated household.

## Autoplay and other screens

Adviser notifications do not themselves pause autoplay. Opening the council does pause it and returns control to the player. Closing the council leaves autoplay stopped; resume it explicitly when ready. Press **Esc** or **Enter** to close the council. Map movement, gameplay shortcuts and other background controls are blocked while the council is open; fullscreen remains available.

On the map, the side card normally appears on the right. Opening a hex/unit record or crowded-hex roster moves the adviser to a compact card above the selected-band panel on the left; it no longer hides the explanation. Cards also appear on Economy, Culture, Units, Diplomacy and History. A map menu or active camera gesture defers the card temporarily. Modal decisions, council readings, the opening briefing and the ending screen keep priority. Visible advice takes priority over a small history toast; its background consumes clicks and wheel input so the controls underneath cannot activate accidentally.

The introduction and developments covered by guidance no longer force a second automatic historical popup. Routine salt warnings likewise use the side adviser when a current matching economic alert exists. **Actual casualties**, encounter choices and the ending screen retain their prominent reading/decision priority. All events remain in History and can still be opened manually. Hidden history toasts keep their normal expiry timer.

## Information and persistence

Advice is computed from the current game after live commands, including autoplay decisions, and refreshed when the council is opened or a concern is selected. Drawing and hovering do not acknowledge reports or execute simulation commands. Foreign threat evidence requires explored land even when Atlas view is enabled. Navigation links recheck that the affected household is still controlled and the target is still observed.

The council is presentation state, separate from historical events and the saved command record. Loading reconstructs the game first, then evaluates current concerns and baselines development cursors; it does not replay historical tutorial notifications. Acknowledgements reset on loading or starting another story, so unresolved concerns can appear again after a reload. New stories receive a fresh introduction. When the people's story ends, gameplay concerns disappear and the ending screen closes the council.

## Validation

Native checks cover actual forecast consequences, four distinct perspectives, hidden-enemy privacy, daughter-household links, manual takeover, blocked background input, severity escalation, stale dismissals, resolution, recurrence, guidance ordering, retirement, load baselines, preference and optional-guidance controls. Normal, maximum-name and minimum-client layouts are rendered and visually inspected. Portraits are embedded in the application, so the desktop launcher needs no loose artwork files. The [artwork record](../src/Clio.Desktop/Assets/Advisers/PROMPTS.md) preserves the exact prompts and provenance. Build-specific counts and evidence are in the [validation record](VALIDATION.md).
