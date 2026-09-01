// Sample client for the SendingEmailViaMicrosoftGraph Azure Function, written for
// .NET Framework 4.7/4.8.
//
// What's different here vs. the .NET 6/8/10 sample in ../Net8Plus:
//   - No Microsoft.Extensions.Http / IHttpClientFactory. Framework apps typically
//     just new up a single, long-lived, static HttpClient and reuse it for the life
//     of the process (creating a new HttpClient per request risks socket exhaustion
//     under load - the classic footgun on this platform).
//   - TLS 1.2 is not always the OS default on older Windows/.NET Framework
//     combinations, so it's set explicitly via ServicePointManager.
//   - Newtonsoft.Json instead of System.Text.Json (the framework predates
//     System.Text.Json; Newtonsoft remains the common choice here).
//   - Program entry point is a classic static class with a Main method, rather than
//     top-level statements (top-level statements require C# 9 / .NET 5+ tooling
//     conventions typically paired with newer TFMs).

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EmailRelayClientSample.NetFramework48
{
    internal static class Program
    {
        // A single static HttpClient shared for the lifetime of the app.
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static async Task<int> Main()
        {
            // Ensure TLS 1.2 is used regardless of OS/.NET Framework defaults.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                                   ?? "https://your-function-app.azurewebsites.net";
            var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                               ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Client.DefaultRequestHeaders.Remove("x-functions-key");
            Client.DefaultRequestHeaders.Add("x-functions-key", functionKey);

            try
            {
                await SendViaSimpleEndpointAsync(functionBaseUrl);
                await SendViaGraphRelayEndpointAsync(functionBaseUrl);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request failed: " + ex);
                return 1;
            }
        }

        /// <summary>Calls POST /Office365SmtpSimple with a flattened payload + one attachment.</summary>
        private static async Task SendViaSimpleEndpointAsync(string baseUrl)
        {
            var attachmentBytes = Encoding.UTF8.GetBytes("Hello from the .NET Framework 4.8 sample client.");

            var payload = new
            {
                to = "recipient@example.com",
                subject = "Hello from .NET Framework 4.8",
                body = "<p>This message was sent via the <b>Office365SmtpSimple</b> endpoint.</p>",
                isBodyHtml = true,
                attachments = new[]
                {
                    new
                    {
                        name = "hello.txt",
                        contentType = "text/plain",
                        contentBytes = attachmentBytes
                    }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await Client.PostAsync(baseUrl.TrimEnd('/') + "/Office365SmtpSimple", content))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Simple] HTTP {(int)response.StatusCode} - {responseBody}");
            }
        }

        /// <summary>Calls POST /Office365SmtpRelay with a full Microsoft Graph Message payload.</summary>
        private static async Task SendViaGraphRelayEndpointAsync(string baseUrl)
        {
            var payload = new
            {
                mailMessage = new
                {
                    subject = "Hello from .NET Framework 4.8 (Graph payload)",
                    body = new
                    {
                        contentType = "HTML",
                        content = "<p>This message was sent via the <b>Office365SmtpRelay</b> endpoint.</p>"
                    },
                    toRecipients = new[]
                    {
                        new { emailAddress = new { address = "recipient@example.com" } }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await Client.PostAsync(baseUrl.TrimEnd('/') + "/Office365SmtpRelay", content))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Relay] HTTP {(int)response.StatusCode} - {responseBody}");
            }
        }
    }
}
