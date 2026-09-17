using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class IndexModel(ShopApiClient api) : PageModel
{
    public IReadOnlyList<ProductSummary> Products { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Products = await api.GetProductsAsync(ct);
    }
}
