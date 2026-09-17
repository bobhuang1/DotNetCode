using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShopCommon;
using ShopWeb.Services;

namespace ShopWeb.Pages;

public sealed class ProductModel(ShopApiClient api, CartSession cart) : PageModel
{
    public ProductSummary? Product { get; private set; }
    public List<SelectListItem> VariantOptions { get; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        Product = await api.GetProductAsync(id, ct);
        if (Product is not null)
        {
            VariantOptions.AddRange(Product.Variants.Select(v => new SelectListItem(
                $"{v.Color} - {v.Size} ({(v.InStock ? $"{v.OnHand} left" : "waitlist")})",
                v.VariantId.ToString())));
        }
    }

    public async Task<IActionResult> OnPostAsync(int id, int variantId, int quantity, CancellationToken ct)
    {
        Product = await api.GetProductAsync(id, ct);
        var variant = Product?.Variants.FirstOrDefault(v => v.VariantId == variantId);
        if (Product is null || variant is null)
        {
            return NotFound();
        }

        await cart.AddItemAsync(new CartItem
        {
            VariantId = variant.VariantId,
            Sku = variant.Sku,
            Name = Product.Name,
            Color = variant.Color,
            Size = variant.Size,
            UnitPrice = variant.Price,
            Quantity = Math.Clamp(quantity, 1, 10),
            ImageUrl = Product.ImageUrl,
        }, ct);

        StatusMessage = $"{Product.Name} ({variant.Color}) added to cart.";
        return RedirectToPage("/Cart");
    }
}
