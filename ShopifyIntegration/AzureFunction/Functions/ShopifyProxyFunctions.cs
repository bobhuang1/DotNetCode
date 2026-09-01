#nullable enable
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ShopifyIntegration.AzureFunction.Services;

namespace ShopifyIntegration.AzureFunction.Functions
{
    /// <summary>
    /// Five generic HTTP endpoints proxying CRUD calls to the Shopify Admin REST API
    /// for whichever resource the URL names (customers, orders, draft_orders,
    /// products). See the README for the full endpoint list and request/response
    /// shapes. Request/response bodies are Shopify's own JSON, passed through
    /// unmodified in both directions.
    /// </summary>
    public class ShopifyProxyFunctions(ShopifyProxyClient client, ILogger<ShopifyProxyFunctions> logger)
    {
        private const string DefaultApiVersion = "2025-01";

        [Function("ShopifyList")]
        public Task<HttpResponseData> ListAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shopify/{resource}")] HttpRequestData req,
            string resource,
            CancellationToken cancellationToken) =>
            RunAsync(req, resource, (creds, res) =>
                client.ListAsync(creds.Domain, creds.ApiVersion, creds.Token, res, req.Url.Query.TrimStart('?'), cancellationToken));

        [Function("ShopifyGetById")]
        public Task<HttpResponseData> GetByIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shopify/{resource}/{id}")] HttpRequestData req,
            string resource,
            string id,
            CancellationToken cancellationToken) =>
            RunAsync(req, resource, (creds, res) =>
                client.GetAsync(creds.Domain, creds.ApiVersion, creds.Token, res, id, cancellationToken));

        [Function("ShopifyCreate")]
        public async Task<HttpResponseData> CreateAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "shopify/{resource}")] HttpRequestData req,
            string resource,
            CancellationToken cancellationToken)
        {
            var body = await req.ReadAsStringAsync() ?? string.Empty;
            return await RunAsync(req, resource, (creds, res) =>
                client.CreateAsync(creds.Domain, creds.ApiVersion, creds.Token, res, body, cancellationToken));
        }

        // Shopify's REST Admin API has no PATCH verb - PATCH is exposed here because
        // it's the generic HTTP verb for a partial update, and internally forwarded
        // to Shopify as PUT (see README, "Why PATCH here calls PUT on Shopify").
        [Function("ShopifyUpdate")]
        public async Task<HttpResponseData> UpdateAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "shopify/{resource}/{id}")] HttpRequestData req,
            string resource,
            string id,
            CancellationToken cancellationToken)
        {
            var body = await req.ReadAsStringAsync() ?? string.Empty;
            return await RunAsync(req, resource, (creds, res) =>
                client.UpdateAsync(creds.Domain, creds.ApiVersion, creds.Token, res, id, body, cancellationToken));
        }

        [Function("ShopifyDelete")]
        public Task<HttpResponseData> DeleteAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "shopify/{resource}/{id}")] HttpRequestData req,
            string resource,
            string id,
            CancellationToken cancellationToken) =>
            RunAsync(req, resource, (creds, res) =>
                client.DeleteAsync(creds.Domain, creds.ApiVersion, creds.Token, res, id, cancellationToken));

        private readonly record struct ShopifyCredentials(string Domain, string Token, string ApiVersion);

        private async Task<HttpResponseData> RunAsync(
            HttpRequestData req, string resourceSegment, Func<ShopifyCredentials, ShopifyResource, Task<ShopifyProxyResult>> call)
        {
            if (!ShopifyResourceRouting.TryParse(resourceSegment, out var resource))
            {
                return await WriteErrorAsync(req, HttpStatusCode.BadRequest,
                    $"Unsupported resource '{resourceSegment}'. Supported: {ShopifyResourceRouting.SupportedSegmentsForErrorMessage()}.");
            }

            var domain = Environment.GetEnvironmentVariable("ShopifyShopDomain");
            var token = Environment.GetEnvironmentVariable("ShopifyAccessToken");
            var apiVersion = Environment.GetEnvironmentVariable("ShopifyApiVersion");

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(token))
            {
                logger.LogError("ShopifyShopDomain / ShopifyAccessToken are not configured.");
                return await WriteErrorAsync(req, HttpStatusCode.InternalServerError, "Server is missing Shopify credentials.");
            }

            if (string.IsNullOrWhiteSpace(apiVersion))
                apiVersion = DefaultApiVersion;

            try
            {
                var result = await call(new ShopifyCredentials(domain, token, apiVersion), resource);

                if (!result.ReachedShopify)
                {
                    return await WriteErrorAsync(req, HttpStatusCode.BadGateway, "Request to Shopify failed.");
                }

                // Pass Shopify's own status code and JSON body straight through -
                // including its own 4xx/5xx errors, which are meaningful to the caller
                // (e.g. a 422 with Shopify's field-level validation messages).
                var response = req.CreateResponse((HttpStatusCode)result.StatusCode);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(string.IsNullOrEmpty(result.Body) ? "{}" : result.Body);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error calling Shopify.");
                return await WriteErrorAsync(req, HttpStatusCode.InternalServerError, "Unexpected error calling Shopify.");
            }
        }

        private static async Task<HttpResponseData> WriteErrorAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync($"{{\"error\":\"{message.Replace("\"", "'")}\"}}");
            return response;
        }
    }
}
