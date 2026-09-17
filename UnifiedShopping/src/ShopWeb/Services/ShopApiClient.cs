using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShopCommon;

namespace ShopWeb.Services;

public sealed class AdminAuthOptions
{
    public const string SectionName = "Admin";

    /// <summary>Demo admin passphrase; production uses real identity (Entra ID/Identity).</summary>
    public string Passphrase { get; set; } = "scarf-admin";
}

/// <summary>
/// Typed client over ShopApi. The MAUI app talks to the exact same endpoints -
/// one API, three front ends. Admin calls attach the demo X-Admin-Key header.
/// </summary>
public sealed class ShopApiClient(HttpClient http, Microsoft.Extensions.Options.IOptions<AdminAuthOptions> adminOptions)
{
    private void AttachAdminKey()
    {
        http.DefaultRequestHeaders.Remove("X-Admin-Key");
        http.DefaultRequestHeaders.Add("X-Admin-Key", adminOptions.Value.Passphrase);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProductSummary>>("api/products", ct) ?? [];

    public async Task<ProductSummary?> GetProductAsync(int id, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<ProductSummary?>($"api/products/{id}", ct);

    public async Task<Cart> QuoteCartAsync(Cart cart, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/cart/quote", cart, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Cart>(cancellationToken: ct) ?? cart;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, bool fromMobile = false, CancellationToken ct = default)
    {
        var url = fromMobile ? "api/checkout/mobile" : "api/checkout";
        var response = await http.PostAsJsonAsync(url, request, ct);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>(cancellationToken: ct);
        return result ?? new CheckoutResult(false, "Checkout failed.", null, OrderStatus.PendingPayment, null, null);
    }

    public async Task<Order?> GetOrderAsync(string orderNumber, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<Order?>($"api/orders/{Uri.EscapeDataString(orderNumber)}", ct);

    public async Task<IReadOnlyList<ShippingRate>> QuoteShippingAsync(string postalCode, string country, decimal subtotal, CancellationToken ct = default)
    {
        var url = $"api/carriers/quote?postalCode={Uri.EscapeDataString(postalCode)}&country={Uri.EscapeDataString(country)}&subtotal={subtotal}";
        return await http.GetFromJsonAsync<List<ShippingRate>>(url, ct) ?? [];
    }

    // ---- Admin endpoints (attach the demo admin key) ----

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(string? status = null, CancellationToken ct = default)
    {
        AttachAdminKey();
        var url = "api/admin/orders?take=100" + (status is null ? "" : $"&status={Uri.EscapeDataString(status)}");
        return await http.GetFromJsonAsync<List<Order>>(url, ct) ?? [];
    }

    public async Task<LabelResult?> CreateLabelAsync(int orderId, CarrierCode carrier, CancellationToken ct = default)
    {
        AttachAdminKey();
        var response = await http.PostAsJsonAsync($"api/admin/orders/{orderId}/label", new LabelRequest(orderId, carrier, null), ct);
        return await response.Content.ReadFromJsonAsync<LabelResult>(cancellationToken: ct);
    }

    public async Task MarkShippedAsync(int shipmentId, CancellationToken ct = default)
    {
        AttachAdminKey();
        await http.PostAsync($"api/admin/shipments/{shipmentId}/ship", content: null, ct);
    }

    public async Task<IReadOnlyList<Coupon>> GetCouponsAsync(CancellationToken ct = default)
    {
        AttachAdminKey();
        return await http.GetFromJsonAsync<List<Coupon>>("api/admin/coupons", ct) ?? [];
    }

    public async Task<IReadOnlyList<Coupon>> GenerateCouponsAsync(CouponGenerationOptions options, CancellationToken ct = default)
    {
        AttachAdminKey();
        var response = await http.PostAsJsonAsync("api/admin/coupons/generate", options, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Coupon>>(cancellationToken: ct) ?? [];
    }

    public async Task<IReadOnlyList<CarrierSetting>> GetCarriersAsync(CancellationToken ct = default)
    {
        AttachAdminKey();
        return await http.GetFromJsonAsync<List<CarrierSetting>>("api/admin/carriers", ct) ?? [];
    }

    public async Task<bool> ApproveReturnAsync(int orderId, string reason, ReturnResolution resolution, int quantity, CancellationToken ct = default)
    {
        AttachAdminKey();
        var response = await http.PostAsJsonAsync($"api/admin/orders/{orderId}/return",
            new { reason, resolution, quantity }, ct);
        return response.IsSuccessStatusCode;
    }
}
