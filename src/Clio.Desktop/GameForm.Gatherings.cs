using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool gatheringChoice;
        private int gatheringSelectedId = -1, gatheringSiteId = -1, gatheringArchivePage, gatheringSitePage;
        private int gatheringChoiceToken, gatheringChoiceHost, gatheringChoiceGuest, gatheringChoiceSite, gatheringChoiceId, gatheringChoiceTurn;
        private string gatheringChoiceKind;
        private double gatheringChoiceFood, gatheringChoiceSalt;
        private double gatheringInvitationCost;
        private bool gatheringInvitationAcceptance;

        private void ResetGatheringUi()
        { gatheringChoice = false; gatheringChoiceKind = null; gatheringSelectedId = gatheringSiteId = -1; gatheringArchivePage = gatheringSitePage = 0; gatheringChoiceToken++; }

        private void CloseGatheringChoice()
        { gatheringChoice = false; gatheringChoiceKind = null; gatheringChoiceToken++; buttons.Clear(); HideMapHover(); Invalidate(); }

        private void OpenGathering(int tribe)
        {
            if (BlockingSheet) return;
            DiplomacyPolity polity = DiplomacyReport.Evaluate(game).Polities.FirstOrDefault(p => p.TribeId == tribe);
            if (polity == null) return;
            StopAutoplay("The gathering record is open. You are in control."); ClearMapTransient(); page = 5;
            SelectDiplomacyPolity(tribe); diplomacyDetailPage = 1;
            Gathering record = game.Gatherings.Where(r => r.GuestTribeId == tribe).OrderByDescending(r => r.Id).FirstOrDefault();
            gatheringSelectedId = record == null ? -1 : record.Id; buttons.Clear(); Invalidate();
        }

        private void OpenGatheringRecord(int id)
        {
            if (BlockingSheet) return;
            Gathering record = game.Gatherings.FirstOrDefault(r => r.Id == id);
            if (record == null) return;
            StopAutoplay("The recorded gathering is open. You are in control."); ClearMapTransient(); page = 5;
            diplomacySelectedTribe = record.GuestTribeId; diplomacyDetailPage = 1; gatheringSelectedId = id;
            buttons.Clear(); Invalidate();
        }

        private void OpenGatheringReading(Advisory report)
        {
            if (report != null && report.RecommendedAction == AdviserAction.ReviewDiplomacy)
            {
                Band former = game.Bands.FirstOrDefault(b => b.Id == report.ActorBandId && b.Population > 0 && game.Explored.Contains(b.CellId));
                if (former != null) OpenGathering(game.TribeOf(former.Id));
                return;
            }
            int eventId;
            if (report == null || !report.Key.StartsWith("guidance:gathering:", StringComparison.Ordinal) ||
                !Int32.TryParse(report.Key.Substring(19), out eventId)) return;
            GatheringEvent entry = game.GatheringEvents.FirstOrDefault(e => e.Id == eventId);
            if (entry != null) OpenGatheringRecord(entry.GatheringId);
        }

        private void RefreshGatheringGuidance()
        {
            GatheringEvent[] entries = game.GatheringEvents.ToArray();
            if (guidanceGatheringCursor > entries.Length) guidanceGatheringCursor = entries.Length;
            foreach (GatheringEvent entry in entries.Skip(guidanceGatheringCursor))
            {
                if (!entry.VisibleToPlayer || entry.GatheringId < 0) continue;
                // Keep the latest development at this gathering in the short
                // council shelf; every earlier event remains in History.
                string prefix = "guidance:gathering:";
                adviserGuidance.RemoveAll(a => a.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                    entries.Any(e => e.GatheringId == entry.GatheringId && a.Key == prefix + e.Id));
                int priority = entry.Kind == GatheringEventKind.Missed || entry.Kind == GatheringEventKind.Interrupted || entry.Kind == GatheringEventKind.ReturnFulfilled ? 6 :
                    entry.Kind == GatheringEventKind.FirstMeeting || entry.Kind == GatheringEventKind.ReturnAgreed ? 5 : 4;
                AddAdviserGuidance(new Advisory(prefix + entry.Id, entry.Turn, entry.HostBandId, entry.GuestBandId, entry.CellId,
                    entry.Title, entry.Detail, entry.Detail, "Read the gathering's recorded cost, attendance and next commitment. No adviser gives the order for you.",
                    AdviserAction.ReviewGathering, priority, false, entry.GuestName));
            }
            guidanceGatheringCursor = entries.Length;
        }

        private void SelectGatheringPage(int value)
        { if (page != 5 || BlockingSheet) return; diplomacyDetailPage = value; gatheringArchivePage = 0; buttons.Clear(); Invalidate(); }

        private static string GatheringStatusText(Gathering record)
        {
            switch (record.Status)
            {
                case GatheringStatus.Traveling: return "An invitation accepted";
                case GatheringStatus.Meeting: return "Together at the gathering";
                case GatheringStatus.ReturnPlanned: return "A return visit promised";
                case GatheringStatus.Completed: return "The gathering is remembered";
                case GatheringStatus.Fulfilled: return "The return promise was kept";
                case GatheringStatus.Missed: return "The meeting was missed";
                case GatheringStatus.Interrupted: return "The gathering was interrupted";
                default: return "The invitation was declined";
            }
        }

        private string GatheringPlace(int cell)
        { return cell >= 0 && cell < game.World.Cells.Length && game.Explored.Contains(cell) ? game.Place(cell) : "A remembered meeting place"; }

        private void DrawDiplomacyDetail(Graphics g, DiplomacyPolity chosen)
        {
            Art.Panel(g, new RectangleF(544, 336, 991, 435), Panel, true);
            IdentityArt.DrawEmblem(g, chosen.TribeId, new RectangleF(574, 355, 58, 58), false);
            Typography.Label(g, chosen.FromPlayerTribe ? "A branch of our people" : "An independent people", new RectangleF(654, 351, 849, 23), 12, Art.Gold, .7f);
            Typography.Line(g, chosen.Name, new RectangleF(652, 378, 849, 46), 35, Art.Ink, TypeRole.Heading, true);
            string[] tabs = { "Relationship", "Gathering", "Record" };
            for (int i = 0; i < tabs.Length; i++)
            { int index = i; Button(g, tabs[i], 577 + i * 184, 441, 172, 32, delegate { SelectGatheringPage(index); }, diplomacyDetailPage == i, false); }
            Art.Rule(g, 577, 487, 924);
            if (diplomacyDetailPage == 0) DrawGatheringRelationship(g, chosen);
            else if (diplomacyDetailPage == 2) DrawGatheringHistory(g, chosen.TribeId);
            else
            {
                Gathering record = gatheringSelectedId == -2 ? null : game.Gatherings.FirstOrDefault(r => r.Id == gatheringSelectedId && r.GuestTribeId == chosen.TribeId) ??
                    game.Gatherings.Where(r => r.GuestTribeId == chosen.TribeId).OrderByDescending(r => r.Id).FirstOrDefault();
                if (record == null) DrawGatheringInvitation(g, chosen);
                else DrawGatheringProgress(g, record);
            }
        }

        private void DrawGatheringRelationship(Graphics g, DiplomacyPolity chosen)
        {
            Typography.Label(g, "Our relationship", new RectangleF(577, 503, 429, 22), 12, Art.Gold);
            Typography.Line(g, chosen.Relation == DiplomaticRelation.Enemy ? "Enemy / active feud" : "Neutral / no feud", new RectangleF(575, 531, 445, 35), 27, chosen.Relation == DiplomaticRelation.Enemy ? BandPanelWarning : Art.Ink, TypeRole.Heading, true);
            Typography.Label(g, "Observed presence", new RectangleF(1101, 503, 398, 22), 12, Art.Gold);
            Typography.Line(g, DiplomacyNeighbor(chosen) ? "Neighbor" : "Further afield", new RectangleF(1098, 531, 401, 35), 27, Art.Ink, TypeRole.Heading, true);
            string origin = chosen.FromPlayerTribe ? "Independent since Turn " + chosen.SecessionTurn + ". Shared ancestry does not give you their orders or supplies." : "These people have their own households and history.";
            Typography.Draw(g, origin, new RectangleF(577, 579, 921, 53), 20, Art.Ink, TypeRole.Body);
            Typography.Line(g, chosen.KnownPopulation.ToString("N0") + " people in " + chosen.KnownBandCount + (chosen.KnownBandCount == 1 ? " observed band." : " observed bands."), new RectangleF(577, 638, 921, 29), 18, Art.Muted, TypeRole.Annotation);
            int tribe = chosen.TribeId;
            Button(g, "Find their band", 577, 707, 230, 35, delegate { InspectDiplomacyPolity(tribe); }, false, false);
            MapTip("Inspect a currently observed band. Your selected household keeps command; no movement is ordered.");
            Button(g, "Arrange a gathering", 824, 707, 265, 35, delegate { SelectGatheringPage(1); }, chosen.FromPlayerTribe && chosen.Relation == DiplomaticRelation.Neutral, false);
            MapTip("Read the invitation, its cost and the proposed meeting place before sending it.");
        }

        private void DrawGatheringInvitation(Graphics g, DiplomacyPolity chosen)
        {
            if (!game.GatheringsEnabled)
            {
                Typography.Line(g, "An invitation across the old divide", new RectangleF(576, 505, 923, 38), 28, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, "This recording predates gatherings. You can begin recording invitations and visits while keeping the earlier history intact.", new RectangleF(578, 564, 916, 79), 21, Art.Muted, TypeRole.Body);
                Button(g, "Enable gatherings", 577, 704, 276, 36, delegate { if (page == 5 && !BlockingSheet) Command("enable-gatherings"); }, true, false); return;
            }
            Band host = CurrentOrderBand;
            int guestId = chosen.RepresentativeBandId;
            int site = gatheringSiteId >= 0 ? gatheringSiteId : host.CellId;
            GatheringInvitation offer = game.PreviewGathering(host.Id, guestId, site);
            Typography.Line(g, "Invite them to a shared fire", new RectangleF(576, 503, 923, 37), 28, Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, "Host: " + host.Name + "  /  " + game.ActionsFor(host.Id) + " actions", new RectangleF(578, 545, 918, 27), 18, Art.Muted, TypeRole.Annotation, true);
            Typography.Label(g, "Proposed place", new RectangleF(578, 579, 487, 21), 11.5f, Art.Gold);
            Typography.Line(g, GatheringPlace(site), new RectangleF(576, 606, 597, 31), 24, Art.Ink, TypeRole.Heading, true);
            Button(g, "Choose place", 1214, 604, 282, 32, delegate { BeginGatheringChoice("site", host.Id, guestId, site, -1, 0, 0); }, false, false);
            string terms = offer.CanInvite ? offer.ActionCost + " action + " + GatheringNumber(offer.FoodCost) + " food. " + (offer.WillAccept ? "They will accept; arrive by Turn " + offer.ArrivalDeadline + "." : "They will decline; sending still pays the invitation cost.") : offer.Reason;
            Typography.Draw(g, terms, new RectangleF(578, 650, 918, 45), 18, offer.CanInvite ? Art.Muted : BandPanelWarning, TypeRole.Body);
            GatheringButton(g, "Review invitation", 577, 711, 269, offer.CanInvite, offer.Reason, delegate { BeginGatheringChoice("invite", host.Id, guestId, site, -1, 0, 0); });
            Typography.Line(g, "No transfer, treaty or reunion is implied.", new RectangleF(867, 713, 629, 31), 17, Art.Muted, TypeRole.Annotation, true);
        }

        private void DrawGatheringProgress(Graphics g, Gathering record)
        {
            gatheringSelectedId = record.Id;
            Typography.Line(g, GatheringStatusText(record), new RectangleF(576, 501, 922, 35), 28, Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, "Host: " + record.HostName, new RectangleF(578, 540, 918, 28), 18, Art.Muted, TypeRole.Annotation, true);
            string deadline = record.Status == GatheringStatus.Meeting ? "Meeting open through Turn " + record.WindowEndTurn : record.ReturnDueTurn >= 0 ? "Return: Turn " + record.ReturnDueTurn + "\u2013" + record.WindowEndTurn :
                !record.Active ? "Recorded on Turn " + record.ClosedTurn : "First arrival: by Turn " + record.ArrivalDeadline;
            Typography.Line(g, deadline, new RectangleF(578, 577, 442, 28), 21, Art.Gold, TypeRole.Heading, true);
            Typography.Line(g, GatheringPlace(record.CellId), new RectangleF(1079, 577, 419, 28), 21, Art.Ink, TypeRole.Heading, true);
            string progress = !record.Active ? record.Reason : record.HostAtSite && record.GuestAtSite ? "Both households are at the meeting place. Any aid is a separate, paid choice." :
                (record.HostAtSite ? "Your host is waiting. " : "Your host must travel to the meeting place. ") + (record.GuestAtSite ? "Their household is waiting there." : record.GuestTravelActionsRemaining >= 0 ? "Their known route has " + record.GuestTravelActionsRemaining + " actions remaining." : "Their current route is not observed.");
            Typography.Draw(g, progress, new RectangleF(578, 616, 918, 65), 19, Art.Muted, TypeRole.Body);
            int id = record.Id, cell = record.CellId;
            Button(g, "Inspect place", 577, 700, 164, 34, delegate { InspectGatheringSite(id, cell); }, false, false);
            if (record.Status == GatheringStatus.Meeting)
            {
                Band guest = record.GuestAtSite ? game.Bands.FirstOrDefault(b => b.Id == record.GuestBandId && b.Population > 0 && b.CellId == record.CellId && game.Explored.Contains(b.CellId)) : null;
                double food = guest == null ? 0 : game.Upkeep(guest), salt = guest == null ? 0 : SaltEconomy.Need(guest);
                DrawGatheringAidButton(g, record, "Food aid", 756, food, 0);
                DrawGatheringAidButton(g, record, "Salt aid", 941, 0, salt);
                DrawGatheringAidButton(g, record, "Food & salt", 1126, food, salt);
                GatheringReturnOffer again = game.PreviewGatheringReturn(record.HostBandId, record.Id);
                GatheringButton(g, "Return visit", 1311, 700, 185, again.CanAgree, again.Reason, delegate { BeginGatheringChoice("return", record.HostBandId, record.GuestBandId, record.CellId, id, 0, 0); });
            }
            else if (record.Active)
            {
                Button(g, "Find your host", 758, 700, 205, 34, delegate { InspectGatheringHost(id); }, false, false);
                Typography.Line(g, record.Status == GatheringStatus.ReturnPlanned ? "A real departure is needed before the promised return." : "Meeting begins when both households reach this place.", new RectangleF(982, 700, 514, 34), 17, Art.Muted, TypeRole.Annotation, true);
            }
            else Button(g, "Another invitation", 758, 700, 238, 34, delegate { if (page == 5 && !BlockingSheet) { gatheringSelectedId = -1; gatheringSiteId = -1; DrawNewGatheringInvitation(); } }, false, false);
            Typography.Line(g, record.AidGiven ? "Aid was given from the host's own supplies." : "They remain independent. Reading this record spends nothing.", new RectangleF(578, 740, 919, 24), 15, Art.Muted, TypeRole.Annotation, true);
        }

        private void DrawNewGatheringInvitation()
        { gatheringSelectedId = -2; buttons.Clear(); Invalidate(); }

        private void DrawGatheringAidButton(Graphics g, Gathering record, string label, float x, double food, double salt)
        {
            GatheringAidOffer offer = game.PreviewGatheringAid(record.HostBandId, record.Id, food, salt);
            // A modest preset: at most one guest need, bounded by the host's
            // available reserve. The separate preview shows the exact amount.
            if (!offer.CanGive && (offer.MaxFood > 0 || offer.MaxSalt > 0))
            { food = Math.Min(food, offer.MaxFood); salt = Math.Min(salt, offer.MaxSalt); offer = game.PreviewGatheringAid(record.HostBandId, record.Id, food, salt); }
            GatheringButton(g, label, x, 700, 170, offer.CanGive, offer.Reason, delegate { BeginGatheringChoice("aid", record.HostBandId, record.GuestBandId, record.CellId, record.Id, food, salt); });
        }

        private void DrawGatheringHistory(Graphics g, int tribe)
        {
            Gathering[] records = game.Gatherings.Where(r => r.GuestTribeId == tribe).OrderByDescending(r => r.Id).ToArray();
            if (records.Length == 0)
            { Typography.Draw(g, "No invitations or visits are recorded between these people yet.", new RectangleF(578, 528, 918, 83), 23, Art.Muted, TypeRole.Annotation); return; }
            gatheringArchivePage = Math.Max(0, Math.Min(gatheringArchivePage, (records.Length - 1) / 3));
            for (int i = 0; i < Math.Min(3, records.Length - gatheringArchivePage * 3); i++)
            {
                Gathering record = records[gatheringArchivePage * 3 + i]; RectangleF row = new RectangleF(577, 501 + i * 68, 920, 59);
                Art.Panel(g, row, Color.FromArgb(24, 35, 38), false);
                Typography.Line(g, "Turn " + record.InvitedTurn + " / " + GatheringStatusText(record), new RectangleF(row.X + 13, row.Y + 4, 890, 29), 22, Art.Ink, TypeRole.Heading, true);
                Typography.Line(g, record.Reason, new RectangleF(row.X + 14, row.Y + 33, 888, 22), 16, Art.Muted, TypeRole.Annotation, true);
                int id = record.Id; buttons.Add(new UiButton(row, delegate { OpenGatheringRecord(id); }));
            }
            if (records.Length > 3)
            {
                Button(g, "Previous", 577, 726, 160, 30, delegate { if (!BlockingSheet) { gatheringArchivePage = Math.Max(0, gatheringArchivePage - 1); buttons.Clear(); Invalidate(); } }, false, false);
                Button(g, "Next", 1337, 726, 160, 30, delegate { if (!BlockingSheet) { gatheringArchivePage = Math.Min((records.Length - 1) / 3, gatheringArchivePage + 1); buttons.Clear(); Invalidate(); } }, false, false);
            }
        }

        private bool DrawGatheringArchiveWithoutPolity(Graphics g, bool unlocked)
        {
            if (!unlocked || game.Gatherings.Count == 0) return false;
            Gathering selectedRecord = game.Gatherings.FirstOrDefault(r => r.Id == gatheringSelectedId) ?? game.Gatherings.OrderByDescending(r => r.Id).First();
            Typography.Line(g, "Remembered gatherings", new RectangleF(44, 304, 473, 38), 29, Art.Ink, TypeRole.Heading);
            Typography.Draw(g, "These commitments remain in the record even while the other household is no longer observed.", new RectangleF(46, 371, 456, 102), 22, Art.Muted, TypeRole.Annotation);
            Art.Panel(g, new RectangleF(544, 336, 991, 435), Panel, true);
            Typography.Line(g, selectedRecord.GuestName, new RectangleF(575, 362, 920, 48), 36, Art.Ink, TypeRole.Heading, true);
            Typography.Label(g, "Recorded identity / no current position implied", new RectangleF(578, 427, 918, 25), 12, Art.Gold);
            DrawGatheringProgress(g, selectedRecord); return true;
        }

        private void InspectGatheringSite(int id, int cell)
        {
            if (page != 5 || BlockingSheet || !game.Gatherings.Any(r => r.Id == id && r.CellId == cell) || !game.Explored.Contains(cell)) return;
            StopAutoplay("The agreed meeting place is selected. Give any travel order yourself."); ClearMapTransient(); page = 0; selected = cell; inspectorPage = 0; selectedAnimalId = -1;
            map.Focus(game.World.Cells[cell]); ShowMapSelection(); Invalidate();
        }

        private void InspectGatheringHost(int id)
        {
            if (page != 5 || BlockingSheet) return;
            Gathering record = game.Gatherings.FirstOrDefault(r => r.Id == id);
            if (record == null || !game.CanControlBand(record.HostBandId)) return;
            Band host = game.Bands.First(b => b.Id == record.HostBandId);
            StopAutoplay("Your gathering host is selected. You are in control."); ClearMapTransient(); ArmMapCommandBand(host.Id);
            page = 0; selected = host.CellId; inspectedBandId = host.Id; inspectorPage = 1; selectedAnimalId = -1;
            map.Focus(game.World.Cells[selected]); ShowMapSelection(); Invalidate();
        }

        private void GatheringButton(Graphics g, string label, float x, float y, float width, bool enabled, string reason, Action action)
        {
            if (enabled) Button(g, label, x, y, width, 34, action, true, false);
            else
            {
                RectangleF box = new RectangleF(x, y, width, 34); Art.Panel(g, box, Color.FromArgb(22, 31, 34), false);
                Typography.Line(g, label, new RectangleF(x + 6, y, width - 12, 34), 13.5f, Color.FromArgb(111, 121, 119), TypeRole.Action, true, StringAlignment.Center);
                buttons.Add(new UiButton(box, delegate { status = reason; Invalidate(); }));
            }
            MapTip(enabled ? "Review the exact cost before confirming. Opening the preview spends nothing." : reason);
        }

        private void BeginGatheringChoice(string kind, int host, int guest, int site, int id, double food, double salt)
        {
            if (page != 5 || BlockingSheet || !game.CanControlBand(host)) return;
            if (kind == "invite" && !game.PreviewGathering(host, guest, site).CanInvite || kind == "aid" && !game.PreviewGatheringAid(host, id, food, salt).CanGive ||
                kind == "return" && !game.PreviewGatheringReturn(host, id).CanAgree) { status = "That choice is no longer available. Review the current gathering."; buttons.Clear(); Invalidate(); return; }
            StopAutoplay("Review this gathering choice. Nothing has been paid yet."); ClearMapTransient();
            gatheringChoice = true; gatheringChoiceKind = kind; gatheringChoiceToken++; gatheringChoiceHost = host; gatheringChoiceGuest = guest;
            gatheringChoiceSite = site; gatheringChoiceId = id; gatheringChoiceFood = food; gatheringChoiceSalt = salt; gatheringChoiceTurn = game.Turn; gatheringSitePage = 0;
            if (kind == "invite") { GatheringInvitation quote = game.PreviewGathering(host, guest, site); gatheringInvitationCost = quote.FoodCost; gatheringInvitationAcceptance = quote.WillAccept; }
            buttons.Clear(); Invalidate();
        }

        private void ConfirmGatheringChoice(int token)
        {
            if (!gatheringChoice || token != gatheringChoiceToken || noticeModal || adviserOpen || encounterChoice || endingOpen || game.Turn != gatheringChoiceTurn || !game.CanControlBand(gatheringChoiceHost)) return;
            string order = null;
            if (gatheringChoiceKind == "invite")
            {
                GatheringInvitation quote = game.PreviewGathering(gatheringChoiceHost, gatheringChoiceGuest, gatheringChoiceSite);
                if (quote.CanInvite && quote.FoodCost == gatheringInvitationCost && quote.WillAccept == gatheringInvitationAcceptance)
                    order = "gather-invite:" + gatheringChoiceGuest + ":" + gatheringChoiceSite;
            }
            else if (gatheringChoiceKind == "aid" && game.PreviewGatheringAid(gatheringChoiceHost, gatheringChoiceId, gatheringChoiceFood, gatheringChoiceSalt).CanGive)
                order = "gather-aid:" + gatheringChoiceId + ":" + gatheringChoiceFood.ToString("R", CultureInfo.InvariantCulture) + ":" + gatheringChoiceSalt.ToString("R", CultureInfo.InvariantCulture);
            else if (gatheringChoiceKind == "return" && game.PreviewGatheringReturn(gatheringChoiceHost, gatheringChoiceId).CanAgree) order = "gather-return:" + gatheringChoiceId;
            if (order == null) { status = "The conditions changed. Cancel and review the current offer."; buttons.Clear(); Invalidate(); return; }
            int host = gatheringChoiceHost; CloseGatheringChoice(); Command("band:" + host + ":" + order);
            Gathering record = game.Gatherings.Where(r => r.HostBandId == host).OrderByDescending(r => r.Id).FirstOrDefault();
            if (record != null) { gatheringSelectedId = record.Id; diplomacySelectedTribe = record.GuestTribeId; diplomacyDetailPage = 1; }
            Invalidate();
        }

        private void DrawGatheringChoice(Graphics g)
        {
            if (!gatheringChoice) return;
            using (Brush shade = new SolidBrush(Color.FromArgb(205, 6, 13, 17))) g.FillRectangle(shade, 0, 0, 1600, 960);
            buttons.Clear(); Art.Panel(g, new RectangleF(354, 182, 892, 620), Color.FromArgb(25, 36, 39), true);
            string title = gatheringChoiceKind == "site" ? "A place to meet" : gatheringChoiceKind == "invite" ? "Send an invitation" : gatheringChoiceKind == "aid" ? "Aid from your own stores" : "A promise to return";
            Typography.Label(g, "Between independent peoples", new RectangleF(387, 205, 818, 25), 12, Art.Gold);
            Typography.Line(g, title, new RectangleF(384, 240, 790, 52), 38, Art.Ink, TypeRole.Display, true);
            Button(g, "\u00d7", 1187, 202, 35, 32, CloseGatheringChoice, false, false);
            Art.Rule(g, 386, 313, 827);
            if (gatheringChoiceKind == "site") { DrawGatheringSites(g); return; }
            Band host = game.Bands.FirstOrDefault(b => b.Id == gatheringChoiceHost && game.CanControlBand(b.Id));
            Gathering record = game.Gatherings.FirstOrDefault(r => r.Id == gatheringChoiceId);
            Band observedGuest = game.Bands.FirstOrDefault(b => b.Id == gatheringChoiceGuest && b.Population > 0 && game.Explored.Contains(b.CellId));
            string guestName = record != null ? record.GuestName : observedGuest == null ? "The invited people" : observedGuest.Name;
            string identities = (host == null ? "Host no longer controlled" : host.Name) + "  /  " + guestName;
            RectangleF identitiesBox = new RectangleF(388, 333, 822, 65);
            Typography.Draw(g, identities, identitiesBox, AdviserReadingSize(g, identities, identitiesBox, 23, 18, TypeRole.Heading), Art.Ink, TypeRole.Heading);
            Typography.Line(g, GatheringPlace(gatheringChoiceSite), new RectangleF(388, 407, 822, 28), 20, Art.Gold, TypeRole.Annotation, true);
            bool allowed; string reason, terms, consequence, confirmation;
            if (gatheringChoiceKind == "invite")
            {
                GatheringInvitation offer = game.PreviewGathering(gatheringChoiceHost, gatheringChoiceGuest, gatheringChoiceSite);
                allowed = offer.CanInvite; reason = offer.Reason;
                terms = offer.ActionCost + " action and " + GatheringNumber(offer.FoodCost) + " food from the host.";
                consequence = offer.WillAccept ? "They will accept this invitation. Their known journey takes " + offer.TravelActions + " actions; both households must reach the meeting place by Turn " + offer.ArrivalDeadline + ". You still direct your host's travel." : "They will decline this invitation. Sending it still pays the action and food cost. No visit is promised.";
                confirmation = offer.WillAccept ? "Send invitation" : "Send despite refusal";
            }
            else if (gatheringChoiceKind == "aid")
            {
                GatheringAidOffer offer = game.PreviewGatheringAid(gatheringChoiceHost, gatheringChoiceId, gatheringChoiceFood, gatheringChoiceSalt);
                allowed = offer.CanGive; reason = offer.Reason;
                terms = "1 action. Give " + GatheringNumber(gatheringChoiceFood) + " food and " + GatheringNumber(gatheringChoiceSalt) + " salt.";
                consequence = host == null ? "The original donor is no longer controlled." : "Your household keeps " + GatheringNumber(host.Food - gatheringChoiceFood) + " food and " + GatheringNumber(host.Salt - gatheringChoiceSalt) + " salt. These exact supplies pass to the guest; they are not copied or loaned. Only one gift may be made at this gathering.";
                confirmation = "Give these supplies";
            }
            else
            {
                GatheringReturnOffer offer = game.PreviewGatheringReturn(gatheringChoiceHost, gatheringChoiceId);
                allowed = offer.CanAgree; reason = offer.Reason;
                terms = offer.ActionCost + " action. Return window: Turn " + offer.ReturnDueTurn + "\u2013" + offer.WindowEndTurn + ".";
                consequence = "The guest must actually leave before a return counts. Your host must attend within the agreed window. Missing it is recorded; this promise grants no supplies, allegiance or treaty.";
                confirmation = "Agree to return";
            }
            Typography.Label(g, "The immediate cost", new RectangleF(388, 456, 817, 23), 12, Art.Gold);
            Typography.Draw(g, terms, new RectangleF(388, 491, 819, 55), 25, Art.Ink, TypeRole.Heading);
            Typography.Draw(g, consequence, new RectangleF(389, 560, 815, 105), 22, Art.Muted, TypeRole.Body);
            if (!allowed) Typography.Draw(g, reason, new RectangleF(389, 665, 815, 48), 18, BandPanelWarning, TypeRole.Body);
            int token = gatheringChoiceToken;
            Button(g, "Cancel / Esc", 387, 738, 236, 38, CloseGatheringChoice, false, false);
            GatheringButton(g, confirmation, 833, 738, 374, allowed && game.Turn == gatheringChoiceTurn, reason, delegate { ConfirmGatheringChoice(token); });
        }

        private void DrawGatheringSites(Graphics g)
        {
            int[] sites = game.GatheringSites(gatheringChoiceHost, gatheringChoiceGuest).ToArray();
            Typography.Draw(g, "Only places and routes known to both households are offered. Choosing one spends nothing and orders no travel.", new RectangleF(388, 330, 817, 60), 21, Art.Muted, TypeRole.Annotation);
            gatheringSitePage = Math.Max(0, Math.Min(gatheringSitePage, Math.Max(0, (sites.Length - 1) / 6)));
            if (sites.Length == 0) Typography.Draw(g, "No mutually known reachable meeting place is currently available.", new RectangleF(389, 441, 815, 85), 24, Art.Ink, TypeRole.Heading);
            for (int i = 0; i < Math.Min(6, sites.Length - gatheringSitePage * 6); i++)
            {
                int cell = sites[gatheringSitePage * 6 + i], token = gatheringChoiceToken;
                GatheringInvitation offer = game.PreviewGathering(gatheringChoiceHost, gatheringChoiceGuest, cell);
                RectangleF row = new RectangleF(387, 411 + i * 48, 821, 42);
                Art.Panel(g, row, Color.FromArgb(27, 39, 41), false);
                Typography.Line(g, GatheringPlace(cell), new RectangleF(400, row.Y + 1, 538, 39), 22, Art.Ink, TypeRole.Heading, true);
                Typography.Line(g, offer.TravelActions + " guest travel actions", new RectangleF(951, row.Y + 1, 241, 39), 16, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
                buttons.Add(new UiButton(row, delegate {
                    if (!gatheringChoice || gatheringChoiceToken != token || gatheringChoiceKind != "site" || !game.GatheringSites(gatheringChoiceHost, gatheringChoiceGuest).Contains(cell)) return;
                    gatheringSiteId = cell; CloseGatheringChoice();
                }));
            }
            int captured = gatheringChoiceToken;
            Button(g, "Cancel / Esc", 387, 738, 236, 38, CloseGatheringChoice, false, false);
            if (sites.Length > 6)
            {
                Button(g, "Previous", 802, 738, 151, 35, delegate { if (gatheringChoice && gatheringChoiceToken == captured) { gatheringSitePage = Math.Max(0, gatheringSitePage - 1); buttons.Clear(); Invalidate(); } }, false, false);
                Button(g, "Next", 1062, 738, 145, 35, delegate { if (gatheringChoice && gatheringChoiceToken == captured) { gatheringSitePage = Math.Min((sites.Length - 1) / 6, gatheringSitePage + 1); buttons.Clear(); Invalidate(); } }, false, false);
            }
        }

        private static string GatheringNumber(double amount) { return amount.ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
