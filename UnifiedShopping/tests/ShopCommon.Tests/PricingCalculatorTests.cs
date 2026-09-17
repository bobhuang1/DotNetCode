using ShopCommon;
using Xunit;

namespace ShopCommon.Tests;

public sealed class PricingCalculatorTests
{
    private static readonly CartItem SilkScarf = new()
    {
        VariantId = 1,
        Sku = "SCF-SILK90-SCARLET",
        Name = "Aurora Silk Scarf",
        UnitPrice = 89m,
        Quantity = 2,
    };

    [Fact]
    public void Subtotal_multiplies_price_times_quantity()
    {
        var totals = PricingCalculator.Calculate([SilkScarf], null, null, null, 0);

        Assert.Equal(178m, totals.Subtotal);
        Assert.Equal(2, totals.ItemCount);
    }

    [Fact]
    public void Coupon_discount_is_percentage_of_subtotal()
    {
        var totals = PricingCalculator.Calculate([SilkScarf], "WELCOME16SHOP", 15m, null, 0);

        Assert.Equal(178m, totals.Subtotal);
        Assert.Equal(26.70m, totals.Discount);
        Assert.Equal("WELCOME16SHOP", totals.CouponApplied);
    }

    [Fact]
    public void Coupon_error_suppresses_discount()
    {
        var totals = PricingCalculator.Calculate([SilkScarf], "WELCOME16SHOP", 15m, "Coupon code has expired.", 0);

        Assert.Equal(0m, totals.Discount);
        Assert.Null(totals.CouponApplied);
    }

    [Fact]
    public void Tax_applies_after_discount()
    {
        var totals = PricingCalculator.Calculate([SilkScarf], null, null, null, 0);

        Assert.Equal(178m, totals.Subtotal);
        Assert.Equal(0m, totals.Discount);
        Assert.Equal(12.46m, totals.Tax); // 7% of 178
    }

    [Fact]
    public void Free_shipping_threshold_waives_standard_shipping()
    {
        var rate = new ShippingRate(CarrierCode.Ups, "UPS", "UPS Ground", 9.50m, 4);

        var single = PricingCalculator.Calculate(
            [new CartItem { VariantId = 1, Sku = "S", Name = "One scarf", UnitPrice = 89m, Quantity = 1 }],
            null, null, null, 0, rate);
        var pair = PricingCalculator.Calculate(
            [SilkScarf], null, null, null, 0, rate);

        // 89 >= 75 threshold -> free; 178 -> free
        Assert.Equal(0m, single.Shipping);
        Assert.Equal(0m, pair.Shipping);
    }

    [Fact]
    public void Grand_total_sums_all_components()
    {
        var rate = new ShippingRate(CarrierCode.Ups, "UPS", "UPS Ground", 9.50m, 4);

        var totals = PricingCalculator.Calculate(
            [new CartItem { VariantId = 9, Sku = "X", Name = "Cheap", UnitPrice = 30m, Quantity = 1 }],
            null, null, null, 0, rate);

        // 30 subtotal, no discount, 9.50 shipping (under threshold), 7% tax = 2.10
        Assert.Equal(41.60m, totals.GrandTotal);
    }
}
