using System.Security.Cryptography;
using System.Text;
using ShopServices;
using Xunit;

namespace ShopCommon.Tests;

public sealed class StripeWebhookVerifierTests
{
    private const string Secret = "whsec_test";
    private const string Body = "{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\"}";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static string Sign(long timestamp, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
    }

    [Fact]
    public void Accepts_A_Correct_Signature()
    {
        var t = Now.ToUnixTimeSeconds();
        Assert.True(StripeWebhookVerifier.IsValid(Body, $"t={t},v1={Sign(t, Body, Secret)}", Secret, Now));
    }

    [Fact]
    public void Accepts_Any_Matching_V1_During_Secret_Rotation()
    {
        var t = Now.ToUnixTimeSeconds();
        var header = $"t={t},v1={Sign(t, Body, "old-secret")},v1={Sign(t, Body, Secret)}";
        Assert.True(StripeWebhookVerifier.IsValid(Body, header, Secret, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("t=1800000000")]
    public void Rejects_Missing_Or_Incomplete_Headers(string? header) =>
        Assert.False(StripeWebhookVerifier.IsValid(Body, header, Secret, Now));

    [Fact]
    public void Rejects_A_Tampered_Body()
    {
        var t = Now.ToUnixTimeSeconds();
        var header = $"t={t},v1={Sign(t, Body, Secret)}";
        Assert.False(StripeWebhookVerifier.IsValid(Body.Replace("succeeded", "failed"), header, Secret, Now));
    }

    [Fact]
    public void Rejects_A_Wrong_Secret()
    {
        var t = Now.ToUnixTimeSeconds();
        Assert.False(StripeWebhookVerifier.IsValid(Body, $"t={t},v1={Sign(t, Body, "attacker")}", Secret, Now));
    }

    [Fact]
    public void Rejects_A_Replayed_Old_Event()
    {
        var t = Now.AddMinutes(-10).ToUnixTimeSeconds();
        Assert.False(StripeWebhookVerifier.IsValid(Body, $"t={t},v1={Sign(t, Body, Secret)}", Secret, Now));
    }
}
