using System.Security.Cryptography;

namespace ShopCommon;

/// <summary>
/// Order numbers look like <c>SO-2026-7KQ2M9XAHC</c>: the year plus 10 characters from a
/// cryptographically secure RNG. The number is shown to customers and used (together with
/// the order email) to look an order up, so it must not be guessable or enumerable.
/// </summary>
public static class OrderNumberGenerator
{
    public const int RandomLength = 10;

    public static string Create(DateTime utcNow) => Create(utcNow, RandomNumberGenerator.GetInt32);

    /// <summary>Testable overload - inject a picker for deterministic output.</summary>
    public static string Create(DateTime utcNow, Func<int, int> pickIndex)
    {
        Span<char> chars = stackalloc char[RandomLength];
        for (var i = 0; i < RandomLength; i++)
        {
            chars[i] = CouponCodeGenerator.Alphabet[pickIndex(CouponCodeGenerator.Alphabet.Length)];
        }
        return $"SO-{utcNow:yyyy}-{new string(chars)}";
    }
}
