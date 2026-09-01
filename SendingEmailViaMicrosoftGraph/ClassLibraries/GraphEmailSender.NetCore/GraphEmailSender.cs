#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using SendingEmailViaMicrosoftGraph.EmailSender.Models;

namespace SendingEmailViaMicrosoftGraph.EmailSender
{
    /// <summary>
    /// Sends email through Microsoft Graph (Users["mailbox"].SendMail) using an
    /// app-only Microsoft Entra ID credential you supply. Intended to be referenced
    /// directly by an existing application (console app, Windows service, WinForms/WPF
    /// app, ASP.NET app, etc.) instead of calling out to a separate relay service.
    ///
    /// The app registration behind the supplied credential needs the Mail.Send
    /// application permission (with admin consent) - see README.md.
    /// </summary>
    public sealed class GraphEmailSender
    {
        private const int MaxSubjectLength = 256;
        private const int MaxBodyLength = 32768;
        private const int MaxRecipients = 50;
        private const int MaxAttachments = 10;
        private const int MaxAttachmentSizeBytes = 5 * 1024 * 1024;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/bmp", "image/gif", "image/tiff", "image/webp",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/pdf",
            "text/plain",
            "application/zip"
        };

        private readonly GraphServiceClient _graphClient;
        private readonly string _senderMailboxAddress;
        private readonly ILogger<GraphEmailSender>? _logger;

        /// <param name="credential">
        /// Any Azure.Core TokenCredential - e.g. ClientSecretCredential, ClientCertificateCredential,
        /// ManagedIdentityCredential, or DefaultAzureCredential - authorized to call Graph as an app.
        /// </param>
        /// <param name="senderMailboxAddress">
        /// Mailbox the message is sent as (e.g. "shared-mailbox@yourtenant.onmicrosoft.com").
        /// The app registration must have permission to send as this mailbox.
        /// </param>
        /// <param name="logger">Optional; pass a logger from your app's DI container to capture failures.</param>
        public GraphEmailSender(TokenCredential credential, string senderMailboxAddress, ILogger<GraphEmailSender>? logger = null)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            if (string.IsNullOrWhiteSpace(senderMailboxAddress))
                throw new ArgumentException("A sender mailbox address is required.", nameof(senderMailboxAddress));

            _senderMailboxAddress = senderMailboxAddress.Trim();
            _logger = logger;
            _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
        }

        /// <summary>Sends a flattened <see cref="SimpleEmailMessage"/> (recipients as strings, optional attachments).</summary>
        public Task<EmailSendResult> SendAsync(SimpleEmailMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            return SendAsync(ConvertSimpleToGraph(message), cancellationToken);
        }

        /// <summary>Sends a full Microsoft Graph <see cref="Message"/>, for callers that need complete control over the payload.</summary>
        public async Task<EmailSendResult> SendAsync(Message graphMessage, CancellationToken cancellationToken = default)
        {
            if (graphMessage == null) throw new ArgumentNullException(nameof(graphMessage));

            NormalizeMessage(graphMessage);
            ValidateMessage(graphMessage, out var validationError);
            if (!string.IsNullOrEmpty(validationError))
                return new EmailSendResult(isSuccess: false, message: validationError);

            try
            {
                var requestInfo = _graphClient.Users[_senderMailboxAddress].SendMail
                    .ToPostRequestInformation(new SendMailPostRequestBody
                    {
                        Message = graphMessage,
                        SaveToSentItems = false
                    });

                await _graphClient.RequestAdapter.SendNoContentAsync(
                    requestInfo,
                    errorMapping: null,
                    cancellationToken: cancellationToken);

                return new EmailSendResult(isSuccess: true, message: "Message sent successfully.");
            }
            catch (ServiceException ex)
            {
                _logger?.LogError(ex, "Graph ServiceException during SendMail.");
                return new EmailSendResult(
                    isSuccess: false,
                    message: ex.Message,
                    graphStatusCode: ex.ResponseStatusCode);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected exception during SendMail.");
                return new EmailSendResult(isSuccess: false, message: ex.Message);
            }
        }

        private static Message ConvertSimpleToGraph(SimpleEmailMessage simple)
        {
            var msg = new Message
            {
                Subject = simple.Subject?.Trim() ?? "(No Subject)",
                Body = new ItemBody
                {
                    ContentType = simple.IsBodyHtml ? BodyType.Html : BodyType.Text,
                    Content = simple.Body ?? "No Body Content"
                },
                ToRecipients = new List<Recipient>(),
                CcRecipients = new List<Recipient>(),
                BccRecipients = new List<Recipient>(),
                Attachments = new List<Attachment>()
            };

            void AddRecipients(string? raw, ICollection<Recipient> dest)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                foreach (var addr in raw!.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    dest.Add(new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = addr.Trim() }
                    });
                }
            }

            AddRecipients(simple.To, msg.ToRecipients);
            AddRecipients(simple.Cc, msg.CcRecipients);
            AddRecipients(simple.Bcc, msg.BccRecipients);

            if (simple.Attachments != null)
            {
                foreach (var a in simple.Attachments)
                {
                    msg.Attachments.Add(new FileAttachment
                    {
                        Name = a.Name,
                        ContentBytes = a.ContentBytes,
                        ContentType = a.ContentType
                    });
                }
            }

            return msg;
        }

        private static void NormalizeMessage(Message message)
        {
            message.Subject ??= "(No Subject)";
            if (message.Subject.Length > MaxSubjectLength)
                message.Subject = message.Subject.Substring(0, MaxSubjectLength);

            message.Body ??= new ItemBody();
            message.Body.Content ??= "(No Body Content)";
            if (message.Body.Content.Length > MaxBodyLength)
                message.Body.Content = message.Body.Content.Substring(0, MaxBodyLength);
        }

        private static void ValidateMessage(Message message, out string validationError)
        {
            validationError = string.Empty;

            ValidateRecipients(message.ToRecipients as List<Recipient>, "To", ref validationError);
            if (!string.IsNullOrEmpty(validationError)) return;

            ValidateRecipients(message.CcRecipients as List<Recipient>, "Cc", ref validationError);
            if (!string.IsNullOrEmpty(validationError)) return;

            ValidateRecipients(message.BccRecipients as List<Recipient>, "Bcc", ref validationError);
            if (!string.IsNullOrEmpty(validationError)) return;

            if (message.Attachments != null && message.Attachments.Count > MaxAttachments)
            {
                validationError = $"Too many attachments. Maximum allowed is {MaxAttachments}.";
                return;
            }

            if (message.Attachments != null)
            {
                foreach (var attachment in message.Attachments)
                {
                    if (attachment is FileAttachment fileAttachment &&
                        fileAttachment.ContentBytes != null &&
                        fileAttachment.ContentBytes.Length > MaxAttachmentSizeBytes)
                    {
                        validationError =
                            $"Attachment '{fileAttachment.Name}' exceeds maximum size of {MaxAttachmentSizeBytes} bytes.";
                        return;
                    }
                }
            }

            var mimeError = ValidateAttachmentMimeTypes(message);
            if (!string.IsNullOrEmpty(mimeError))
                validationError = mimeError!;
        }

        private static void ValidateRecipients(List<Recipient>? recipients, string label, ref string validationError)
        {
            if (recipients == null || recipients.Count == 0)
            {
                if (label == "To")
                    validationError = "No To address specified.";
                return;
            }

            if (recipients.Count > MaxRecipients)
            {
                validationError = $"Too many {label} recipients. Maximum allowed is {MaxRecipients}.";
                return;
            }

            foreach (var r in recipients)
            {
                var addr = r.EmailAddress?.Address?.Trim();
                if (string.IsNullOrEmpty(addr) || !addr!.Contains("@"))
                {
                    validationError = $"Invalid {label} address: '{addr}'.";
                    return;
                }
            }
        }

        private static string? ValidateAttachmentMimeTypes(Message message)
        {
            if (message.Attachments == null || message.Attachments.Count == 0)
                return null;

            foreach (var attachment in message.Attachments)
            {
                if (attachment is FileAttachment fileAttachment)
                {
                    var mime = fileAttachment.ContentType?.Trim().ToLowerInvariant();

                    if (string.IsNullOrEmpty(mime))
                        return $"Attachment '{fileAttachment.Name}' has no MIME type.";

                    if (!AllowedMimeTypes.Contains(mime!))
                        return $"Attachment '{fileAttachment.Name}' MIME type '{mime}' is not allowed.";
                }
            }

            return null;
        }
    }
}
