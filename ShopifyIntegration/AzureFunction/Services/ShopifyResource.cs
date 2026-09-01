#nullable enable

namespace ShopifyIntegration.AzureFunction.Services
{
    /// <summary>
    /// The Shopify Admin REST resources this proxy supports. All four follow the
    /// same flat <c>/admin/api/{version}/{resource}.json</c> URL shape - other
    /// resources (e.g. resources nested under a parent, like an order's
    /// fulfillments) are out of scope for this generic proxy.
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

    public static class ShopifyResourceRouting
    {
        // The URL path segment {resource} callers use, mapped to the enum - and
        // (being the same dictionary) the full list of resources this proxy accepts.
        private static readonly Dictionary<string, ShopifyResource> BySegment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = ShopifyResource.Customers,
            ["orders"] = ShopifyResource.Orders,
            ["draft_orders"] = ShopifyResource.DraftOrders,
            ["products"] = ShopifyResource.Products
        };

        public static bool TryParse(string? segment, out ShopifyResource resource) =>
            BySegment.TryGetValue(segment ?? string.Empty, out resource);

        public static string ToSegment(ShopifyResource resource) => resource switch
        {
            ShopifyResource.Customers => "customers",
            ShopifyResource.Orders => "orders",
            ShopifyResource.DraftOrders => "draft_orders",
            ShopifyResource.Products => "products",
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        public static string SupportedSegmentsForErrorMessage() => string.Join(", ", BySegment.Keys);
    }
}
