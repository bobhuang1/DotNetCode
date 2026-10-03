using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;
using ShopServices;
using Xunit;

namespace ShopCommon.Tests;

/// <summary>
/// CheckoutService against a seeded SQLite database with the demo gateway: order email,
/// order number format and coupon redemption limits.
/// </summary>
public sealed class CheckoutServiceTests
{
    private static ShopDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"checkout-{Guid.NewGuid():N}.db")}")
            .Options;

        var db = new ShopDbContext(options);
        db.Database.Migrate();
        ShopSeeder.SeedAsync(db).GetAwaiter().GetResult();
        return db;
    }

    private static CheckoutService CreateCheckout(ShopDbContext db) =>
        new(db, [new DemoPaymentGateway(PaymentProvider.Stripe)], TimeProvider.System);

    private static async Task<CheckoutRequest> RequestAsync(ShopDbContext db, string? email, string? coupon = null)
    {
        var variantId = await db.Inventory
            .Where(i => i.OnHand - i.Reserved >= 5)
            .Select(i => i.ProductVariantId)
            .FirstAsync();
        var cart = new Cart { Items = [new CartItem { VariantId = variantId, Quantity = 1 }], CouponCode = coupon };
        var shipTo = new Address { Name = "Test Buyer", Line1 = "1 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" };
        return new CheckoutRequest(cart, shipTo, PaymentProvider.Stripe, CustomerEmail: email);
    }

    [Fact]
    public async Task Checkout_requires_an_email()
    {
        using var db = CreateDb();
        var result = await CreateCheckout(db).PlaceOrderAsync(await RequestAsync(db, email: null), OrderChannel.Web);

        Assert.False(result.Succeeded);
        Assert.Equal("A valid email address is required.", result.Error);
    }

    [Fact]
    public async Task Order_stores_the_customer_email_and_a_random_order_number()
    {
        using var db = CreateDb();
        var result = await CreateCheckout(db).PlaceOrderAsync(await RequestAsync(db, " Buyer@Example.com "), OrderChannel.Web);

        Assert.True(result.Succeeded, result.Error);
        Assert.Matches(@"^SO-\d{4}-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{10}$", result.OrderNumber);
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderNumber == result.OrderNumber);
        Assert.Equal("Buyer@Example.com", order.CustomerEmail);
    }

    [Fact]
    public async Task Coupon_cannot_be_redeemed_past_its_limit()
    {
        using var db = CreateDb();
        db.Coupons.Add(new Coupon { Code = "ONEUSEONLY234567", PercentOff = 10, MaxRedemptions = 1, CreatedUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var checkout = CreateCheckout(db);

        var first = await checkout.PlaceOrderAsync(await RequestAsync(db, "a@example.com", " oneuseonly234567 "), OrderChannel.Web);
        var second = await checkout.PlaceOrderAsync(await RequestAsync(db, "b@example.com", "ONEUSEONLY234567"), OrderChannel.Web);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(second.Succeeded);
        var coupon = await db.Coupons.AsNoTracking().SingleAsync(c => c.Code == "ONEUSEONLY234567");
        Assert.Equal(1, coupon.TimesUsed);
    }
}
