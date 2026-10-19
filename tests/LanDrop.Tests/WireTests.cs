using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

namespace LanDrop.Tests;

public sealed class WireTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "lan-drop-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    [Fact]
    public void A_link_arrives_as_the_link()
    {
        var s = new MemoryStream();
        Wire.WriteLink(s, "1234", "https://example.com/a b");
        s.Position = 0;
        Assert.Equal("https://example.com/a%20b", Wire.Read(s, "1234", folder));
    }

    [Fact]
    public void A_ping_arrives_as_nothing()
    {
        var s = new MemoryStream();
        Wire.WritePing(s);
        s.Position = 0;
        Assert.Null(Wire.Read(s, "1234", folder));
    }

    [Fact]
    public void A_file_sent_twice_lands_twice_without_overwriting()
    {
        var source = Path.Combine(Path.GetTempPath(), "notes-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(source, "hello");
        try
        {
            var s = new MemoryStream();
            Wire.WriteFile(s, "1234", source);
            Wire.WriteFile(s, "1234", source);
            s.Position = 0;
            var first = (string)Wire.Read(s, "1234", folder);
            var second = (string)Wire.Read(s, "1234", folder);
            Assert.Equal(Path.Combine(folder, Path.GetFileName(source)), first);
            Assert.Equal(Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + " (2).txt"), second);
            Assert.Equal("hello", File.ReadAllText(second));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void The_time_arrives_as_the_sending_pcs_utc_clock()
    {
        var s = new MemoryStream();
        var before = DateTime.UtcNow;
        Wire.WriteTime(s, "1234");
        s.Position = 0;
        var time = Assert.IsType<DateTime>(Wire.Read(s, "1234", folder));
        Assert.Equal(DateTimeKind.Utc, time.Kind);
        Assert.InRange(time, before, DateTime.UtcNow);
    }

    [Fact]
    public void A_wrong_code_is_refused_and_saves_nothing()
    {
        var s = Message(Wire.File, "notes.txt", "hello");
        Assert.Throws<InvalidDataException>(() => Wire.Read(s, "9999", folder));
        Assert.False(Directory.Exists(folder));
    }

    [Theory]
    [InlineData(@"..\evil.txt")]
    [InlineData(@"C:\Windows\evil.txt")]
    [InlineData("..")]
    public void A_file_name_with_a_path_is_refused(string name)
    {
        Assert.Throws<InvalidDataException>(() => Wire.Read(Message(Wire.File, name, "x"), "1234", folder));
    }

    [Theory]
    [InlineData(@"file:///C:/Windows/System32/calc.exe")]
    [InlineData("calc")]
    public void A_link_that_is_not_a_web_link_is_refused(string link)
    {
        Assert.Throws<InvalidDataException>(() => Wire.Read(Message(Wire.Link, link, ""), "1234", folder));
    }

    [Theory]
    [InlineData("pc1.lan", "pc1")]
    [InlineData("pc1", "pc1")]
    [InlineData("192.168.1.5", "192.168.1.5")]
    public void A_pc_shows_by_its_short_name_or_its_ip(string host, string shown) =>
        Assert.Equal(shown, Sender.Short(host));

    [Fact]
    public void The_sender_reaches_a_connected_pc()
    {
        var sender = new Sender("1234", new IPEndPoint(IPAddress.Loopback, 17851));
        sender.Start();
        var source = Path.GetTempFileName();
        File.WriteAllBytes(source, new byte[3_000_000]);
        try
        {
            using var client = new TcpClient("127.0.0.1", 17851);
            Assert.True(SpinWait.SpinUntil(() => sender.Count == 1, 5000));
            Assert.Single(sender.Names);
            int reached = 0, total = 0;
            var sending = new Thread(() => reached = sender.Send(source, out total));
            sending.Start();
            string saved;
            while ((saved = (string)Wire.Read(client.GetStream(), "1234", folder)) == null) { }
            sending.Join();
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(saved));
            Assert.Equal(1, reached);
            Assert.Equal(1, total);
        }
        finally
        {
            File.Delete(source);
        }
    }

    static MemoryStream Message(byte kind, string name, string payload)
    {
        var s = new MemoryStream();
        var bytes = Encoding.UTF8.GetBytes(payload);
        Wire.Write(s, "1234", kind, name, new MemoryStream(bytes), bytes.Length);
        s.Position = 0;
        return s;
    }
}
