using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LanDrop;

/// <summary>
/// The first-run window: asks for the code and whether this PC sends or receives, and writes
/// them to the settings file. <see cref="Form.DialogResult"/> is OK once they are saved.
/// </summary>
sealed class SetupForm : Form
{
    readonly string path;
    readonly TextBox code = new() { Width = 160 };
    readonly RadioButton receives = new() { Text = "Цей ПК отримує від ПК:", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly TextBox host = new() { Width = 160, Enabled = false, Anchor = AnchorStyles.Left };

    public SetupForm(string path)
    {
        this.path = path;
        Text = "LAN Drop";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var label = new Label { Text = "Код (однаковий на всіх ПК):", AutoSize = true };
        var sends = new RadioButton { Text = "Цей ПК надсилає", AutoSize = true, Checked = true, Margin = new Padding(3, 12, 3, 3) };
        var save = new Button { Text = "Зберегти", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 12, 3, 3) };
        receives.CheckedChanged += (_, _) => host.Enabled = receives.Checked;
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
            MessageBox.Show(this, "Введіть код.", "LAN Drop");
            return;
        }
        if (receives.Checked && h.Length == 0)
        {
            MessageBox.Show(this, "Введіть назву ПК, що надсилає.", "LAN Drop");
            return;
        }
        try
        {
            File.WriteAllLines(path, receives.Checked ? new[] { "code=" + c, "connect=" + h } : new[] { "code=" + c });
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Не вдалося зберегти налаштування поруч із програмою. Скопіюйте папку з LAN Drop на цей ПК і запустіть ще раз.", "LAN Drop");
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
