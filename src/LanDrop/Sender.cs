using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LanDrop;

/// <summary>
/// The sending PC's side: accepts connections from the other PCs and sends to all of them at
/// once. A PC is dropped when a write to it fails; a ping every ten seconds makes that happen
/// for PCs that were switched off.
/// </summary>
public sealed class Sender
{
    readonly string code;
    readonly TcpListener listener;
    readonly Dictionary<TcpClient, string> clients = new(); // each PC's name for the window
    Timer pings;

    public Sender(string code) : this(code, new IPEndPoint(IPAddress.Any, Wire.Port)) { }

    internal Sender(string code, IPEndPoint at)
    {
        this.code = code;
        listener = new TcpListener(at);
    }

    public int Count { get { lock (clients) return clients.Count; } }

    /// <summary>
    /// The connected PCs' names, sorted. A PC shows as its IP address until its name is found,
    /// and for good when it cannot be.
    /// </summary>
    public string[] Names { get { lock (clients) return clients.Values.OrderBy(n => n).ToArray(); } }

    /// <summary>A host name without its DNS suffix ("pc1.lan" is "pc1"); an IP address as it is.</summary>
    internal static string Short(string host) => IPAddress.TryParse(host, out _) ? host : host.Split('.')[0];

    /// <summary>Starts listening. Throws <see cref="SocketException"/> when the port is taken.</summary>
    public void Start()
    {
        listener.Start();
        new Thread(Accept) { IsBackground = true }.Start();
        pings = new Timer(state => ToAll(Wire.WritePing, true, out _), null, 10000, 10000);
    }

    /// <summary>
    /// Sends a web link or a readable file to every connected PC and blocks until done. Returns
    /// how many PCs got it; <paramref name="total"/> is how many were connected at the start.
    /// </summary>
    public int Send(string linkOrFile, out int total)
    {
        var link = Wire.IsWebLink(linkOrFile);
        return ToAll(s =>
        {
            if (link) Wire.WriteLink(s, code, linkOrFile);
            else Wire.WriteFile(s, code, linkOrFile);
        }, false, out total);
    }

    /// <summary>Sends this PC's clock to every connected PC; returns like <see cref="Send"/>.</summary>
    public int SendTime(out int total) => ToAll(s => Wire.WriteTime(s, code), false, out total);

    void Accept()
    {
        while (true)
        {
            var c = listener.AcceptTcpClient();
            c.SendTimeout = 30000;
            var ip = ((IPEndPoint)c.Client.RemoteEndPoint).Address;
            lock (clients) clients[c] = ip.ToString();
            // A failed lookup can take seconds, so it runs aside and other PCs connect meanwhile.
            new Thread(() =>
            {
                var name = NameOf(ip);
                lock (clients) if (clients.ContainsKey(c)) clients[c] = name;
            }) { IsBackground = true }.Start();
        }
    }

    static string NameOf(IPAddress ip)
    {
        try { return Short(Dns.GetHostEntry(ip).HostName); }
        catch (SocketException) { return ip.ToString(); }
    }

    // A ping skips a PC that is busy receiving a file rather than waiting for it to finish.
    int ToAll(Action<Stream> write, bool skipBusy, out int total)
    {
        TcpClient[] snapshot;
        lock (clients) snapshot = clients.Keys.ToArray();
        total = snapshot.Length;
        var reached = 0;
        var threads = snapshot.Select(c => new Thread(() =>
        {
            if (skipBusy) { if (!Monitor.TryEnter(c)) return; }
            else Monitor.Enter(c);
            try
            {
                write(c.GetStream());
                Interlocked.Increment(ref reached);
            }
            catch (Exception)
            {
                lock (clients) clients.Remove(c);
                c.Close();
            }
            finally { Monitor.Exit(c); }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        return reached;
    }
}
