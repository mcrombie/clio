using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private int selectedAnimalId = -1, encounterTargetId = -1;
        private int encounterActorId = -1;
        private UnitKind encounterTargetKind;
        private bool encounterChoice;
        private readonly Timer unitAnimationTimer = new Timer { Interval = 40 };
        private bool BlockingSheet { get { return BattleOverlayActive || noticeModal || encounterChoice || endingOpen || adviserOpen || gatheringChoice || openingAnnouncement || StoryModeBlocking; } }
        private bool UnitVisible(int cell) { return cell >= 0 && cell < game.World.Cells.Length && (!map.Fog || game.Explored.Contains(cell)); }

        private void SelectInspectorTab(int tab)
        {
            inspectorPage = tab; selectedAnimalId = -1;
            if (tab == 1)
            {
                Band band = UnitVisible(selected) ? game.Bands.FirstOrDefault(b => b.CellId == selected && b.Population > 0) : null;
                inspectedBandId = band == null ? -1 : band.Id;
            }
            Invalidate();
        }

        private void SelectAnimal(Beast animal)
        {
            if (animal == null || animal.Count <= 0 || !UnitVisible(animal.CellId)) return;
            selectedAnimalId = animal.Id; selected = animal.CellId; inspectorPage = 2;
            map.SelectedAnimalId = animal.Id; map.SelectedBandId = -1; Invalidate();
        }

        private void SynchronizeUnitSelection()
        {
            if (commandedBandId >= 0 && !game.CanControlBand(commandedBandId)) ArmMapCommandBand(-1);
            if (selectedAnimalId >= 0 && inspectorPage == 2)
            {
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == selectedAnimalId && b.Count > 0);
                if (animal == null || !UnitVisible(animal.CellId)) selectedAnimalId = -2;
                else selected = animal.CellId;
            }
            else if (inspectorPage == 1 && inspectedBandId >= 0)
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == inspectedBandId && b.Population > 0);
                if (band != null && UnitVisible(band.CellId)) selected = band.CellId;
                else inspectedBandId = -2;
            }
        }

        private string AnimalUnitName(Beast animal)
        {
            if (game.LivestockEnabled && animal.Domestic)
                return LivestockEconomy.DisplayName(game, animal) + (LivestockEconomy.IsLivestock(animal) ? " herd" : "");
            return animal.Domestic && !String.IsNullOrEmpty(animal.BreedName) ? animal.BreedName : animal.Kind == BeastKind.Wolves ? "Wolf pack" : animal.Kind == BeastKind.Mammoths ? "Mammoth herd" : animal.Kind == BeastKind.Dragon ? "Dragon" : animal.Kind + " herd";
        }

        private static string UnitTemperament(UnitProfile unit)
        { return unit.Hostile && unit.Temperament == "Companion" ? "Hostile companion" : unit.Temperament; }

        private string AnimalUnitDescription(Beast animal, UnitProfile unit)
        {
            if (game.LivestockEnabled && animal.Kind == BeastKind.Deer)
                return "Deer remain wild. They can be hunted, but cannot be domesticated.";
            if (!animal.Domestic) return unit.Description;
            if (game.LivestockEnabled && game.CanControlBand(animal.OwnerId))
            {
                if (animal.Kind == BeastKind.Wolves) return "Dogs help this band's hunts. They do not gather or produce food, and their care costs food each turn.";
                if (LivestockEconomy.IsLivestock(animal)) return "Milk scales with the number of living animals. Slaughter provides meat now by killing animals, reducing the herd and its future milk output.";
            }
            if (game.CanControlBand(animal.OwnerId)) return "Travels with its own household in your tribe. Its benefits and care are recorded in that household's ledger.";
            return unit.Hostile ? "This group belongs to a hostile people and can be attacked separately." : "This group belongs to another people. Attacking it starts a feud.";
        }

        private void MoveSelection()
        {
            if (!RequireManualOrders()) return;
            StopAutoplay(null);
            if (game.Rules == SimulationRules.Classic) { Command("move:" + selected); return; }
            if (game.TribesEnabled && !HasCommandBand) { status = "Select one of your tribe's bands before giving an order."; Invalidate(); return; }
            if (LostUnitSelection()) return;
            Band actor = CurrentOrderBand;
            Beast animal = game.Beasts.FirstOrDefault(b => b.Id == selectedAnimalId && b.Count > 0 && (!b.Domestic || !game.CanControlBand(b.OwnerId)));
            if (inspectorPage == 2 && animal != null && UnitVisible(animal.CellId)) { OpenEncounterChoice(UnitKind.Animal, animal.Id); return; }
            if (inspectorPage == 1 && !game.CanControlBand(inspectedBandId))
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == inspectedBandId && b.Population > 0 && UnitVisible(b.CellId));
                if (band != null) { OpenEncounterChoice(UnitKind.Band, band.Id); return; }
            }
            if (game.Explored.Contains(selected) && game.World.Cells[selected].IsLand && game.World.Cells[actor.CellId].Neighbors.Contains(selected))
            {
                animal = game.Beasts.Where(b => b.CellId == selected && b.Count > 0 && (!b.Domestic || !game.CanControlBand(b.OwnerId))).OrderBy(b => b.Id).FirstOrDefault();
                if (animal != null) { SelectAnimal(animal); OpenEncounterChoice(UnitKind.Animal, animal.Id); return; }
                Band band = game.Bands.FirstOrDefault(b => b.CellId == selected && !game.CanControlBand(b.Id) && b.Population > 0);
                if (band != null) { inspectedBandId = band.Id; inspectorPage = 1; OpenEncounterChoice(UnitKind.Band, band.Id); return; }
            }
            Command("move:" + selected);
        }

        private void ChooseAttack()
        {
            if (!RequireManualOrders()) return;
            StopAutoplay(null);
            if (game.Rules == SimulationRules.Classic) { Command("hunt"); return; }
            if (LostUnitSelection()) return;
            if (inspectorPage == 1 && inspectedBandId >= 0 && !game.CanControlBand(inspectedBandId)) { OpenEncounterChoice(UnitKind.Band, inspectedBandId); return; }
            Beast target = game.Beasts.FirstOrDefault(b => b.Id == selectedAnimalId && b.Count > 0 && UnitVisible(b.CellId));
            if (target == null) target = game.Beasts.Where(b => b.CellId == CurrentOrderBand.CellId && b.Count > 0 && !b.Domestic).OrderBy(b => b.Id).FirstOrDefault();
            if (target != null) { SelectAnimal(target); OpenEncounterChoice(UnitKind.Animal, target.Id); }
            else { StopAutoplay(null); status = "Select an animal group or another people's banner to inspect an attack."; Invalidate(); }
        }

        private void ChooseBefriend()
        {
            if (!RequireManualOrders()) return;
            StopAutoplay(null);
            if (game.Rules == SimulationRules.Classic) { Command("tame"); return; }
            if (LostUnitSelection()) return;
            Beast target = game.Beasts.FirstOrDefault(b => b.Id == selectedAnimalId && b.Count > 0 && UnitVisible(b.CellId));
            if (target == null) target = game.Beasts.Where(b => b.CellId == CurrentOrderBand.CellId && b.Count > 0 && !b.Domestic && LivestockEconomy.CanDomesticate(game, b)).OrderByDescending(b => EncounterRules.Animal(game, b).FriendChance).ThenBy(b => b.Id).FirstOrDefault();
            if (target != null) { SelectAnimal(target); OpenEncounterChoice(UnitKind.Animal, target.Id); }
            else { StopAutoplay(null); status = "Select a wild animal unit. Each group carries its own trust and temperament."; Invalidate(); }
        }

        private bool LostUnitSelection()
        {
            SynchronizeUnitSelection();
            bool lost = inspectorPage == 2 && selectedAnimalId == -2 || inspectorPage == 1 && inspectedBandId == -2;
            if (lost) { status = "The selected unit is no longer in view. Choose another counter before giving an order."; Invalidate(); }
            return lost;
        }

        private UnitProfile EncounterTarget()
        {
            if (encounterTargetKind == UnitKind.Animal)
            {
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == encounterTargetId && b.Count > 0 && UnitVisible(b.CellId));
                return animal == null ? null : EncounterRules.Animal(game, animal);
            }
            Band band = game.Bands.FirstOrDefault(b => b.Id == encounterTargetId && b.Population > 0 && UnitVisible(b.CellId));
            return band == null ? null : EncounterRules.Band(game, band);
        }

        private void OpenEncounterChoice(UnitKind kind, int id)
        {
            if (BlockingSheet || game.Rules != SimulationRules.MobileUnits) return;
            if (game.TribesEnabled && !HasCommandBand) { status = "Select a band from your tribe before reviewing its encounter."; Invalidate(); return; }
            encounterActorId = CurrentOrderBand.Id;
            ClearMapTransient();
            StopAutoplay(null);
            encounterTargetKind = kind; encounterTargetId = id;
            if (EncounterTarget() == null) { status = "That unit is no longer in view."; Invalidate(); return; }
            StopAutoplay(null); activeNotice = null; noticeTimer.Stop(); encounterChoice = true;
            buttons.Clear(); dragging = false; Capture = false; map.IsNavigating = false; Invalidate();
        }

        private void CloseEncounterChoice()
        { encounterChoice = false; buttons.Clear(); Invalidate(); }

        private void CommitEncounter(bool befriend)
        {
            if (!encounterChoice) return;
            if (!game.CanControlBand(encounterActorId)) { CloseEncounterChoice(); status = "That band is no longer under your command."; return; }
            UnitProfile target = EncounterTarget();
            if (target == null) { CloseEncounterChoice(); status = "That unit is no longer in view."; return; }
            EncounterOutlook outlook = EncounterRules.Outlook(game, encounterActorId, encounterTargetKind, encounterTargetId);
            if (befriend ? !outlook.CanBefriend : !outlook.CanAttack)
            { status = befriend ? outlook.BefriendReason : outlook.AttackReason; Invalidate(); return; }
            string command = befriend ? "befriend-animal:" : encounterTargetKind == UnitKind.Animal ? "attack-animal:" : "attack-band:";
            int targetId = encounterTargetId, actorId = encounterActorId; CloseEncounterChoice();
            Command(game.TribesEnabled ? "band:" + actorId + ":" + command + targetId : command + targetId);
        }

        private void EncounterAction(Graphics g, string label, RectangleF bounds, bool enabled, Action action, bool active)
        {
            if (enabled) Button(g, label, bounds.X, bounds.Y, bounds.Width, bounds.Height, action, active, false);
            else
            {
                if (Art.PaperMode) MapPaper.Surface(g, bounds, false);
                else
                {
                    Art.Fill(g, Color.FromArgb(24, 34, 37), bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    using (Pen pen = new Pen(Border)) g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
                Typography.Line(g, label, new RectangleF(bounds.X + 7, bounds.Y, bounds.Width - 14, bounds.Height), 14, Art.PaperMode ? MapPaper.DisabledInk : Color.FromArgb(102, 114, 111), TypeRole.Action, true, StringAlignment.Center);
            }
        }

        private void DrawEncounterChoice(Graphics g)
        {
            if (!encounterChoice) return;
            UnitProfile target = EncounterTarget();
            if (target == null) { CloseEncounterChoice(); return; }
            if (!game.CanControlBand(encounterActorId)) { CloseEncounterChoice(); return; }
            EncounterOutlook outlook = EncounterRules.Outlook(game, encounterActorId, encounterTargetKind, encounterTargetId);
            Band actor = game.Bands.First(b => b.Id == encounterActorId);
            Beast animal = encounterTargetKind == UnitKind.Animal ? game.Beasts.First(b => b.Id == encounterTargetId) : null;
            Band band = encounterTargetKind == UnitKind.Band ? game.Bands.First(b => b.Id == encounterTargetId) : null;
            using (Brush shade = new SolidBrush(Art.PaperMode ? Color.FromArgb(112, MapPaper.MutedInk) : Color.FromArgb(195, 6, 13, 17))) g.FillRectangle(shade, 0, 0, 1600, 960);
            buttons.Clear();
            Art.Panel(g, new RectangleF(365, 163, 870, 667), Color.FromArgb(25, 38, 41), true);
            Typography.Label(g, "Where the paths cross", new RectangleF(409, 184, 782, 26), 13, Art.Gold, 1.2f, StringAlignment.Center);
            Art.Rule(g, 409, 224, 782);
            if (animal != null) IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(760, 239, 80, 49), Art.Gold, animal.Domestic);
            else IdentityArt.DrawEmblem(g, game.TribeOf(band.Id), new RectangleF(775, 239, 50, 50), false);
            Typography.Line(g, animal == null ? band.Name : AnimalUnitName(animal), new RectangleF(412, 297, 776, 51), 38, Art.Ink, TypeRole.Display, true, StringAlignment.Center);
            Typography.Label(g, UnitTemperament(target) + "  /  " + target.Count + (animal == null ? " people" : target.Count == 1 ? " animal" : " animals"), new RectangleF(419, 352, 762, 26), 12, target.Hostile ? Art.PaperMode ? MapPaper.Warning : Color.FromArgb(218, 142, 116) : Art.Gold, .7f, StringAlignment.Center);
            Typography.Draw(g, animal == null ? target.Description : AnimalUnitDescription(animal, target), new RectangleF(418, 396, 764, 54), 18, Art.Muted, TypeRole.Body);
            Art.Panel(g, new RectangleF(411, 466, 372, 75), Panel, false);
            Art.Panel(g, new RectangleF(806, 466, 382, 75), Panel, false);
            Typography.Label(g, "Your band: " + actor.Name, new RectangleF(429, 472, 340, 24), 11.5f, Art.Muted);
            Typography.Line(g, outlook.PlayerStrength.ToString("0"), new RectangleF(428, 497, 339, 35), 30, Art.Ink, TypeRole.Number);
            Typography.Label(g, "Their fighting strength", new RectangleF(824, 472, 346, 24), 11.5f, Art.Muted);
            Typography.Line(g, outlook.TargetStrength.ToString("0"), new RectangleF(822, 497, 348, 35), 30, Art.Gold, TypeRole.Number);
            Typography.Draw(g, outlook.CanAttack ? outlook.Summary : outlook.AttackReason, new RectangleF(418, 554, 764, 48), 17, Art.Ink, TypeRole.Body);
            string friendship = animal == null ? "Attacking another people begins a feud. Their band may return the attack." : animal.Domestic ? "This is a domestic lineage. Its allegiance belongs to its people." :
                "Peaceful encounter: " + (target.FriendChance * 100).ToString("0.#") + "% chance of trust. " + animal.PositiveContacts + " / " + target.TrustThreshold + " trust; offering " + target.OfferingCost.ToString("0") + ". A rejected approach can cause injuries.";
            if (animal != null && game.LivestockEnabled && !LivestockEconomy.CanDomesticate(game, animal))
                friendship = "Deer cannot be domesticated. Hunt this wild group or leave it alone; peaceful approaches do not turn it into a domestic herd.";
            Typography.Draw(g, friendship, new RectangleF(418, 615, 764, 56), 17, Art.Muted, TypeRole.Body);
            Typography.Line(g, game.TacticalBattlesEnabled ? "Attack opens deployment on the region's actual hexes. Campaign cost: " + outlook.ActionCost + (outlook.ActionCost == 1 ? " action." : " actions.") : outlook.RequiresMove ? outlook.ActionCost + (outlook.ActionCost == 1 ? " action includes" : " actions include") + " the approach and encounter." + (outlook.ActionCost > 1 ? " Mountains and river crossings take extra effort." : "") : "One action. Damage and trust stay with each moving unit.", new RectangleF(418, 681, 764, 29), 17, Art.Gold, TypeRole.Annotation, true);
            if (animal != null && !outlook.CanBefriend) Typography.Line(g, Timeline.DisplayText(game, outlook.BefriendReason), new RectangleF(418, 712, 764, 24), 14, Art.Muted, TypeRole.Annotation, true);
            Art.Rule(g, 409, 746, 782);
            Button(g, "Leave alone  ·  Esc", 413, 763, 189, 42, CloseEncounterChoice, false, false);
            bool peacefulPassage = game.CanMoveBand(actor.Id, target.CellId) && !EncounterRules.HostileAt(game, target.CellId, actor.Id);
            if (peacefulPassage) Button(g, "Move without contact", 613, 763, 159, 42, delegate
            {
                int actorId = encounterActorId, cell = target.CellId;
                if (!game.CanControlBand(actorId) || !game.CanMoveBand(actorId, cell) || EncounterRules.HostileAt(game, cell, actorId)) return;
                CloseEncounterChoice(); Command(game.TribesEnabled ? "band:" + actorId + ":move:" + cell : "move:" + cell);
            }, false, false);
            if (animal != null) EncounterAction(g, "Befriend  [B]", new RectangleF(785, 763, 190, 42), outlook.CanBefriend, delegate { CommitEncounter(true); }, false);
            EncounterAction(g, game.TacticalBattlesEnabled ? "Enter battle  [A]" : "Attack  [A]", new RectangleF(994, 763, 194, 42), outlook.CanAttack, delegate { CommitEncounter(false); }, true);
        }

        private void DrawFightingBandDetails(Graphics g, Band band)
        {
            UnitProfile unit = EncounterRules.Band(game, band);
            InspectorPair(g, "Fighting strength", unit.Strength.ToString("0"), 557);
            InspectorPair(g, "Condition", unit.CurrentHealth.ToString("0") + " / " + unit.MaxHealth.ToString("0"), 587);
            Meter(g, 1295, 619, 267, unit.MaxHealth <= 0 ? 0 : unit.CurrentHealth / (double)unit.MaxHealth, unit.Hostile ? Art.PaperMode ? MapPaper.Warning : Color.FromArgb(207, 125, 100) : LedgerGreen);
            Typography.Line(g, game.Languages[band.LanguageId].Name + " · " + (band.Cohesion * 100).ToString("0") + "% cohesion", new RectangleF(1295, 634, 267, 27), 17, Art.Muted, TypeRole.Annotation, true);
            Typography.Draw(g, game.CanControlBand(band.Id) ? "Your household. Wounds weaken its fighting strength and recover over time." : unit.Hostile ? "A hostile people. The feud can bring further attacks; strength and shelter matter." : "An independent people. Attacking will begin a feud.", new RectangleF(1295, 673, 267, 57), 16, unit.Hostile ? Art.Gold : Art.Muted, TypeRole.Annotation);
            if (game.CanControlBand(band.Id)) Button(g, "Open household ledger", 1295, 740, 267, 36, delegate { ArmMapCommandBand(band.Id); OpenEconomy(0); }, false, false);
            else
            {
                EncounterOutlook outlook = EncounterRules.Outlook(game, CurrentOrderBand.Id, UnitKind.Band, band.Id);
                EncounterAction(g, "Inspect attack  [A]", new RectangleF(1295, 740, 267, 36), outlook.CanAttack, delegate { OpenEncounterChoice(UnitKind.Band, band.Id); }, true);
                if (!outlook.CanAttack) Typography.Line(g, outlook.AttackReason, new RectangleF(1295, 719, 267, 21), 13, Art.Muted, TypeRole.Annotation, true);
            }
        }

        private void DrawAnimalUnitInspector(Graphics g)
        {
            Beast[] animals = game.Beasts.Where(b => b.CellId == selected && b.Count > 0).OrderBy(b => b.Id).ToArray();
            Typography.Label(g, "Moving animal units", new RectangleF(1294, 253, 269, 25), 12, Art.Gold);
            if (animals.Length == 0 || selectedAnimalId == -2)
            {
                Typography.Line(g, "The tracks pass on", new RectangleF(1292, 304, 272, 44), 30, Art.Ink, TypeRole.Heading, true);
                Art.Icon(g, "move", 1400, 405, 48, Art.Muted);
                Typography.Draw(g, selectedAnimalId == -2 ? "The selected group has died or passed beyond your people's sight. Its place in the inspector stays empty until you choose another unit." : "No animal group remains in this place. Packs and herds roam as turns advance; their trust travels with them.", new RectangleF(1300, 493, 255, 125), 19, Art.Muted, TypeRole.Annotation);
                Typography.Draw(g, "Select an animal counter on the map to inspect that individual group.", new RectangleF(1300, 641, 255, 69), 16, Art.Ink, TypeRole.Body);
                if (animals.Length > 0) Button(g, "Inspect remaining groups", 1295, 739, 267, 37, delegate { SelectAnimal(animals[0]); }, false, false);
                return;
            }
            int index = Array.FindIndex(animals, b => b.Id == selectedAnimalId);
            if (index < 0) { index = 0; selectedAnimalId = animals[0].Id; map.SelectedAnimalId = selectedAnimalId; }
            Beast animal = animals[index]; UnitProfile unit = EncounterRules.Animal(game, animal);
            int current = index;
            Button(g, "Previous", 1295, 285, 88, 29, delegate { SelectAnimal(animals[(current + animals.Length - 1) % animals.Length]); }, false, false);
            Typography.Line(g, (index + 1) + " / " + animals.Length, new RectangleF(1390, 285, 81, 29), 16, Art.Muted, TypeRole.Number, true, StringAlignment.Center);
            Button(g, "Next", 1478, 285, 84, 29, delegate { SelectAnimal(animals[(current + 1) % animals.Length]); }, false, false);
            IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(1295, 336, 43, 33), Art.Ink, animal.Domestic);
            FittedTitle(g, AnimalUnitName(animal), new RectangleF(1349, 328, 213, 63), 27, Art.Ink);
            Typography.Line(g, "Unit " + animal.Id + " · " + animal.Count + (animal.Count == 1 ? " animal · " : " animals · ") + (animal.Domestic ? "domestic" : "wild"), new RectangleF(1295, 395, 267, 25), 16, Art.Muted, TypeRole.Annotation, true);
            Typography.Line(g, UnitTemperament(unit), new RectangleF(1293, 427, 269, 31), 22, unit.Hostile ? Art.PaperMode ? MapPaper.Warning : Color.FromArgb(221, 145, 113) : Art.Gold, TypeRole.Heading, true);
            InspectorPair(g, "Condition", unit.CurrentHealth.ToString("0") + " / " + unit.MaxHealth.ToString("0"), 467);
            Meter(g, 1295, 499, 267, unit.MaxHealth <= 0 ? 0 : unit.CurrentHealth / (double)unit.MaxHealth, LedgerGreen);
            InspectorPair(g, "Fighting strength", unit.Strength.ToString("0"), 514);
            if (animal.Domestic)
            {
                Band owner = game.Bands.FirstOrDefault(b => b.Id == animal.OwnerId);
                string belonging = owner == null || owner.Population <= 0 ? "An unaccompanied lineage" : game.CanControlBand(owner.Id) || UnitVisible(owner.CellId) ? "Companions of " + owner.Name : "Companions of another people";
                Typography.Line(g, belonging, new RectangleF(1295, 553, 267, 28), 16, Art.Gold, TypeRole.Annotation, true);
                Typography.Draw(g, DomesticLineageBenefit(animal), new RectangleF(1295, 592, 267, 53), 17, Art.Ink, TypeRole.Body);
                bool harvest = game.LivestockEnabled && LivestockEconomy.IsLivestock(animal) && game.CanControlBand(animal.OwnerId);
                Typography.Draw(g, harvest ? LivestockHarvestSummary(animal) : AnimalUnitDescription(animal, unit), new RectangleF(1295, 655, 267, 75), 16, Art.Muted, TypeRole.Annotation);
                if (harvest) DrawLivestockHarvestButton(g, animal, new RectangleF(1295, 739, 267, 37));
                else if (game.CanControlBand(animal.OwnerId)) Button(g, "Household resources", 1295, 739, 267, 37, delegate { ArmMapCommandBand(animal.OwnerId); OpenEconomy(2); }, false, false);
                else
                {
                    EncounterOutlook outlook = EncounterRules.Outlook(game, CurrentOrderBand.Id, UnitKind.Animal, animal.Id);
                    EncounterAction(g, "Inspect attack  [A]", new RectangleF(1295, 739, 267, 37), outlook.CanAttack, delegate { OpenEncounterChoice(UnitKind.Animal, animal.Id); }, false);
                }
            }
            else
            {
                bool tamable = LivestockEconomy.CanDomesticate(game, animal);
                InspectorPair(g, tamable ? "Trust" : "Domestication", tamable ? animal.PositiveContacts + " / " + unit.TrustThreshold : "Not possible", 552);
                InspectorPair(g, tamable ? "Peaceful chance" : "Options", tamable ? (unit.FriendChance * 100).ToString("0.#") + "%" : "Hunt or leave", 582);
                Typography.Draw(g, unit.Description, new RectangleF(1295, 624, 267, 65), 16, Art.Muted, TypeRole.Annotation);
                EncounterOutlook outlook = EncounterRules.Outlook(game, CurrentOrderBand.Id, UnitKind.Animal, animal.Id);
                EncounterAction(g, "Attack  [A]", new RectangleF(1295, 706, 128, 38), outlook.CanAttack, delegate { OpenEncounterChoice(UnitKind.Animal, animal.Id); }, false);
                EncounterAction(g, "Befriend  [B]", new RectangleF(1433, 706, 129, 38), outlook.CanBefriend, delegate { OpenEncounterChoice(UnitKind.Animal, animal.Id); }, false);
                string note = outlook.CanBefriend ? (outlook.RequiresMove ? "Move + encounter: " + outlook.ActionCost + (outlook.ActionCost == 1 ? " action" : " actions") : "Offering: " + unit.OfferingCost.ToString("0") + " food; injuries possible") : Timeline.DisplayText(game, outlook.BefriendReason);
                Typography.Line(g, note, new RectangleF(1295, 751, 267, 31), 14, Art.Gold, TypeRole.Annotation, true);
            }
        }
    }
}
