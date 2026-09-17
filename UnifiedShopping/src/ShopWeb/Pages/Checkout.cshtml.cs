using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class CheckoutModel(ShopApiClient api, CartSession cartSession) : PageModel
{
    public IReadOnlyList<ShippingRate> Rates { get; private set; } = [];
    public Order? PlacedOrder { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadRatesAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(
        Address shipTo, CarrierCode carrier, PaymentProvider provider, CancellationToken ct)
    {
        var cart = await api.QuoteCartAsync(cartSession.Current, ct);
        if (cart.Items.Count == 0)
        {
            return RedirectToPage("/Cart");
        }

        var rates = await api.QuoteShippingAsync(shipTo.PostalCode, shipTo.Country, cart.Totals.Subtotal, ct);
        var selected = rates.FirstOrDefault(r => r.Carrier == carrier) ?? rates.FirstOrDefault();
        if (selected is not null)
        {
            cart.Totals.Shipping = selected.Cost;
            cart.Totals.GrandTotal = cart.Totals.Subtotal - cart.Totals.Discount + selected.Cost + cart.Totals.Tax;
        }

        var result = await api.CheckoutAsync(new CheckoutRequest(cart, shipTo, provider), fromMobile: false, ct);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error ?? "Checkout failed. Please try again.";
            await LoadRatesAsync(ct);
            return Page();
        }

        if (result.PaymentRedirectUrl is { } redirectUrl)
        {
            // Real gateways redirect to Stripe Checkout / PayPal approval here.
            return Redirect(redirectUrl);
        }

        PlacedOrder = await api.GetOrderAsync(result.OrderNumber!, ct);
        await cartSession.ClearAsync();
        return Page();
    }

    private async Task LoadRatesAsync(CancellationToken ct)
    {
        var cart = await api.QuoteCartAsync(cartSession.Current, ct);
        Rates = await api.QuoteShippingAsync("10001", "US", cart.Totals.Subtotal, ct);
    }
}
