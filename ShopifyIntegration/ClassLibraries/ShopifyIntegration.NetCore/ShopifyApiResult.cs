namespace ShopifyIntegration.NetCore;

/// <summary>
/// The outcome of one call to the Shopify Admin REST API. <see cref="Body"/> is
/// the raw JSON Shopify returned (or, on a non-2xx response, Shopify's own JSON
/// error body) - this client does not parse or reshape it, so you can deserialize
/// it into whatever model fits your app.
/// </summary>
public sealed class ShopifyApiResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The HTTP status code Shopify returned, or 0 if the request never reached Shopify.</summary>
    public int StatusCode { get; init; }

    public string Body { get; init; } = string.Empty;

    /// <summary>Set when <see cref="IsSuccess"/> is false; null on success.</summary>
    public string? ErrorMessage { get; init; }
}
