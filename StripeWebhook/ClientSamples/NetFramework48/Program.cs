// Sample client for the StripeWebhook Azure Function, written for
// .NET Framework 4.7/4.8.
//
// Unlike the client samples for the other integrations in this repo, this one
// isn't calling a CRUD API - it's simulating what Stripe itself does: POST a
// JSON event body with a signed "Stripe-Signature" header. That lets you test
// the function end-to-end without the Stripe CLI or a real Stripe account, as
// long as STRIPE_WEBHOOK_SECRET here matches StripeWebhookSecret in the
// function's configuration.
//
// What's different here vs. the .NET 6/8/10 sample in ../Net8Plus:
//   - No Microsoft.Extensions.Http / IHttpClientFactory. Framework apps typically
//     just new up a single, long-lived, static HttpClient and reuse it for the life
//     of the process (creating a new HttpClient per request risks socket exhaustion
//     under load - the classic footgun on this platform).
//   - TLS 1.2 is not always the OS default on older Windows/.NET Framework
//     combinations, so it's set explicitly via ServicePointManager.
//   - Program entry point is a classic static class with a Main method, rather than
//     top-level statements (top-level statements require C# 9 / .NET 5+ tooling
//     conventions typically paired with newer TFMs).
//   - Convert.ToHexString doesn't exist on net48 (added in .NET 5), so the HMAC
//     digest is hex-encoded by hand.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StripeWebhookClientSample.NetFramework48
{
    internal static class Program
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static async Task<int> Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                                   ?? "http://localhost:7071";
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
                                 ?? "whsec_REPLACE_WITH_YOUR_LOCAL_TEST_SECRET";

            try
            {
                await SendSampleEventAsync(functionBaseUrl, webhookSecret);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request failed: " + ex);
                return 1;
            }
        }

        private static async Task SendSampleEventAsync(string baseUrl, string webhookSecret)
        {
            // A minimal, representative event body - only the fields this sample's
            // signature verifier and event-type dispatch actually look at (id, type).
            // A real Stripe event's "data.object" is far richer than this.
            var json = "{" +
                       "\"id\":\"evt_test_" + Guid.NewGuid().ToString("N").Substring(0, 16) + "\"," +
                       "\"type\":\"payment_intent.succeeded\"," +
                       "\"data\":{\"object\":{\"id\":\"pi_test_123\",\"amount\":2000,\"currency\":\"usd\"}}" +
                       "}";

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = ComputeSignature(timestamp, json, webhookSecret);
            var signatureHeader = string.Format("t={0},v1={1}", timestamp, signature);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                content.Headers.Remove("Content-Type");
                content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                Client.DefaultRequestHeaders.Remove("Stripe-Signature");
                Client.DefaultRequestHeaders.Add("Stripe-Signature", signatureHeader);

                using (var response = await Client.PostAsync(baseUrl.TrimEnd('/') + "/stripe/webhook", content))
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("HTTP {0} - {1}", (int)response.StatusCode, responseBody);
                }
            }
        }

        /// <summary>Same scheme as StripeSignatureVerifier.cs in the Function: HMAC-SHA256("{t}.{body}", secret), hex-encoded.</summary>
        private static string ComputeSignature(long timestamp, string body, string secret)
        {
            var signedPayload = timestamp + "." + body;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
