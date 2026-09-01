# GraphEmailSender.NetFramework

A standard .NET Framework 4.8 class library that sends email through Microsoft
Graph directly - no separate relay service, no SMTP. Reference the project (or
package it as a NuGet package) from an existing WinForms/WPF app, Windows
service, console app, or classic ASP.NET application.

This is the .NET Framework counterpart to
[`../GraphEmailSender.NetCore`](../GraphEmailSender.NetCore); the code is
functionally identical, only the project/target framework differs.

## Install

Reference the project directly, or pack it:

```bash
dotnet pack GraphEmailSender.NetFramework.csproj -c Release
```

## Usage

```csharp
using Azure.Identity;
using SendingEmailViaMicrosoftGraph.EmailSender;
using SendingEmailViaMicrosoftGraph.EmailSender.Models;

// Any Azure.Core TokenCredential works - client secret shown here.
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

var emailSender = new GraphEmailSender(credential, senderMailboxAddress: "sender@example.com");

var result = await emailSender.SendAsync(new SimpleEmailMessage
{
    To = "recipient@example.com",
    Subject = "Hello",
    Body = "<p>Hello from GraphEmailSender.</p>",
    IsBodyHtml = true
});

if (!result.IsSuccess)
{
    // result.Message / result.GraphStatusCode describe the failure
}
```

A full, runnable example is in [`Sample/ConsoleSample.NetFramework`](Sample/ConsoleSample.NetFramework).

You can also pass a full Microsoft Graph `Message` object (`Microsoft.Graph.Models.Message`)
to `SendAsync` when you need complete control over the payload (importance,
categories, custom headers, etc.), instead of the flattened `SimpleEmailMessage`.

## Authentication & permissions

`GraphEmailSender` accepts any `Azure.Core.TokenCredential`, so how you
authenticate is up to the consuming app - `ClientSecretCredential`,
`ClientCertificateCredential`, `ManagedIdentityCredential`,
`DefaultAzureCredential`, etc. all work.

Whatever credential you use, the underlying Entra ID app registration needs the
Microsoft Graph **`Mail.Send`** *application* permission, with admin consent
granted, and permission to send as the mailbox you pass as
`senderMailboxAddress` (consider an
[application access policy](https://learn.microsoft.com/graph/auth-limit-mailbox-access)
to scope `Mail.Send` down to specific mailboxes rather than the whole tenant).

## Validation

`SendAsync` normalizes and validates the message before calling Graph:

- A `To` address is required; up to 50 recipients per To/Cc/Bcc.
- Up to 10 attachments, 5 MB each.
- Attachment MIME type must be on an allow-list (common Office/image/PDF/text/zip
  types) - see `AllowedMimeTypes` in `GraphEmailSender.cs`.
- Subject truncated to 256 characters, body to 32,768 characters, rather than
  rejected outright.

Validation failures return `EmailSendResult { IsSuccess = false, Message = "..." }`
without calling Graph.
