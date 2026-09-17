using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class OrderStatusModel(ShopApiClient api) : PageModel
{
    public string? OrderNumber { get; private set; }
    public Order? Order { get; private set; }

    public async Task OnGetAsync(string? orderNumber, CancellationToken ct)
    {
        OrderNumber = orderNumber;
        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            Order = await api.GetOrderAsync(orderNumber.Trim(), ct);
        }
    }
}
