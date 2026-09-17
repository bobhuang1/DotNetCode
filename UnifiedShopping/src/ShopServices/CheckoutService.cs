using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>
/// Checkout orchestration: reserve stock -> persist PENDING order -> hand off to the
/// gateway -> capture -> mark PAID -> publish event. Failure at any step releases stock.
/// </summary>
public class CheckoutService(ShopDbContext db, IEnumerable<IPaymentGateway> gateways, TimeProvider clock)
{
    public async Task<CheckoutResult> PlaceOrderAsync(CheckoutRequest request, OrderChannel channel, CancellationToken ct = default)
    {
        var cart = request.Cart;
        if (cart.Items.Count == 0)
        {
            return new CheckoutResult(false, "Cart is empty.", null, OrderStatus.PendingPayment, null, null);
        }

        var (items, validationError) = await new CartService(db).ValidateAsync(cart, ct);
        if (items.Count == 0)
        {
            return new CheckoutResult(false, validationError ?? "No items available to order.", null, OrderStatus.PendingPayment, null, null);
        }

        var variantIds = items.Select(i => i.VariantId).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // 1. Reserve inventory with optimistic concurrency; a lost race = 409-style retry.
            var inventory = await db.Inventory
                .Where(i => db.ProductVariants
                    .Where(v => variantIds.Contains(v.Id))
                    .Select(v => v.Id)
                    .Contains(i.ProductVariantId))
                .ToListAsync(ct);

            var invByVariant = inventory.ToDictionary(i => i.ProductVariantId);

            foreach (var item in items)
            {
                if (!invByVariant.TryGetValue(item.VariantId, out var inv) || inv.OnHand - inv.Reserved < item.Quantity)
                {
                    return new CheckoutResult(false, $"Insufficient stock for {item.Sku}.", null, OrderStatus.PendingPayment, null, null);
                }
                inv.Reserved += item.Quantity;
                inv.LastAdjustedUtc = clock.GetUtcNow().UtcDateTime;
            }

            // 2. Price the order server-side (never trust client totals) and persist as PendingPayment.
            var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            decimal? percentOff = null;
            string? couponMessage = null;

            if (!string.IsNullOrWhiteSpace(cart.CouponCode))
            {
                (percentOff, couponMessage) = await new CartService(db).ValidateCouponAsync(cart.CouponCode, subtotal, ct);
                if (couponMessage is not null)
                {
                    return new CheckoutResult(false, couponMessage, null, OrderStatus.PendingPayment, null, null);
                }
            }

            var totals = PricingCalculator.Calculate(items, cart.CouponCode, percentOff, couponMessage, 0, selectedRate: null);
            var order = new Order
            {
                OrderNumber = $"SO-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(100000, 999999)}",
                CustomerEmail = request.ShipTo.Name is null ? "guest@example.com" : request.ShipTo.Name,
                Status = OrderStatus.PendingPayment,
                Channel = channel,
                ShipTo = request.ShipTo,
                CouponCode = totals.CouponApplied,
                CouponPercentOff = totals.CouponPercentOff,
                Subtotal = totals.Subtotal,
                DiscountTotal = totals.Discount,
                ShippingTotal = totals.Shipping,
                TaxTotal = totals.Tax,
                GrandTotal = totals.GrandTotal,
                CreatedUtc = clock.GetUtcNow().UtcDateTime,
                UpdatedUtc = clock.GetUtcNow().UtcDateTime,
                Events =
                [
                    new OrderEvent
                    {
                        TimestampUtc = clock.GetUtcNow().UtcDateTime,
                        Actor = channel.ToString(),
                        Message = $"Order placed with {items.Count} line(s); awaiting payment.",
                    },
                ],
            };

            foreach (var item in items)
            {
                order.Lines.Add(new OrderLine
                {
                    ProductVariantId = item.VariantId,
                    Sku = item.Sku,
                    Name = item.Name,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal,
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            // 3. Hand off to the gateway. Demo gateways capture instantly; real ones redirect.
            var gateway = gateways.FirstOrDefault(g => g.Provider == request.Provider)
                ?? throw new InvalidOperationException($"No payment gateway registered for {request.Provider}.");

            var (reference, redirectUrl) = await gateway.CreateIntentAsync(
                totals.GrandTotal, "USD", order.OrderNumber, request.ReturnUrl, request.CancelUrl, ct);

            order.Payment = new PaymentRecord
            {
                OrderId = order.Id,
                Provider = request.Provider,
                Status = redirectUrl is null ? PaymentStatus.Captured : PaymentStatus.Pending,
                ProviderReference = reference,
                Amount = totals.GrandTotal,
                CapturedUtc = redirectUrl is null ? clock.GetUtcNow().UtcDateTime : null,
            };

            order.Events.Add(new OrderEvent
            {
                TimestampUtc = clock.GetUtcNow().UtcDateTime,
                Actor = request.Provider.ToString(),
                Message = redirectUrl is null
                    ? $"Payment captured ({PricingCalculator.FormatMoney(totals.GrandTotal)} via {request.Provider})."
                    : "Payment initiated; waiting for gateway confirmation.",
            });

            if (redirectUrl is null)
            {
                order.Status = OrderStatus.Paid;
                await MarkCouponRedeemedAsync(order.CouponCode, ct);
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new CheckoutResult(true, null, order.OrderNumber, order.Status, redirectUrl, reference);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Called by payment webhooks/return URLs once the gateway confirms funds.</summary>
    public async Task<CheckoutResult> MarkPaidAsync(string orderNumber, string providerReference, PaymentProvider provider, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        if (order is null)
        {
            return new CheckoutResult(false, $"Order {orderNumber} not found.", null, OrderStatus.PendingPayment, null, null);
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return new CheckoutResult(true, null, order.OrderNumber, order.Status, null, providerReference); // idempotent
        }

        order.Status = OrderStatus.Paid;
        if (order.Payment is not null)
        {
            order.Payment.Status = PaymentStatus.Captured;
            order.Payment.ProviderReference = providerReference;
            order.Payment.CapturedUtc = clock.GetUtcNow().UtcDateTime;
        }

        order.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = provider.ToString(),
            Message = $"Payment confirmed ({PricingCalculator.FormatMoney(order.GrandTotal)}).",
        });

        await MarkCouponRedeemedAsync(order.CouponCode, ct);
        await db.SaveChangesAsync(ct);

        return new CheckoutResult(true, null, order.OrderNumber, order.Status, null, providerReference);
    }

    private async Task MarkCouponRedeemedAsync(string? couponCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return;
        }

        await db.Coupons
            .Where(c => c.Code == couponCode)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.TimesUsed, c => c.TimesUsed + 1), ct);
    }
}
