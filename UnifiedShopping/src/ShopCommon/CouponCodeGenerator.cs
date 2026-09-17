using System.Security.Cryptography;

namespace ShopCommon;

/// <summary>
/// Generates 16-character alphanumeric coupon codes from a cryptographically secure RNG,
/// using an unambiguous alphabet (no 0/O/1/I/L) so codes survive being read aloud or
/// typed from a printout. Pure and injectable for deterministic tests.
/// </summary>
public static class CouponCodeGenerator
{
    public const int CodeLength = 16;

    /// <summary>34 characters - all letters/digits, excluding 0, O, 1, I, L.</summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string Generate() => Generate(RandomNumberGenerator.GetInt32);

    /// <summary>Testable overload - inject a picker for deterministic output.</summary>
    public static string Generate(Func<int, int> pickIndex)
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[pickIndex(Alphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>Codes may carry a readable prefix (e.g. "SPRING"); the generator pads to 16 chars.</summary>
    public static string GeneratePrefixed(string prefix) => GeneratePrefixed(prefix, RandomNumberGenerator.GetInt32);

    public static string GeneratePrefixed(string prefix, Func<int, int> pickIndex)
    {
        var clean = new string((prefix ?? "")
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Where(c => !Excluded.Contains(char.ToUpperInvariant(c)))
            .Take(CodeLength - 4)
            .ToArray());

        if (clean.Length == 0)
        {
            return Generate(pickIndex);
        }

        var remaining = CodeLength - clean.Length;
        Span<char> chars = stackalloc char[remaining];
        for (var i = 0; i < remaining; i++)
        {
            chars[i] = Alphabet[pickIndex(Alphabet.Length)];
        }
        return clean + new string(chars);
    }

    public static bool IsWellFormed(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && code.Length == CodeLength
        && code.All(c => Alphabet.Contains(char.ToUpperInvariant(c)));

    private static readonly char[] Excluded = ['0', 'O', '1', 'I', 'L'];
}
