// Shows how to reference the GraphEmailSender.NetCore class library directly from an
// existing .NET 6/8/10 app and send mail without going through the Azure Function /
// HTTP endpoint.

using Azure.Identity;
using SendingEmailViaMicrosoftGraph.EmailSender;
using SendingEmailViaMicrosoftGraph.EmailSender.Models;

// App registration (Entra ID) credentials. Replace with your own values, or better,
// load them from configuration / a secret store. The app registration needs the
// Mail.Send application permission with admin consent.
var tenantId = Environment.GetEnvironmentVariable("GRAPH_TENANT_ID") ?? "YOUR_TENANT_ID";
var clientId = Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID") ?? "YOUR_CLIENT_ID";
var clientSecret = Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET") ?? "YOUR_CLIENT_SECRET";
var senderMailbox = Environment.GetEnvironmentVariable("GRAPH_SENDER_MAILBOX") ?? "sender@example.com";

// ClientSecretCredential is one concrete TokenCredential implementation.
// GraphEmailSender accepts any Azure.Core TokenCredential, so ClientCertificateCredential,
// ManagedIdentityCredential, DefaultAzureCredential, etc. work too.
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

var emailSender = new GraphEmailSender(credential, senderMailbox);

var result = await emailSender.SendAsync(new SimpleEmailMessage
{
    To = "recipient@example.com",
    Subject = "Hello from GraphEmailSender.NetCore",
    Body = "<p>Sent directly from a .NET 8 console app via Microsoft Graph.</p>",
    IsBodyHtml = true
});

Console.WriteLine(result.IsSuccess
    ? $"Sent: {result.Message}"
    : $"Failed: {result.Message} (Graph status: {result.GraphStatusCode})");

return result.IsSuccess ? 0 : 1;
