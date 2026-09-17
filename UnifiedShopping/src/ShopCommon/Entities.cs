namespace ShopCommon;

public sealed class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>For OEM scarves: fabric composition, e.g. "100% mulberry silk, 16 momme".</summary>
    public string Material { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }

    /// <summary>List price shown struck-through when higher than <see cref="Price"/>.</summary>
    public decimal? CompareAtPrice { get; set; }
    public int WeightGrams { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];
}

public sealed class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Full sellable SKU, e.g. "SCF-SILK90-SCARLET-OS".</summary>
    public string Sku { get; set; } = "";
    public string Color { get; set; } = "";
    public string Size { get; set; } = "";

    /// <summary>0 means "use the parent product's price".</summary>
    public decimal PriceOverride { get; set; }

    public InventoryItem? Inventory { get; set; }
}

public sealed class InventoryItem
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int OnHand { get; set; }

    /// <summary>Units held by unpaid/pending orders; reduced when orders ship or cancel.</summary>
    public int Reserved { get; set; }
    public int ReorderPoint { get; set; }
    public DateTime LastAdjustedUtc { get; set; }
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public DateTime CreatedUtc { get; set; }

    public List<Order> Orders { get; set; } = [];
}

public sealed class Address
{
    public string Name { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string? Line2 { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "US";
    public string? Phone { get; set; }
}

public sealed class Order
{
    public int Id { get; set; }

    /// <summary>Human-readable number shown to customers, e.g. "SO-2026-000123".</summary>
    public string OrderNumber { get; set; } = "";
    public int? CustomerId { get; set; }
    public string CustomerEmail { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public OrderChannel Channel { get; set; }

    public Address ShipTo { get; set; } = new();

    public List<OrderLine> Lines { get; set; } = [];

    public string? CouponCode { get; set; }
    public decimal? CouponPercentOff { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public PaymentRecord? Payment { get; set; }
    public List<Shipment> Shipments { get; set; } = [];
    public List<OrderEvent> Events { get; set; } = [];
    public ReturnRequest? Return { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Price/name are snapshotted at purchase time; the catalog can change afterwards.</summary>
public sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductVariantId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PaymentRecord
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Gateway reference: Stripe PaymentIntent id, PayPal order id, etc.</summary>
    public string? ProviderReference { get; set; }
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime? CapturedUtc { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public CarrierCode Carrier { get; set; }

    /// <summary>Display name, required when <see cref="Carrier"/> is <see cref="CarrierCode.Custom"/>.</summary>
    public string? CarrierName { get; set; }
    public string TrackingNumber { get; set; } = "";
    public ShipmentStatus Status { get; set; } = ShipmentStatus.LabelCreated;
    public decimal LabelCost { get; set; }
    public DateTime? ShippedUtc { get; set; }
    public DateTime? DeliveredUtc { get; set; }

    /// <summary>Base64 PDF of the label as returned by the carrier client.</summary>
    public string? LabelPdfBase64 { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Append-only audit trail rendered in the admin order timeline.</summary>
public sealed class OrderEvent
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Actor { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class Coupon
{
    public int Id { get; set; }

    /// <summary>Exactly 16 alphanumeric characters from the unambiguous alphabet.</summary>
    public string Code { get; set; } = "";
    public decimal PercentOff { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresUtc { get; set; }
    public int? MaxRedemptions { get; set; }
    public int TimesUsed { get; set; }
    public decimal? MinSubtotal { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ReturnRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public ReturnResolution Resolution { get; set; } = ReturnResolution.Refund;
    public string Reason { get; set; } = "";
    public string? AdminNotes { get; set; }
    public int QuantityReturned { get; set; }
    public decimal RefundAmount { get; set; }
    public bool RestockReturnedItems { get; set; }

    /// <summary>Carrier claim/case number for lost-package or insurance claims.</summary>
    public string? ClaimReference { get; set; }
    public DateTime RequestedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Admin-managed carrier credentials/behavior; custom carriers are just rows here.</summary>
public sealed class CarrierSetting
{
    public int Id { get; set; }
    public CarrierCode Code { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;

    /// <summary>Base URL of the carrier's REST API; blank means use the built-in mock.</summary>
    public string? ApiBaseUrl { get; set; }
    public string? AccountNumber { get; set; }

    /// <summary>Template with {0} for the tracking number, e.g. "https://www.ups.com/track?tracknum={0}".</summary>
    public string? TrackingUrlTemplate { get; set; }

    /// <summary>Flat fallback rate per shipment when no live API is configured.</summary>
    public decimal FallbackBaseRate { get; set; }
    public decimal FallbackPerKgRate { get; set; }
}
