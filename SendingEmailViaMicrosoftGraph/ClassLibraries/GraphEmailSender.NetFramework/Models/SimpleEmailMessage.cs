#nullable enable
using System.Collections.Generic;

namespace SendingEmailViaMicrosoftGraph.EmailSender.Models
{
    /// <summary>A flattened email shape that is easier to build than a full Microsoft Graph Message.</summary>
    public class SimpleEmailMessage
    {
        public string? To { get; set; } // Required, comma or semicolon separated list of email addresses
        public string? Cc { get; set; } // Optional, comma or semicolon separated list of email addresses
        public string? Bcc { get; set; } // Optional, comma or semicolon separated list of email addresses

        public string? Subject { get; set; } // Optional, but recommended for deliverability
        public string? Body { get; set; } // Optional, but recommended for deliverability
        public bool IsBodyHtml { get; set; } // true if Body is HTML, false if plain text

        public List<EmailAttachment>? Attachments { get; set; } // Optional list of attachments
    }

    public class EmailAttachment
    {
        public string? Name { get; set; } // Required, file name of the attachment
        public string? ContentType { get; set; } // Required, MIME type, e.g. "application/pdf"
        public byte[]? ContentBytes { get; set; } // Required, raw file content
    }
}
