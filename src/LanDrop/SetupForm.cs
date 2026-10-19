using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static LanDrop.Lang;

namespace LanDrop;

/// <summary>
/// The settings window, shown on the first run and from the receiving PCs' tray icon: asks for
/// the code and whether this PC sends or receives, starting from the current settings, and
/// writes them to the settings file. <see cref="Form.DialogResult"/> is OK once they are saved.
/// </summary>
sealed class SetupForm : Form
{
    readonly string path;
    readonly TextBox code = new() { Width = 160 };
    readonly RadioButton receives = new() { Text = T("Цей ПК отримує від ПК:", "This PC receives from PC:"), AutoSize = true, Anchor = AnchorStyles.Left };
    readonly TextBox host = new() { Width = 160, Enabled = false, Anchor = AnchorStyles.Left };

    public SetupForm(string path, Dictionary<string, string> settings)
    {
        this.path = path;
        Text = "LAN Drop";
        Icon = Program.AppIcon;
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var label = new Label { Text = T("Код (однаковий на всіх ПК):", "Code (the same on every PC):"), AutoSize = true };
        var sends = new RadioButton { Text = T("Цей ПК надсилає", "This PC sends"), AutoSize = true, Checked = true, Margin = new Padding(3, 12, 3, 3) };
        var save = new Button { Text = T("Зберегти", "Save"), AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 12, 3, 3) };
        receives.CheckedChanged += (_, _) => host.Enabled = receives.Checked;
        if (settings.TryGetValue("code", out var c)) code.Text = c;
        if (settings.TryGetValue("connect", out var h) && h.Length > 0)
        {
            sends.Checked = false;
            receives.Checked = true;
            host.Text = h;
        }
        save.Click += (_, _) => Save();
        AcceptButton = save;

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        grid.Controls.Add(label, 0, 0);
        grid.SetColumnSpan(label, 2);
        grid.Controls.Add(code, 0, 1);
        grid.Controls.Add(sends, 0, 2);
        grid.Controls.Add(receives, 0, 3);
        grid.Controls.Add(host, 1, 3);
        grid.Controls.Add(save, 0, 4);
        grid.SetColumnSpan(save, 2);
        Controls.Add(grid);
    }

    void Save()
    {
        var c = code.Text.Trim();
        var h = host.Text.Trim();
        if (c.Length == 0)
        {
            MessageBox.Show(this, T("Введіть код.", "Type a code."), "LAN Drop");
            return;
        }
        if (receives.Checked && h.Length == 0)
        {
            MessageBox.Show(this, T("Введіть назву ПК, що надсилає.", "Type the name of the PC that sends."), "LAN Drop");
            return;
        }
        try
        {
            File.WriteAllLines(path, receives.Checked ? new[] { "code=" + c, "connect=" + h } : new[] { "code=" + c });
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            MessageBox.Show(this, T("Не вдалося зберегти налаштування поруч із програмою. Скопіюйте папку з LAN Drop на цей ПК і запустіть ще раз.", "Could not save the settings next to the app. Copy the LAN Drop folder onto this PC and run it again."), "LAN Drop");
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
