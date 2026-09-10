# Livestock, milk, meat and hunting dogs

The livestock-29 prototype makes herd size matter to production. These are game balance values, not historical measurements. Milk and meat enter the existing **food reserves**; their sources are shown separately in the ledger. Separate dairy/meat inventories, pasture, lactation cycles and preservation are deferred.

| Animal | Domestic role | Food production at breed yield 1 |
| --- | --- | --- |
| Wild aurochs ? cattle | Milk herd, with optional slaughter | 0.5 food per animal per turn; 18 food per slaughtered animal |
| Goats | Milk herd, with optional slaughter | 0.25 food per animal per turn; 8 food per slaughtered animal |
| Wolves ? dogs | Hunting support | No milk, meat action, direct food or gathering bonus |
| Deer | Wild game only in Clio | Cannot be domesticated; can be hunted |

## Production and care

Milk arrives automatically at turn close while an owned herd accompanies its living band on the same hex. Output is **count ? breed yield ? the milk rate**, without the old 25-food production cap. Ten cattle at yield 1 give 5 milk food each turn; ten goats give 2.5. Herd growth or losses change future production.

Care consumes food each turn: 0.06 per head of cattle, 0.04 per goat, and 0.3 per dog. The food ledger shows this expense separately from milk income and people's needs. Wild animals produce nothing for your people.

Select your cattle or goat counter to see milk, care, and a slaughter preview. **Slaughter** spends one action from the owning band and kills **max(1, floor(herd size / 10))** animals. Meat equals the number killed ? breed yield ? the meat rate. A twenty-cattle herd at yield 1 provides 36 food from two animals, leaving eighteen and reducing milk from 10 to 9 per turn. Slaughtering a final animal removes that herd's future output. Dogs and wild herds cannot use this action.

**Economy ? Resources** lists domestic herds and milk output. Cumulative receipts distinguish milk, slaughter meat, and animals slaughtered; meat is not counted as hunting income. The herd card can also be opened from the companions list. No extra map beacon or main action icon is introduced.

## Dogs and domestication

Dogs improve an owning band's initiated animal attacks by **1.5% strength per nearby dog, capped at 12%**. They do not increase gathering, generate food directly, or increase strength against people. Legacy classic hunts use the corresponding chance-point bonus. Dogs still require care.

Goats use repeated peaceful approaches and remembered trust, like other tamable groups; their base approach chance is 75%, the offering costs 10 food, and domestication requires 6 trust. Aurochs retain their existing trust-based route to cattle. Deer remain huntable moving groups, but the new rules reject attempts to befriend them for domestication.

## Gameplay modes

- **Manual:** select an owned herd and use its Slaughter button; the preview names the animals lost and the remaining milk output.
- **Semiautomatic:** a low-food household with livestock can trigger a choice between preserving milk production and using some animals for meat. The meat priority stops slaughtering once a band holds three turns of food. Each slaughter still costs an action.
- **Automatic:** bands may slaughter in a food emergency when ordinary gathering will not meet immediate needs and meat gives more food. This is a fallback, not a free recurring meat credit.

## Saved stories

The new rules are introduced through a recorded `enable-livestock` command **after** old actions replay. New stories enable them on creation. Earlier dogs, cattle and deer therefore keep their former effects while an old history is being reconstructed; subsequent play uses the new model.

On upgrade, previously domestic deer are released alive on their current hex, with ownership and domestication trust cleared. The journal explains this transition. Wild goats are seeded deterministically without consuming the old random stream, including an accessible group near the leading band for experimentation.

Livestock stories save as **CLIO-STORY-13**, retaining the mode and decision metadata used by V12. Earlier files remain readable. Loading does not overwrite the original save and leaves automation stopped. These changes were compiled, with test and playthrough verification deferred at the user's request.
