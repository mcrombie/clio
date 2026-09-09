using System;
using System.IO;
using System.Windows.Forms;

namespace Clio.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (GameForm form = new GameForm(args.Length == 0 ? AdviserPreferences.DefaultPath : null))
                {
                    if (args.Length == 2 && args[0] == "--render") { form.Render(Path.GetFullPath(args[1])); return 0; }
                    if (args.Length == 2 && args[0] == "--smoke") { form.Smoke(Path.GetFullPath(args[1])); return 0; }
                    form.PrepareForPlay(); Application.Run(form); return 0;
                }
            }
            catch (Exception ex)
            {
                if (args.Length > 0) { Console.Error.WriteLine(ex); return 1; }
                MessageBox.Show(ex.Message, "Clio could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
            }
        }
    }
}
