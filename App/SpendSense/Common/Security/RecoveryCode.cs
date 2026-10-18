using System.Text;

namespace SpendSense.Common.Security;

/// <summary>
/// 128 random bits written as 26 Base32 characters in groups of four (<c>7KQ2-M9XD-…</c>). Parsing ignores case,
/// spaces and dashes, and reads 0 as O and 1 as I, which Base32 doesn't use.
/// </summary>
public static class RecoveryCode
{
    public const int Size = 16;

    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    static readonly int Length = (Size * 8 + 4) / 5;

    public static string Format(byte[] code)
    {
        var text = new StringBuilder(Length);
        int buffer = 0, bits = 0;
        foreach (var b in code)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                text.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            text.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        return string.Join('-', text.ToString().Chunk(4).Select(chunk => new string(chunk)));
    }

    public static bool TryParse(string? input, out byte[] code)
    {
        code = [];
        var text = (input ?? "").ToUpperInvariant().Replace('0', 'O').Replace('1', 'I')
            .Where(c => c is not ('-' or ' ')).ToArray();
        if (text.Length != Length)
            return false;

        var bytes = new List<byte>(Size);
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
                return false;
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        if (bytes.Count != Size)
            return false;

        code = [.. bytes];
        return true;
    }
}
