#nullable enable
using System.Net;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace MicrosoftCrmIntegration.AzureFunction.Services
{
    public readonly record struct DataverseCredentials(string ResourceUrl, string TenantId, string ClientId, string ClientSecret, string ApiVersion);

    /// <summary>
    /// The outcome of one call to the Dataverse Web API, forwarded through almost
    /// verbatim - this proxy does not parse or reshape Dataverse's JSON.
    /// </summary>
    public sealed class DataverseProxyResult
    {
        public bool ReachedDataverse { get; init; }
        public int StatusCode { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    /// <summary>
    /// Forwards CRUD calls to the Microsoft Dataverse Web API for whichever table
    /// the caller specifies. Request and response bodies are passed through as raw
    /// JSON, exactly as Dataverse's Web API defines them (see
    /// https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview) -
    /// this proxy does not model Dataverse's schema itself, so it stays generic
    /// instead of reimplementing it.
    /// </summary>
    public sealed class DataverseProxyClient(IHttpClientFactory httpClientFactory, ILogger<DataverseProxyClient> logger)
    {
        public const string HttpClientName = "Dataverse";

        // Cached because this class is registered as a DI singleton and the
        // credentials come from process environment variables that don't change
        // for the lifetime of the Function App instance. ClientSecretCredential
        // caches and refreshes tokens internally and is thread-safe.
        private TokenCredential? _credential;
        private string? _credentialKey;

        public Task<DataverseProxyResult> ListAsync(DataverseCredentials creds, string entitySetName, string? odataQueryString, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Get, creds, BuildUrl(creds, entitySetName, null, odataQueryString), null, cancellationToken);

        public Task<DataverseProxyResult> GetAsync(DataverseCredentials creds, string entitySetName, Guid id, string? odataQueryString, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Get, creds, BuildUrl(creds, entitySetName, id, odataQueryString), null, cancellationToken);

        public Task<DataverseProxyResult> CreateAsync(DataverseCredentials creds, string entitySetName, string jsonBody, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Post, creds, BuildUrl(creds, entitySetName, null, null), jsonBody, cancellationToken);

        public Task<DataverseProxyResult> UpdateAsync(DataverseCredentials creds, string entitySetName, Guid id, string jsonBody, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Patch, creds, BuildUrl(creds, entitySetName, id, null), jsonBody, cancellationToken);

        public Task<DataverseProxyResult> DeleteAsync(DataverseCredentials creds, string entitySetName, Guid id, CancellationToken cancellationToken) =>
            SendAsync(HttpMethod.Delete, creds, BuildUrl(creds, entitySetName, id, null), null, cancellationToken);

        private async Task<DataverseProxyResult> SendAsync(HttpMethod method, DataverseCredentials creds, string url, string? jsonBody, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                var token = await GetAccessTokenAsync(creds, cancellationToken);
                response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("Authorization", $"Bearer {token}");
                    request.Headers.Add("OData-MaxVersion", "4.0");
                    request.Headers.Add("OData-Version", "4.0");
                    request.Headers.Add("Accept", "application/json");
                    if (method == HttpMethod.Post)
                        request.Headers.Add("Prefer", "return=representation");
                    if (jsonBody is not null)
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    return request;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Request to Dataverse failed.");
                return new DataverseProxyResult { ReachedDataverse = false, StatusCode = 0, Body = string.Empty };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new DataverseProxyResult
                {
                    ReachedDataverse = true,
                    StatusCode = (int)response.StatusCode,
                    Body = body
                };
            }
        }

        private Task<AccessToken> GetAccessTokenAsync(DataverseCredentials creds, CancellationToken cancellationToken)
        {
            var key = $"{creds.TenantId}|{creds.ClientId}|{creds.ResourceUrl}";
            if (_credential is null || _credentialKey != key)
            {
                _credential = new ClientSecretCredential(creds.TenantId, creds.ClientId, creds.ClientSecret);
                _credentialKey = key;
            }

            var context = new TokenRequestContext([$"{creds.ResourceUrl}/.default"]);
            return _credential.GetTokenAsync(context, cancellationToken).AsTask();
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
                logger.LogWarning("Dataverse rate-limited (429). Retrying in {Delay}.", delay);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }

            throw new InvalidOperationException("Dataverse retry loop exited without a response.");
        }

        private static string BuildUrl(DataverseCredentials creds, string entitySetName, Guid? id, string? odataQueryString)
        {
            var path = id.HasValue ? $"{entitySetName}({id.Value})" : entitySetName;
            var url = $"{creds.ResourceUrl.TrimEnd('/')}/api/data/{creds.ApiVersion}/{path}";
            return string.IsNullOrEmpty(odataQueryString) ? url : $"{url}?{odataQueryString.TrimStart('?')}";
        }
    }
}
