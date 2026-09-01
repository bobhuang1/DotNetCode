#nullable enable
using Microsoft.Graph.Models;

namespace SendingEmailViaMicrosoftGraph.AzureFunction.Models
{
    /// <summary>
    /// Request body for the Office365SmtpRelay endpoint - accepts a full Microsoft Graph
    /// Message payload for callers that need control over the entire message shape.
    /// </summary>
    public class GraphRelayRequest
    {
        public required Message MailMessage { get; init; }
    }

    public class GraphRelayResponse
    {
        public required string Message { get; set; }
    }
}
