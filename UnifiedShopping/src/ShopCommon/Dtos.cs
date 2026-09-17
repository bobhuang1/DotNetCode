namespace ShopCommon;

/// <summary>A customer's cart as sent by the web storefront or the MAUI app.</summary>
public sealed class Cart
{
    public List<CartItem> Items { get; set; } = [];
    public string? CouponCode { get; set; }

    /// <summary>Applied coupon description when <see cref="CouponCode"/> validated; error otherwise.</summary>
    public string? CouponMessage { get; set; }
    public CartTotals Totals { get; set; } = new();
}

public sealed class CartItem
{
    public int VariantId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public string Size { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed class CartTotals
{
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CouponApplied { get; set; }
    public decimal? CouponPercentOff { get; set; }
    public int ItemCount { get; set; }
}

public sealed record CheckoutRequest(
    Cart Cart,
    Address ShipTo,
    PaymentProvider Provider,
    string? ReturnUrl = null,
    string? CancelUrl = null);

public sealed record CheckoutResult(
    bool Succeeded,
    string? Error,
    string? OrderNumber,
    OrderStatus Status,
    string? PaymentRedirectUrl,
    string? ProviderReference);

public sealed record ShippingRate(CarrierCode Carrier, string? CarrierName, string ServiceName, decimal Cost, int EstimatedDays);

public sealed record ShippingQuoteRequest(Address ShipTo, decimal Subtotal, int TotalWeightGrams);

public sealed record RateRequest(Address ShipTo, decimal Subtotal, int TotalWeightGrams);

public sealed record LabelRequest(int OrderId, CarrierCode Carrier, string? CarrierName);

public sealed record LabelResult(bool Succeeded, string? Error, string TrackingNumber, string TrackingUrl, string LabelPdfBase64, decimal Cost);

public sealed record TrackingUpdate(CarrierCode Carrier, string TrackingNumber, ShipmentStatus Status, string? Description, DateTime TimestampUtc);

public sealed record CouponGenerationOptions
{
    /// <summary>How many codes to generate in one batch.</summary>
    public int Count { get; init; } = 1;
    public decimal PercentOff { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    public int? MaxRedemptions { get; init; }
    public decimal? MinSubtotal { get; init; }

    /// <summary>Optional readable prefix; the generator fills the rest to 16 chars.</summary>
    public string? Prefix { get; init; }
}

/// <summary>Lightweight identity used by the sample auth seam; replace with real auth in production.</summary>
public sealed record AdminPrincipal(string UserName, bool IsAdmin);

public sealed record ProductSummary(
    int Id,
    string Sku,
    string Name,
    string Material,
    string? ImageUrl,
    decimal Price,
    decimal? CompareAtPrice,
    int TotalOnHand,
    IReadOnlyList<VariantSummary> Variants);

public sealed record VariantSummary(int VariantId, string Sku, string Color, string Size, decimal Price, int OnHand, bool InStock);

public sealed record PlaceOrderRequest(Cart Cart, Address ShipTo);
