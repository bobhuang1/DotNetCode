using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShopServices;

/// <summary>
/// Verifies Stripe's <c>Stripe-Signature</c> header (https://docs.stripe.com/webhooks#verify-manually):
/// HMAC-SHA256 of "{timestamp}.{raw body}" with the endpoint's signing secret, any matching
/// <c>v1</c> value accepted, and a timestamp tolerance against replays. Same scheme as the
/// standalone StripeWebhook sample in this repository.
/// </summary>
public static class StripeWebhookVerifier
{
    public const string SecretConfigKey = "Payments:Stripe:WebhookSecret";

    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    public static bool IsValid(string rawBody, string? signatureHeader, string secret, DateTimeOffset now, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var element in signatureHeader.Split(','))
        {
            var parts = element.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            switch (parts[0].Trim())
            {
                case "t" when long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t):
                    timestamp = t;
                    break;
                case "v1":
                    signatures.Add(parts[1].Trim());
                    break;
            }
        }

        if (timestamp is null || signatures.Count == 0)
        {
            return false;
        }

        var age = now - DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        if (age.Duration() > (tolerance ?? DefaultTolerance))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Encoding.UTF8.GetBytes(
            Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp.Value}.{rawBody}"))).ToLowerInvariant());

        foreach (var provided in signatures)
        {
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            if (providedBytes.Length == expected.Length && CryptographicOperations.FixedTimeEquals(providedBytes, expected))
            {
                return true;
            }
        }

        return false;
    }
}
