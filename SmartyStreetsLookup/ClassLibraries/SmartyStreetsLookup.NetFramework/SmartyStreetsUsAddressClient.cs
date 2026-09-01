#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartyStreetsLookup.NetFramework.Models;

namespace SmartyStreetsLookup.NetFramework
{
    /// <summary>
    /// Calls the SmartyStreets US Street API (https://www.smarty.com/docs/cloud/us-street-api)
    /// directly over HTTP - no SmartyStreets SDK dependency, no intermediary service.
    /// Intended to be referenced directly by an existing .NET Framework application.
    /// </summary>
    public sealed class SmartyStreetsUsAddressClient
    {
        private const string BaseUrl = "https://us-street.api.smarty.com/street-address";

        private readonly HttpClient _httpClient;
        private readonly string _authId;
        private readonly string _authToken;

        static SmartyStreetsUsAddressClient()
        {
            // TLS 1.2 is not always the OS/.NET Framework default on older machines -
            // set it explicitly once, for every instance of this client in the process.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        /// <param name="authId">Your SmartyStreets US Street API auth-id.</param>
        /// <param name="authToken">Your SmartyStreets US Street API auth-token.</param>
        /// <param name="httpClient">
        /// Optional. Pass a shared, long-lived <see cref="HttpClient"/> if your app already
        /// has one (recommended - see README); otherwise a private one is created for you.
        /// </param>
        public SmartyStreetsUsAddressClient(string authId, string authToken, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(authId)) throw new ArgumentException("An auth ID is required.", nameof(authId));
            if (string.IsNullOrWhiteSpace(authToken)) throw new ArgumentException("An auth token is required.", nameof(authToken));

            _authId = authId;
            _authToken = authToken;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<AddressLookupResult> LookupAsync(AddressLookupRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.AddressLine1))
                return new AddressLookupResult { IsValid = false, Message = "AddressLine1 is required." };

            var query = BuildQueryString(new Dictionary<string, string?>
            {
                ["auth-id"] = _authId,
                ["auth-token"] = _authToken,
                ["street"] = request.AddressLine1,
                ["street2"] = request.AddressLine2,
                ["city"] = request.City,
                ["state"] = request.State,
                ["zipcode"] = request.PostalCode
            });

            HttpResponseMessage response;
            try
            {
                response = await GetWithRetryAsync(BaseUrl + "?" + query, cancellationToken);
            }
            catch (Exception ex)
            {
                return new AddressLookupResult { IsValid = false, Message = "Request to SmartyStreets failed: " + ex.Message };
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return new AddressLookupResult
                    {
                        IsValid = false,
                        Message = string.Format("SmartyStreets US lookup failed with status {0}.", (int)response.StatusCode)
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                var candidates = JsonConvert.DeserializeObject<List<UsCandidateDto>>(json) ?? new List<UsCandidateDto>();

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
                var zip = c.Components != null ? c.Components.ZipCode : null;
                var plus4 = c.Components != null ? c.Components.Plus4Code : null;

                return new AddressLookupResult
                {
                    IsValid = true,
                    Message = string.Format("Found {0} US candidate(s).", candidates.Count),
                    CandidateCount = candidates.Count,
                    CompanyName = request.CompanyName,
                    AddressLine1 = c.DeliveryLine1 ?? string.Empty,
                    AddressLine2 = c.DeliveryLine2 ?? string.Empty,
                    City = c.Components != null ? (c.Components.CityName ?? string.Empty) : string.Empty,
                    State = c.Components != null ? (c.Components.State ?? string.Empty) : string.Empty,
                    PostalCode = string.IsNullOrEmpty(plus4) ? (zip ?? string.Empty) : zip + "-" + plus4,
                    Country = "US"
                };
            }
        }

        private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken cancellationToken)
        {
            const int maxAttempts = 2;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode != (HttpStatusCode)429 || attempt == maxAttempts)
                    return response;

                var delay = response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue
                    ? response.Headers.RetryAfter.Delta.Value
                    : TimeSpan.FromSeconds(2);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }

            throw new InvalidOperationException("SmartyStreets retry loop exited without a response.");
        }

        private static string BuildQueryString(Dictionary<string, string?> parameters)
        {
            var parts = parameters
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => Uri.EscapeDataString(kvp.Key) + "=" + Uri.EscapeDataString(kvp.Value!));
            return string.Join("&", parts);
        }

        private sealed class UsCandidateDto
        {
            [JsonProperty("delivery_line_1")]
            public string? DeliveryLine1 { get; set; }

            [JsonProperty("delivery_line_2")]
            public string? DeliveryLine2 { get; set; }

            [JsonProperty("components")]
            public UsComponentsDto? Components { get; set; }
        }

        private sealed class UsComponentsDto
        {
            [JsonProperty("city_name")]
            public string? CityName { get; set; }

            [JsonProperty("state_abbreviation")]
            public string? State { get; set; }

            [JsonProperty("zipcode")]
            public string? ZipCode { get; set; }

            [JsonProperty("plus4_code")]
            public string? Plus4Code { get; set; }
        }
    }
}
