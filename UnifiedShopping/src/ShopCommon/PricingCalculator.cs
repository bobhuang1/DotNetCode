using System.Security.Cryptography;
using System.Text;

namespace ShopCommon;

/// <summary>
/// Pure pricing math shared by the storefront, the MAUI app, and the admin site.
/// A plane over money: cart lines in, itemized totals out.
/// </summary>
public static class PricingCalculator
{
    /// <summary>Default sample sales tax; production would call a tax service per address.</summary>
    public const decimal DefaultTaxRate = 0.07m;

    /// <summary>Free shipping over this subtotal (before discounts) - a common scarf-store policy.</summary>
    public const decimal FreeShippingThreshold = 75m;

    public static CartTotals Calculate(
        IReadOnlyList<CartItem> items,
        string? couponCode,
        decimal? couponPercentOff,
        string? couponError,
        decimal subtotalWeightGrams,
        ShippingRate? selectedRate = null)
    {
        var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
        var itemCount = items.Sum(i => i.Quantity);

        decimal discount = 0;
        string? appliedCode = null;
        decimal? percentOff = null;

        if (!string.IsNullOrWhiteSpace(couponCode) && couponPercentOff is > 0 && couponError is null)
        {
            discount = Math.Round(subtotal * couponPercentOff.Value / 100m, 2, MidpointRounding.AwayFromZero);
            appliedCode = couponCode.ToUpperInvariant();
            percentOff = couponPercentOff;
        }

        var shipping = selectedRate?.Cost ?? 0m;
        if (selectedRate is not null && subtotal - discount >= FreeShippingThreshold)
        {
            shipping = 0m;
        }

        var taxable = Math.Max(0, subtotal - discount);
        var tax = Math.Round(taxable * DefaultTaxRate, 2, MidpointRounding.AwayFromZero);

        return new CartTotals
        {
            Subtotal = subtotal,
            Discount = discount,
            Shipping = shipping,
            Tax = tax,
            GrandTotal = Math.Round(taxable + shipping + tax, 2, MidpointRounding.AwayFromZero),
            CouponApplied = appliedCode,
            CouponPercentOff = percentOff,
            ItemCount = itemCount,
        };
    }

    public static string FormatMoney(decimal amount) => amount.ToString("C", System.Globalization.CultureInfo.InvariantCulture);
}
