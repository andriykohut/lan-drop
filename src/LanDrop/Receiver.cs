using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace LanDrop;

/// <summary>
/// The other PCs' side: stays connected to the sending PC, reconnecting whenever the connection
/// drops, and acts on whatever arrives. Links open in the default browser; files are saved to a
/// "LAN Drop" folder on the desktop and opened with the program Windows uses for them; the
/// sending PC's time sets this PC's clock.
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
                    switch (Wire.Read(s, code, folder))
                    {
                        case string open:
                            try { Process.Start(open); }
                            catch (Win32Exception) { } // no program for this file type; it stays in the folder
                            break;
                        case DateTime utc:
                            SetClock(utc);
                            break;
                    }
                }
            }
            catch (Exception) { } // sending PC off or restarting, or a message failed the code check
            Thread.Sleep(5000);
        }
    }

    // Fails without the right to change the clock (a standard account, or an admin under UAC);
    // the clock then stays as it is.
    static void SetClock(DateTime utc)
    {
        var t = new SystemTime
        {
            Year = (ushort)utc.Year, Month = (ushort)utc.Month, Day = (ushort)utc.Day,
            Hour = (ushort)utc.Hour, Minute = (ushort)utc.Minute, Second = (ushort)utc.Second,
            Milliseconds = (ushort)utc.Millisecond,
        };
        SetSystemTime(ref t);
    }

    [DllImport("kernel32.dll")]
    static extern bool SetSystemTime(ref SystemTime time);

    [StructLayout(LayoutKind.Sequential)]
    struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }
}
