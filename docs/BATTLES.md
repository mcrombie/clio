# Regions and tactical battles

`battles-32` adds tactical battles to Clio's existing campaign map. This is a first playable combat system, using the actual bands, animals and terrain already in the story.

## Geographic regions

Choose **Regions** in Map views to see regional colors, shared boundaries and a label for each visible region. Regions group connected geography. They do not indicate ownership, allegiance or control; **Polities** remains the separate view for peoples.

World generation already divides connected land into regions averaging roughly 48 hexes. This release exposes those existing divisions; it does not regenerate the map or alter saved geography. Only shared boundaries between known land hexes are drawn with fog enabled. A label uses visible, remembered land and does not reveal a region's unseen extent. Labels avoid unit banners and resource markers. Normal Terrain view keeps its quieter appearance.

## From campaign to battlefield

Choose **Enter battle** from an attack encounter to open a battlefield on the same map. The battle has deployment, fighting and finished phases. Position the participating groups before starting, then direct their movement and attacks through combat rounds. Nearby bands of the same people can support either side. They remain members of their original campaign units; battlefield groups are not newly created bands or population.

During deployment, select one of your formations on the map or in the bottom roster, then click an empty highlighted gold hex to reposition it. **Begin battle** starts round one. Initial placements are provided, so rearranging every formation is optional.

During fighting, each surviving formation has **3 tactical action points per round**, separate from its band's campaign actions.

| Order | Interaction | Effect |
| --- | --- | --- |
| Select | Click a formation or roster entry; N or Tab selects the next ready formation | Shows that formation's available orders |
| Move | Click or right-click a highlighted adjacent empty hex | Spends the terrain's movement cost |
| Strike | Click a highlighted adjacent enemy | Spends 1 point; hover first for damage and surviving retaliation |
| Defend | D or the Defend icon | Spends remaining points and reduces incoming strike damage until the next round |
| End round | Enter, Space or End round | Gives up unused points; the opposing side acts, then the next round begins |
| Retreat | Click the retreat icon | Available only when every surviving participant on your side has an open campaign route away; concedes and withdraws those units, paying ordinary travel food; Escape does not retreat |
| Auto-resolve | A or Auto-resolve | Plays the remaining battle using the same tactical rules |

Movement costs **1 point** on ordinary land, **2** in forest, wetland or ice, and **3** in mountains or across a river. A formation cannot enter occupied ground. Most mountains therefore remain passable, at the cost of a full round's movement.

An attack from higher ground deals more damage. Forest cover, crossing a river and a defending target reduce incoming strike damage. A second friendly formation adjacent to the target provides a flanking bonus. The preview shows the applicable modifiers and retaliation before a strike; this first pass uses immediate melee attacks rather than ranged weapons or a facing system.

The land is part of the decision: inspect terrain and the available positions before committing. Campaign movement rules remain in effect outside battle, including the higher cost of mountains and river crossings.

Victory requires defeating every opposing formation; retreat concedes, and combat that reaches the **12-round limit** ends in disengagement. After disengagement, survivors withdraw where open campaign routes remain; surrounded units can remain in place. A blocked explicit retreat is unavailable rather than moving a unit through an enemy.

When a battle finishes, its result reports the outcome, losses, recovered food and rounds fought. The food report identifies which side actually received it; enemy recovery is not added to your stores. Withdrawals spend the normal travel food of the bands that move, separate from any recovered food. These are actual consequences for the campaign's people, animal counts and reserves. **Return to campaign** (Enter or Space) closes the result. H or F1 toggles the optional battle help, and Save or Ctrl+S works in every phase.

An incoming attack can interrupt **End turn**. The turn remains unfinished while the battle is open. Closing the result resumes the remaining world activity; another incoming encounter can open another battlefield before the same turn settles. Food, salt, wood, animal care and population changes settle once when the campaign turn actually advances, not once per combat round.

## History and saved stories

History records committed outcomes rather than every tactical strike. One primary attack counts as one battle. Supporting units have separate casualty receipts so their real losses enter the totals without counting several battles. Recovered food is credited once to its actual recipient; withdrawal movement and travel food use their own receipts. Losses remain attached to the original households and animal groups.

Tactical battles use the recorded `enable-tactical-battles` command and the `TacticalBattlesEnabled` flag. Existing stories replay their previous commands under their original rules before adopting the new rules. `CLIO-STORY-14` saves retain deployment, an unfinished fighting round or a finished result; a restored battle waits for the player to continue.

## Inspiration and scope

Humankind's published combat overview describes a battlefield drawn from the world map, deployment, direct orders and the value of terrain and position. Those are the design references for this pass. Clio uses its own rules, interface and existing simulation. [Humankind Feature Focus 10: Art of War](https://community.amplitude-studios.com/amplitude-studios/humankind/blogs/761)

The battlefield contains at most **49 connected land hexes**, taken from the encounter and approach regions; a very small region can extend over a neighboring boundary to make room for deployment. Seeing that tactical patch does not add its unknown terrain to the campaign's remembered places or reveal unrelated world units. Up to **three source participants per side**, including the principal unit, can take part, subject to space. Each source splits into at most three temporary formations. Support bands must be on or adjacent to their side's principal unit and inside the battlefield when it opens. Joining costs them no extra campaign action; later reinforcements are not part of this first pass.

Companion animals are not automatically recruited as support. An animal group can be a principal participant when attacked or attacking. Dogs retain their hunting-only strength contribution and provide no strength bonus against people.

Automatic fights involving your people use these same tactical rules. Fights entirely between nonplayer units still use the previous immediate encounter resolver in this release.

This prototype is an extension of the current early-human campaign. Geographic regions are not yet administrative provinces, and tactical formations do not introduce permanent military unit types. The existing food, salt, wood, domestication and tribal-contact systems continue to supply the campaign consequences.
