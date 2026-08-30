using System.Security.Cryptography;

namespace Adesha.Api.Tests;

/// <summary>
/// RFC 6238 TOTP generator for tests, compatible with ASP.NET Core Identity's
/// authenticator token provider (Base32 key, 30-second step, 6 digits, HMAC-SHA1).
/// </summary>
internal static class TotpGenerator
{
    internal static string GenerateCode(string base32Key, TimeProvider? timeProvider = null)
    {
        var key = Base32Decode(base32Key);
        var timestep = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds() / 30;

        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, timestep);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpperInvariant();

        var output = new List<byte>(input.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var c in input)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"Invalid base32 character '{c}'.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return [.. output];
    }
}
