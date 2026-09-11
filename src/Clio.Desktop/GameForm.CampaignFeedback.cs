using System;
using System.Drawing;
using System.Windows.Forms;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private readonly Timer campaignFeedbackTimer = new Timer { Interval = 5500 };
        private string campaignLastStatus, campaignFeedbackText = "";
        private bool campaignFeedbackVisible;
        private static readonly RectangleF CampaignFeedbackBounds = new RectangleF(428, 901, 844, 40);

        private bool CampaignFeedbackContains(PointF point)
        { return campaignFeedbackVisible && !autoplay && !BlockingSheet && CampaignFeedbackBounds.Contains(point); }

        private void DrawCampaignFeedback(Graphics g)
        {
            if (campaignLastStatus != status)
            {
                bool first = campaignLastStatus == null;
                campaignLastStatus = status;
                campaignFeedbackText = Timeline.DisplayText(game, status ?? "");
                campaignFeedbackVisible = !first && !autoplay && campaignFeedbackText.Length > 0;
                campaignFeedbackTimer.Stop();
                if (campaignFeedbackVisible) campaignFeedbackTimer.Start();
            }
            if (!campaignFeedbackVisible || autoplay || BlockingSheet) return;
            RectangleF box = CampaignFeedbackBounds;
            if (Art.PaperMode) MapPaper.Surface(g, box, false);
            else Art.Fill(g, Color.FromArgb(210, 18, 29, 32), box.X, box.Y, box.Width, box.Height);
            Typography.Line(g, campaignFeedbackText, new RectangleF(box.X + 13, box.Y + 5, box.Width - 26, box.Height - 10), 16, Art.Ink, TypeRole.Body, true);
            buttons.Add(new UiButton(box, delegate { campaignFeedbackVisible = false; campaignFeedbackTimer.Stop(); buttons.Clear(); Invalidate(); })
            { Tip = campaignFeedbackText + "\nClick to dismiss. Recorded outcomes remain in History." });
        }
    }
}
