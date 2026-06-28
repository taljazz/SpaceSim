using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SpaceSim.Menus;

/// <summary>
/// A small, screen-reader-accessible modal dialog (opened with F3) listing the system's audio output
/// devices so the player can choose which one the simulator plays through. Models the dark, keyboard-first
/// style of <see cref="TopicReaderForm"/>: arrow the list, Enter (or OK) to accept, Escape (or Cancel) to
/// dismiss. The chosen device is exposed via <see cref="SelectedDeviceNumber"/> / <see cref="SelectedDeviceName"/>.
/// </summary>
public sealed class AudioDeviceForm : Form
{
    private readonly ListBox _list;
    private readonly List<(string? Id, string Name)> _devices;

    /// <summary>The chosen device's WASAPI endpoint ID (null = system default). Valid only when DialogResult is OK.</summary>
    public string? SelectedDeviceId { get; private set; }

    /// <summary>The chosen device's display name. Valid only when DialogResult is OK.</summary>
    public string SelectedDeviceName { get; private set; } = "System default";

    public AudioDeviceForm(List<(string? Id, string Name)> devices, string? currentId)
    {
        _devices = devices;

        Text = "Audio Output Device";
        Size = new Size(520, 420);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        KeyPreview = true;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;

        var label = new Label
        {
            Text = "Choose the sound device to play through:",
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f),
            Padding = new Padding(8, 10, 8, 0),
            AccessibleName = "Choose the sound device to play through",
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f),
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Audio output devices",
            TabStop = true,
        };
        foreach (var d in devices) _list.Items.Add(d.Name);
        int idx = devices.FindIndex(d => d.Id == currentId);
        _list.SelectedIndex = idx >= 0 ? idx : 0;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100, ForeColor = Color.Black };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100, ForeColor = Color.Black };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        // Add the fill control last so it occupies the area between the top label and the bottom buttons.
        Controls.Add(buttons);
        Controls.Add(label);
        Controls.Add(_list);

        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) => CaptureSelection();
        _list.DoubleClick += (_, _) => { CaptureSelection(); DialogResult = DialogResult.OK; Close(); };

        Shown += (_, _) => _list.Focus();
    }

    private void CaptureSelection()
    {
        int i = _list.SelectedIndex;
        if (i >= 0 && i < _devices.Count)
        {
            SelectedDeviceId = _devices[i].Id;
            SelectedDeviceName = _devices[i].Name;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (Control c in Controls)
                c.Dispose();
        base.Dispose(disposing);
    }
}
