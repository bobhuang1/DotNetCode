namespace ShopifyIntegration.NetCore;

/// <summary>
/// The Shopify Admin REST resources this client supports. All four follow the
/// same flat <c>/admin/api/{version}/{resource}.json</c> URL shape - other
/// resources (e.g. resources nested under a parent, like an order's
/// fulfillments) are out of scope for this generic client.
/// </summary>
public enum ShopifyResource
{
    Customers,
    Orders,

    /// <summary>
    /// Shopify's closest equivalent to a standalone "invoice": a draft order can
    /// be built up and then emailed to a customer as a payable invoice.
    /// </summary>
    DraftOrders,
    Products
}
