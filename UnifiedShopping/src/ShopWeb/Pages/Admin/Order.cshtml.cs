using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages.Admin;

public sealed class OrderModel(ShopApiClient api) : PageModel
{
    public Order? Order { get; private set; }
    public LabelResult? LabelResult { get; private set; }

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        await LoadAsync(id, ct);
    }

    public async Task<IActionResult> OnPostBackAsync(int id, CancellationToken ct) => await LoadAsync(id, ct);

    public async Task<IActionResult> OnPostLabelAsync(int id, CarrierCode carrier, CancellationToken ct)
    {
        LabelResult = await api.CreateLabelAsync(id, carrier, ct);
        return await LoadAsync(id, ct);
    }

    public async Task<IActionResult> OnPostShipAsync(int id, int shipmentId, CancellationToken ct)
    {
        await api.MarkShippedAsync(shipmentId, ct);
        return await LoadAsync(id, ct);
    }

    public async Task<IActionResult> OnPostTrackingAsync(int id, int shipmentId, CancellationToken ct)
    {
        await api.MarkShippedAsync(shipmentId, ct); // no-op if already shipped; keeps sample small
        return await LoadAsync(id, ct);
    }

    public async Task<IActionResult> OnPostReturnAsync(
        int id, string reason, ReturnResolution resolution, int quantity, CancellationToken ct)
    {
        await api.ApproveReturnAsync(id, reason, resolution, quantity, ct);
        return await LoadAsync(id, ct);
    }

    private async Task<IActionResult> LoadAsync(int id, CancellationToken ct)
    {
        var orders = await api.GetOrdersAsync(ct: ct);
        Order = orders.FirstOrDefault(o => o.Id == id);
        return Order is null ? NotFound() : Page();
    }
}
