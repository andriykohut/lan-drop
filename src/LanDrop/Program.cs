using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using static LanDrop.Lang;

namespace LanDrop;

static class Program
{
    public static readonly Icon AppIcon = new(typeof(Program).Assembly.GetManifestResourceStream("LanDrop.LanDrop.ico"));

    // lan-drop.ini beside the exe, written by the settings window: "code=" on every PC, and
    // "connect=" naming the sending PC on the others. Without "connect=" this PC sends.
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lan-drop.ini");
        var settings = Settings(path);
        var firstRun = !settings.TryGetValue("code", out var code) || code.Length == 0;
        if (firstRun)
        {
            using (var setup = new SetupForm(path, settings))
                if (setup.ShowDialog() != DialogResult.OK) return;
            settings = Settings(path);
            code = settings["code"];
        }

        if (settings.TryGetValue("connect", out var host) && host.Length > 0)
        {
            using var running = new Mutex(true, "LanDrop.Receiver", out var first);
            if (!first) return;
            StartWithWindows();
            if (firstRun) MessageBox.Show(T("Готово. LAN Drop працює у фоні й запускатиметься разом із Windows.", "Done. LAN Drop runs in the background and will start with Windows."), "LAN Drop");
            var receiving = new Thread(() => Receiver.Run(host, code)) { IsBackground = true };
            receiving.SetApartmentState(ApartmentState.STA); // as on the main thread, for the shell that opens what arrives
            receiving.Start();
            if (ShowStatus(path, host))
            {
                running.Close(); // or the new copy finds this one still running and quits
                Process.Start(Application.ExecutablePath);
            }
            return;
        }

        var sender = new Sender(code);
        try
        {
            sender.Start();
        }
        catch (SocketException)
        {
            MessageBox.Show(T($"LAN Drop уже відкрито на цьому ПК, або порт {Wire.Port} зайняла інша програма.", $"LAN Drop is already open on this PC, or another program is using port {Wire.Port}."), "LAN Drop");
            return;
        }
        Application.Run(new SendForm(sender));
    }

    // The icon by the clock, until the app exits: the program icon while connected to the
    // sending PC, a warning sign while not, and the same in words when pointed at. Its menu
    // opens the settings window or closes the app. Returns true when new settings were saved,
    // for the app to start again with them.
    static bool ShowStatus(string path, string host)
    {
        using var icon = new NotifyIcon { Visible = true, ContextMenuStrip = new ContextMenuStrip() };
        var saved = false;
        icon.ContextMenuStrip.Items.Add(T("Відкрити налаштування", "Open settings"), null, (_, _) =>
        {
            using var setup = new SetupForm(path, Settings(path));
            if (setup.ShowDialog() != DialogResult.OK) return;
            saved = true;
            Application.Exit();
        });
        icon.ContextMenuStrip.Items.Add(T("Закрити", "Close"), null, (_, _) => Application.Exit());
        var small = new Icon(AppIcon, SystemInformation.SmallIconSize); // the 16 px drawing, not the 32 px one shrunk
        var timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        timer.Tick += (_, _) => Show();
        Show();
        Application.Run();
        return saved;

        void Show()
        {
            var connected = Receiver.Connected;
            icon.Icon = connected ? small : SystemIcons.Warning;
            var text = connected ? T($"LAN Drop: підключено до {host}", $"LAN Drop: connected to {host}") : T($"LAN Drop: не підключено до {host}", $"LAN Drop: not connected to {host}");
            icon.Text = text.Length > 63 ? text.Substring(0, 63) : text; // longer throws on .NET Framework
        }
    }

    // Rewritten on every start so the entry follows the folder if it was moved.
    static void StartWithWindows()
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            run.SetValue("LAN Drop", "\"" + Application.ExecutablePath + "\"");
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException) { } // blocked by policy: it still receives until sign-out
    }

    static Dictionary<string, string> Settings(string path)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return settings;
        foreach (var line in File.ReadAllLines(path))
        {
            var i = line.IndexOf('=');
            if (i > 0) settings[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
        }
        return settings;
    }
}
