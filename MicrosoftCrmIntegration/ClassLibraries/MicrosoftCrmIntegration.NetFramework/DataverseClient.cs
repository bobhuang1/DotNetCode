#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace MicrosoftCrmIntegration.NetFramework
{
    /// <summary>
    /// A thin, generic wrapper over the Microsoft Dataverse (Dynamics 365 CE / Power
    /// Platform) Web API - https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview -
    /// for CRUD operations against any table (accounts, contacts, incidents,
    /// products, pricelevels, or a custom one). No Dataverse SDK dependency
    /// (Microsoft.PowerPlatform.Dataverse.Client/Microsoft.Xrm.Sdk); this calls the
    /// OData REST endpoint directly, and request/response bodies are passed through
    /// as raw JSON exactly as Dataverse defines them, so this client stays generic
    /// instead of reimplementing Dataverse's schema.
    ///
    /// Authenticates with a Microsoft Entra ID app registration's client
    /// credentials (client id/secret/tenant) via <see cref="ClientSecretCredential"/>
    /// - see README for the one-time setup (an Application User must also be
    /// created for the app registration in the target Dataverse environment).
    /// </summary>
    public sealed class DataverseClient
    {
        private const string DefaultApiVersion = "v9.2";
        private static readonly Regex EntityIdRegex = new Regex(@"\(([0-9a-fA-F\-]{36})\)\s*$", RegexOptions.Compiled);

        private readonly HttpClient _httpClient;
        private readonly TokenCredential _credential;
        private readonly string _resourceUrl;
        private readonly string _apiVersion;

        static DataverseClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        /// <param name="resourceUrl">Your Dataverse environment URL, e.g. "https://yourorg.crm.dynamics.com".</param>
        /// <param name="tenantId">Microsoft Entra tenant id.</param>
        /// <param name="clientId">The app registration's application (client) id.</param>
        /// <param name="clientSecret">The app registration's client secret.</param>
        /// <param name="apiVersion">Dataverse Web API version. Defaults to "v9.2", the long-standing stable version.</param>
        /// <param name="httpClient">
        /// Optional. Pass a shared, long-lived <see cref="HttpClient"/> if your app already
        /// has one (recommended - see README); otherwise a private one is created for you.
        /// </param>
        public DataverseClient(
            string resourceUrl, string tenantId, string clientId, string clientSecret,
            string apiVersion = DefaultApiVersion, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(resourceUrl)) throw new ArgumentException("A Dataverse resource URL is required.", nameof(resourceUrl));
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("A tenant id is required.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("A client id is required.", nameof(clientId));
            if (string.IsNullOrWhiteSpace(clientSecret)) throw new ArgumentException("A client secret is required.", nameof(clientSecret));

            _resourceUrl = resourceUrl.TrimEnd('/');
            _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            _apiVersion = string.IsNullOrWhiteSpace(apiVersion) ? DefaultApiVersion : apiVersion;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>GET the entity set. Pass OData query options (e.g. "$select=name&amp;$filter=statecode eq 0&amp;$top=25") as <paramref name="odataQueryString"/>.</summary>
        public Task<DataverseApiResult> ListAsync(string entitySetName, string? odataQueryString = null, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Get, BuildUrl(entitySetName, null, odataQueryString), null, cancellationToken);

        /// <summary>GET a single record by id. Pass "$select=..." as <paramref name="odataQueryString"/> to limit columns.</summary>
        public Task<DataverseApiResult> GetAsync(string entitySetName, Guid id, string? odataQueryString = null, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Get, BuildUrl(entitySetName, id, odataQueryString), null, cancellationToken);

        /// <summary>POST to create a record. <paramref name="jsonBody"/> is the exact Dataverse column/value JSON, e.g. {"name": "Contoso"}.</summary>
        public Task<DataverseApiResult> CreateAsync(string entitySetName, string jsonBody, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Post, BuildUrl(entitySetName, null, null), jsonBody, cancellationToken);

        /// <summary>PATCH to partially update a record - Dataverse's native update verb, unlike Shopify's REST API this needs no verb translation.</summary>
        public Task<DataverseApiResult> UpdateAsync(string entitySetName, Guid id, string jsonBody, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(new HttpMethod("PATCH"), BuildUrl(entitySetName, id, null), jsonBody, cancellationToken);

        /// <summary>DELETE a record by id.</summary>
        public Task<DataverseApiResult> DeleteAsync(string entitySetName, Guid id, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendAsync(HttpMethod.Delete, BuildUrl(entitySetName, id, null), null, cancellationToken);

        private async Task<DataverseApiResult> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                var token = await GetAccessTokenAsync(cancellationToken);
                response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("Authorization", "Bearer " + token);
                    request.Headers.Add("OData-MaxVersion", "4.0");
                    request.Headers.Add("OData-Version", "4.0");
                    request.Headers.Add("Accept", "application/json");
                    if (method == HttpMethod.Post)
                        request.Headers.Add("Prefer", "return=representation");
                    if (jsonBody != null)
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    return request;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return new DataverseApiResult { IsSuccess = false, StatusCode = 0, ErrorMessage = "Request to Dataverse failed: " + ex.Message };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                Guid? createdEntityId = null;
                if (response.Headers.TryGetValues("OData-EntityId", out var values))
                {
                    var match = EntityIdRegex.Match(values.FirstOrDefault() ?? string.Empty);
                    if (match.Success && Guid.TryParse(match.Groups[1].Value, out var parsedId))
                        createdEntityId = parsedId;
                }

                return new DataverseApiResult
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Body = body,
                    CreatedEntityId = createdEntityId,
                    ErrorMessage = response.IsSuccessStatusCode ? null : string.Format("Dataverse returned HTTP {0}.", (int)response.StatusCode)
                };
            }
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            // ClientSecretCredential caches and refreshes tokens internally and is
            // thread-safe, so it's safe (and recommended by Azure.Identity) to call
            // GetTokenAsync on every request rather than managing a cache ourselves.
            var context = new TokenRequestContext(new[] { _resourceUrl + "/.default" });
            var token = await _credential.GetTokenAsync(context, cancellationToken);
            return token.Token;
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

            throw new InvalidOperationException("Dataverse retry loop exited without a response.");
        }

        private string BuildUrl(string entitySetName, Guid? id, string? odataQueryString)
        {
            var path = id.HasValue ? entitySetName + "(" + id.Value + ")" : entitySetName;
            var url = _resourceUrl + "/api/data/" + _apiVersion + "/" + path;
            if (string.IsNullOrEmpty(odataQueryString)) return url;
            return url + "?" + odataQueryString!.TrimStart('?');
        }
    }
}
