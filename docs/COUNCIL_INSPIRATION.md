# Council inspiration and review notes

This records the historical references and implemented council design for `council-20`. The implementation description was checked against source; completed native reviews are recorded separately in [VALIDATION.md](VALIDATION.md).

## What the original manuals establish

| Source | Supported design observation |
| --- | --- |
| [Civilization I manual, “Advisors/World Reports”](https://www.civfanatics.com/content/civ1/manual/civ1_man.htm) | Advisers organize the civilization's information into distinct reports. City status, military, intelligence, attitudes, trade and science each have a specific remit. The trade report connects income and maintenance to an actionable assessment of the treasury. Intelligence is limited by established embassies. These are useful precedents for role-specific evidence and direct links into Clio's ledgers. |
| [Civilization II instruction manual, printed pp. 157 and 165](https://manuals.plus/m/f01fb8dcf1d6cafb32aa296472b7905c9cebaa5617cc287254f043fc7ac8dc9b.pdf#page=176) | The manual calls the familiar council the **Town Council**. It describes a video meeting where the player can consult one adviser or the whole group about the current situation. Automatic appearances can be disabled while consultation remains available from the Advisers menu. The reviewed manual does not explicitly establish the often-described individual biases or arguments; we should not cite it as proof of those details. |
| [Civilization III manual, printed pp. 38–39 and 154–155](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3910/manuals/manual.pdf?t=1569013660#page=85) | Advisers provide relevant context during diplomacy, with a More control for further advice. The tutorial explicitly chooses a different research target from the Science Advisor's suggestion to pursue its own defensive strategy. This supports advice as a recommendation the player can assess and overrule. |

The first two links are hosted reproductions of the original manuals; the third is the manual distributed through Steam. Clio uses original portraits and dialogue. Its four-person cast and competing viewpoints are Clio's adaptation, not claims about the manuals.

## Implemented adaptation for Clio

Four people interpret the same observed facts: Sula protects reserves; Tavo values safe approaches and useful exploration; Yara values memory, discovery and emerging traditions; Lian values contact between households. The council separates recorded facts from each person's speech, argument, tradeoff and an attributed reply from another voice. Their recommendations lead to different relevant inspections without changing the shared forecast.

Favoring a voice changes the default speaker for ordinary counsel; it does not reorder concerns. Urgent and Critical reports retain their specialist by default, while all four voices remain directly accessible. The preference survives new/load within the application session. Selecting or favoring a portrait does not acknowledge concerns, issue orders, change household dispositions or alter autoplay policy.

A short map message opens the full reading, which offers a relevant inspection page. Optional introductions and routine commentary can be quieted; economic and military warnings remain enabled. Actual losses, encounter decisions and endings retain their established event priority. Reading pauses autoplay and leaves control with the player when the council closes. Inspection spends no action; any subsequent order remains the player's choice.

## Lifecycle and source safeguards

The read-only audit covered `CouncilPerspectives.cs`, `GameForm.Council.cs`, the adviser and guidance controllers, report policy, events, map interaction, endings and the manual/autoplay/loading paths in `GameForm.cs`.

- **Refresh:** current reports are cached after live manual/autoplay commands, after successful story replacement, and on deliberate council consultation. Drawing and hovering do not advance the simulation or acknowledge reports; the new opinion formatter may read current forecasts. Save replay reconstructs the game first, then resets current advice and baselines historical guidance cursors.
- **Identity:** acknowledgements track a stable concern key, displayed severity and an occurrence token, independently of speaker and preference. Resolution removes the current concern; recurrence gets a new token. The inspection button captures the displayed report and token, rechecks the selected reading/voice, and acknowledges only displayed severity after fresh evaluation.
- **Priority:** current economic/military concerns precede optional guidance. Ending, encounter and important event sheets own input before the council; opening council pauses autoplay, and closing it leaves the player in control. Small adviser messages yield to map inspections, stack rosters, menus and camera navigation.
- **Evidence:** foreign threat checks require explored land, even in Atlas view. Opinions reuse forecast and contact helpers. Inspect links revalidate the controlled actor and visible target. Legacy rules omit personality-specific wandering claims.
- **Destination:** Units inspection explicitly selects the relevant household and a band-capable filter; companion readings select Food & care rather than retaining Salt. A hidden or dead secession subject keeps its map/relationship reading open with an unavailable-target message, before acknowledgement or navigation. Valid foreign map and Diplomacy inspections preserve the player's commanded household; supply and household inspections deliberately select the living subject.

## Focused regression checks

1. Favor each speaker while another has an unread Urgent or Critical report. That warning must still surface, remain in the council, and retain its unread state until actually reviewed. Ordinary preference changes must not make an unchanged warning recur.
2. Capture a dismiss/read/inspect callback, then escalate, resolve/recur, load another story, close the council, kill or secede the actor, or move the target out of known land. Old controls must not dismiss new information or navigate to a different hidden subject.
3. Compare full game state, command history and next autoplay decisions before and after opening, favoring, filtering, paging, reading and rendering. Only intended presentation state and explicit inspection selection may change.
4. Exercise council from every main page; Space, movement, combat, Next band, right-click and blank portrait/card areas must not operate underlying gameplay. Esc/Enter and fullscreen should preserve the established modal rules. Opening counsel must not leave an old event-resume flag that restarts autoplay on close.
5. Turn optional guidance off and produce salt casualties, an encounter and an ending. Preserve those event priorities and their History records. Test loading without a flood of replayed tutorials and starting anew with a fresh introduction.
6. Render four portraits with long household names, all urgency levels, empty counsel, dense reports and minimum-window/fullscreen sizes. Ensure unread counts and selection remain coherent when filtering or reports disappear; bind filter identity explicitly rather than assuming four speakers equal the old three report roles.

These are review requirements, not a test-pass record. Candidate-specific native evidence belongs in the validation artifacts after integration.
