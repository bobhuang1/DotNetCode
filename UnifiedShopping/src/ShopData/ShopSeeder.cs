using Microsoft.EntityFrameworkCore;
using ShopCommon;

namespace ShopData;

/// <summary>
/// Idempotent demo seed: a small OEM-scarf catalog with variants and inventory,
/// three carrier settings (UPS/FedEx/USPS), and one example coupon. Safe to call on
/// every startup - it only fills in what is missing, so real admin edits survive.
/// </summary>
public static class ShopSeeder
{
    public static async Task SeedAsync(ShopDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (!await db.Products.AnyAsync(ct))
        {
            var scarves = new List<Product>
            {
                BuildScarf(
                    "SCF-SILK90", "Aurora Silk Scarf 90x90", "100% mulberry silk, 16 momme, hand-rolled hem",
                    price: 89m, compareAt: 119m, weight: 95,
                    imageUrl: "/img/scarf-silk-aurora.svg",
                    variants: [("SCARLET", 48), ("NAVY", 64), ("IVORY", 30)]),
                BuildScarf(
                    "SCF-SILK110", "Grand Foulard Silk 110x110", "100% mulberry silk, 18 momme, generous square",
                    price: 129m, compareAt: null, weight: 140,
                    imageUrl: "/img/scarf-silk-grand.svg",
                    variants: [("EMERALD", 22), ("GARNET", 18)]),
                BuildScarf(
                    "SCF-CASHWRAP", "Cashmere-Blend Wrap", "70% cashmere / 30% silk, brushed finish",
                    price: 189m, compareAt: 229m, weight: 260,
                    imageUrl: "/img/scarf-cashmere-wrap.svg",
                    variants: [("CAMEL", 12), ("CHARCOAL", 26)]),
                BuildScarf(
                    "SCF-LINENSS", "Summer Linen Scarf", "100% European linen, stonewashed",
                    price: 59m, compareAt: null, weight: 80,
                    imageUrl: "/img/scarf-linen-summer.svg",
                    variants: [("SKY", 40), ("SAND", 55), ("MOSS", 8)]),
            };

            db.Products.AddRange(scarves);
        }

        if (!await db.CarrierSettings.AnyAsync(ct))
        {
            db.CarrierSettings.AddRange(
                new CarrierSetting
                {
                    Code = CarrierCode.Ups,
                    DisplayName = "UPS",
                    TrackingUrlTemplate = "https://www.ups.com/track?tracknum={0}",
                    FallbackBaseRate = 8.50m,
                    FallbackPerKgRate = 3.20m,
                },
                new CarrierSetting
                {
                    Code = CarrierCode.FedEx,
                    DisplayName = "FedEx",
                    TrackingUrlTemplate = "https://www.fedex.com/fedextrack/?trknbr={0}",
                    FallbackBaseRate = 8.25m,
                    FallbackPerKgRate = 3.35m,
                },
                new CarrierSetting
                {
                    Code = CarrierCode.Usps,
                    DisplayName = "USPS",
                    TrackingUrlTemplate = "https://tools.usps.com/go/TrackConfirmAction?tLabels={0}",
                    FallbackBaseRate = 6.90m,
                    FallbackPerKgRate = 2.80m,
                });
        }

        if (!await db.Coupons.AnyAsync(c => c.Code == "WELCOME16SHOP"))
        {
            db.Coupons.Add(new Coupon
            {
                Code = "WELCOME16SHOP",
                PercentOff = 15m,
                MaxRedemptions = 1000,
                MinSubtotal = 50m,
                CreatedUtc = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static Product BuildScarf(
        string skuRoot, string name, string material, decimal price,
        decimal? compareAt, int weight, string imageUrl,
        (string Color, int OnHand)[] variants)
    {
        var product = new Product
        {
            Sku = skuRoot,
            Name = name,
            Material = material,
            Price = price,
            CompareAtPrice = compareAt,
            WeightGrams = weight,
            ImageUrl = imageUrl,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };

        foreach (var (color, onHand) in variants)
        {
            var variant = new ProductVariant
            {
                Sku = $"{skuRoot}-{color}",
                Color = ToTitle(color),
                Size = skuRoot.Contains("WRAP", StringComparison.OrdinalIgnoreCase) ? "180x70" : "90x90",
            };
            product.Variants.Add(variant);
            variant.Inventory = new InventoryItem
            {
                OnHand = onHand,
                ReorderPoint = 10,
                LastAdjustedUtc = DateTime.UtcNow,
            };
        }

        return product;
    }

    private static string ToTitle(string upper) =>
        char.ToUpperInvariant(upper[0]) + upper[1..].ToLowerInvariant();
}
