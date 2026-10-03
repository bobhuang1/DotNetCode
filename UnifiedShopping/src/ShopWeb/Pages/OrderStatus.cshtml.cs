using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class OrderStatusModel(ShopApiClient api) : PageModel
{
    public string? OrderNumber { get; private set; }
    public string? Email { get; private set; }
    public Order? Order { get; private set; }
    public bool NotFound { get; private set; }

    public async Task OnGetAsync(string? orderNumber, string? email, CancellationToken ct)
    {
        OrderNumber = orderNumber;
        Email = email;
        if (!string.IsNullOrWhiteSpace(orderNumber) && !string.IsNullOrWhiteSpace(email))
        {
            Order = await api.GetOrderAsync(orderNumber.Trim(), email.Trim(), ct);
            NotFound = Order is null;
        }
    }
}
