#nullable enable
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MicrosoftCrmIntegration.AzureFunction.Services;

namespace MicrosoftCrmIntegration.AzureFunction.Functions
{
    /// <summary>
    /// Five generic HTTP endpoints proxying CRUD calls to the Microsoft Dataverse
    /// Web API for whichever table the URL names (accounts, contacts, incidents,
    /// products, pricelevels, or any custom table). See the README for the full
    /// endpoint list and request/response shapes. Request/response bodies are
    /// Dataverse's own JSON, passed through unmodified in both directions.
    /// </summary>
    public class DataverseProxyFunctions(DataverseProxyClient client, ILogger<DataverseProxyFunctions> logger)
    {
        private const string DefaultApiVersion = "v9.2";

        [Function("DataverseList")]
        public Task<HttpResponseData> ListAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/{entitySet}")] HttpRequestData req,
            string entitySet,
            CancellationToken cancellationToken) =>
            RunAsync(req, creds => client.ListAsync(creds, entitySet, req.Url.Query.TrimStart('?'), cancellationToken));

        [Function("DataverseGetById")]
        public Task<HttpResponseData> GetByIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/{entitySet}/{id}")] HttpRequestData req,
            string entitySet,
            string id,
            CancellationToken cancellationToken) =>
            RunWithIdAsync(req, id, (creds, guid) => client.GetAsync(creds, entitySet, guid, req.Url.Query.TrimStart('?'), cancellationToken));

        [Function("DataverseCreate")]
        public async Task<HttpResponseData> CreateAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/{entitySet}")] HttpRequestData req,
            string entitySet,
            CancellationToken cancellationToken)
        {
            var body = await req.ReadAsStringAsync() ?? string.Empty;
            return await RunAsync(req, creds => client.CreateAsync(creds, entitySet, body, cancellationToken));
        }

        [Function("DataverseUpdate")]
        public async Task<HttpResponseData> UpdateAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "dataverse/{entitySet}/{id}")] HttpRequestData req,
            string entitySet,
            string id,
            CancellationToken cancellationToken)
        {
            var body = await req.ReadAsStringAsync() ?? string.Empty;
            return await RunWithIdAsync(req, id, (creds, guid) => client.UpdateAsync(creds, entitySet, guid, body, cancellationToken));
        }

        [Function("DataverseDelete")]
        public Task<HttpResponseData> DeleteAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "dataverse/{entitySet}/{id}")] HttpRequestData req,
            string entitySet,
            string id,
            CancellationToken cancellationToken) =>
            RunWithIdAsync(req, id, (creds, guid) => client.DeleteAsync(creds, entitySet, guid, cancellationToken));

        private Task<HttpResponseData> RunWithIdAsync(HttpRequestData req, string id, Func<DataverseCredentials, Guid, Task<DataverseProxyResult>> call)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return WriteErrorAsync(req, HttpStatusCode.BadRequest, $"'{id}' is not a valid record id (expected a GUID).");
            }

            return RunAsync(req, creds => call(creds, guid));
        }

        private async Task<HttpResponseData> RunAsync(HttpRequestData req, Func<DataverseCredentials, Task<DataverseProxyResult>> call)
        {
            var resourceUrl = Environment.GetEnvironmentVariable("DataverseUrl");
            var tenantId = Environment.GetEnvironmentVariable("DataverseTenantId");
            var clientId = Environment.GetEnvironmentVariable("DataverseClientId");
            var clientSecret = Environment.GetEnvironmentVariable("DataverseClientSecret");
            var apiVersion = Environment.GetEnvironmentVariable("DataverseApiVersion");

            if (string.IsNullOrWhiteSpace(resourceUrl) || string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                logger.LogError("DataverseUrl / DataverseTenantId / DataverseClientId / DataverseClientSecret are not fully configured.");
                return await WriteErrorAsync(req, HttpStatusCode.InternalServerError, "Server is missing Dataverse credentials.");
            }

            if (string.IsNullOrWhiteSpace(apiVersion))
                apiVersion = DefaultApiVersion;

            var creds = new DataverseCredentials(resourceUrl, tenantId, clientId, clientSecret, apiVersion);

            try
            {
                var result = await call(creds);

                if (!result.ReachedDataverse)
                {
                    return await WriteErrorAsync(req, HttpStatusCode.BadGateway, "Request to Dataverse failed.");
                }

                // Pass Dataverse's own status code and JSON body straight through -
                // including its own 4xx/5xx errors, which are meaningful to the caller
                // (e.g. a 400 with Dataverse's field-level validation message).
                var response = req.CreateResponse((HttpStatusCode)result.StatusCode);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(string.IsNullOrEmpty(result.Body) ? "{}" : result.Body);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error calling Dataverse.");
                return await WriteErrorAsync(req, HttpStatusCode.InternalServerError, "Unexpected error calling Dataverse.");
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
