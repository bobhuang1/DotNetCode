# AzureFunction - Email Relay over HTTP

An isolated-worker Azure Function, targeting **.NET 10**, that exposes two
HTTP-triggered endpoints for sending email through Microsoft Graph. Useful as a
central "email relay" service that other applications call over HTTPS instead of
each integrating the Graph SDK (and holding Graph credentials) themselves.

Client sample code for calling these endpoints lives in
[`../ClientSamples`](../ClientSamples) (separate samples for .NET Framework 4.7/4.8
and .NET 6/8/10, since `HttpClient` is set up differently on each).

## Endpoints

Both are `POST`, secured with a [function key](https://learn.microsoft.com/azure/azure-functions/function-keys-how-to)
(`AuthorizationLevel.Function`) supplied via the `x-functions-key` header or
`?code=` query string.

### `POST /Office365SmtpSimple`

Flattened, easy-to-build request body:

```json
{
  "to": "someone@example.com, someone-else@example.com",
  "cc": "",
  "bcc": "",
  "subject": "Hello",
  "body": "<p>Hello from the sample function.</p>",
  "isBodyHtml": true,
  "attachments": [
    { "name": "notes.txt", "contentType": "text/plain", "contentBytes": "SGVsbG8h" }
  ]
}
```

### `POST /Office365SmtpRelay`

Accepts a full [Microsoft Graph `Message`](https://learn.microsoft.com/graph/api/resources/message)
payload wrapped in `mailMessage`, for callers that need full control (importance,
categories, custom headers, CC/BCC as structured recipients, etc.):

```json
{
  "mailMessage": {
    "subject": "Hello",
    "body": { "contentType": "HTML", "content": "<p>Hello from the sample function.</p>" },
    "toRecipients": [ { "emailAddress": { "address": "someone@example.com" } } ],
    "attachments": [
      {
        "@odata.type": "#microsoft.graph.fileAttachment",
        "name": "notes.txt",
        "contentType": "text/plain",
        "contentBytes": "SGVsbG8h"
      }
    ]
  }
}
```

### Response

Both endpoints return `{ "message": "..." }` with HTTP 200 on success, 400 for
validation failures, or 500 for unexpected/Graph errors.

### Validation applied to both endpoints

- A `To` address is required; up to 50 recipients per To/Cc/Bcc.
- Up to 10 attachments, 5 MB each.
- Attachment MIME type must be on an allow-list (common Office/image/PDF/text/zip
  types) - see `AllowedMimeTypes` in `EmailRelayFunctions.cs`.
- Subject truncated to 256 characters, body to 32,768 characters, rather than
  rejected outright.

## How authentication works

The function authenticates to Graph using **`DefaultAzureCredential`**, which
resolves to the Function App's managed identity when running in Azure. There are
no client secrets in this project by design.

Setup:

1. Create the Function App with a **system-assigned managed identity** enabled
   (Azure Portal → Function App → Identity → System assigned → On).
2. Grant that identity the Graph **`Mail.Send`** *application* permission. The
   portal doesn't support assigning Graph app roles to a managed identity
   directly, so this is typically done with Microsoft Graph PowerShell:

   ```powershell
   Connect-MgGraph -Scopes "Application.Read.All","AppRoleAssignment.ReadWrite.All"

   $graphSpId = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id
   $functionSpId = (Get-MgServicePrincipal -Filter "displayName eq '<your-function-app-name>'").Id
   $mailSendRole = (Get-MgServicePrincipal -ServicePrincipalId $graphSpId).AppRole `
       | Where-Object { $_.Value -eq "Mail.Send" }

   New-MgServicePrincipalAppRoleAssignment `
       -ServicePrincipalId $functionSpId `
       -PrincipalId $functionSpId `
       -ResourceId $graphSpId `
       -AppRoleId $mailSendRole.Id
   ```

3. Set the `SenderMailboxAddress` app setting to the mailbox the function should
   send as (a shared mailbox is recommended). The identity needs permission to
   send as that mailbox - `Mail.Send` at the application-permission level allows
   sending as any mailbox in the tenant, so scope this down with an
   [application access policy](https://learn.microsoft.com/graph/auth-limit-mailbox-access)
   if you only want it to send as specific mailboxes.

## Local development

`Debugger.IsAttached` short-circuits the Graph call entirely when you run/debug
locally (F5) - no real credential or network call is made, and the function
returns a canned "pretend sent" success response. This means:

- You can exercise the validation logic and both endpoint shapes locally with
  zero Azure/Graph setup.
- To actually test a real send, publish to Azure (or otherwise run without a
  debugger attached) with a valid managed identity + `Mail.Send` permission.

Configuration for local runs:

```bash
cp local.settings.json.sample local.settings.json
```

Then edit `local.settings.json` with your own `TestRecipientEmailAddress` /
`SenderMailboxAddress` values. `local.settings.json` is git-ignored - never commit
real values into it.

| Setting | Purpose |
|---|---|
| `TestRecipientEmailAddress` | Recipient used only by the built-in "empty body" test path (currently disabled via `IsTestModeEnabled = false` in code) |
| `SenderMailboxAddress` | Mailbox the function sends as |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional. If set, OpenTelemetry traces/metrics/logs export to Application Insights. If unset, the function still runs with telemetry disabled. |

## Running locally

```bash
func start
```

(requires the [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local))

## Project layout

```
AzureFunction/
├── Program.cs                  Isolated-worker host bootstrap, optional OpenTelemetry wiring
├── EmailRelayFunctions.cs      Both HTTP endpoints + validation/send pipeline
├── Models/
│   ├── SimpleEmailMessage.cs   Request model for /Office365SmtpSimple
│   └── GraphRelayModels.cs     Request/response models for /Office365SmtpRelay
├── host.json
└── local.settings.json.sample  Copy to local.settings.json for local runs
```
