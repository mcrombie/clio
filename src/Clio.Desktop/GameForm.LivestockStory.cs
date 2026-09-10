namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // Introduce these rules after replay, so old saved actions retain their outcomes.
        private void EnableLivestockForStory()
        {
            if (game.LivestockEnabled || game.IsOver || commands.Count >= MaximumCommands) return;
            JournalSnapshot before = StoryJournal.Capture(game);
            string result = Execute(game, "enable-livestock");
            commands.Add("enable-livestock");
            journal.Record(game, before, "enable-livestock", result);
        }
    }
}
