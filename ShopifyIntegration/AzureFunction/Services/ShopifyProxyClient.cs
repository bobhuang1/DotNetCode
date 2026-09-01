#nullable enable
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShopifyIntegration.AzureFunction.Services
{
    /// <summary>
    /// The outcome of one call to the Shopify Admin REST API, forwarded through
    /// almost verbatim - this proxy does not parse or reshape Shopify's JSON.
    /// </summary>
    public sealed class ShopifyProxyResult
    {
        public bool ReachedShopify { get; init; }
        public int StatusCode { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    /// <summary>
    /// Forwards CRUD calls to the Shopify Admin REST API for whichever resource the
    /// caller specifies. Request and response bodies are passed through as raw JSON,
    /// exactly as Shopify's REST API defines them (see
    /// https://shopify.dev/docs/api/admin-rest) - this proxy does not model
    /// Shopify's schema itself, so it stays generic instead of reimplementing it.
    /// </summary>
    public sealed class ShopifyProxyClient(IHttpClientFactory httpClientFactory, ILogger<ShopifyProxyClient> logger)
    {
        public const string HttpClientName = "Shopify";

        public Task<ShopifyProxyResult> ListAsync(
            string shopDomain, string apiVersion, string accessToken, ShopifyResource resource, string? queryString, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Get, BuildUrl(shopDomain, apiVersion, resource, null, queryString), accessToken, null, cancellationToken);

        public Task<ShopifyProxyResult> GetAsync(
            string shopDomain, string apiVersion, string accessToken, ShopifyResource resource, string id, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Get, BuildUrl(shopDomain, apiVersion, resource, id, null), accessToken, null, cancellationToken);

        public Task<ShopifyProxyResult> CreateAsync(
            string shopDomain, string apiVersion, string accessToken, ShopifyResource resource, string jsonBody, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Post, BuildUrl(shopDomain, apiVersion, resource, null, null), accessToken, jsonBody, cancellationToken);

        // Shopify's REST Admin API has no PATCH verb; an update is sent as PUT.
        public Task<ShopifyProxyResult> UpdateAsync(
            string shopDomain, string apiVersion, string accessToken, ShopifyResource resource, string id, string jsonBody, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Put, BuildUrl(shopDomain, apiVersion, resource, id, null), accessToken, jsonBody, cancellationToken);

        public Task<ShopifyProxyResult> DeleteAsync(
            string shopDomain, string apiVersion, string accessToken, ShopifyResource resource, string id, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Delete, BuildUrl(shopDomain, apiVersion, resource, id, null), accessToken, null, cancellationToken);

        private async Task<ShopifyProxyResult> SendAsync(HttpMethod method, string url, string accessToken, string? jsonBody, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("X-Shopify-Access-Token", accessToken);
                    if (jsonBody is not null)
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    return request;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Request to Shopify failed.");
                return new ShopifyProxyResult { ReachedShopify = false, StatusCode = 0, Body = string.Empty };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ShopifyProxyResult
                {
                    ReachedShopify = true,
                    StatusCode = (int)response.StatusCode,
                    Body = body
                };
            }
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
        {
            const int maxAttempts = 2;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var client = httpClientFactory.CreateClient(HttpClientName);
                HttpResponseMessage response;
                using (var request = requestFactory())
                {
                    response = await client.SendAsync(request, cancellationToken);
                }

                if (response.StatusCode != (HttpStatusCode)429 || attempt == maxAttempts)
                    return response;

                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                logger.LogWarning("Shopify rate-limited (429). Retrying in {Delay}.", delay);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }

            throw new InvalidOperationException("Shopify retry loop exited without a response.");
        }

        private static string BuildUrl(string shopDomain, string apiVersion, ShopifyResource resource, string? id, string? queryString)
        {
            var segment = ShopifyResourceRouting.ToSegment(resource);
            var path = string.IsNullOrEmpty(id) ? segment : $"{segment}/{id}";
            var url = $"https://{shopDomain}/admin/api/{apiVersion}/{path}.json";
            return string.IsNullOrEmpty(queryString) ? url : $"{url}?{queryString.TrimStart('?')}";
        }
    }
}
