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
    readonly List<TcpClient> clients = new();
    Timer pings;

    public Sender(string code) : this(code, new IPEndPoint(IPAddress.Any, Wire.Port)) { }

    internal Sender(string code, IPEndPoint at)
    {
        this.code = code;
        listener = new TcpListener(at);
    }

    public int Count { get { lock (clients) return clients.Count; } }

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

    void Accept()
    {
        while (true)
        {
            var c = listener.AcceptTcpClient();
            c.SendTimeout = 30000;
            lock (clients) clients.Add(c);
        }
    }

    // A ping skips a PC that is busy receiving a file rather than waiting for it to finish.
    int ToAll(Action<Stream> write, bool skipBusy, out int total)
    {
        TcpClient[] snapshot;
        lock (clients) snapshot = clients.ToArray();
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
