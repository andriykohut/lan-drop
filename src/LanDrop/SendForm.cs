using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using static LanDrop.Lang;

namespace LanDrop;

/// <summary>The sending PC's window: one box for a link or a file, and a button that sends it.</summary>
sealed class SendForm : Form
{
    readonly Sender sender;
    readonly TextBox what = new() { Width = 360, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    readonly Button send = new() { Text = T("Надіслати", "Send"), AutoSize = true, Anchor = AnchorStyles.Right };
    readonly Label status = new() { AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
    Thread sending;
    volatile string note = "";

    public SendForm(Sender sender)
    {
        this.sender = sender;
        Text = "LAN Drop";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var browse = new Button { Text = "…", AutoSize = true };
        browse.Click += (_, _) => Browse();
        send.Click += (_, _) => Send();
        AcceptButton = send;

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        grid.Controls.Add(new Label { Text = T("Посилання або файл:", "Link or file:"), AutoSize = true }, 0, 0);
        grid.Controls.Add(what, 0, 1);
        grid.Controls.Add(browse, 1, 1);
        grid.Controls.Add(send, 0, 2);
        grid.SetColumnSpan(send, 2);
        grid.Controls.Add(status, 0, 3);
        grid.SetColumnSpan(status, 2);
        Controls.Add(grid);

        var timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        timer.Tick += (_, _) => Tick();
        Tick();
    }

    void Browse()
    {
        using var dialog = new OpenFileDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK) what.Text = dialog.FileName;
    }

    void Send()
    {
        var text = what.Text.Trim().Trim('"');
        if (!Wire.IsWebLink(text) && !CanRead(text)) note = T("Вставте посилання (http… або https…) або виберіть файл", "Paste a link (http… or https…) or choose a file");
        else if (sender.Count == 0) note = T("Немає підключених ПК", "No PCs connected");
        else
        {
            send.Enabled = false;
            note = T("Надсилання…", "Sending…");
            sending = new Thread(() =>
            {
                var reached = sender.Send(text, out var total);
                note = T($"Надіслано на {reached} з {total} ПК", $"Sent to {reached} of {total} PCs");
            }) { IsBackground = true };
            sending.Start();
        }
        Tick();
    }

    void Tick()
    {
        if (sending != null && !sending.IsAlive)
        {
            sending = null;
            send.Enabled = true;
        }
        status.Text = T("Підключено ПК: ", "PCs connected: ") + sender.Count + (note.Length > 0 ? Environment.NewLine + note : "");
    }

    static bool CanRead(string path)
    {
        try
        {
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
