# Turns, provisioning and living lineages

## Current interface: turn iterations

The `places-09` revision measures progress as **Turn 1, Turn 2, ...**, with two priorities per turn. The Map header, Economy chart, History records, event cards and action controls use turns. New Story no longer offers a calendar choice. New games use the existing `Abstract` pace: no years are assigned to an iteration. Conditions are displayed as *Mild conditions*, *Lean conditions*, *Harsh conditions* and *Abundant conditions*.

Loading an older story preserves its recorded pace and all of its original simulation outcomes, but the interface displays turn numbers. The presentation helper translates older calendar labels without changing the simulation's replay records. Provisioning, care, movement and population resolve once per turn. The earlier calendar APIs below remain for save compatibility and historical validation; they are no longer player options.

## Retained calendar rules for older saves

New historical stories resolve one aggregate chapter at a time. A chapter represents the community's choices, provisioning and demographic change across an interval; the engine does not secretly run 25 or 100 annual simulations.

| Pace | Calendar | Conditions |
| --- | --- | --- |
| Generations | 25 years per chapter | Prevailing conditions across years |
| Centuries | 100 years per chapter | Prevailing conditions across years |
| Abstract | Numbered chapters, no fixed year count | Prevailing conditions across years |
| Legacy seasons | Original numbered chapters | Original three-chapter seasons |

The founding is **Year 0**. The next chapter begins at Year 25 or Year 100. Dates are relative to that fictional founding: a Chinese-, Egyptian- or Sumerian-inspired culture does not imply a historical BCE start date. Calendar scaling changes displayed elapsed years, not the number of resource, animal or demographic ticks. Legacy constructor forms retain the original seasons and all original action results.

Historical conditions are *Mild years*, *Lean years*, *Harsh years* and *Abundant years*, with gathering factors of 1.00, 0.90, 0.72 and 1.14. Two adjacent chapters share the same prevailing condition. The world's seed and interval determine the sequence without consuming action or ecology randomness. These describe aggregate pressures on production, not a winter lasting a human generation. Unsheltered communities in cold places can suffer exposure during harsh years.

Provisioning in historical mode measures **capacity to meet community needs**. It is not a warehouse of grain consumed unchanged for centuries. Gathering increases capacity; a hearth and cattle supply continuing output; people and animal care create recurring needs; the retention deduction abstracts the loss of capacity between chapters. The existing two-action structure, knowledge thresholds and demographic increments remain prototype chapter mechanics, rather than calibrated annual historical rates.

## Visible domestic effects

Living lineages persist while their successive animal generations change. Their numbers represent the current supported working groups.

The table below describes the **Classic** rules retained by earlier saves. With **MobileUnits**, dogs assist gathering and fighting strength; targeted attacks resolve damage rather than a hunt-success roll. Deer and mammoths also aid gathering, mammoths and dragons contribute fighting strength, and every companion species has care costs. Mobile rules apply these effects even with the legacy calendar. [Current encounter and companion effects](UNIT_ENCOUNTERS.md).

| Effect | Classic historical modes | Classic seasonal stories |
| --- | --- | --- |
| Dog gathering aid | +1% per dog, capped at +10% | None |
| Dog hunting aid | +1.5 percentage points per dog, capped at +12 points; total hunt chance capped at 97% | +5 points from an owned dog group |
| Dog care | 0.3 capacity per animal per chapter | Original 0.3 provisions |
| Cattle output | `min(count × 0.7, 25) × lineage Yield` per chapter | Original formula |
| Cattle care | 0.06 capacity per animal per chapter | None |

Classic historical group growth is bounded by the owning community and by a working-group maximum of 24 dogs or 60 cattle per lineage. This keeps a useful partnership from becoming an unlimited compounding care burden during long observation. These are prototype support limits, not historical population estimates. Classic seasonal animal growth remains unchanged; MobileUnits adds species-specific support limits and releases surviving groups whose owner dies.

`BandEconomy` is the shared source for displayed assistance, care, cattle output, hunt probabilities and end-chapter forecasts. Its player forecast accounts for the actual order of care deductions, passive production, human needs, exposure, growth and retention. It reports care actually charged as well as nominal care needs: charges cannot exceed available capacity. The forecast includes genuine births, hunger losses and exposure losses, so a journal can reconcile them with realized population changes. Autonomous bands choose their movement and actions before their accounting; a current-place forecast does not predict those autonomous decisions.

Mobile attacks and retreats can also change the player's state before accounting. The displayed forecast describes the current household; the journal uses the actual post-encounter economy receipt when that chapter closes, keeping combat and demographic consequences separate.

The tests exercise actual assisted hunts and gathering, exact accounting through good and hungry chapters, legacy compatibility, and multi-polity autoplay spanning 6,000 relative years.
