using System;
using System.Drawing;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed class NewStoryForm : Form
    {
        internal const string ZholPeople = "Zholhen";
        private readonly ComboBox culture = new ComboBox(), style = new ComboBox(), ancestry = new ComboBox();
        private readonly TextBox name = new TextBox();
        private readonly NumericUpDown seed = new NumericUpDown();
        private readonly CheckBox four = new CheckBox(), guided = new CheckBox();
        private readonly Label description = new Label(), vocabulary = new Label(), nameHint = new Label(), paletteLabel = new Label(), turnDescription = new Label();
        private readonly ToolTip hints = new ToolTip();
        public int WorldSeed { get { return (int)seed.Value; } }
        public LanguageStyle SoundStyle { get { return (LanguageStyle)style.SelectedIndex; } }
        public Ancestry FoundingAncestry { get { return (Ancestry)ancestry.SelectedIndex; } }
        public CultureTemplateId Culture { get { return (CultureTemplateId)culture.SelectedIndex; } }
        public HistoryPace Pace { get { return HistoryPace.Abstract; } }
        // Explicitly record the current suggestion so released blank-name saves
        // can keep their original founding name under the old replay rules.
        public string BandName { get { return Culture == CultureTemplateId.Zhol && String.IsNullOrWhiteSpace(name.Text) ? ZholPeople : name.Text; } }
        public bool GuidedOpening { get { return guided.Checked; } }
        public bool FourBands { get { return !guided.Checked && four.Checked; } }

        public NewStoryForm(int worldSeed, LanguageStyle soundStyle, Ancestry foundingAncestry, bool fourBands, CultureTemplateId foundingCulture)
            : this(worldSeed, soundStyle, foundingAncestry, fourBands, foundingCulture, HistoryPace.Abstract) { }

        public NewStoryForm(int worldSeed, LanguageStyle soundStyle, Ancestry foundingAncestry, bool fourBands, CultureTemplateId foundingCulture, HistoryPace historicalPace)
        {
            Text = "Begin a new Clio story"; ClientSize = new Size(580, 714);
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = Art.PanelColor; ForeColor = Art.Ink; Font = Typography.Font(TypeRole.Body, 14);
            DoubleBuffered = true;
            AddLabel("Founding culture", 28, 96, 520);
            culture.SetBounds(28, 122, 524, 29); culture.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (CultureTemplateId value in Enum.GetValues(typeof(CultureTemplateId)))
                culture.Items.Add(value == CultureTemplateId.Zhol ? "Zhol  ·  custom Mandarin–Spanish fusion" :
                    HistoricalCultures.DisplayName(value) + (value == CultureTemplateId.Generated ? "" : "  ·  experimental"));
            description.SetBounds(28, 165, 524, 43); description.ForeColor = Art.Muted;
            vocabulary.SetBounds(28, 215, 524, 32); vocabulary.Font = Typography.Font(TypeRole.Annotation, 18); vocabulary.ForeColor = Art.Gold;
            AddLabel("Band name", 28, 257, 520);
            name.SetBounds(28, 281, 524, 29); name.MaxLength = 40;
            nameHint.SetBounds(28, 315, 524, 25); nameHint.Font = Typography.Font(TypeRole.Annotation, 15); nameHint.ForeColor = Art.Muted;
            AddLabel("World seed", 28, 349, 244);
            paletteLabel.Text = "Generated sound palette"; paletteLabel.SetBounds(304, 349, 248, 24); paletteLabel.ForeColor = Art.Gold;
            seed.SetBounds(28, 373, 248, 29); seed.Minimum = Int32.MinValue; seed.Maximum = Int32.MaxValue; seed.Value = worldSeed;
            style.SetBounds(304, 373, 248, 29); style.DropDownStyle = ComboBoxStyle.DropDownList;
            style.Items.AddRange(Enum.GetNames(typeof(LanguageStyle))); style.SelectedIndex = (int)soundStyle;
            AddLabel("Founding ancestry", 28, 416, 524);
            ancestry.SetBounds(28, 440, 524, 29); ancestry.DropDownStyle = ComboBoxStyle.DropDownList;
            ancestry.Items.AddRange(Enum.GetNames(typeof(Ancestry))); ancestry.SelectedIndex = (int)foundingAncestry;
            guided.SetBounds(28, 482, 524, 30); guided.Text = "Guided beginning · one influence per turn"; guided.Checked = true;
            four.SetBounds(28, 514, 524, 30); four.Text = "Four founding bands, each with its own language"; four.Checked = fourBands;
            turnDescription.SetBounds(28, 551, 524, 40); turnDescription.ForeColor = Art.Ink;
            Label note = new Label { Text = "Begin with fifty people in a generated world. Your chosen culture shapes their first names and language; their history is open.", Left = 28, Top = 595, Width = 524, Height = 44, ForeColor = Art.Muted };
            Button begin = new Button { Text = "Begin story", Left = 370, Top = 645, Width = 182, Height = 43, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Left = 256, Top = 645, Width = 100, Height = 43, DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { culture, description, vocabulary, name, nameHint, paletteLabel, seed, style, ancestry, guided, four, turnDescription, note, begin, cancel });
            foreach (Control control in Controls)
            {
                if (control is TextBox || control is ComboBox || control is NumericUpDown)
                { control.BackColor = Art.Background; control.ForeColor = Art.Ink; }
                Button button = control as Button;
                if (button != null)
                {
                    button.Font = Typography.Font(TypeRole.Action, 14); button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Art.Border; button.BackColor = Art.Background;
                }
                ComboBox combo = control as ComboBox;
                if (combo != null)
                {
                    combo.FlatStyle = FlatStyle.Flat; combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = 23;
                    combo.DrawItem += DrawChoice;
                }
            }
            begin.BackColor = Art.Gold; begin.ForeColor = Art.Background;
            AcceptButton = begin; CancelButton = cancel;
            hints.SetToolTip(style, "For generated languages. With Zhol or a historical first culture, this palette applies to the other founding bands.");
            culture.SelectedIndexChanged += delegate { UpdatePreview(); };
            style.SelectedIndexChanged += delegate { UpdatePreview(); };
            seed.ValueChanged += delegate { UpdatePreview(); };
            four.CheckedChanged += delegate { UpdatePreview(); };
            guided.CheckedChanged += delegate { UpdatePreview(); };
            culture.SelectedIndex = (int)foundingCulture;
            // Keyboard traversal follows the page rather than the order of decorative labels.
            Control[] order = { culture, name, seed, style, ancestry, guided, four, begin, cancel };
            for (int i = 0; i < order.Length; i++) order[i].TabIndex = i;
        }
        private void AddLabel(string text, int x, int y, int width)
        { Controls.Add(new Label { Text = text, Left = x, Top = y, Width = width, Height = 24, ForeColor = Art.Gold }); }
        private void DrawChoice(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            int index = e.Index >= 0 ? e.Index : combo.SelectedIndex;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Art.Mix(Art.Background, Art.Gold, 0.17) : Art.Background;
            using (Brush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (index >= 0 && index < combo.Items.Count)
            {
                Rectangle bounds = new Rectangle(e.Bounds.X + 5, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, combo.Items[index].ToString(), combo.Font, bounds,
                    combo.Enabled ? Art.Ink : Art.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
        private void UpdatePreview()
        {
            four.Enabled = !GuidedOpening;
            turnDescription.Text = GuidedOpening
                ? "Rest on turn 1. The First Adviser introduces Gather and Move on turn 2, with one influence per turn."
                : "Two priorities per band, each turn. Daughters remain in your tribe. Food and salt sustain each household.";
            if (culture.SelectedIndex < 0 || style.SelectedIndex < 0) return;
            LanguageProfile language = HistoricalCultures.Create(Culture, WorldSeed, 0, SoundStyle);
            description.Text = HistoricalCultures.Description(Culture);
            vocabulary.Text = "water · " + language.Words["water"] + "     fire · " + language.Words["fire"] + "     people · " + language.Words["people"];
            string suggested = Culture == CultureTemplateId.Zhol ? ZholPeople : Culture == CultureTemplateId.Generated ? LanguageGenerator.PlaceName(language, "people", 0) : HistoricalCultures.DefaultBandName(Culture);
            nameHint.Text = "Leave blank for " + suggested + ".";
            style.Enabled = Culture == CultureTemplateId.Generated || FourBands;
            paletteLabel.Text = Culture == CultureTemplateId.Generated ? "Generated sound palette" : "Other bands' sound palette";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Typography.Line(e.Graphics, "Choose the first hearths", new RectangleF(26, 16, 528, 45), 34, Art.Ink, TypeRole.Display);
            Typography.Line(e.Graphics, "An imagined world. A culture to carry into it.", new RectangleF(28, 62, 524, 27), 18, Art.Muted, TypeRole.Annotation);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) hints.Dispose(); base.Dispose(disposing); }
    }
}
