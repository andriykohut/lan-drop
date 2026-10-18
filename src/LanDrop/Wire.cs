using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LanDrop;

/// <summary>
/// What travels from the sending PC to the other PCs. A message is a ping byte, or a link or a
/// file followed by an HMAC-SHA256 over the whole message, keyed with the code typed at setup.
/// </summary>
public static class Wire
{
    public const int Port = 17850;

    internal const byte Ping = 0, Link = 1, File = 2;

    public static void WritePing(Stream s) => s.WriteByte(Ping);

    public static void WriteLink(Stream s, string code, string url) =>
        Write(s, code, Link, url, Stream.Null, 0);

    public static void WriteFile(Stream s, string code, string path)
    {
        using var f = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Write(s, code, File, Path.GetFileName(path), f, f.Length);
    }

    /// <summary>
    /// Reads one message and returns what to open: the link, or the path the file was saved to
    /// in <paramref name="folder"/> (a file already there is kept, the new one gets a " (2)" name).
    /// Returns null for a ping. Throws <see cref="InvalidDataException"/> when the message fails
    /// the code check or is not a web link or a plain file name; nothing is saved then.
    /// </summary>
    public static string Read(Stream s, string code, string folder)
    {
        var r = new BinaryReader(s, Encoding.UTF8);
        var kind = r.ReadByte();
        if (kind == Ping) return null;
        var name = r.ReadString();
        var length = r.ReadInt64();
        if (kind != Link && kind != File || length < 0) throw new InvalidDataException("Unknown message.");

        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(code));
        var header = Header(kind, name, length);
        mac.TransformBlock(header, 0, header.Length, null, 0);
        var temp = Path.GetTempFileName();
        try
        {
            using (var f = System.IO.File.Create(temp)) Copy(s, f, length, mac);
            mac.TransformFinalBlock(new byte[0], 0, 0);
            if (!Same(r.ReadBytes(32), mac.Hash)) throw new InvalidDataException("Wrong code.");
            if (kind == File) return MoveInto(temp, folder, name);
            if (!IsWebLink(name)) throw new InvalidDataException("Not a web link.");
            return new Uri(name).AbsoluteUri;
        }
        finally
        {
            if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
        }
    }

    public static bool IsWebLink(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    internal static void Write(Stream s, string code, byte kind, string name, Stream payload, long length)
    {
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(code));
        var header = Header(kind, name, length);
        s.Write(header, 0, header.Length);
        mac.TransformBlock(header, 0, header.Length, null, 0);
        Copy(payload, s, length, mac);
        mac.TransformFinalBlock(new byte[0], 0, 0);
        s.Write(mac.Hash, 0, mac.Hash.Length);
    }

    static byte[] Header(byte kind, string name, long length)
    {
        var m = new MemoryStream();
        var w = new BinaryWriter(m, Encoding.UTF8);
        w.Write(kind);
        w.Write(name);
        w.Write(length);
        w.Flush();
        return m.ToArray();
    }

    static void Copy(Stream from, Stream to, long length, HashAlgorithm mac)
    {
        var buffer = new byte[81920];
        while (length > 0)
        {
            var n = from.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
            if (n == 0) throw new EndOfStreamException();
            to.Write(buffer, 0, n);
            mac.TransformBlock(buffer, 0, n, null, 0);
            length -= n;
        }
    }

    static string MoveInto(string temp, string folder, string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Trim('.', ' ').Length == 0)
            throw new InvalidDataException("Not a plain file name.");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, name);
        for (var i = 2; System.IO.File.Exists(target); i++)
            target = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + " (" + i + ")" + Path.GetExtension(name));
        System.IO.File.Move(temp, target);
        return target;
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
