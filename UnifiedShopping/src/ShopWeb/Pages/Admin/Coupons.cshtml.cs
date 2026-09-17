using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages.Admin;

public sealed class CouponsModel(ShopApiClient api) : PageModel
{
    public IReadOnlyList<Coupon> Coupons { get; private set; } = [];
    public IReadOnlyList<Coupon> JustGenerated { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Coupons = await api.GetCouponsAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(
        decimal percentOff, int count, string? prefix, DateTime? expiresUtc, decimal? minSubtotal, CancellationToken ct)
    {
        JustGenerated = await api.GenerateCouponsAsync(new CouponGenerationOptions
        {
            PercentOff = percentOff,
            Count = count,
            Prefix = prefix,
            ExpiresUtc = expiresUtc,
            MinSubtotal = minSubtotal,
        }, ct);

        Coupons = await api.GetCouponsAsync(ct);
        return Page();
    }
}
