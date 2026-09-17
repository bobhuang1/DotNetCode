using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>Admin-facing shipping operations: quotes, labels, tracking, fulfillment.</summary>
public class ShippingService(ShopDbContext db, ICarrierGatewayFactory carrierFactory, TimeProvider clock)
{
    public async Task<IReadOnlyList<ShippingRate>> QuoteAllCarriersAsync(ShippingQuoteRequest request, CancellationToken ct = default)
    {
        var settings = await db.CarrierSettings.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        var tasks = settings.Select(async setting =>
        {
            var client = await carrierFactory.ResolveAsync(setting.Code, setting.DisplayName, ct);
            try
            {
                return await client.QuoteAsync(new RateRequest(request.ShipTo, request.Subtotal, request.TotalWeightGrams), setting, ct);
            }
            catch (Exception)
            {
                // A carrier outage must not take the storefront down; skip and log.
                return new List<ShippingRate>();
            }
        });

        var all = await Task.WhenAll(tasks);
        return all.SelectMany(r => r).OrderBy(r => r.Cost).ToList();
    }

    public async Task<LabelResult> CreateLabelAsync(LabelRequest request, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null)
        {
            return new LabelResult(false, $"Order {request.OrderId} not found.", "", "", "", 0m);
        }

        var setting = await db.CarrierSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Code == request.Carrier, ct);
        var client = await carrierFactory.ResolveAsync(request.Carrier, request.CarrierName, ct);

        var declaredValue = order.Lines.Sum(l => l.LineTotal);
        var totalWeight = order.Lines.Count * 100; // demo weight; real code would sum variant weights
        var result = await client.CreateLabelAsync(request, order.ShipTo, declaredValue, setting ?? new CarrierSetting(), ct);

        if (!result.Succeeded)
        {
            return result;
        }

        var shipment = new Shipment
        {
            OrderId = order.Id,
            Carrier = request.Carrier,
            CarrierName = request.CarrierName ?? setting?.DisplayName ?? request.Carrier.ToString(),
            TrackingNumber = result.TrackingNumber,
            Status = ShipmentStatus.LabelCreated,
            LabelCost = result.Cost,
            LabelPdfBase64 = result.LabelPdfBase64,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        order.Shipments.Add(shipment);
        order.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = "Admin",
            Message = $"Shipping label created ({shipment.CarrierName} {shipment.TrackingNumber}).",
        });

        await db.SaveChangesAsync(ct);
        return result with { TrackingUrl = MockCarrierClient.BuildTrackingUrl(result.TrackingNumber, setting ?? new CarrierSetting()) };
    }

    /// <summary>Marks a shipment shipped: status, audit event, and reserved stock becomes sold.</summary>
    public async Task<bool> MarkShippedAsync(int shipmentId, CancellationToken ct = default)
    {
        var shipment = await db.Shipments.Include(s => s.Order).ThenInclude(o => o!.Lines).FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        if (shipment is null || shipment.Order is null || shipment.Status != ShipmentStatus.LabelCreated)
        {
            return false;
        }

        shipment.Status = ShipmentStatus.PickedUp;
        shipment.ShippedUtc = clock.GetUtcNow().UtcDateTime;

        if (shipment.Order.Status == OrderStatus.Paid)
        {
            shipment.Order.Status = OrderStatus.Shipped;
        }

        shipment.Order.Events.Add(new OrderEvent
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Actor = "Admin",
            Message = $"Shipment {shipment.TrackingNumber} marked shipped.",
        });

        foreach (var line in shipment.Order.Lines)
        {
            var inv = await db.Inventory.FirstOrDefaultAsync(i => i.ProductVariantId == line.ProductVariantId, ct);
            if (inv is not null)
            {
                inv.Reserved = Math.Max(0, inv.Reserved - line.Quantity);
                inv.OnHand = Math.Max(0, inv.OnHand - line.Quantity);
                inv.LastAdjustedUtc = clock.GetUtcNow().UtcDateTime;
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TrackingUpdate>> SyncTrackingAsync(int shipmentId, CancellationToken ct = default)
    {
        var shipment = await db.Shipments.Include(s => s.Order).FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        if (shipment is null)
        {
            return [];
        }

        var setting = await db.CarrierSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Code == shipment.Carrier, ct);
        var client = await carrierFactory.ResolveAsync(shipment.Carrier, shipment.CarrierName, ct);
        var updates = await client.GetTrackingAsync(shipment.TrackingNumber, setting ?? new CarrierSetting(), ct);

        var latest = updates.OrderBy(u => u.TimestampUtc).LastOrDefault();
        if (latest is not null)
        {
            shipment.Status = latest.Status;
            if (latest.Status == ShipmentStatus.Delivered)
            {
                shipment.DeliveredUtc = latest.TimestampUtc;
                if (shipment.Order is not null && shipment.Order.Status == OrderStatus.Shipped)
                {
                    shipment.Order.Status = OrderStatus.Delivered;
                }
            }

            shipment.Order?.Events.Add(new OrderEvent
            {
                TimestampUtc = clock.GetUtcNow().UtcDateTime,
                Actor = shipment.CarrierName ?? shipment.Carrier.ToString(),
                Message = $"Tracking: {latest.Status}{(latest.Description is null ? "" : $" - {latest.Description}")}",
            });

            await db.SaveChangesAsync(ct);
        }

        return updates;
    }
}
