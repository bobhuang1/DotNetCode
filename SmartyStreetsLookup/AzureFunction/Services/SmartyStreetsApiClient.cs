#nullable enable
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartyStreetsLookup.AzureFunction.Models;

namespace SmartyStreetsLookup.AzureFunction.Services
{
    /// <summary>
    /// Thin wrapper over SmartyStreets' US Street and International Street REST APIs
    /// (called directly over HTTP - no SmartyStreets SDK dependency). Retries once on
    /// HTTP 429 (rate limited); any other non-success status or exception is reported
    /// back as a failed <see cref="AddressLookupResult"/> rather than thrown, so the
    /// calling function can always return a well-formed JSON response.
    /// </summary>
    public sealed class SmartyStreetsApiClient(IHttpClientFactory httpClientFactory, ILogger<SmartyStreetsApiClient> logger)
    {
        public const string HttpClientName = "SmartyStreets";

        private const string UsBaseUrl = "https://us-street.api.smarty.com/street-address";
        private const string InternationalBaseUrl = "https://international-street.api.smarty.com/verify";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<AddressLookupResult> LookupUsAsync(
            AddressLookupRequest request, string authId, string authToken, CancellationToken cancellationToken)
        {
            var query = BuildQueryString(new Dictionary<string, string?>
            {
                ["auth-id"] = authId,
                ["auth-token"] = authToken,
                ["street"] = request.AddressLine1,
                ["street2"] = request.AddressLine2,
                ["city"] = request.City,
                ["state"] = request.State,
                ["zipcode"] = request.PostalCode
            });

            HttpResponseMessage response;
            try
            {
                response = await GetWithRetryAsync($"{UsBaseUrl}?{query}", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SmartyStreets US lookup request failed.");
                return new AddressLookupResult { IsValid = false, Message = "Request to SmartyStreets failed." };
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("SmartyStreets US lookup returned {StatusCode}.", response.StatusCode);
                    return new AddressLookupResult
                    {
                        IsValid = false,
                        Message = $"SmartyStreets US lookup failed with status {(int)response.StatusCode}."
                    };
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var candidates = JsonSerializer.Deserialize<List<UsCandidateDto>>(json, JsonOptions) ?? [];

                if (candidates.Count == 0)
                {
                    return new AddressLookupResult
                    {
                        IsValid = false,
                        Message = "No US candidates found.",
                        CompanyName = request.CompanyName,
                        AddressLine1 = request.AddressLine1,
                        AddressLine2 = request.AddressLine2,
                        City = request.City,
                        State = request.State,
                        PostalCode = request.PostalCode,
                        Country = "US"
                    };
                }

                var c = candidates[0];
                var zip = c.Components?.ZipCode ?? string.Empty;
                var plus4 = c.Components?.Plus4Code;

                return new AddressLookupResult
                {
                    IsValid = true,
                    Message = $"Found {candidates.Count} US candidate(s).",
                    CandidateCount = candidates.Count,
                    CompanyName = request.CompanyName,
                    AddressLine1 = c.DeliveryLine1 ?? string.Empty,
                    AddressLine2 = c.DeliveryLine2 ?? string.Empty,
                    City = c.Components?.CityName ?? string.Empty,
                    State = c.Components?.State ?? string.Empty,
                    PostalCode = string.IsNullOrEmpty(plus4) ? zip : $"{zip}-{plus4}",
                    Country = "US"
                };
            }
        }

        public async Task<AddressLookupResult> LookupInternationalAsync(
            AddressLookupRequest request, string authId, string authToken, CancellationToken cancellationToken)
        {
            var query = BuildQueryString(new Dictionary<string, string?>
            {
                ["auth-id"] = authId,
                ["auth-token"] = authToken,
                ["country"] = request.Country,
                ["address1"] = request.AddressLine1,
                ["address2"] = request.AddressLine2,
                ["locality"] = request.City,
                ["administrative_area"] = request.State,
                ["postal_code"] = request.PostalCode
            });

            HttpResponseMessage response;
            try
            {
                response = await GetWithRetryAsync($"{InternationalBaseUrl}?{query}", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SmartyStreets International lookup request failed.");
                return new AddressLookupResult { IsValid = false, Message = "Request to SmartyStreets failed." };
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("SmartyStreets International lookup returned {StatusCode}.", response.StatusCode);
                    return new AddressLookupResult
                    {
                        IsValid = false,
                        Message = $"SmartyStreets International lookup failed with status {(int)response.StatusCode}."
                    };
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var candidates = JsonSerializer.Deserialize<List<InternationalCandidateDto>>(json, JsonOptions) ?? [];

                if (candidates.Count == 0)
                {
                    return new AddressLookupResult
                    {
                        IsValid = false,
                        Message = "No international candidates found.",
                        CompanyName = request.CompanyName,
                        AddressLine1 = request.AddressLine1,
                        AddressLine2 = request.AddressLine2,
                        City = request.City,
                        State = request.State,
                        PostalCode = request.PostalCode,
                        Country = request.Country
                    };
                }

                var c = candidates[0];

                return new AddressLookupResult
                {
                    IsValid = true,
                    Message = $"Found {candidates.Count} international candidate(s).",
                    CandidateCount = candidates.Count,
                    CompanyName = request.CompanyName,
                    AddressLine1 = c.Address1 ?? string.Empty,
                    AddressLine2 = c.Address2 ?? string.Empty,
                    City = c.Components?.Locality ?? string.Empty,
                    State = c.Components?.AdministrativeArea ?? string.Empty,
                    PostalCode = c.Components?.PostalCode ?? string.Empty,
                    // SmartyStreets' International Street API returns the resolved country as its
                    // 3-letter ISO code (e.g. "GBR"), not the 2-letter code the request came in as.
                    Country = c.Components?.CountryIso3 ?? request.Country
                };
            }
        }

        private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken cancellationToken)
        {
            const int maxAttempts = 2;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var client = httpClientFactory.CreateClient(HttpClientName);
                var response = await client.GetAsync(url, cancellationToken);

                if (response.StatusCode != (HttpStatusCode)429 || attempt == maxAttempts)
                {
                    return response;
                }

                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                logger.LogWarning("SmartyStreets rate-limited (429). Retrying in {Delay}.", delay);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }

            // Unreachable: the loop above always returns on its final attempt.
            throw new InvalidOperationException("SmartyStreets retry loop exited without a response.");
        }

        private static string BuildQueryString(Dictionary<string, string?> parameters) =>
            string.Join("&", parameters
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        private sealed class UsCandidateDto
        {
            [JsonPropertyName("delivery_line_1")]
            public string? DeliveryLine1 { get; set; }

            [JsonPropertyName("delivery_line_2")]
            public string? DeliveryLine2 { get; set; }

            [JsonPropertyName("components")]
            public UsComponentsDto? Components { get; set; }
        }

        private sealed class UsComponentsDto
        {
            [JsonPropertyName("city_name")]
            public string? CityName { get; set; }

            [JsonPropertyName("state_abbreviation")]
            public string? State { get; set; }

            [JsonPropertyName("zipcode")]
            public string? ZipCode { get; set; }

            [JsonPropertyName("plus4_code")]
            public string? Plus4Code { get; set; }
        }

        private sealed class InternationalCandidateDto
        {
            [JsonPropertyName("address1")]
            public string? Address1 { get; set; }

            [JsonPropertyName("address2")]
            public string? Address2 { get; set; }

            [JsonPropertyName("components")]
            public InternationalComponentsDto? Components { get; set; }
        }

        private sealed class InternationalComponentsDto
        {
            [JsonPropertyName("locality")]
            public string? Locality { get; set; }

            [JsonPropertyName("administrative_area")]
            public string? AdministrativeArea { get; set; }

            [JsonPropertyName("postal_code")]
            public string? PostalCode { get; set; }

            [JsonPropertyName("country_iso_3")]
            public string? CountryIso3 { get; set; }
        }
    }
}
