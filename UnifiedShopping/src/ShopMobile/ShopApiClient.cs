using System.Net.Http.Json;
using ShopCommon;

namespace ShopMobile;

/// <summary>
/// One typed client for the mobile app. The base URL points at the deployed ShopApi
/// (Azure App Service URL in production; dev machine IP when running on a device).
/// </summary>
public sealed class ShopApiClient
{
    private readonly HttpClient _http;

    public ShopApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public static ShopApiClient CreateDefault() => new(ApiConfig.BaseUrl);

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ProductSummary>>("api/products", ct) ?? [];

    public async Task<Cart> QuoteCartAsync(Cart cart, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cart/quote", cart, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Cart>(cancellationToken: ct) ?? cart;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/checkout/mobile", request, ct);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>(cancellationToken: ct);
        return result ?? new CheckoutResult(false, "Checkout failed.", null, OrderStatus.PendingPayment, null, null);
    }

    public async Task<Order?> GetOrderAsync(string orderNumber, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<Order?>($"api/orders/{Uri.EscapeDataString(orderNumber)}", ct);
}

/// <summary>Endpoint configuration; swap via environment or a settings screen in real apps.</summary>
public static class ApiConfig
{
    /// <summary>
    /// Dev: Android emulator uses 10.0.2.2 for host loopback; iOS sim uses localhost.
    /// Production: the Azure App Service / Front Door URL of the ShopApi deployment.
    /// </summary>
    public static string BaseUrl { get; set; } =
#if ANDROID
        "http://10.0.2.2:5080/";
#elif IOS || MACCATALYST
        "http://localhost:5080/";
#else
        "http://localhost:5080/";
#endif
}
