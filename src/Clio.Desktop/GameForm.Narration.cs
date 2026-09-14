using System;
using System.Drawing;
using System.IO;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private FirstAdviserVoice firstAdviserVoice;
        private const string FirstEveningSpeech = "Let morning find us ready. Fifty people have placed their trust in you. Tonight, that is enough. The first matters of the tribe begin in the morning, after rest. Bed early; rise with the light. A rested chief is more likely to meet the day's troubles with a clear head. Even wisdom benefits from a full night's sleep.";
        private const string FirstInfluenceSpeech = "Your word carries weight. You are chief of the tribe. That gives you one influence each turn: the power to direct our common effort. Unspent influence carries forward. For now, choose between two uses. Each costs one influence. We need not make government any more complicated before breakfast.";

        private FirstAdviserVoice AdviserVoice
        {
            get
            {
                if (firstAdviserVoice == null)
                {
                    string preference = String.IsNullOrEmpty(adviserPreferencePath) ? null :
                        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(adviserPreferencePath)), "voice-muted.txt");
                    firstAdviserVoice = new FirstAdviserVoice(preference);
                }
                return firstAdviserVoice;
            }
        }

        private void StopFirstAdviserVoice()
        { if (firstAdviserVoice != null) firstAdviserVoice.Stop(); }

        private void SpeakGuidedAdviser()
        {
            if (!Visible || !IsHandleCreated || !GuidedGame || !guidedReport || guidedTravel || guidedMenu || guidedSupplies) return;
            if (AdviserVoice.Muted) return;
            if (game.IsOver) AdviserVoice.Speak("The hearth has gone cold. Your people's journey ends on turn " + game.Turn + ". The tribe could no longer sustain itself. Its travels and choices remain in this story. A new beginning can take a different path.");
            else if (game.Turn == 1) AdviserVoice.Speak(FirstEveningSpeech, "opening");
            else if (game.Turn == 2) AdviserVoice.Speak(FirstInfluenceSpeech, "influence");
            else
            {
                string title; string body = GuidedSituationAdvice(out title);
                AdviserVoice.Speak(title + ". " + body);
            }
        }

        private void ToggleFirstAdviserVoice()
        {
            AdviserVoice.SetMuted(!AdviserVoice.Muted);
            if (!AdviserVoice.Muted) SpeakGuidedAdviser();
            Invalidate();
        }

        private void DrawFirstAdviserVoiceControl(Graphics g)
        {
            string problem = AdviserVoice.Problem;
            string tip = AdviserVoice.Muted ? "Voice is muted. Click to hear the current adviser report. [V]" :
                "Mute the First Adviser immediately. This preference is remembered. [V]";
            if (!String.IsNullOrEmpty(problem)) tip += " " + problem;
            GuidedButton(g, AdviserVoice.Muted ? "Voice muted" : "Voice on", new RectangleF(1044, 22, 174, 46),
                ToggleFirstAdviserVoice, tip);
        }
    }
}
