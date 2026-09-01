using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartyStreetsLookup.NetCore.Models;

namespace SmartyStreetsLookup.NetCore;

/// <summary>
/// Calls the SmartyStreets US Street API (https://www.smarty.com/docs/cloud/us-street-api)
/// directly over HTTP - no SmartyStreets SDK dependency, no intermediary service.
/// Intended to be referenced directly by an existing .NET 6/8/10 application.
/// </summary>
public sealed class SmartyStreetsUsAddressClient
{
    private const string BaseUrl = "https://us-street.api.smarty.com/street-address";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _authId;
    private readonly string _authToken;

    /// <param name="authId">Your SmartyStreets US Street API auth-id.</param>
    /// <param name="authToken">Your SmartyStreets US Street API auth-token.</param>
    /// <param name="httpClient">
    /// Optional. In an app with a DI container, register this type via
    /// <c>AddHttpClient&lt;SmartyStreetsUsAddressClient&gt;()</c> so the client comes
    /// from <see cref="IHttpClientFactory"/> (see README); otherwise a private one is
    /// created for you.
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
        ArgumentNullException.ThrowIfNull(request);
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
            response = await GetWithRetryAsync($"{BaseUrl}?{query}", cancellationToken);
        }
        catch (Exception ex)
        {
            return new AddressLookupResult { IsValid = false, Message = $"Request to SmartyStreets failed: {ex.Message}" };
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
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

    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode != (HttpStatusCode)429 || attempt == maxAttempts)
                return response;

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }

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
}
