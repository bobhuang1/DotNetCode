# Sending Email via Microsoft Graph

Sample code for sending email through **Microsoft Graph** (`/users/{id}/sendMail`)
instead of SMTP - useful once an Office 365 tenant disables basic-auth/legacy SMTP
AUTH and app-only Graph access becomes the supported way to send mail from an app
or service.

This repo has three independent, self-contained pieces. Pick whichever fits your
scenario - they don't depend on each other.

| Folder | What it is | When to use it |
|---|---|---|
| [`AzureFunction/`](AzureFunction) | An isolated-worker Azure Function (.NET 10) exposing two HTTP endpoints that relay email via Graph | You want a shared, centrally-deployed email-sending service that other apps call over HTTPS (e.g. legacy apps that can't easily add Graph SDK dependencies themselves) |
| [`ClientSamples/`](ClientSamples) | Sample HTTP client code that calls the Azure Function above | You're integrating a caller with the Azure Function and want a starting point for either .NET Framework 4.7/4.8 or .NET 6/8/10 |
| [`ClassLibraries/`](ClassLibraries) | Two standalone class libraries (`GraphEmailSender.NetFramework` and `GraphEmailSender.NetCore`) that call Microsoft Graph directly | You want to send mail via Graph from *inside* your own app, with no extra HTTP hop |

## Why two ways to send (Azure Function vs. class library)?

- The **Azure Function** centralizes the Graph app registration/credentials in one
  place and authenticates using its own managed identity. Good when many
  applications - possibly including ones you don't want to hand Graph credentials
  to directly - need to send mail.
- The **class libraries** are for when you're building (or already own) the calling
  app and would rather skip the network hop and just call Graph in-process. They
  accept any `Azure.Core.TokenCredential`, so you can authenticate however fits
  your app (client secret, certificate, managed identity, etc.).

## Both endpoints / both APIs, two message shapes

Every entry point (the two Function endpoints, and both class libraries) exposes
the same two ways to describe a message:

- **`Office365SmtpSimple`** - a flattened shape (`To`, `Cc`, `Bcc` as strings,
  `Subject`, `Body`, `IsBodyHtml`, `Attachments`) that's easy to build from
  legacy code.
- **`Office365SmtpRelay`** - accepts a full [Microsoft Graph `Message`](https://learn.microsoft.com/graph/api/resources/message)
  object, for callers that need complete control over the payload (importance,
  categories, custom headers, etc.).

Both paths validate input (recipient counts, attachment size/count, allow-listed
attachment MIME types) and normalize it (subject/body truncation) before calling
Graph.

## Prerequisites common to all three

1. An **Entra ID (Azure AD) app registration** with the Microsoft Graph
   **`Mail.Send`** *application* permission, with admin consent granted.
2. A mailbox the app is allowed to send as (a shared mailbox is recommended over a
   personal one).
3. For the Azure Function: the Function App's managed identity needs that Graph
   app role assignment (see [`AzureFunction/README.md`](AzureFunction/README.md)).
   For the class libraries: any credential (client secret, certificate, managed
   identity) tied to the app registration works.

## Security note

This is sample/portfolio code. All credentials, tenant IDs, and mailbox addresses
in this repo are placeholders (`example.com`, `YOUR_TENANT_ID`, etc.) - replace
them with your own before running anything for real, and never commit real
secrets. `local.settings.json` is git-ignored for exactly that reason; only
`local.settings.json.sample` is checked in.
