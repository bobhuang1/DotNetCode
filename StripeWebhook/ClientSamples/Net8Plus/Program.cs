// Sample client for the StripeWebhook Azure Function, written for .NET 6/8/10.
//
// Unlike the client samples for the other integrations in this repo, this one
// isn't calling a CRUD API - it's simulating what Stripe itself does: POST a
// JSON event body with a signed "Stripe-Signature" header. That lets you test
// the function end-to-end without the Stripe CLI or a real Stripe account, as
// long as STRIPE_WEBHOOK_SECRET here matches StripeWebhookSecret in the
// function's configuration.
//
// What's different here vs. the .NET Framework 4.8 sample in ../NetFramework48:
//   - HttpClient comes from IHttpClientFactory via the generic host's DI container
//     (AddHttpClient<T>), which is the idiomatic modern pattern: it manages the
//     underlying SocketsHttpHandler pool/lifetime for you, so there's no need for a
//     hand-rolled static singleton HttpClient.
//   - Top-level statements instead of a Main method.
//   - TLS 1.2+ is the platform default, so nothing needs to be set explicitly.
//   - Convert.ToHexString (added in .NET 5) hex-encodes the HMAC digest
//     directly, instead of hand-rolling it byte by byte like the net48 sample does.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                       ?? "http://localhost:7071";
var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
                     ?? "whsec_REPLACE_WITH_YOUR_LOCAL_TEST_SECRET";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<StripeWebhookClient>(client =>
{
    client.BaseAddress = new Uri(functionBaseUrl.TrimEnd('/') + "/");
});

using var host = builder.Build();

var client = host.Services.GetRequiredService<StripeWebhookClient>();
await client.SendSampleEventAsync(webhookSecret);

internal sealed class StripeWebhookClient(HttpClient httpClient)
{
    public async Task SendSampleEventAsync(string webhookSecret)
    {
        // A minimal, representative event body - only the fields this sample's
        // signature verifier and event-type dispatch actually look at (id, type).
        // A real Stripe event's "data.object" is far richer than this.
        var eventId = $"evt_test_{Guid.NewGuid():N}"[..24];
        var json = $$"""
        {
            "id": "{{eventId}}",
            "type": "payment_intent.succeeded",
            "data": { "object": { "id": "pi_test_123", "amount": 2000, "currency": "usd" } }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeSignature(timestamp, json, webhookSecret);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Remove("Stripe-Signature");
        httpClient.DefaultRequestHeaders.Add("Stripe-Signature", $"t={timestamp},v1={signature}");

        using var response = await httpClient.PostAsync("stripe/webhook", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"HTTP {(int)response.StatusCode} - {responseBody}");
    }

    /// <summary>Same scheme as StripeSignatureVerifier.cs in the Function: HMAC-SHA256("{t}.{body}", secret), hex-encoded.</summary>
    private static string ComputeSignature(long timestamp, string body, string secret)
    {
        var signedPayload = $"{timestamp}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
