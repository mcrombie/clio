namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // A recorded rule change keeps legacy command replays unchanged. This is
        // also used at turn one, so each saved story carries its wood starting point.
        private void EnableWoodForStory()
        {
            if (game.WoodEnabled || game.IsOver || commands.Count >= MaximumCommands) return;
            JournalSnapshot before = StoryJournal.Capture(game);
            string result = Execute(game, "enable-wood");
            commands.Add("enable-wood");
            journal.Record(game, before, "enable-wood", result);
        }
    }
}
