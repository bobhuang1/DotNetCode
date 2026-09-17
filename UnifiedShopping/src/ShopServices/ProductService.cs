using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>Catalog + inventory operations for both the storefront and the admin sub-site.</summary>
public class ProductService(ShopDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProductSummary>> GetActiveProductsAsync(CancellationToken ct = default)
    {
        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Inventory)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return [.. products.Select(ToSummary)];
    }

    public async Task<ProductSummary?> GetProductAsync(int productId, CancellationToken ct = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Inventory)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct);

        return product is null ? null : ToSummary(product);
    }

    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        product.CreatedUtc = clock.GetUtcNow().UtcDateTime;
        product.UpdatedUtc = product.CreatedUtc;
        db.Products.Add(product);

        foreach (var variant in product.Variants)
        {
            variant.Inventory ??= new InventoryItem { LastAdjustedUtc = product.CreatedUtc };
        }

        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<bool> UpdateProductAsync(int id, Action<Product> mutate, CancellationToken ct = default)
    {
        var product = await db.Products.Include(p => p.Variants).ThenInclude(v => v.Inventory).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return false;
        }

        mutate(product);
        product.UpdatedUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Admin inventory adjustment; goes through the same concurrency-checked row as checkout.</summary>
    public async Task<bool> AdjustInventoryAsync(int variantId, int delta, int? reorderPoint = null, CancellationToken ct = default)
    {
        var inv = await db.Inventory.FirstOrDefaultAsync(i => i.ProductVariantId == variantId, ct);
        if (inv is null)
        {
            return false;
        }

        inv.OnHand = Math.Max(0, inv.OnHand + delta);
        if (reorderPoint is { } rp)
        {
            inv.ReorderPoint = rp;
        }
        inv.LastAdjustedUtc = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Product>> GetAllProductsForAdminAsync(CancellationToken ct = default) =>
        await db.Products
            .Include(p => p.Variants).ThenInclude(v => v.Inventory)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    private static ProductSummary ToSummary(Product p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.Material,
        p.ImageUrl,
        p.Price,
        p.CompareAtPrice,
        p.Variants.Sum(v => v.Inventory is null ? 0 : Math.Max(0, v.Inventory.OnHand - v.Inventory.Reserved)),
        [.. p.Variants.Select(v => new VariantSummary(
            v.Id,
            v.Sku,
            v.Color,
            v.Size,
            v.PriceOverride != 0 ? v.PriceOverride : p.Price,
            v.Inventory?.OnHand ?? 0,
            (v.Inventory?.OnHand ?? 0) - (v.Inventory?.Reserved ?? 0) > 0))]);
}
