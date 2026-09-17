using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages.Admin;

public sealed class CarriersModel(ShopApiClient api) : PageModel
{
    public IReadOnlyList<CarrierSetting> Carriers { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Carriers = await api.GetCarriersAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(
        CarrierCode code, string displayName, string? apiBaseUrl,
        string? trackingUrlTemplate, decimal fallbackBaseRate, decimal fallbackPerKgRate, CancellationToken ct)
    {
        // Demo: display only. Wire to PUT /api/admin/carriers for persistence.
        await api.GetCarriersAsync(ct);
        Carriers = await api.GetCarriersAsync(ct);
        return Page();
    }
}
