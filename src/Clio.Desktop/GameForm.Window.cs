using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool fullscreen, changingWindowMode;
        private bool fullscreenAtStartup;
        private Rectangle windowedBounds;
        private FormWindowState windowedState;
        private FormBorderStyle windowedBorder;
        private Size windowedMinimum, windowedMaximum;

        private RectangleF GameViewport
        {
            get
            {
                if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return RectangleF.Empty;
                float scale = Math.Min(ClientSize.Width / 1600f, ClientSize.Height / 960f);
                float width = 1600 * scale, height = 960 * scale;
                return new RectangleF((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
            }
        }

        private static bool IsFullscreenShortcut(Keys keys)
        { return keys == Keys.F11 || keys == (Keys.Alt | Keys.Enter); }

        // Only the interactive entry point requests startup fullscreen. Render
        // and smoke processes keep their offscreen form geometry.
        internal void PrepareForPlay() { fullscreenAtStartup = true; }

        private void ApplyStartupWindowMode()
        {
            if (!fullscreenAtStartup) return;
            fullscreenAtStartup = false; SetFullscreen(true);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (IsFullscreenShortcut(keyData))
            {
                // Bit 30 marks an already-held key: one transition per press.
                if ((message.LParam.ToInt64() & (1L << 30)) == 0) ToggleFullscreen();
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void ToggleFullscreen() { SetFullscreen(!fullscreen); }

        private void CancelWindowGesture()
        {
            ClearMapTransient();
            dragging = false; moved = false; Capture = false; map.IsNavigating = false;
            settleCamera.Stop(); buttons.Clear(); hoverPoint = new PointF(-1, -1);
            Cursor = Cursors.Default;
        }

        private void SetFullscreen(bool enabled)
        {
            if (enabled == fullscreen || changingWindowMode || IsDisposed) return;
            CancelWindowGesture(); changingWindowMode = true;
            SuspendLayout();
            try
            {
                if (enabled)
                {
                    // Resolve the monitor before normalizing a maximized window.
                    Rectangle monitor = Screen.FromControl(this).Bounds;
                    windowedState = WindowState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
                    windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                    windowedBorder = FormBorderStyle; windowedMinimum = MinimumSize; windowedMaximum = MaximumSize;
                    MinimumSize = Size.Empty; MaximumSize = Size.Empty;
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.None;
                    fullscreen = true; Bounds = monitor;
                }
                else
                {
                    fullscreen = false;
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = windowedBorder;
                    Rectangle restore = windowedBounds;
                    // A disconnected monitor must not strand the restored window.
                    if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(restore)))
                    {
                        Rectangle work = Screen.FromControl(this).WorkingArea;
                        restore.Location = new Point(work.X + Math.Max(0, (work.Width - restore.Width) / 2),
                            work.Y + Math.Max(0, (work.Height - restore.Height) / 2));
                    }
                    Bounds = restore; MinimumSize = windowedMinimum; MaximumSize = windowedMaximum;
                    WindowState = windowedState;
                }
            }
            finally { changingWindowMode = false; ResumeLayout(); Invalidate(); }
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            // A resolution/monitor change while playing keeps the borderless
            // window on a complete current screen, using Framework 4 coordinates.
            if (message.Msg == 0x007E && fullscreen && !changingWindowMode && WindowState != FormWindowState.Minimized)
            {
                changingWindowMode = true;
                try { CancelWindowGesture(); WindowState = FormWindowState.Normal; Bounds = Screen.FromControl(this).Bounds; }
                finally { changingWindowMode = false; Invalidate(); }
            }
        }
    }
}
