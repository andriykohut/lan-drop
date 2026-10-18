using System;
using System.Collections.Generic;
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
    // lan-drop.ini beside the exe, written by the first-run window: "code=" on every PC, and
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
            using (var setup = new SetupForm(path))
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
            Receiver.Run(host, code);
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
