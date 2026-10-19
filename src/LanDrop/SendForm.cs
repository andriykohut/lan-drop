using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static LanDrop.Lang;

namespace LanDrop;

/// <summary>
/// The sending PC's window: one box for a link or a file and a button that sends it, a
/// button that sets every PC's clock to this one's, and the list of connected PCs.
/// </summary>
sealed class SendForm : Form
{
    readonly Sender sender;
    readonly TextBox what = new() { Width = 360, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    readonly Button send = new() { Text = T("Надіслати", "Send"), AutoSize = true, Anchor = AnchorStyles.Right };
    readonly Button clock = new() { Text = T("Синхронізувати час", "Sync time"), AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Label status = new() { AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
    readonly ListBox pcs = new() { Height = 120, Anchor = AnchorStyles.Left | AnchorStyles.Right, AccessibleName = T("Підключені ПК", "Connected PCs") };
    Thread sending;
    volatile string note = "";

    public SendForm(Sender sender)
    {
        this.sender = sender;
        Text = "LAN Drop";
        Icon = Program.AppIcon;
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var browse = new Button { Text = "…", AutoSize = true };
        browse.Click += (_, _) => Browse();
        send.Click += (_, _) => Send();
        clock.Click += (_, _) => SyncTime();
        AcceptButton = send;

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        grid.Controls.Add(new Label { Text = T("Посилання або файл:", "Link or file:"), AutoSize = true }, 0, 0);
        grid.Controls.Add(what, 0, 1);
        grid.Controls.Add(browse, 1, 1);
        grid.Controls.Add(clock, 0, 2);
        grid.Controls.Add(send, 1, 2);
        grid.Controls.Add(status, 0, 3);
        grid.SetColumnSpan(status, 2);
        grid.Controls.Add(pcs, 0, 4);
        grid.SetColumnSpan(pcs, 2);
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
        if (!Wire.IsWebLink(text) && !CanRead(text))
        {
            note = T("Вставте посилання (http… або https…) або виберіть файл", "Paste a link (http… or https…) or choose a file");
            Tick();
            return;
        }
        Start(() =>
        {
            var reached = sender.Send(text, out var total);
            return T($"Надіслано на {reached} з {total} ПК", $"Sent to {reached} of {total} PCs");
        });
    }

    void SyncTime() => Start(() =>
    {
        var reached = sender.SendTime(out var total);
        return T($"Час надіслано на {reached} з {total} ПК", $"Time sent to {reached} of {total} PCs");
    });

    // Runs the send on a background thread; its result becomes the note under the PC count.
    void Start(Func<string> run)
    {
        if (sender.Count == 0) note = T("Немає підключених ПК", "No PCs connected");
        else
        {
            send.Enabled = clock.Enabled = false;
            note = T("Надсилання…", "Sending…");
            sending = new Thread(() => note = run()) { IsBackground = true };
            sending.Start();
        }
        Tick();
    }

    void Tick()
    {
        if (sending != null && !sending.IsAlive)
        {
            sending = null;
            send.Enabled = clock.Enabled = true;
        }
        var names = sender.Names;
        status.Text = T("Підключено ПК: ", "PCs connected: ") + names.Length + (note.Length > 0 ? Environment.NewLine + note : "");
        // Refilled only on a change, so the list keeps its scroll position between ticks.
        if (!names.SequenceEqual(pcs.Items.Cast<string>()))
        {
            pcs.Items.Clear();
            pcs.Items.AddRange(names);
        }
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
