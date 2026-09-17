using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class CartModel(ShopApiClient api, CartSession cartSession) : PageModel
{
    public Cart Cart { get; private set; } = new();
    public ShippingRate? ShippingRate { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await RecalculateAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateAsync(int variantId, int quantity, CancellationToken ct)
    {
        await cartSession.UpdateQuantityAsync(variantId, quantity);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int variantId, CancellationToken ct)
    {
        await cartSession.UpdateQuantityAsync(variantId, 0);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCouponAsync(string couponCode, CancellationToken ct)
    {
        var cart = cartSession.Current;
        cart.CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
        await cartSession.SaveAsync(cart);
        return RedirectToPage();
    }

    private async Task RecalculateAsync(CancellationToken ct)
    {
        Cart = await api.QuoteCartAsync(cartSession.Current, ct);
        await cartSession.SaveAsync(Cart);

        // Estimate with a mid-range rate; checkout re-prices with the customer's real address.
        var zip = "10001";
        var rates = await api.QuoteShippingAsync(zip, "US", Cart.Totals.Subtotal, ct);
        ShippingRate = rates.OrderBy(r => r.Cost).FirstOrDefault();
    }
}
