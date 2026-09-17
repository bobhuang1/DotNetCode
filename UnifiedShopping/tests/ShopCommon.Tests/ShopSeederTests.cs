using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;
using ShopServices;
using Xunit;

namespace ShopCommon.Tests;

/// <summary>
/// Exercises CartService (coupon validation + catalog re-pricing) against a real
/// SQLite database seeded with the demo catalog - the same path production uses,
/// just on a throwaway file.
/// </summary>
public sealed class CartServiceTests
{
    private static (ShopDbContext Db, CartService Carts) Create()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"cart-{Guid.NewGuid():N}.db")}")
            .Options;

        var db = new ShopDbContext(options);
        db.Database.Migrate();
        ShopSeeder.SeedAsync(db).GetAwaiter().GetResult();

        return (db, new CartService(db));
    }

    [Fact]
    public async Task Valid_coupon_returns_percent_off()
    {
        var (db, carts) = Create();
        var (percentOff, error) = await carts.ValidateCouponAsync("WELCOME16SHOP", subtotal: 100m);

        Assert.Null(error);
        Assert.Equal(15m, percentOff);
    }

    [Fact]
    public async Task Unknown_coupon_is_rejected_with_message()
    {
        var (_, carts) = Create();
        var (percentOff, error) = await carts.ValidateCouponAsync("NOTAREALCODE1234", 100m);

        Assert.Null(percentOff);
        Assert.Equal("Coupon code is not valid.", error);
    }

    [Fact]
    public async Task Coupon_below_minimum_subtotal_is_rejected()
    {
        var (_, carts) = Create();
        var (_, error) = await carts.ValidateCouponAsync("WELCOME16SHOP", subtotal: 10m);

        Assert.NotNull(error);
        Assert.Contains("at least", error);
    }

    [Fact]
    public async Task Cart_items_are_repriced_from_the_catalog()
    {
        var (db, carts) = Create();
        var variant = await db.ProductVariants.Include(v => v.Product).FirstAsync();

        var cart = new Cart
        {
            Items =
            [
                new CartItem { VariantId = variant.Id, Sku = variant.Sku, Name = "stale name", UnitPrice = 0.01m, Quantity = 2 },
            ],
        };

        var result = await carts.ComputeTotalsAsync(cart);

        var item = Assert.Single(result.Items);
        Assert.Equal(variant.PriceOverride != 0 ? variant.PriceOverride : variant.Product!.Price, item.UnitPrice);
        Assert.Equal(item.UnitPrice * 2, result.Totals.Subtotal);
    }
}
