#nullable enable
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SmartyStreetsLookup.AzureFunction.Models;
using SmartyStreetsLookup.AzureFunction.Services;

namespace SmartyStreetsLookup.AzureFunction.Functions
{
    /// <summary>POST /UsAddressLookup - validates a US address via the SmartyStreets US Street API.</summary>
    public class UsAddressLookupFunction(SmartyStreetsApiClient client, ILogger<UsAddressLookupFunction> logger)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        [Function("UsAddressLookup")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            CancellationToken cancellationToken)
        {
            var authId = Environment.GetEnvironmentVariable("SmartyStreetsUsAuthId");
            var authToken = Environment.GetEnvironmentVariable("SmartyStreetsUsAuthToken");
            if (string.IsNullOrWhiteSpace(authId) || string.IsNullOrWhiteSpace(authToken))
            {
                logger.LogError("SmartyStreetsUsAuthId / SmartyStreetsUsAuthToken are not configured.");
                return await WriteResponseAsync(req, HttpStatusCode.InternalServerError,
                    Failure("Server is missing SmartyStreets US credentials."));
            }

            AddressLookupRequest? request;
            try
            {
                var body = await req.ReadAsStringAsync() ?? string.Empty;
                request = JsonSerializer.Deserialize<AddressLookupRequest>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unable to parse request body.");
                return await WriteResponseAsync(req, HttpStatusCode.BadRequest, Failure("Request body is not valid JSON."));
            }

            if (request is null || string.IsNullOrWhiteSpace(request.AddressLine1))
            {
                return await WriteResponseAsync(req, HttpStatusCode.BadRequest, Failure("AddressLine1 is required."));
            }

            try
            {
                var result = await client.LookupUsAsync(request, authId, authToken, cancellationToken);
                return await WriteResponseAsync(req, HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "US address lookup failed unexpectedly.");
                return await WriteResponseAsync(req, HttpStatusCode.InternalServerError,
                    Failure("Unexpected error during address lookup."));
            }
        }

        private static AddressLookupResult Failure(string message) => new() { IsValid = false, Message = message };

        private static async Task<HttpResponseData> WriteResponseAsync(
            HttpRequestData req, HttpStatusCode statusCode, AddressLookupResult result)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
            return response;
        }
    }
}
