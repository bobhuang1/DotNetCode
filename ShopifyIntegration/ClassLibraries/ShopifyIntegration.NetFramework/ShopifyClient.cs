#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShopifyIntegration.NetFramework
{
    /// <summary>
    /// A thin, generic wrapper over the Shopify Admin REST API's most common
    /// resources (customers, orders, draft orders, products) for CRUD operations -
    /// no SDK dependency, no Shopify-specific object model. Request and response
    /// bodies are passed through as raw JSON strings, exactly as Shopify's REST API
    /// defines them (see https://shopify.dev/docs/api/admin-rest), so this client
    /// stays generic instead of reimplementing Shopify's schema.
    ///
    /// Authenticates with a static Admin API access token, the credential a custom
    /// app (Settings -&gt; Apps and sales channels -&gt; Develop apps) issues in the
    /// Shopify admin - no OAuth authorization-code flow is implemented here, since
    /// that's for building a public app installable by other merchants, out of
    /// scope for this generic sample.
    /// </summary>
    public sealed class ShopifyClient
    {
        private const string DefaultApiVersion = "2025-01";

        private readonly HttpClient _httpClient;
        private readonly string _shopDomain;
        private readonly string _accessToken;
        private readonly string _apiVersion;

        static ShopifyClient()
        {
            // TLS 1.2 is not always the OS/.NET Framework default on older machines -
            // set it explicitly once, for every instance of this client in the process.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        /// <param name="shopDomain">e.g. "your-store.myshopify.com".</param>
        /// <param name="accessToken">A custom app's Admin API access token.</param>
        /// <param name="apiVersion">A Shopify API version, e.g. "2025-01". Defaults to a recent stable version - Shopify versions release quarterly, so pin the one you've tested against.</param>
        /// <param name="httpClient">
        /// Optional. Pass a shared, long-lived <see cref="HttpClient"/> if your app already
        /// has one (recommended - see README); otherwise a private one is created for you.
        /// </param>
        public ShopifyClient(string shopDomain, string accessToken, string apiVersion = DefaultApiVersion, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(shopDomain)) throw new ArgumentException("A shop domain is required.", nameof(shopDomain));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("An access token is required.", nameof(accessToken));

            _shopDomain = shopDomain.Trim();
            _accessToken = accessToken;
            _apiVersion = string.IsNullOrWhiteSpace(apiVersion) ? DefaultApiVersion : apiVersion;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>GET the resource collection. Pass Shopify list filters (e.g. "limit=50&amp;status=any") as <paramref name="queryString"/>.</summary>
        public Task<ShopifyApiResult> ListAsync(ShopifyResource resource, string? queryString = null, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Get, BuildUrl(resource, null, queryString), null, cancellationToken);

        /// <summary>GET a single resource by id.</summary>
        public Task<ShopifyApiResult> GetAsync(ShopifyResource resource, long id, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Get, BuildUrl(resource, id, null), null, cancellationToken);

        /// <summary>POST to create a resource. <paramref name="jsonBody"/> must be the full Shopify-shaped envelope, e.g. {"customer": {...}}.</summary>
        public Task<ShopifyApiResult> CreateAsync(ShopifyResource resource, string jsonBody, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Post, BuildUrl(resource, null, null), jsonBody, cancellationToken);

        /// <summary>
        /// Update a resource. Sent to Shopify as an HTTP PUT (Shopify's REST Admin
        /// API has no PATCH verb) - exposed here as "Update" rather than "Patch" so
        /// the method name doesn't imply an HTTP verb this client doesn't use.
        /// </summary>
        public Task<ShopifyApiResult> UpdateAsync(ShopifyResource resource, long id, string jsonBody, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Put, BuildUrl(resource, id, null), jsonBody, cancellationToken);

        /// <summary>DELETE a resource by id.</summary>
        public Task<ShopifyApiResult> DeleteAsync(ShopifyResource resource, long id, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Delete, BuildUrl(resource, id, null), null, cancellationToken);

        private async Task<ShopifyApiResult> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("X-Shopify-Access-Token", _accessToken);
                    if (jsonBody != null)
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    return request;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return new ShopifyApiResult { IsSuccess = false, StatusCode = 0, ErrorMessage = "Request to Shopify failed: " + ex.Message };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ShopifyApiResult
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Body = body,
                    ErrorMessage = response.IsSuccessStatusCode ? null : string.Format("Shopify returned HTTP {0}.", (int)response.StatusCode)
                };
            }
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
        {
            const int maxAttempts = 2;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                HttpResponseMessage response;
                using (var request = requestFactory())
                {
                    response = await _httpClient.SendAsync(request, cancellationToken);
                }

                if (response.StatusCode != (HttpStatusCode)429 || attempt == maxAttempts)
                    return response;

                var delay = response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue
                    ? response.Headers.RetryAfter.Delta.Value
                    : TimeSpan.FromSeconds(2);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }

            throw new InvalidOperationException("Shopify retry loop exited without a response.");
        }

        private string BuildUrl(ShopifyResource resource, long? id, string? queryString)
        {
            var segment = ToPathSegment(resource);
            var path = id.HasValue ? segment + "/" + id.Value : segment;
            var url = string.Format("https://{0}/admin/api/{1}/{2}.json", _shopDomain, _apiVersion, path);
            if (string.IsNullOrEmpty(queryString)) return url;
            return url + "?" + queryString!.TrimStart('?');
        }

        private static string ToPathSegment(ShopifyResource resource)
        {
            switch (resource)
            {
                case ShopifyResource.Customers: return "customers";
                case ShopifyResource.Orders: return "orders";
                case ShopifyResource.DraftOrders: return "draft_orders";
                case ShopifyResource.Products: return "products";
                default: throw new ArgumentOutOfRangeException(nameof(resource));
            }
        }
    }
}
