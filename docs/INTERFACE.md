# The map-edge interface

`horizon-33` lets the campaign map fill the viewport, with compact controls around its edges. It builds on `quiet-30`: symbols summarize the situation, hover explains them, and clicking opens the relevant detail. This is a presentation pass, with the existing simulation, gameplay modes and saved commands retained.

## Essential supplies on the map; complete records on other pages

The campaign map keeps four metrics at the top center: people, food, salt and wood. Main page navigation sits at the upper right, with adviser and event counters immediately beneath it. The broad central landscape stays available for travel and inspection. Band readiness, contact and conflict remain available through Units, Diplomacy and their relevant tooltips.

Economy, Culture, Units, Diplomacy and History retain the fuller tribal status strip. Its symbols provide the following routes to detail; the map's four resource and population metrics retain their corresponding explanations.

| Symbol | What it shows | Click to open |
| --- | --- | --- |
| People | Living tribal population and net change since the prior recorded turn | Demographics |
| Food | Total food reserves; warning if a band has less than one turn of food and animal care | Economy Overview for the least supplied band |
| Salt | Total salt; warning if any band has fewer than two turns | The least supplied band's salt ledger and known sources |
| Wood | Total wood; warning if a band cannot fuel a full fire | The least supplied band's wood ledger |
| Cooperation | How many bands are drifting, or Stable | Units and reunion deadlines |
| Conflict | Known, living hostile bands | Encounter history |
| Bands | Bands with actions remaining / all controlled bands | The next ready band |
| Adviser | Unread advice | The council |
| History | Unread events | Events and their consequences |

Resource totals do not pool supplies. Each band still carries its own food, salt and wood; the shortage cues prevent a prosperous leader from hiding an undersupplied daughter. Food's short reserve estimate excludes future camp and milk income, which remain visible in the full forecast. Wood shortages remove fire benefits rather than adding a separate survival penalty.

Population change includes births, deaths and departures from the tribe. Stable cooperation means no current cultural drift, not that all bands share a hex. Conflict counts known hostile people, not wildlife or enemies hidden in unexplored land. Tooltips spell out these distinctions.

## Map details when requested

In `zoom-31`, scroll the mouse wheel in or out to change map scale. The closest view now reaches twice the previous maximum magnification. The starting view and farthest zoom-out are unchanged.

The map-edge toolbar uses symbols for map controls and useful-place highlights. Hover identifies each control and its current state. The existing layers, known-land/atlas choice and food, salt and exploration information remain available. Compact orders stay at the bottom edge rather than taking a full-width band away from the landscape.

The selected-band panel starts closed. Open it from the band badge when you need that band's supplies, actions, personality or reunion information, then close it to restore space. The compact bottom action icons retain their explanatory tooltips and shortcuts.

In `battles-32`, Regions view outlines connected geographic regions. Entering combat opens a separate tactical view of the actual landscape, with compact formation orders and terrain explanations on hover. Deployment, rounds and the result stay together on that screen; campaign panels return after the result is closed. See [Regions and tactical battles](BATTLES.md) for controls and campaign consequences.

Selecting a hex or unit opens a compact record with immediate facts and useful actions. Expand it for the fuller place or unit account and ledger links. Opening a record spends no action. Inspecting a foreign group does not transfer your orders away from your controlled band.

Automatic advice and routine event cues stay out of the way while a band panel, map record, crowded-hex roster or menu is open. Their counters and full records remain accessible; opening an inspector does not acknowledge advice.

## Economy without a wall of numbers

Economy Overview is a compact summary with clear routes to the detailed accounts. Demographics, Resources, Trade and Finance still hold the underlying mechanics, forecasts and recorded transactions. Milk, meat, care, food, salt and wood remain explained in their appropriate ledgers. Reducing the overview does not remove economic information.

## One notification at a time

Automatic advice is a small right-edge card below the map's navigation and counters: portrait, adviser role, one actionable sentence, the affected band, **Details** and a visible dismiss button. Details opens the full council explanation, tradeoffs and competing views. **High remains the default**, with beginner teaching intact; Moderate, Low and None keep their existing behavior. Ledger pages retain their existing cue position.

Routine events use a smaller icon-and-title cue with **Read** and dismiss. Hover gives the impact; Read opens the full account. Growth uses a green people symbol, losses a red people symbol, contact and gatherings a cooperation symbol, and combat a conflict symbol. Combat summaries use recorded casualties and damage, without inferring a victory.

An adviser cue takes precedence over a routine event cue; both use the same map-edge location, so they never stack over one another. Clicking a cue or its background opens its details without selecting the map underneath. Every event remains in History. Manual play no longer automatically opens a large history reading for each major outcome; the explicit autoplay pause-on-major-events setting remains available. Opening briefings, encounters requiring choices, semiautomatic story decisions and the ending screen retain their dedicated presentations.

## Delivery status

Validation remains paused for this `horizon-33` design iteration, as requested. No new regression tests, screenshot renders or playthrough checks are being run for this layout pass. Earlier validation records describe their own builds and are not verification of this interface pass.

## Paper adviser portraits

The adviser set now uses natural human pencil-and-ink studies on pale warm paper, drawing on Michael's Cromblog bird studies, human profile and shaded mountains. Square paper edges replace the circular portrait medals. All four existing roles, notices, guidance frequency and council access remain. See [portrait prompts and provenance](../src/Clio.Desktop/Assets/Advisers/PROMPTS.md).
