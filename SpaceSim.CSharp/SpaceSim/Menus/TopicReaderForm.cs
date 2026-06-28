using System.Drawing;
using System.Windows.Forms;

namespace SpaceSim.Menus;

/// <summary>
/// A read-only, screen-reader-accessible window that shows a single help topic's full text in a multiline
/// TextBox the player can review line by line (and character by character) with their screen reader's review
/// cursor — richer than a one-shot spoken read. Modelled on Aircraft Explorer's TopicReaderForm. Opened with
/// R from the help guide; Escape, or closing the window, returns to the guide.
/// </summary>
public sealed class TopicReaderForm : Form
{
    public TopicReaderForm(string title, string content)
    {
        Text = title;
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        KeyPreview = true;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowIcon = false;
        ShowInTaskbar = false;

        var textBox = new TextBox
        {
            Text = content,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f),
            WordWrap = true,
            AccessibleName = title,
            AccessibleRole = AccessibleRole.Text,
            TabStop = true
        };

        Controls.Add(textBox);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        Shown += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectionStart = 0;
            textBox.SelectionLength = 0;
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (Control c in Controls)
                c.Dispose();
        base.Dispose(disposing);
    }
}
