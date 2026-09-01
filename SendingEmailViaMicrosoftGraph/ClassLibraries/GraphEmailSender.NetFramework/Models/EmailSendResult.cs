#nullable enable
namespace SendingEmailViaMicrosoftGraph.EmailSender.Models
{
    /// <summary>Outcome of a <see cref="GraphEmailSender"/> send attempt.</summary>
    public sealed class EmailSendResult
    {
        public EmailSendResult(bool isSuccess, string message, int? graphStatusCode = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            GraphStatusCode = graphStatusCode;
        }

        public bool IsSuccess { get; }
        public string Message { get; }

        /// <summary>HTTP status code returned by Microsoft Graph, when available.</summary>
        public int? GraphStatusCode { get; }
    }
}
