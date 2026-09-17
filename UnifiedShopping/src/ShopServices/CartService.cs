using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>Cart and coupon rules shared by the storefront, the MAUI app, and the admin "create order" flow.</summary>
public class CartService(ShopDbContext db)
{
    public async Task<Cart> ComputeTotalsAsync(Cart cart, ShippingRate? selectedRate = null, CancellationToken ct = default)
    {
        var (items, error) = await ValidateAsync(cart, ct);
        cart.Items = items;

        string? couponError = null;
        decimal? percentOff = null;

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            (percentOff, couponError) = await ValidateCouponAsync(cart.CouponCode, cart.Items.Sum(i => i.UnitPrice * i.Quantity), ct);
        }

        cart.CouponMessage = couponError;
        cart.Totals = PricingCalculator.Calculate(items, cart.CouponCode, percentOff, couponError, 0, selectedRate);
        if (cart.Totals.CouponApplied is null)
        {
            cart.CouponMessage ??= couponError;
        }

        return cart;
    }

    /// <summary>Re-prices the cart from current catalog data; unknown or inactive SKUs are dropped.</summary>
    public async Task<(List<CartItem> Items, string? Error)> ValidateAsync(Cart cart, CancellationToken ct = default)
    {
        if (cart.Items.Count == 0)
        {
            return ([], "Cart is empty.");
        }

        var variantIds = cart.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Include(v => v.Inventory)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var priced = new List<CartItem>();
        string? error = null;

        foreach (var item in cart.Items)
        {
            if (!variants.TryGetValue(item.VariantId, out var variant) || variant.Product is not { IsActive: true })
            {
                error = $"An item is no longer available (variant {item.VariantId}).";
                continue;
            }

            var unitPrice = variant.PriceOverride != 0 ? variant.PriceOverride : variant.Product!.Price;
            var available = variant.Inventory is null ? 0 : variant.Inventory.OnHand - variant.Inventory.Reserved;
            var qty = Math.Clamp(item.Quantity, 0, available);

            if (qty < item.Quantity)
            {
                error = $"Only {available} left of {variant.Sku}.";
            }

            if (qty > 0)
            {
                priced.Add(new CartItem
                {
                    VariantId = variant.Id,
                    Sku = variant.Sku,
                    Name = variant.Product!.Name,
                    Color = variant.Color,
                    Size = variant.Size,
                    UnitPrice = unitPrice,
                    Quantity = qty,
                    ImageUrl = variant.Product.ImageUrl,
                });
            }
        }

        return (priced, error);
    }

    public async Task<(decimal? PercentOff, string? Error)> ValidateCouponAsync(string code, decimal subtotal, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, ct);

        if (coupon is null || !coupon.IsActive)
        {
            return (null, "Coupon code is not valid.");
        }

        if (coupon.ExpiresUtc is { } expires && expires < DateTime.UtcNow)
        {
            return (null, "Coupon code has expired.");
        }

        if (coupon.MaxRedemptions is { } max && coupon.TimesUsed >= max)
        {
            return (null, "Coupon code has reached its usage limit.");
        }

        if (coupon.MinSubtotal is { } min && subtotal < min)
        {
            return (null, $"Coupon requires a subtotal of at least {PricingCalculator.FormatMoney(min)}.");
        }

        return (coupon.PercentOff, null);
    }
}
