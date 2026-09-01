// Sample client for the SendingEmailViaMicrosoftGraph Azure Function, written for
// .NET 6/8/10.
//
// What's different here vs. the .NET Framework 4.8 sample in ../NetFramework48:
//   - HttpClient comes from IHttpClientFactory via the generic host's DI container
//     (AddHttpClient<T>), which is the idiomatic modern pattern: it manages the
//     underlying SocketsHttpHandler pool/lifetime for you, so there's no need for a
//     hand-rolled static singleton HttpClient.
//   - System.Text.Json instead of Newtonsoft.Json.
//   - Top-level statements instead of a Main method.
//   - TLS 1.2+ is the platform default, so nothing needs to be set explicitly.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                       ?? "https://your-function-app.azurewebsites.net";
var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                   ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<EmailRelayClient>(client =>
{
    client.BaseAddress = new Uri(functionBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
});

using var host = builder.Build();

var emailClient = host.Services.GetRequiredService<EmailRelayClient>();

await emailClient.SendViaSimpleEndpointAsync();
await emailClient.SendViaGraphRelayEndpointAsync();

internal sealed class EmailRelayClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls POST /Office365SmtpSimple with a flattened payload + one attachment.</summary>
    public async Task SendViaSimpleEndpointAsync()
    {
        var attachmentBytes = "Hello from the .NET 8+ sample client."u8.ToArray();

        var payload = new SimpleEmailRequest(
            To: "recipient@example.com",
            Subject: "Hello from .NET 8+",
            Body: "<p>This message was sent via the <b>Office365SmtpSimple</b> endpoint.</p>",
            IsBodyHtml: true,
            Attachments: [new SimpleEmailAttachment("hello.txt", "text/plain", attachmentBytes)]);

        using var response = await httpClient.PostAsJsonAsync("Office365SmtpSimple", payload, JsonOptions);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Simple] HTTP {(int)response.StatusCode} - {responseBody}");
    }

    /// <summary>Calls POST /Office365SmtpRelay with a full Microsoft Graph Message payload.</summary>
    public async Task SendViaGraphRelayEndpointAsync()
    {
        var payload = new
        {
            mailMessage = new
            {
                subject = "Hello from .NET 8+ (Graph payload)",
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

        using var response = await httpClient.PostAsJsonAsync("Office365SmtpRelay", payload, JsonOptions);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Relay] HTTP {(int)response.StatusCode} - {responseBody}");
    }
}

internal sealed record SimpleEmailRequest(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml,
    List<SimpleEmailAttachment>? Attachments = null);

internal sealed record SimpleEmailAttachment(string Name, string ContentType, byte[] ContentBytes);
