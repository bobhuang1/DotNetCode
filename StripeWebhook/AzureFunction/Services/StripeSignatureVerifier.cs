#nullable enable
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StripeWebhook.AzureFunction.Services
{
    /// <summary>
    /// Verifies a Stripe webhook request's <c>Stripe-Signature</c> header, per
    /// Stripe's publicly documented signing scheme
    /// (https://docs.stripe.com/webhooks#verify-manually) - implemented by hand
    /// with the BCL's <see cref="HMACSHA256"/> rather than taking a dependency on
    /// the Stripe.net SDK, to keep this sample dependency-light.
    ///
    /// The header looks like: <c>t=1614556800,v1=5257a869e7ec...,v1=affa62cdca3f...</c>
    /// (Stripe includes multiple v1 values while a webhook secret is being rotated -
    /// a match against any one of them is a valid signature.) The signed payload is
    /// the literal string "{timestamp}.{raw request body}", HMAC-SHA256'd with the
    /// webhook's signing secret.
    /// </summary>
    public static class StripeSignatureVerifier
    {
        /// <summary>
        /// Stripe's own default replay-protection tolerance. Reject a signature whose
        /// timestamp is older (or, allowing clock skew, newer) than this.
        /// </summary>
        public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

        public static (bool IsValid, string Reason) Verify(string rawBody, string? signatureHeader, string webhookSecret, TimeSpan? tolerance = null)
        {
            if (string.IsNullOrEmpty(signatureHeader))
                return (false, "Missing Stripe-Signature header.");

            long? timestamp = null;
            var providedSignatures = new List<string>();

            foreach (var element in signatureHeader.Split(','))
            {
                var parts = element.Split('=', 2);
                if (parts.Length != 2) continue;

                switch (parts[0].Trim())
                {
                    case "t":
                        if (long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
                            timestamp = t;
                        break;
                    case "v1":
                        providedSignatures.Add(parts[1].Trim());
                        break;
                }
            }

            if (timestamp is null)
                return (false, "Stripe-Signature header has no timestamp ('t=').");
            if (providedSignatures.Count == 0)
                return (false, "Stripe-Signature header has no v1 signature.");

            var effectiveTolerance = tolerance ?? DefaultTolerance;
            var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
            var age = DateTimeOffset.UtcNow - eventTime;
            if (age.Duration() > effectiveTolerance)
                return (false, $"Timestamp {eventTime:u} is outside the {effectiveTolerance.TotalSeconds}s tolerance (age: {age.TotalSeconds:F0}s).");

            var signedPayload = $"{timestamp.Value}.{rawBody}";
            var expectedSignature = ComputeHmacSha256Hex(signedPayload, webhookSecret);

            foreach (var provided in providedSignatures)
            {
                if (FixedTimeEquals(expectedSignature, provided))
                    return (true, "OK");
            }

            return (false, "No provided v1 signature matched the computed signature.");
        }

        private static string ComputeHmacSha256Hex(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexStringLower(hash);
        }

        // Constant-time comparison so signature checking doesn't leak timing
        // information about how many leading characters matched.
        private static bool FixedTimeEquals(string a, string b)
        {
            var bytesA = Encoding.UTF8.GetBytes(a);
            var bytesB = Encoding.UTF8.GetBytes(b);
            return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
        }
    }
}
