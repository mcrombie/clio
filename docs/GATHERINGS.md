# Gatherings between independent peoples

Implemented first slice, 2026-09-08. This begins the recurring-gathering arc in [future development notes](FUTURE_DEVELOPMENT.md). Larger assemblies, shared projects, leagues, chiefdoms and agriculture remain future work.

## Playing the arc

After a band separates into an independent polity, open **Diplomacy**, choose a known peaceful splinter, and choose **Arrange a gathering**. The selected controlled band hosts it. The **Gathering** subtab shows the offer or current commitment; **Record** preserves previous replies and outcomes.

An invitation uses one host action and provisions equal to 10% of the host's population, rounded up. The preview shows the exact cost, expected reply, meeting place and deadline. A valid invitation costs these supplies even when declined. Opening a preview costs nothing. Confirm explicitly to send it; Escape dismisses the sheet and Enter never spends resources.

Choose safe land remembered by both peoples, within eight travel actions of each. Their routes use ordinary terrain and river costs. An invitation does not move either band: the independent guest travels with its ordinary action budget, attending to survival first. Your host must reach the same place before the displayed deadline. Autoplay honors an already accepted commitment when survival permits.

At a physical meeting, the host may spend an action to give one donation of food, salt or both. Actual supplies leave the host and enter the guest's reserve. The offer is limited to two turns of the guest's needs and must leave the host one turn of its own food, companion care and salt. The guest keeps its own command, supplies and language. The donation is a gift with no automatic repayment.

While together, the host may spend one action to agree another meeting six turns later, with two turns of grace. The guest must actually leave and return. Both bands must be at the agreed place together during that window. Merely remaining together does not keep a return promise.

The record distinguishes accepted or refused invitations, first meetings, donations, completed visits without a return promise, fulfilled promises, missed meetings and interruptions. It records attendance and the reason for closure. Death, a changed political relationship or hostility can interrupt a commitment. Wait three turns after a reply or completed gathering before inviting the same people again.

## Reading the consequences

- **Diplomacy / Gathering:** current place, status, deadlines and available choices.
- **Diplomacy / Record:** remembered commitments, including when a participant leaves current observation.
- **Economy / Trade:** invitation provisions and gifts actually delivered. Food outflow is already included once in Finance; salt donations are a distinct salt outflow.
- **History:** each reply, meeting, gift and outcome, with a link to its gathering record.
- **Advisers:** Lian values renewed ties; Sula questions the provisions spent; Tavo considers exposure and obligations; Yara emphasizes remembered promises. Their arguments use recorded facts. No adviser silently issues the order.

This slice adds relationships through specific actions and remembered obligations. It does not yet add tribute, prices, treaties, recurring deliveries, shared storage or a chiefdom promotion.

## Map opportunities

Raised map badges identify places worth inspecting:

| Badge | Meaning | Action |
| --- | --- | --- |
| Blue compass | Exploration opportunity beside unknown hexes | Travel there to extend the known map |
| Gold grain | Food opportunity | Inspect the gathering yield; move there and gather |
| Pale salt crystals | Salt source | Move there, then gather salt |

Hover directly over a badge to highlight it and its hex. The tooltip names the opportunity and explains the selected band's actual reach and movement cost. Left-click opens the place record; right-click issues normal movement when legal. These are map opportunities, not adviser orders or resources collected automatically on arrival. Unit stacks retain priority and fog still hides unknown resources.

## Persistence

New stories use save format V10 with `GatheringRelations`. Older saves replay their original rules first. Loading through the game can then record `enable-gatherings`; subsequent saves retain `LegacyGatherings` founding plus that explicit command. Replaying a file does not rewrite past actions or consume a different historical random stream. Recorded invitation, gift and return orders rebuild the same meeting state and ledger.
