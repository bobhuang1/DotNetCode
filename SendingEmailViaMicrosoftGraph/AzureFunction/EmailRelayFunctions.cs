#nullable enable
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using SendingEmailViaMicrosoftGraph.AzureFunction.Models;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace SendingEmailViaMicrosoftGraph.AzureFunction
{
    public static class MyTelemetry
    {
        public static readonly ActivitySource ActivitySource =
            new(EmailRelayFunctions.GraphActivitySourceName);
    }

    /// <summary>
    /// Two HTTP-triggered endpoints that send email through Microsoft Graph instead of
    /// SMTP, using the Azure Function's managed identity to authenticate:
    ///
    ///   POST /Office365SmtpRelay  - accepts a full Microsoft Graph Message payload.
    ///   POST /Office365SmtpSimple - accepts a flattened, easier-to-build payload.
    ///
    /// Both endpoints validate, normalize, and then relay the message via
    /// GraphServiceClient.Users[mailbox].SendMail.
    /// </summary>
    public class EmailRelayFunctions
    {
        public const string GraphActivitySourceName = "SendingEmailViaMicrosoftGraph.Graph";

        // Flip on for local manual testing of the "empty body" path without a real send.
        private const bool IsTestModeEnabled = false;
        private const bool EnableRetry = false;

        private const int MaxSubjectLength = 256;
        private const int MaxBodyLength = 32768;
        private const int MaxRecipients = 50;
        private const int MaxAttachments = 10;
        private const int MaxAttachmentSizeBytes = 5 * 1024 * 1024;
        private const int MaxRequestBodyBytes = 10 * 1024 * 1024;

        private readonly ILogger<EmailRelayFunctions> _logger;
        private readonly GraphServiceClient? _graphClient;
        private readonly string _testRecipientEmailAddress;
        private readonly string _senderMailboxAddress;
        private readonly bool _debuggerAttached;

        private static readonly HashSet<string> AllowedMimeTypes =
        [
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
        ];

        public EmailRelayFunctions(ILogger<EmailRelayFunctions> logger, IConfiguration configuration)
        {
            _logger = logger;

            // TestRecipientEmailAddress / SenderMailboxAddress come from app settings
            // (see local.settings.json.sample). Replace the fallback values below or,
            // better, always set them via configuration.
            _testRecipientEmailAddress =
                configuration["TestRecipientEmailAddress"]?.Trim()
                ?? "developer@example.com";

            _senderMailboxAddress =
                configuration["SenderMailboxAddress"]?.Trim()
                ?? "sender@example.com";

            _debuggerAttached = Debugger.IsAttached;

            if (_debuggerAttached)
            {
                Console.WriteLine("====================================================");
                Console.WriteLine(" LOCAL DEBUG MODE - GRAPH DISABLED");
                Console.WriteLine("====================================================");
                _graphClient = null;
                return;
            }

            // DefaultAzureCredential picks up the Function App's system- or
            // user-assigned managed identity when running in Azure. The Graph app
            // registration behind that identity needs the Mail.Send application
            // permission (with admin consent) - see AzureFunction/README.md.
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeEnvironmentCredential = true
            };

            try
            {
                var credential = new DefaultAzureCredential(credentialOptions);

                _graphClient = new GraphServiceClient(
                    credential,
                    ["https://graph.microsoft.com/.default"]);

                _logger.LogInformation("EmailRelayFunctions Graph client initialized.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing DefaultAzureCredential.");
                throw;
            }
        }

        // ============================================================
        // SHARED PIPELINE
        // ============================================================

        private async Task<Message?> DeserializeGraphMessageAsync(string body)
        {
            var requestMessage = JsonConvert.DeserializeObject<GraphRelayRequest>(
                body,
                new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                    {
                        NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy
                        {
                            ProcessDictionaryKeys = true,
                            OverrideSpecifiedNames = false
                        }
                    }
                });

            return requestMessage?.MailMessage;
        }

        private static Message RemapMessage(Message? input)
        {
            var output = new Message
            {
                Subject = input?.Subject?.Trim() ?? "(No Subject)",
                Body = new ItemBody
                {
                    ContentType = input?.Body?.ContentType ?? BodyType.Text,
                    Content = input?.Body?.Content ?? "No Body Content"
                },
                ToRecipients = [],
                CcRecipients = [],
                BccRecipients = [],
                Attachments = []
            };

            void CopyRecipients(ICollection<Recipient>? src, ICollection<Recipient> dest)
            {
                if (src == null) return;
                foreach (var r in src)
                {
                    var addr = r.EmailAddress?.Address?.Trim();
                    if (!string.IsNullOrEmpty(addr))
                        dest.Add(new Recipient { EmailAddress = new EmailAddress { Address = addr } });
                }
            }

            CopyRecipients(input?.ToRecipients, output.ToRecipients);
            CopyRecipients(input?.CcRecipients, output.CcRecipients);
            CopyRecipients(input?.BccRecipients, output.BccRecipients);

            if (input?.Attachments != null)
            {
                foreach (var a in input.Attachments)
                {
                    if (a is FileAttachment fa)
                    {
                        output.Attachments.Add(new FileAttachment
                        {
                            Name = fa.Name,
                            ContentBytes = fa.ContentBytes,
                            ContentType = fa.ContentType
                        });
                    }
                }
            }

            return output;
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
                ToRecipients = [],
                CcRecipients = [],
                BccRecipients = [],
                Attachments = []
            };

            void AddRecipients(string? raw, ICollection<Recipient> dest)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                foreach (var addr in raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
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
                message.Subject = message.Subject[..MaxSubjectLength];

            message.Body ??= new ItemBody();
            message.Body.Content ??= "(No Body Content)";
            if (message.Body.Content.Length > MaxBodyLength)
                message.Body.Content = message.Body.Content[..MaxBodyLength];
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

            if (message.Attachments is { Count: > MaxAttachments })
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
                validationError = mimeError;
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
                if (string.IsNullOrEmpty(addr) || !addr.Contains('@'))
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

                    if (!AllowedMimeTypes.Contains(mime))
                        return $"Attachment '{fileAttachment.Name}' MIME type '{mime}' is not allowed.";
                }
            }

            return null;
        }

        private async Task<(bool IsSendSuccess, string Message, string GraphCode, int? StatusCode)>
            SendEmailWithRetryAsync(Message mailMessage, CancellationToken cancellationToken)
        {
            var toRecipient = mailMessage.ToRecipients?.FirstOrDefault()?.EmailAddress?.Address
                              ?? "(unknown recipient)";

            using var activity = MyTelemetry.ActivitySource.StartActivity("Graph.SendMail");
            activity?.SetTag("smtp.to", toRecipient);

            if (_debuggerAttached)
            {
                var msg = $"Graph disabled in local debug mode, pretend message sent successfully to {toRecipient}.";
                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.SetTag("debug.mode", "local");
                activity?.SetTag("graph.enabled", false);

                return (true, msg, "GraphDisabled", null);
            }

            const int maxRetries = 5;
            const int baseDelayMs = 500;

            int attempts = EnableRetry ? maxRetries : 1;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    var requestInfo = _graphClient!.Users[_senderMailboxAddress].SendMail
                        .ToPostRequestInformation(new SendMailPostRequestBody
                        {
                            Message = mailMessage,
                            SaveToSentItems = false
                        });

                    await _graphClient.RequestAdapter.SendNoContentAsync(
                        requestInfo,
                        errorMapping: null,
                        cancellationToken: cancellationToken);

                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.SetTag("retry.attempt", attempt);

                    return (true, "Message Sent Successfully.", "None", null);
                }
                catch (ServiceException ex)
                {
                    _logger.LogError(ex, "Graph ServiceException during SendMail.");

                    activity?.SetStatus(ActivityStatusCode.Error);
                    activity?.SetTag("graph.error", ex.Message);
                    activity?.SetTag("retry.attempt", attempt);

                    if (EnableRetry && attempt < maxRetries)
                    {
                        var jitter = Random.Shared.Next(0, 250);
                        var delay = Math.Min((int)(Math.Pow(2, attempt) * baseDelayMs) + jitter, 5000);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    return (false,
                            ex.Message,
                            "GraphException",
                            ex.ResponseStatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception during SendMail.");

                    activity?.SetStatus(ActivityStatusCode.Error);
                    activity?.SetTag("exception.message", ex.Message);
                    activity?.SetTag("retry.attempt", attempt);

                    if (EnableRetry && attempt < maxRetries)
                    {
                        var jitter = Random.Shared.Next(0, 250);
                        var delay = Math.Min((int)(Math.Pow(2, attempt) * baseDelayMs) + jitter, 5000);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    return (false,
                            ex.Message,
                            "Exception",
                            null);
                }
            }

            var exhausted = "SendMail failed after maximum retry attempts.";
            _logger.LogError(exhausted);

            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("retry.exhausted", true);

            return (false,
                    exhausted,
                    "RetryExhausted",
                    null);
        }

        private async Task<string> ReadRequestBodyWithLimitAsync(
            HttpRequestData req,
            int maxBytes,
            CancellationToken cancellationToken)
        {
            // Read in chunks and bail as soon as the limit is exceeded, so an
            // oversized body is rejected without being fully buffered in memory.
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            var total = 0;
            int read;
            while ((read = await req.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                total += read;
                if (total > maxBytes)
                    throw new InvalidOperationException("Request body exceeds maximum allowed size.");
                await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            // Decode as UTF-8; strip a leading BOM that some HTTP clients attach so it
            // is not mistaken for the start of JSON content.
            var decoded = Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\uFEFF');

            _logger.LogDebug("Read request body ({RawLength} bytes, {DecodedLength} chars)", total, decoded.Length);

            return decoded;
        }

        private async Task<HttpResponseData> ReturnResponse(HttpRequestData req, HttpStatusCode statusCode, string json)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Remove("Content-Type");
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(json);
            return response;
        }

        private async Task<HttpResponseData> BadRequest(HttpRequestData req, string msg)
        {
            return await ReturnResponse(req, HttpStatusCode.BadRequest,
                JsonConvert.SerializeObject(new GraphRelayResponse { Message = msg }));
        }

        private async Task<HttpResponseData> InternalError(HttpRequestData req, string msg)
        {
            return await ReturnResponse(req, HttpStatusCode.InternalServerError,
                JsonConvert.SerializeObject(new GraphRelayResponse { Message = msg }));
        }

        private async Task<HttpResponseData> Ok(HttpRequestData req, string msg)
        {
            return await ReturnResponse(req, HttpStatusCode.OK,
                JsonConvert.SerializeObject(new GraphRelayResponse { Message = msg }));
        }

        // ============================================================
        // ENDPOINTS
        // ============================================================

        [Function("Office365SmtpRelay")]
        [OpenApiOperation(operationId: "Office365SmtpRelay")]
        [OpenApiSecurity("apikeyheader_auth", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiRequestBody("application/json", typeof(GraphRelayRequest), Description = "JSON request body: { mailMessage: <Microsoft Graph Message> }")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(GraphRelayResponse))]
        [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "application/json", typeof(GraphRelayResponse))]
        [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "application/json", typeof(GraphRelayResponse))]
        public async Task<HttpResponseData> SendGraphMessage(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["InvocationId"] = context.InvocationId,
                ["FunctionName"] = "Office365SmtpRelay"
            }))
            {
                try
                {
                    var body = (await ReadRequestBodyWithLimitAsync(req, MaxRequestBodyBytes, context.CancellationToken)).Trim();

                    Message message;

                    if (string.IsNullOrEmpty(body))
                    {
                        if (!IsTestModeEnabled)
                            return await BadRequest(req, "Request body is empty.");

                        message = new Message
                        {
                            Subject = "Test From SendingEmailViaMicrosoftGraph",
                            Body = new ItemBody
                            {
                                ContentType = BodyType.Html,
                                Content = $"<h1>Test</h1><br />{DateTime.UtcNow:G}<br /><br />"
                            },
                            ToRecipients =
                            [
                                new Recipient
                                {
                                    EmailAddress = new EmailAddress { Address = _testRecipientEmailAddress }
                                }
                            ]
                        };
                    }
                    else
                    {
                        Message? raw;
                        try
                        {
                            raw = await DeserializeGraphMessageAsync(body);
                        }
                        catch (JsonException je)
                        {
                            return await BadRequest(req, $"Invalid JSON payload: {je.Message}");
                        }
                        if (raw == null)
                            return await BadRequest(req, "Invalid JSON payload.");

                        message = RemapMessage(raw);
                    }

                    NormalizeMessage(message);
                    ValidateMessage(message, out var validationError);
                    if (!string.IsNullOrEmpty(validationError))
                        return await BadRequest(req, validationError);

                    var sendResult = await SendEmailWithRetryAsync(message, context.CancellationToken);

                    if (!sendResult.IsSendSuccess)
                        return await InternalError(req, sendResult.Message);

                    return await Ok(req, sendResult.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception in SendGraphMessage.");
                    return await InternalError(req, ex.Message);
                }
            }
        }

        [Function("Office365SmtpSimple")]
        [OpenApiOperation(operationId: "Office365SmtpSimple")]
        [OpenApiSecurity("apikeyheader_auth", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiRequestBody("application/json", typeof(SimpleEmailMessage), Description = "Simple email message payload")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(GraphRelayResponse))]
        [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "application/json", typeof(GraphRelayResponse))]
        [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "application/json", typeof(GraphRelayResponse))]
        public async Task<HttpResponseData> SendSimpleMessage(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["InvocationId"] = context.InvocationId,
                ["FunctionName"] = "Office365SmtpSimple"
            }))
            {
                try
                {
                    var body = (await ReadRequestBodyWithLimitAsync(req, MaxRequestBodyBytes, context.CancellationToken)).Trim();

                    if (string.IsNullOrEmpty(body))
                        return await BadRequest(req, "Request body is empty.");

                    SimpleEmailMessage? simple;
                    try
                    {
                        simple = JsonConvert.DeserializeObject<SimpleEmailMessage>(body);
                    }
                    catch (Exception ex)
                    {
                        return await BadRequest(req, $"Invalid JSON payload: {ex.Message}");
                    }

                    if (simple == null)
                        return await BadRequest(req, "Invalid JSON payload.");

                    var graphMessage = ConvertSimpleToGraph(simple);

                    NormalizeMessage(graphMessage);
                    ValidateMessage(graphMessage, out var validationError);
                    if (!string.IsNullOrEmpty(validationError))
                        return await BadRequest(req, validationError);

                    var sendResult = await SendEmailWithRetryAsync(graphMessage, context.CancellationToken);

                    if (!sendResult.IsSendSuccess)
                        return await InternalError(req, sendResult.Message);

                    return await Ok(req, sendResult.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception in SendSimpleMessage.");
                    return await InternalError(req, ex.Message);
                }
            }
        }
    }
}
