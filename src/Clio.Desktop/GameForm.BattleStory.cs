using System;
using System.Collections.Generic;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void EnableBattlesForStory()
        {
            if (game.TacticalBattlesEnabled || game.IsOver || commands.Count >= MaximumCommands) return;
            JournalSnapshot before = StoryJournal.Capture(game);
            string result = Execute(game, "enable-tactical-battles");
            commands.Add("enable-tactical-battles");
            journal.Record(game, before, "enable-tactical-battles", result);
        }

        // Battle orders are global to the active engagement, not ordinary band
        // actions. Every tactical choice still belongs to the saved command log.
        private void BattleCommand(string command)
        { RunBattleCommand(command, false); }

        private void RunBattleCommand(string command, bool automatic)
        {
            if (game.Battle == null || !command.StartsWith("battle-", StringComparison.Ordinal)) return;
            if (commands.Count >= MaximumCommands)
            { StopAutoplay("The story record is full. Save this story before continuing."); return; }
            if (!automatic) StopAutoplay(null);
            HideMapHover();
            JournalSnapshot before = StoryJournal.Capture(game);
            status = Execute(game, command); commands.Add(command); chronicleOffset = 0;
            SynchronizeUnitSelection();
            PresentBattleOrNotices(journal.Record(game, before, command, status), automatic);
            if (game.Battle == null && game.IsOver) StopAutoplay("Your people's story has ended.");
            buttons.Clear(); Invalidate();
        }

        private void AutoplayBattleStep()
        {
            if (game.Battle == null) return;
            try { RunBattleCommand(game.Battle.Phase == BattlePhase.Finished ? "battle-close" : "battle-auto", true); }
            catch (Exception error) { StopAutoplay("Battle paused: " + error.Message); }
        }

        private void PresentBattleOrNotices(List<StoryNotice> notices, bool automatic)
        {
            if (game.Battle != null)
            {
                // The battle owns the screen. Readings remain in History rather
                // than opening a second modal over deployment or the result.
                activeNotice = null; noticeModal = false; noticeTimer.Stop();
                encounterChoice = false; adviserOpen = false;
                ClearMapTransient(); buttons.Clear(); dragging = false; Capture = false;
                map.IsNavigating = false; settleCamera.Stop();
                if (!automatic) StopAutoplay(null);
            }
            else
            {
                PresentNotices(notices, automatic);
                ReportEndingIfNeeded(); RefreshAdvisers();
            }
            Invalidate();
        }
    }
}
