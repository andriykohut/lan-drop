using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace LanDrop;

/// <summary>
/// The other PCs' side: stays connected to the sending PC, reconnecting whenever the connection
/// drops, and opens whatever arrives. Links open in the default browser; files are saved to a
/// "LAN Drop" folder on the desktop and opened with the program Windows uses for them.
/// </summary>
static class Receiver
{
    public static void Run(string host, string code)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LAN Drop");
        while (true)
        {
            try
            {
                using var c = new TcpClient(host, Wire.Port);
                c.ReceiveTimeout = 30000; // three missed pings: the sending PC is gone
                var s = c.GetStream();
                while (true)
                {
                    var open = Wire.Read(s, code, folder);
                    if (open == null) continue;
                    try { Process.Start(open); }
                    catch (Win32Exception) { } // no program for this file type; it stays in the folder
                }
            }
            catch (Exception) { } // sending PC off or restarting, or a message failed the code check
            Thread.Sleep(5000);
        }
    }
}
