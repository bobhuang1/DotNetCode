using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages.Admin;

public sealed class IndexModel(ShopApiClient api) : PageModel
{
    public IReadOnlyList<Order> Orders { get; private set; } = [];
    public OrderStatus? StatusFilter { get; private set; }

    public async Task OnGetAsync(string? status, CancellationToken ct)
    {
        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
        {
            StatusFilter = parsed;
        }

        Orders = await api.GetOrdersAsync(status, ct);
    }
}
