using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace AuthMd.AspNetCore;

internal static class Secrets
{
    /// <summary>A 256-bit random token, e.g. <c>clm_…</c>.</summary>
    public static string NewToken(string prefix) =>
        prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>The 6-digit code the Agent hands to the User.</summary>
    public static string NewUserCode() =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");

    /// <summary>Compares a presented secret to the expected one in constant time.</summary>
    public static bool Matches(string expected, string? presented) =>
        presented is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
