using System.Net.Http;
using McpServerLibrary.Configuration;

namespace McpServerLibrary.Services
{
    /// <summary>
    /// Minimal, credential-aware HTTP client for the generic downstream REST API.
    /// </summary>
    public interface IGenericApiClient
    {
        /// <summary>Gets a resource from the downstream API using Basic authentication.</summary>
        /// <param name="resourcePath">Path relative to the API base URL, e.g. "orders/ORD-0001".</param>
        /// <param name="credentials">Credentials resolved from Key Vault.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<string> GetAsync(string resourcePath, GenericCredentials credentials, CancellationToken cancellationToken = default);

        /// <summary>Posts a JSON payload to the downstream API using Basic authentication.</summary>
        /// <param name="resourcePath">Path relative to the API base URL, e.g. "orders/ORD-0001".</param>
        /// <param name="jsonBody">JSON payload to send.</param>
        /// <param name="credentials">Credentials resolved from Key Vault.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<string> PostAsync(string resourcePath, string jsonBody, GenericCredentials credentials, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Sends Basic-auth requests to <see cref="GenericCredentials.BaseUrl"/> using a shared
    /// <see cref="System.Net.Http.HttpClient"/> registered via <c>AddHttpClient</c>.
    /// </summary>
    public sealed class GenericApiClient : IGenericApiClient
    {
        private readonly HttpClient _http;

        public GenericApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> GetAsync(string resourcePath, GenericCredentials credentials, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(resourcePath, credentials));
            ApplyBasicAuth(request, credentials);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> PostAsync(string resourcePath, string jsonBody, GenericCredentials credentials, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(resourcePath, credentials));
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            ApplyBasicAuth(request, credentials);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        }

        private static Uri BuildUri(string resourcePath, GenericCredentials credentials)
        {
            if (string.IsNullOrWhiteSpace(credentials.BaseUrl))
            {
                throw new InvalidOperationException("GenericCredentials.BaseUrl is not configured.");
            }

            return resourcePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? new Uri(resourcePath)
                : new Uri($"{credentials.BaseUrl.TrimEnd('/')}/{resourcePath.TrimStart('/')}");
        }

        private static void ApplyBasicAuth(HttpRequestMessage request, GenericCredentials credentials)
        {
            string token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.ApiSecret}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
        }

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Downstream API returned {(int)response.StatusCode}: {body}");
            }

            return body;
        }
    }
}