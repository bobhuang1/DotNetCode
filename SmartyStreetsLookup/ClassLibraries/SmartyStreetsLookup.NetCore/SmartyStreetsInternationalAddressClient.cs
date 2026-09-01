using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartyStreetsLookup.NetCore.Models;

namespace SmartyStreetsLookup.NetCore;

/// <summary>
/// Calls the SmartyStreets International Street API
/// (https://www.smarty.com/docs/cloud/international-street-api) directly over
/// HTTP - no SmartyStreets SDK dependency, no intermediary service. Intended to
/// be referenced directly by an existing .NET 6/8/10 application.
/// </summary>
public sealed class SmartyStreetsInternationalAddressClient
{
    private const string BaseUrl = "https://international-street.api.smarty.com/verify";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _authId;
    private readonly string _authToken;

    /// <param name="authId">Your SmartyStreets International Street API auth-id.</param>
    /// <param name="authToken">Your SmartyStreets International Street API auth-token.</param>
    /// <param name="httpClient">
    /// Optional. In an app with a DI container, register this type via
    /// <c>AddHttpClient&lt;SmartyStreetsInternationalAddressClient&gt;()</c> so the
    /// client comes from <see cref="IHttpClientFactory"/> (see README); otherwise a
    /// private one is created for you.
    /// </param>
    public SmartyStreetsInternationalAddressClient(string authId, string authToken, HttpClient? httpClient = null)
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
        if (string.IsNullOrWhiteSpace(request.Country))
            return new AddressLookupResult { IsValid = false, Message = "Country is required." };

        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["auth-id"] = _authId,
            ["auth-token"] = _authToken,
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
