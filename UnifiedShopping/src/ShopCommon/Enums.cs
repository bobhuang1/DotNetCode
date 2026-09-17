namespace ShopCommon;

/// <summary>Lifecycle of an order. Payments sit before fulfillment; returns sit after.</summary>
public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Processing = 2,
    PartiallyShipped = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Refunded = 7,
    Closed = 8,
}

public enum PaymentProvider
{
    Stripe = 0,
    PayPal = 1,

    /// <summary>Admin-recorded payment (phone/check/wire) - no gateway involved.</summary>
    Manual = 2,
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5,
}

/// <summary>Well-known carriers. <see cref="Custom"/> covers any admin-configured carrier.</summary>
public enum CarrierCode
{
    Ups = 0,
    FedEx = 1,
    Usps = 2,
    Custom = 3,
}

public enum ShipmentStatus
{
    LabelCreated = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Exception = 5,
    Lost = 6,
}

public enum ReturnStatus
{
    Requested = 0,
    Approved = 1,
    Received = 2,
    Inspected = 3,
    Completed = 4,
    Rejected = 5,
    LostClaim = 6,
    InsuranceClaimFiled = 7,
}

public enum ReturnResolution
{
    Refund = 0,
    Replace = 1,
}

/// <summary>Which front end placed the order - useful for analytics and support.</summary>
public enum OrderChannel
{
    Web = 0,
    Mobile = 1,
    Admin = 2,
}
