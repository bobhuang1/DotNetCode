using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>Returns, refunds, insurance claims, coupons, and carrier configuration.</summary>
public class AdminService(ShopDbContext db, IEnumerable<IPaymentGateway> gateways, TimeProvider clock)
{
    // ---- Returns / lost items / insurance claims ----

    public async Task<ReturnRequest?> ApproveReturnAsync(int orderId, string reason, ReturnResolution resolution, int quantity, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Return).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.Return is not null)
        {
            return null;
        }

        var request = new ReturnRequest
        {
            OrderId = orderId,
            Reason = reason,
            Resolution = resolution,
            QuantityReturned = quantity,
            Status = ReturnStatus.Approved,
            RequestedUtc = clock.GetUtcNow().UtcDateTime,
            UpdatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        db.Returns.Add(request);
        order.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = "Admin",
            Message = $"Return approved ({resolution}; qty {quantity}). Reason: {reason}",
        });

        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<bool> CompleteReturnAsync(int returnId, decimal refundAmount, bool restock, string? claimReference = null, CancellationToken ct = default)
    {
        var request = await db.Returns.Include(r => r.Order).ThenInclude(o => o!.Payment).FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (request is null || request.Order is null)
        {
            return false;
        }

        request.Status = ReturnStatus.Completed;
        request.RefundAmount = refundAmount;
        request.RestockReturnedItems = restock;
        request.AdminNotes ??= "";
        request.UpdatedUtc = clock.GetUtcNow().UtcDateTime;

        if (claimReference is not null)
        {
            request.ClaimReference = claimReference;
        }

        var order = request.Order;
        if (refundAmount > 0 && order.Payment is { Status: PaymentStatus.Captured })
        {
            var gateway = gateways.FirstOrDefault(g => g.Provider == order.Payment.Provider);
            if (gateway is not null)
            {
                var result = await gateway.RefundAsync(order.Payment.ProviderReference ?? "", refundAmount, ct);
                if (!result.Succeeded)
                {
                    request.AdminNotes = $"{request.AdminNotes} Refund failed: {result.Error}".Trim();
                    await db.SaveChangesAsync(ct);
                    return false;
                }

                order.Payment.RefundedAmount += result.RefundedAmount;
                order.Payment.Status = order.Payment.RefundedAmount >= order.Payment.Amount
                    ? PaymentStatus.Refunded
                    : PaymentStatus.PartiallyRefunded;
            }
        }

        if (restock)
        {
            var lines = await db.OrderLines.Where(l => l.OrderId == order.Id).ToListAsync(ct);
            foreach (var line in lines)
            {
                var inv = await db.Inventory.FirstOrDefaultAsync(i => i.ProductVariantId == line.ProductVariantId, ct);
                if (inv is not null)
                {
                    inv.OnHand += Math.Min(line.Quantity, request.QuantityReturned);
                    inv.LastAdjustedUtc = clock.GetUtcNow().UtcDateTime;
                }
            }
        }

        order.Status = ReturnResolution.Refund == request.Resolution ? OrderStatus.Refunded : OrderStatus.Closed;
        order.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = "Admin",
            Message = $"Return completed: refund {PricingCalculator.FormatMoney(refundAmount)}{(claimReference is null ? "" : $"; claim {claimReference}")}.",
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> FileInsuranceClaimAsync(int returnId, string claimReference, CancellationToken ct = default)
    {
        var request = await db.Returns.Include(r => r.Order).FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (request is null)
        {
            return false;
        }

        request.Status = ReturnStatus.InsuranceClaimFiled;
        request.ClaimReference = claimReference;
        request.UpdatedUtc = clock.GetUtcNow().UtcDateTime;
        request.Order?.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = "Admin",
            Message = $"Insurance/lost-package claim filed with carrier: {claimReference}.",
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Coupons ----

    public async Task<IReadOnlyList<Coupon>> GenerateCouponsAsync(CouponGenerationOptions options, CancellationToken ct = default)
    {
        if (options.PercentOff is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "PercentOff must be between 1 and 100.");
        }

        var created = new List<Coupon>();
        var now = clock.GetUtcNow().UtcDateTime;

        for (var i = 0; i < Math.Clamp(options.Count, 1, 500); i++)
        {
            string code;
            do
            {
                code = string.IsNullOrWhiteSpace(options.Prefix)
                    ? CouponCodeGenerator.Generate()
                    : CouponCodeGenerator.GeneratePrefixed(options.Prefix);
            }
            while (await db.Coupons.AnyAsync(c => c.Code == code, ct)); // uniqueness with an existing store

            created.Add(new Coupon
            {
                Code = code,
                PercentOff = options.PercentOff,
                ExpiresUtc = options.ExpiresUtc,
                MaxRedemptions = options.MaxRedemptions,
                MinSubtotal = options.MinSubtotal,
                CreatedUtc = now,
            });
        }

        db.Coupons.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    // ---- Carrier configuration (UPS / FedEx / USPS / custom) ----

    public async Task<IReadOnlyList<CarrierSetting>> GetCarrierSettingsAsync(CancellationToken ct = default) =>
        await db.CarrierSettings.AsNoTracking().OrderBy(s => s.Code).ThenBy(s => s.DisplayName).ToListAsync(ct);

    public async Task<bool> UpsertCarrierSettingAsync(CarrierSetting setting, CancellationToken ct = default)
    {
        var existing = await db.CarrierSettings.FirstOrDefaultAsync(s => s.Code == setting.Code && s.DisplayName == setting.DisplayName, ct);
        if (existing is null)
        {
            db.CarrierSettings.Add(setting);
        }
        else
        {
            existing.IsActive = setting.IsActive;
            existing.ApiBaseUrl = setting.ApiBaseUrl;
            existing.AccountNumber = setting.AccountNumber;
            existing.TrackingUrlTemplate = setting.TrackingUrlTemplate;
            existing.FallbackBaseRate = setting.FallbackBaseRate;
            existing.FallbackPerKgRate = setting.FallbackPerKgRate;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }
}
