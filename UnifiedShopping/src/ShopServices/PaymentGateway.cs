using ShopCommon;

namespace ShopServices;

/// <summary>
/// Gateway-agnostic payment seam. Real deployments bind Stripe.net or the PayPal
/// REST SDK here; the sample ships deterministic demo implementations so the full
/// checkout flow runs with no external accounts.
/// </summary>
public interface IPaymentGateway
{
    PaymentProvider Provider { get; }

    /// <summary>Creates a gateway-side intent/order and returns the URL to redirect the customer to (hosted page) or null when in-page capture is used.</summary>
    Task<(string Reference, string? RedirectUrl)> CreateIntentAsync(decimal amount, string currency, string orderNumber, string? returnUrl, string? cancelUrl, CancellationToken ct);

    /// <summary>Verifies/decides a gateway webhook or return payload; demo mode accepts everything.</summary>
    Task<PaymentCaptureResult> ConfirmAsync(string reference, CancellationToken ct);

    Task<RefundResult> RefundAsync(string reference, decimal amount, CancellationToken ct);
}

public sealed record PaymentCaptureResult(bool Succeeded, string? Error, PaymentStatus Status, string? RawPayload = null);

public sealed record RefundResult(bool Succeeded, string? Error, decimal RefundedAmount);

/// <summary>Demo implementation: deterministic, instant success. Swap for real SDK calls in production.</summary>
public sealed class DemoPaymentGateway : IPaymentGateway
{
    public PaymentProvider Provider { get; }

    public DemoPaymentGateway(PaymentProvider provider) => Provider = provider;

    public Task<(string Reference, string? RedirectUrl)> CreateIntentAsync(
        decimal amount, string currency, string orderNumber, string? returnUrl, string? cancelUrl, CancellationToken ct)
    {
        var prefix = Provider switch { PaymentProvider.Stripe => "pi_demo", PaymentProvider.PayPal => "PAYID-DEMO", _ => "MANUAL" };
        var reference = $"{prefix}-{Guid.NewGuid():N}"[..24];
        return Task.FromResult<(string, string?)>((reference, null));
    }

    public Task<PaymentCaptureResult> ConfirmAsync(string reference, CancellationToken ct) =>
        Task.FromResult(new PaymentCaptureResult(true, null, PaymentStatus.Captured, $"demo-capture:{reference}"));

    public Task<RefundResult> RefundAsync(string reference, decimal amount, CancellationToken ct) =>
        Task.FromResult(new RefundResult(true, null, amount));
}

/// <summary>
/// Real Stripe integration point - shows the exact shape of the SDK calls
/// (Services.PaymentIntents) while remaining switchable for offline development.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    public const string ConfigKey = "Payments:Stripe:SecretKey";

    private readonly string _secretKey;

    public StripePaymentGateway(string secretKey) => _secretKey = secretKey;

    public PaymentProvider Provider => PaymentProvider.Stripe;

    public Task<(string Reference, string? RedirectUrl)> CreateIntentAsync(
        decimal amount, string currency, string orderNumber, string? returnUrl, string? cancelUrl, CancellationToken ct)
    {
        // Production shape (Stripe.net v4x):
        //   var options = new PaymentIntentCreateOptions
        //   {
        //       Amount = (long)(amount * 100), Currency = currency,
        //       Metadata = new Dictionary<string, string> { ["orderNumber"] = orderNumber },
        //       AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        //   };
        //   var intent = await new PaymentIntentService().CreateAsync(options, cancellationToken: ct);
        //   return (intent.Id, intent.NextAction?.RedirectToUrl?.Url);
        throw new NotImplementedException("Set Payments:Stripe:SecretKey and install Stripe.net to enable live Stripe.");
    }

    public Task<PaymentCaptureResult> ConfirmAsync(string reference, CancellationToken ct)
    {
        // Production shape: retrieve the intent, check intent.Status == "succeeded",
        // verify the amount matches the order before marking the order Paid.
        throw new NotImplementedException("Stripe live capture not configured in sample mode.");
    }

    public Task<RefundResult> RefundAsync(string reference, decimal amount, CancellationToken ct)
    {
        // Production shape: new RefundService().CreateAsync(new RefundCreateOptions
        // { PaymentIntent = reference, Amount = (long)(amount * 100) }, cancellationToken: ct);
        throw new NotImplementedException("Stripe live refund not configured in sample mode.");
    }
}

/// <summary>PayPal REST integration point (Orders v2): create order -> approve URL -> capture.</summary>
public sealed class PayPalPaymentGateway : IPaymentGateway
{
    public const string ConfigKey = "Payments:PayPal:ClientId";
    public const string SecretConfigKey = "Payments:PayPal:ClientSecret";

    private readonly string _clientId;
    private readonly string _clientSecret;

    public PayPalPaymentGateway(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public PaymentProvider Provider => PaymentProvider.PayPal;

    public Task<(string Reference, string? RedirectUrl)> CreateIntentAsync(
        decimal amount, string currency, string orderNumber, string? returnUrl, string? cancelUrl, CancellationToken ct)
    {
        // Production shape (PayPal Orders v2):
        //   POST /v2/checkout/orders with purchase_units[0].amount = { value, currency_code },
        //   then return response.links["approve"].Href as the redirect URL.
        throw new NotImplementedException("Set Payments:PayPal:ClientId/ClientSecret to enable live PayPal.");
    }

    public Task<PaymentCaptureResult> ConfirmAsync(string reference, CancellationToken ct)
    {
        // Production shape: POST /v2/checkout/orders/{id}/capture; treat COMPLETED as captured.
        throw new NotImplementedException("PayPal live capture not configured in sample mode.");
    }

    public Task<RefundResult> RefundAsync(string reference, decimal amount, CancellationToken ct)
    {
        // Production shape: POST /v2/payments/captures/{capture_id}/refund.
        throw new NotImplementedException("PayPal live refund not configured in sample mode.");
    }
}
